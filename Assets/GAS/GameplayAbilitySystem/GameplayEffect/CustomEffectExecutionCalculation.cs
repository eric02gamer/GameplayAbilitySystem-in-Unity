using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    /*
     * 用法和虚幻GAS不同
     */
    /// Gameplay Effect 的自定义属性更改计算类
    public abstract class CustomEffectExecutionCalculation : ScriptableObject
    {
        protected const string CreatePath = "GAS/Custom GE Execution/";
        
        public virtual bool ConditionalExecuteCheck(GameplayEffectSpec spec, AbilitySystemComponent targetAsc)
        {
            return false;
        }
    }
}
