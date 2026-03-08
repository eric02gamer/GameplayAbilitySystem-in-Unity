using System;
using EGF;

namespace GAS
{
    [Serializable]
    public class ConditionalEffect
    {
        public GameplayEffect effectClass;
        public GameplayTagContainer requiredSourceTags;
    }

    [Serializable]
    public class EffectExecution
    {
        public CustomEffectExecutionCalculation executionCalculation;
        public ConditionalEffect[] conditionalEffects;
    }

    [Serializable]
    public class GameplayCueExecution
    {
        public int minLevel;
        public int maxLevel;
        public GameplayTagContainer cueTriggerTags;
    }

    [Serializable]
    public class GameplayTagsRequirement
    {
        public GameplayTagContainer requireTags;
        public GameplayTagContainer ignoreTags;

        public bool CheckRequirement(GameplayTagContainer checkTagContainer, bool emptyDefault)
        {
            var requireIsEmpty = requireTags.IsEmpty();
            var ignoreIsEmpty = ignoreTags.IsEmpty();
            if (requireIsEmpty && ignoreIsEmpty) return emptyDefault;
            
            var requireCheck = requireIsEmpty || checkTagContainer.Contains(requireTags);
            var ignoreCheck = ignoreIsEmpty || !checkTagContainer.ContainsAny(ignoreTags);
            
            return requireCheck && ignoreCheck;
        }
    }

    [Serializable]
    public class EffectGrantAbility
    {
        public enum RemovalPolicy
        {
            /// Effect 移除，Ability 会立即移除
            CancelAbilityImmediate,
            /// 等待 Ability 执行完毕后移除
            RemoveAbilityOnEnd,
            /// 即使 Effect 移除，Ability 也不会移除，需要手动移除。
            DoNothing,
        }
        
        public GameplayAbility ability;
        // // TODO: 目前赋予的 GA 与来源 GE 等级一致
        // public int level;
        public RemovalPolicy removalPolicy;
    }
}
