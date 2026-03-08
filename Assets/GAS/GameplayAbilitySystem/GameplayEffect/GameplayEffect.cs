using System;
using System.Collections;
using System.Collections.Generic;
using EGF;
using UnityEditor;
using UnityEngine;

namespace GAS
{
    /*
     * 计划加入的功能清单
     * 持续时间
     * 周期性生效
     * 概率生效
     * 等级曲线：使用 AnimationCurve代替等级数值表（注意字段数据内的 Float 也要替换）
     *
     * 暂时不加入的功能
     * TODO；float 等级功能：浮点数可用数据表或曲线进行配置，实现根据等级的不同数值不同。（本项目暂不考虑制作 float 等级，后续替换等级曲线）
     * TODO: 等级功能 Plus：等级数据可读取表格，不同对象读取同一张表格时，读取请求使用代理对象指向同一个读取结果。
     */
    /// <summary>
    /// 定义具体的效果逻辑和行为的类，例如增加属性、添加状态等，根据配置来生成不同的 IGameplayEffectSpec 实例
    /// </summary>
    [CreateAssetMenu(menuName = "GAS/Gameplay Effect", fileName = "New Gameplay Effect")]
    public class GameplayEffect: ScriptableObject
    {
        /// GE 中断并恢复时的周期处理策略
        public enum PeriodicInhibitionPolicy
        {
            NeverReset,
            Reset,
            ExecuteAndReset,
        }
        /// GE 的叠加类型
        public enum StackingType
        {
            /// 不对叠加进行限制，Effect可以无限叠加且同时生效
            None,
            /// 对 效果源 叠加
            AggregateBySource,
            /// 对 目标   叠加
            AggregateByTarget,
        }
        /// GE 的叠加处理策略
        public enum StackResetPolicy
        {
            ResetOnSuccessfulStacking,
            NeverReset,
        }
        /// 叠加 GE 的失效处理策略
        public enum StackExpirationPolicy
        {
            /// 全部移除
            ClearEntireStack,
            /// Stack 减 1，重置持续时间但不会重新应用 "reapply"
            RemoveSingleStackAndRefreshDuration,
            /// 仅刷新持续时间，可配合 OnStackCountChange 回调手动控制 Stack 的减少
            RefreshDuration,
        }

        [Header("==================== Gameplay Effect")]
        [Tooltip("持续时间策略，Instant 和持续时间为0的 HasDuration 类型的 GE将作为瞬时GE")] public DurationSetting durationSetting;
        [Tooltip("如果启用，将记录创建GE实例时<源对象>的属性快照，并用于后续所有GE和衍生GE修改属性的计算")] public bool attributesSnapshot;
        [Tooltip("基础属性调整器")] public GameplayEffectModifier[] modifiers;
        [Tooltip("自定义执行时的计算方法，或执行额外的GE")] public EffectExecution[] executions;
        [Tooltip("每次成功应用，会一同应用的条件 GE")] public ConditionalEffect[] conditionalGameplayEffects;

        [Space(10),Header("==================== Period")]
        [Tooltip("周期性生效GE，仅 HasDuration 或 Infinite 可用，周期为0时将无周期效果")] public LevelFloat period = 0;
        [Tooltip("是否在周期性效果开始时立即执行")] public bool executePeriodicEffectOnApplication = true;
        [Tooltip("GE 中断并恢复时的周期处理策略")] public PeriodicInhibitionPolicy periodicInhibitionPolicy;
        
        [Space(10),Header("==================== Application")]
        [Tooltip("GE成功应用到目标的概率，1为必定应用，0为不会应用")] public LevelFloat chanceToApply = 1;
        [Tooltip("GE应用到目标的条件")] public CustomEffectApplicationRequirement[] applicationRequirements;

        [Space(10),Header("==================== Stacking")]
        public StackingType stackingType;
        [Tooltip("Stack 数量限制，该限制对于非持续性叠加GE也有效，不过非持续性GE将忽略其它 stack 设置")] public int stackLimitedCount;
        public StackResetPolicy stackDurationResetPolicy;
        public StackResetPolicy stackPeriodResetPolicy;
        public StackExpirationPolicy stackExpirationPolicy;

        [Space(10),Header("==================== Overflow")]
        [Tooltip("Stack 数量溢出时触发的 GE，可以启用stack并按溢出时的stack数目执行")] public GameplayEffect[] overflowEffects;
        [Tooltip("禁用溢出【到达数量限制后，后续的Stack将失败，Duration等刷新也不会进行】")] public bool denyOverflowApplication;
        [Tooltip("溢出后清除 stack，启用该设置也会禁用溢出")] public bool clearStackOnOverflow;

        [Space(10),Header("==================== Expiration")]
        [Tooltip("提前失效（例如被其它技能强制移除）引发的后续 Effect，对于瞬时GE无效，只会以stack=1执行")] public GameplayEffect[] prematureExpirationEffects;
        [Tooltip("正常失效触发的后续 GE，对于瞬时GE无效，只会以stack=1执行")] public GameplayEffect[] routineExpirationEffects;

        [Space(10),Header("==================== Display")]
        [Tooltip("成功应用GE才会触发Cues")] public bool requireModifierSuccessToTriggerCues = true;
        [Tooltip("如果启用，GE将重复执行第一个生成的Cue实例")] public bool suppressStackingCues;
        [Tooltip("触发Cues作为GE的游戏效果，可以是声音、特效等。其中的minLevel，maxLevel用于限制Effect有效触发Cues的等级")] public GameplayCueExecution[] gameplayCues;
        // TODO: // [Tooltip("用于展示GE的UI数据")] public UIData uiData;
        
        [Space(10),Header("==================== Tags")]
        [Tooltip("GE拥有，但不会给到应用对象的Tags")] public GameplayTagContainer assetTags;
        [Tooltip("GE拥有，并且会给到应用对象的Tags")] public GameplayTagContainer grantedTags;
        [Tooltip("运行对象所需的Tag条件，可控制GE的启用和关闭(GE中断)。关闭期间该GE不生效但仍存在")] public GameplayTagsRequirement ongoingTagsRequirement;
        [Tooltip("对象移除GE的Tag条件。持续时间内对象满足条件将移除GE，应用前对象满足条件则应用失败。条件为空则不阻止。")] public GameplayTagsRequirement removalTagsRequirement;
        [Tooltip("若该GE应用成功，则移除满足条件的其余GE。")] public GameplayTagContainer removeGameplayEffectsWithTags;

        [Space(10),Header("==================== Immunity")]
        [Tooltip("应用期间，对符合条件的GE免疫")] public GameplayTagsRequirement grantedImmunityTags;

        [Space(10), Header("==================== Granted Ability")]
        public EffectGrantAbility[] grantAbilities;
    }
}
