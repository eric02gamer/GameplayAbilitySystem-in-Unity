using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace GAS
{
    // TODO: 后续添加 DynamicGrantedTags 功能支持（已知对于某些情形下动态添加特定标签，方便统一移除 Effect 有帮助）
    
    // GameplayEffectSpec
    public struct GameplayEffectSpec
    {
        public GameplayEffectSpec Initialize(AbilitySystemComponent storageAsc, GameplayEffect effect, GameplayEffectContextHandle contextHandle, int initLevel)
        {
            handle = GameplayEffectSpecHandle.NewHandle(storageAsc);
            isReplica = false;
            gameplayEffect = effect;
            gameplayEffectContextHandle = contextHandle;
            
            // 记录属性快照，但不会覆盖已有的快照
            var context = contextHandle.GetData();
            if (!context.AttributesSnapshotCaptured && effect.attributesSnapshot)
            {
                context.CreateCapturedAttributesSpec();
                contextHandle.SetData(context);
            }
            
            // 叠加计数初始化
            stackLimit = gameplayEffect.stackLimitedCount;
            stackCount = 1;
            
            SetLevelAndUpdate(initLevel);
            
            isValid = true;
            return this;
        }

        public static GameplayEffectSpec CreateAsReplica(AbilitySystemComponent storageAsc, GameplayEffect effect, GameplayEffectContextHandle contextHandle, int initLevel)
        {
            var spec = new GameplayEffectSpec().Initialize(storageAsc, effect, contextHandle, initLevel);
            spec.isReplica = true;
            return spec;
        }
        
        // 移交副本操作中为 Context 生成新句柄
        public GameplayEffectSpec TransferToNewAsc(AbilitySystemComponent instigatorAsc, GameplayEffectContextHandle contextHandle)
        {
            handle = GameplayEffectSpecHandle.NewHandle(instigatorAsc);
            isReplica = true;
            gameplayEffectContextHandle = contextHandle;
            return this;
        }

        // 基础信息
        private GameplayEffectSpecHandle handle;
        private bool isReplica;
        private GameplayEffect gameplayEffect;
        private GameplayEffectContextHandle gameplayEffectContextHandle;
        private bool isValid;
        
        // GE 配置数据
        // ------------------------------------------------------------
        private int level;
        // duration
        private DurationSetting.DurationPolicy durationPolicy;
        private bool hasPresetDuration;
        private float presetDuration;
        // period
        private float period;
        // chanceToApply
        private float chanceToApply;
        // stack
        private int stackLimit;
        private int stackCount;

        #region Property

        public GameplayEffectSpecHandle Handle => handle;
        public GameplayEffect GameplayEffect => gameplayEffect;
        /// 创建源和上下文信息
        public GameplayEffectContextHandle ContextHandle => gameplayEffectContextHandle;
        /// 是否是副本
        public bool IsReplica => isReplica;

        public bool IsValid => isValid;
        
        // 配置数据
        // --------------------------------------------------
        public int Level => level;
        public DurationSetting.DurationPolicy DurationPolicy => durationPolicy;
        public bool HasPresetDuration => hasPresetDuration;
        public float PresetDuration => presetDuration;
        public float Period => period;
        public float ChanceToApply => chanceToApply;
        public int StackCount => stackCount;

        #endregion

        private void SetLevelAndUpdate(int levelInt)
        {
            level = levelInt;
            // 持续时间
            var durationSetting = gameplayEffect.durationSetting;
            durationPolicy = durationSetting.durationPolicy;
            hasPresetDuration = durationPolicy == DurationSetting.DurationPolicy.HasDuration &&
                                durationSetting.durationMagnitude.magnitudeType == Magnitude.MagnitudeType.Float;
            presetDuration = hasPresetDuration ? durationSetting.durationMagnitude.GetValueFloat(levelInt) : 0;
            // 周期
            period = durationPolicy == DurationSetting.DurationPolicy.Instant
                ? 0
                : gameplayEffect.period.Evaluate(levelInt);
            
            // 应用成功率
            chanceToApply = gameplayEffect.chanceToApply.Evaluate(levelInt);
        }

        public void SetLevel(int levelValue)
        {
            SetLevelAndUpdate(levelValue);
        }

        public void SetDuration(float durationValue)
        {
            hasPresetDuration = true;
            presetDuration = durationValue;
        }

        public void SetPeriod(float periodValue)
        {
            period = periodValue;
        }

        public void SetChanceToApply(float chanceValue)
        {
            chanceToApply = chanceValue;
        }

        public void SetStackCount(int count)
        {
            if(stackLimit < 1) return;

            stackCount = Mathf.Clamp(count, 1, stackLimit);
        }

        public void SetStackCountToMax()
        {
            if(stackLimit < 1) return;

            stackCount = stackLimit;
        }

        #region Apply

        public float GetApplyDuration(AbilitySystemComponent targetAsc)
        {
            if (!isValid) return 0;
            if (hasPresetDuration) return presetDuration;

            var durationMagnitude = gameplayEffect.durationSetting.durationMagnitude;
            switch (durationMagnitude.attributeSource)
            {
                case Magnitude.AttributeSource.Source:
                    var sourceAsc = ContextHandle.GetData().Instigator;
                    return durationMagnitude.GetValueAttributeSource(sourceAsc, level);
                
                case Magnitude.AttributeSource.Target:
                    return durationMagnitude.GetValueAttributeTarget(targetAsc, level);
                
                default:
                    return 0;
            }
        }
        
        #endregion
    }
}
