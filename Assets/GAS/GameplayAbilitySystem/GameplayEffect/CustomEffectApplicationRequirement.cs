using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    /// Gameplay Effect 自定义扩展应用条件
    public abstract class CustomEffectApplicationRequirement : ScriptableObject
    {
        /// 判断执行条件
        public abstract bool CanApplyGameplayEffect(GameplayEffectSpec effectSpec, AbilitySystemComponent targetAsc);
    }
}
