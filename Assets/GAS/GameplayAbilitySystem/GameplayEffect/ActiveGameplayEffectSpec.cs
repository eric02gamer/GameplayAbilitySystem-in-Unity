using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    public struct ActiveGameplayEffectSpec
    {
        internal ActiveGameplayEffectSpec Initialize(AbilitySystemComponent targetAsc, GameplayEffectSpec gameplayEffectSpec)
        {
            // 初始化
            stackCountRecord = new ChangeTimeSortedDictionary<AbilitySystemComponent, int>();
            // 初始数据
            handle = ActiveEffectSpecHandle.NewHandle(targetAsc);
            specHandle = gameplayEffectSpec.Handle;
            contextHandle = specHandle.GetData().ContextHandle;
            gameplayEffect = gameplayEffectSpec.GameplayEffect;

            duration = gameplayEffectSpec.HasPresetDuration
                ? gameplayEffectSpec.PresetDuration
                : gameplayEffectSpec.GetApplyDuration(targetAsc);
            period = gameplayEffectSpec.Period;
            stackLimit = gameplayEffectSpec.GameplayEffect.stackLimitedCount;
            
            OnInitialize();
            isValid = true;
            return this;
        }
        
        // 基础数据
        private ActiveEffectSpecHandle handle;
        private GameplayEffectSpecHandle specHandle;
        private GameplayEffectContextHandle contextHandle;
        private GameplayEffect gameplayEffect;
        private bool isValid;

        public ActiveEffectSpecHandle Handle => handle;
        public GameplayEffectSpecHandle SpecHandle => specHandle;
        public GameplayEffectContextHandle ContextHandle => contextHandle;
        public bool IsValid => isValid;
        public GameplayEffect GameplayEffect => gameplayEffect;
        
        // 记录 stack 计数（Asc 的 hash，叠加数量）
        private ChangeTimeSortedDictionary<AbilitySystemComponent, int> stackCountRecord;
        private int stackCount;
        public int Stack => stackCount;
        public bool EnableStack => gameplayEffect.stackingType != GameplayEffect.StackingType.None && stackLimit > 0;
        public bool EnablePeriod => period > AbilitySystemComponent.FEpsilon;
        
        // GE 配置数据
        // ------------------------------------------------------------
        private float duration;
        private float period;
        private int stackLimit;
        
        public float Duration => duration;
        public float Period => period;
        public int StackLimit => stackLimit;
        
        // 运行时数据，按倒计时计算减少至 0
        public float durationTimer;
        public float periodTimer;
        public bool paused;

        #region Function

        // /// 检查剩余多少数目可叠加
        // public int CalculateRemainingStackCapacity(AbilitySystemComponent sourceAsc)
        // {
        //     switch (gameplayEffect.stackingType)
        //     {
        //         case GameplayEffect.StackingType.AggregateBySource:
        //             var sourceOldRecord = sourceAsc.GetSourceStackCount(gameplayEffect);
        //             return stackLimit - sourceOldRecord;
        //         case GameplayEffect.StackingType.AggregateByTarget:
        //         case GameplayEffect.StackingType.None:
        //             return stackLimit - stackCount;
        //         default:
        //             throw new ArgumentOutOfRangeException();
        //     }
        // }
        
        /// 添加 stack 计数，返回结果为（本次添加是否导致了溢出）
        // TODO: 计算结果待检查 AddStack
        public bool AddStack(AbilitySystemComponent sourceAsc, int stackToAdd)
        {
            if (!isValid || stackToAdd < 1) return false;
            
            var oldStackCount = stackCount;
            switch (gameplayEffect.stackingType)
            {
                case GameplayEffect.StackingType.AggregateBySource:
                    AddStackBySource(sourceAsc, stackToAdd);
                    break;
                case GameplayEffect.StackingType.AggregateByTarget:
                    AddStackByTarget(stackToAdd);
                    break;
                case GameplayEffect.StackingType.None:
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            if (gameplayEffect.stackDurationResetPolicy == GameplayEffect.StackResetPolicy.ResetOnSuccessfulStacking)
                durationTimer = duration;
            if (gameplayEffect.stackPeriodResetPolicy == GameplayEffect.StackResetPolicy.ResetOnSuccessfulStacking)
                periodTimer = period;

            SaveStackToSpec();
            
            // 返回结果：本次添加是否导致溢出
            return stackCount < oldStackCount + stackToAdd;
        }
        
        // TODO: 计算结果待检查 RemoveStack
        public void RemoveStack(AbilitySystemComponent sourceAsc, int stackToRemove)
        {
            switch (gameplayEffect.stackingType)
            {
                case GameplayEffect.StackingType.AggregateBySource:
                    RemoveStackBySource(sourceAsc, stackToRemove);
                    break;
                case GameplayEffect.StackingType.AggregateByTarget:
                    RemoveStackByTarget(stackToRemove);
                    break;
            }

            SaveStackToSpec();
        }

        private void SaveStackToSpec()
        {
            var spec = specHandle.GetData();
            spec.SetStackCount(stackCount);
            specHandle.SetData(spec);
        }

        private void RemoveStackForceMod(int stackToRemove)
        {
            if (!isValid || stackToRemove < 1) return;
            List<AbilitySystemComponent> waitToRemove = new List<AbilitySystemComponent>();
            AbilitySystemComponent waitToReplaceAsc = null;
            int waitToReplaceCount = 0;
            
            foreach (var pair in stackCountRecord)
            {
                if(stackToRemove < 1) break;
                var left = pair.Value;
                if (left <= stackToRemove)
                {
                    waitToRemove.Add(pair.Key);
                    stackToRemove -= left;
                }
                else
                {
                    waitToReplaceAsc = pair.Key;
                    waitToReplaceCount = left - stackToRemove;
                }
            }

            // 全部移除
            foreach (var item in waitToRemove)
            {
                if(!stackCountRecord.TryGetValue(item, out var removeCount)) continue;
                
                stackCountRecord.Remove(item);
                item.RemoveSourceStackCount(gameplayEffect, removeCount);
                stackCount -= removeCount;
            }
            // 部分移除
            if (!waitToReplaceAsc || !stackCountRecord.TryGetValue(waitToReplaceAsc, out var current)) return;
            stackCountRecord.AddOrUpdate(waitToReplaceAsc, waitToReplaceCount);
            var remove = current - waitToReplaceCount;
            waitToReplaceAsc.RemoveSourceStackCount(gameplayEffect, remove);
            stackCount -= remove;
        }

        private void AddStackBySource(AbilitySystemComponent sourceAsc, int stackToChange)
        {
            if(!sourceAsc) return;
            
            var specOldRecord = stackCountRecord.TryGetValue(sourceAsc, out var stack) ? stack : 0;
            var sourceOldRecord = sourceAsc.GetSourceStackCount(gameplayEffect);
            
            if (sourceOldRecord + stackToChange > stackLimit)
                stackToChange = stackLimit - sourceOldRecord;
            
            if(stackToChange < 1) return;
            var specNewRecord = specOldRecord + stackToChange;
            stackCountRecord.AddOrUpdate(sourceAsc, specNewRecord);
            sourceAsc.AddSourceStackCount(gameplayEffect, stackToChange);
            stackCount = specNewRecord;
        }
        private void RemoveStackBySource(AbilitySystemComponent sourceAsc, int stackToChange)
        {
            if(!sourceAsc)
                RemoveStackForceMod(stackToChange);
            else
            {
                var specOldRecord = stackCountRecord.TryGetValue(sourceAsc, out var stack) ? stack : 0;
                var sourceOldRecord = sourceAsc.GetSourceStackCount(gameplayEffect);
            
                if (sourceOldRecord - stackToChange < 1)
                    stackToChange = sourceOldRecord;
            
                var specNewRecord = specOldRecord - stackToChange;
                stackCountRecord.AddOrUpdate(sourceAsc, specNewRecord);
                sourceAsc.RemoveSourceStackCount(gameplayEffect, stackToChange);
                stackCount = specNewRecord;
            }
        }

        private void AddStackByTarget(int stackToChange)
        {
            stackCount = Mathf.Clamp(stackCount + stackToChange, 0, stackLimit);
        }
        private void RemoveStackByTarget(int stackToChange)
        {
            stackCount = Mathf.Clamp(stackCount - stackToChange, 0, stackLimit);
        }
        
        private void OnInitialize()
        {
            periodTimer = gameplayEffect.executePeriodicEffectOnApplication ? 0 : period;
            durationTimer = duration;
        }

        #endregion
    }
}
