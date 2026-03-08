using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    public abstract class CustomEffectExecutionModifierCalculation : CustomEffectExecutionCalculation
    {
        /// 如果在自定义算法中可能会修改的属性，添加属性 hash 到 targetAttrsHashList 用于GAS系统跟踪
        public abstract void ListModifierTargetAttrs(HashSet<int> targetAttrsHashList);
        public abstract void ModifierExecute(GameplayEffectSpec spec, AbilitySystemComponent targetAsc,
            List<(int, float)> changeList, Dictionary<int, float> overrideDict);
    }
}
