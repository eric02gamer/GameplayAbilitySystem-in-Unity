using System;
using System.Collections;
using System.Collections.Generic;
using EGF;
using UnityEngine;

namespace GAS
{
    public abstract class GameplayAbility: ScriptableObject
    {
        protected const string CreatePath = "GAS/Gameplay Ability/";
        /// 实例化策略
        public enum InstanceStrategy
        {
            /// 非实例化，不能保存运行中信息，无法跟踪运行和结束状态
            NonInstanced,
            /// 每个对象一个实例，完整的开始、结束生命周期，允许保存运行中信息
            InstancedPerActor,
            // TODO:
            // InstancedPerExecution,
        }
        
        public enum ActiveCountPolicy
        {
            Unlimited,
            Limited,
        }
        
        [Serializable]
        public class AbilityTrigger
        {
            public enum TriggerSource
            {
                GameplayEvent,
                /// 每次添加标签都触发
                OnTagAdd,
                /// 添加标签时触发能力，移除标签时取消能力，如果是限时的能力，在标签存在期间结束，也不会重复触发
                OnTagPresent,
            }
            
            public GameplayTagContainer triggerTag;
            public TriggerSource triggerSource;
        }
        
        [Header("Tags")]
        [Tooltip("技能自身标签")]public GameplayTagContainer abilityTags;
        [Tooltip("执行瞬间，取消拥有所有这些标签的其它技能")] public GameplayTagContainer cancelAbilitiesWithTags;
        [Tooltip("执行时，阻止拥有所有这些标签的其它技能")] public GameplayTagContainer blockAbilitiesWithTags;
        [Tooltip("执行时，技能的所有者将被给予这组标记")] public GameplayTagContainer activationOwnedTags;
        [Tooltip("技能发起对象具有所有这些标记时，技能才会被激活。")] public GameplayTagContainer activationRequiredTags;
        [Tooltip("技能发起对象没有任何这些标记时，技能才会被激活")] public GameplayTagContainer activationBlockedTags;
        // 关于 Target 的约束由能力的具体实现进行判断
        // [Tooltip("目标具有所有这些标记时，技能才会被激活。")] public GameplayTagContainer targetRequiredTags;
        // [Tooltip("目标没有任何这些标记时，技能才会被激活")] public GameplayTagContainer targetBlockedTags;
        
        [Header("Trigger")]
        [Tooltip("能力触发器，可设置能力触发方式，用于触发的标签只能有一个有效")]public AbilityTrigger[] abilityTriggers;
        
        /// 执行次数限制
        [Header("Limit")]
        [Tooltip("执行次数限制，非实例化技能无限制")]public ActiveCountPolicy activeCountPolicyLimit;
        public int maxActiveCount;
        
        public GameplayEffect costGameplayEffect;
        public GameplayEffect cooldownGameplayEffect;
        
        /// 技能是否在被 GE 赋予时自动激活
        public virtual bool AutoActiveOnEffectGranted => false;
        
        // ----------------------------------------------------------------------------------------------------
        // 监听激活和结束事件
        private event Action<GameplayAbilitySpecHandle> ONActivate;
        private event Action<GameplayAbilitySpecHandle> ONEnd;
        
        public void RegisterOnActivateAbility(Action<GameplayAbilitySpecHandle> onActivateAbility)
        {
            if(!instanced || onActivateAbility == null) return;
            ONActivate += onActivateAbility;
        }
        public void UnregisterOnActivateAbility(Action<GameplayAbilitySpecHandle> onActivateAbility)
        {
            if(!instanced || onActivateAbility == null) return;
            ONActivate -= onActivateAbility;
        }
        public void RegisterOnEndAbility(Action<GameplayAbilitySpecHandle> onEndAbility)
        {
            if(!instanced || onEndAbility == null) return;
            ONEnd += onEndAbility;
        }
        public void UnregisterOnEndAbility(Action<GameplayAbilitySpecHandle> onEndAbility)
        {
            if(!instanced || onEndAbility == null) return;
            ONEnd -= onEndAbility;
        }

        // ----------------------------------------------------------------------------------------------------

        #region Property
        
        /// 实例化策略
        public virtual InstanceStrategy Instance => InstanceStrategy.NonInstanced;
        /// 实例化标记
        internal bool instanced;
        
        // TODO: 目前实例化技能还需要 if(!Instanced) 进行保护，后续添加自动保护
        /// 实例化标记，仅当 Instance == InstanceStrategy.InstancedPerActor 时，且赋予给了具体对象的 ASC 后为 true
        protected bool Instanced => instanced;
        
        private GameplayAbilitySpecHandle instanceHandle;
        private AbilitySystemComponent owner;  // 拥有能力的Asc对象 // 循环引用风险，记得最后清除
        private int level;
        public void SetOwner(AbilitySystemComponent ownerAsc)
        {
            owner = ownerAsc;
        }
        protected AbilitySystemComponent GetOwner()
        {
            return owner;
        }
        public void SetLevel(int levelInt)
        {
            level = levelInt;
        }
        protected int GetLevel()
        {
            return level;
        }
        
        #endregion
        
        public GameplayAbility CloneAsInstance(GameplayAbilitySpecHandle specHandle)
        {
            var abilityInstance = Instantiate(this);
            abilityInstance.instanced = true;
            abilityInstance.instanceHandle = specHandle;
            
            return abilityInstance;
        }
        public void CleanInstanceData()
        {
            instanced = false;
            owner = null;
        }
        
        public void CallActivateAbility()
        {
            if(instanced)
                ONActivate?.Invoke(instanceHandle);
            ActivateAbility();
        }
        /// 从事件启用能力，自动处理条件判断
        public void CallActivateAbilityFromEvent(GameplayEventData eventData)
        {
            if(instanced)
                ONActivate?.Invoke(instanceHandle);
            ActivateAbilityFromEvent(eventData);
        }
        internal void CancelAbility()
        {
            OnEndAbility(true);
            if(instanced)
                ONEnd?.Invoke(instanceHandle);
        }
        protected void EndAbility()
        {
            OnEndAbility(false);
            if(instanced)
                ONEnd?.Invoke(instanceHandle);
        }
        protected ActiveEffectSpecHandle ApplyCostGameplayEffect()
        {
            if(!owner || !costGameplayEffect) return new ActiveEffectSpecHandle();
            return owner.ApplyGameplayEffectToSelf(costGameplayEffect, owner, level);
        }
        protected ActiveEffectSpecHandle ApplyCooldownGameplayEffect()
        {
            if (!owner || !cooldownGameplayEffect) return new ActiveEffectSpecHandle();
            return owner.ApplyGameplayEffectToSelf(cooldownGameplayEffect, owner, level);
        }
        
        public virtual float GetRemainingCooldownTime()
        {
            return 0;
        }

        public virtual float GetRemainingDuration()
        {
            return 0;
        }

        public virtual void GetCooldownTimeAndDuration(out float cooldownTime, out float duration)
        {
            cooldownTime = 0;
            duration = 0;
        }
        
        // 可重载部分
        // ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// 实例化的 Ability 成功添加
        /// </summary>
        public virtual void OnInstanceEnable() { }
        /// <summary>
        /// 实例化的 Ability 准备移除
        /// </summary>
        public virtual void OnInstanceDisable() { }
        
        /// 处理目标数据的添加
        public virtual void OnAddTargetData(GameplayTarget target){}

        /// 检查是否符合手动执行条件
        public virtual bool CanActivateAbility()
        {
            return true;
        }
        /// 是否响应事件激活
        public virtual bool ShouldAbilityRespondToEvent(GameplayEventData gameplayEventData)
        {
            return false;
        }
        /// 从事件响应并激活能力
        protected virtual void ActivateAbilityFromEvent(GameplayEventData gameplayEventData){ }
        /// 支付能力代价，无法承受代价返回 false，
        protected virtual bool CommitExecution()
        {
            return true;
        }
        protected abstract void ActivateAbility();
        /// 结束或取消能力，必须为同步函数
        protected abstract void OnEndAbility(bool cancelled);
    }
}
