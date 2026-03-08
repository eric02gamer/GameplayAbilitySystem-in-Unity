using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EGF;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;

namespace GAS
{
    public struct GameplayAbilitySpec
    {
        public GameplayAbility ability;
        // private readonly Type abilityClassType;
        private readonly int sourceAbilityHash;
        public int level;
        // 能力执行次数
        public int leftActivateCount;
        
        public bool Instanced => ability && ability.instanced;
        public bool IsValid => ability;

        public GameplayAbilitySpec(GameplayAbility gameplayAbility)
        {
            ability = gameplayAbility;
            // abilityClassType = gameplayAbility.GetType();
            sourceAbilityHash = gameplayAbility.GetHashCode();
            level = 1;
            leftActivateCount = ability.maxActiveCount;
        }

        public bool IsSameAbilitySource(GameplayAbilitySpec other)
        {
            return sourceAbilityHash == other.sourceAbilityHash;
        }
        
        public bool IsSameAbilitySource(GameplayAbility other)
        {
            return sourceAbilityHash == other.GetHashCode();
        }

        public float GetRemainingCooldownTime()
        {
            if (ability && ability.instanced)
                return ability.GetRemainingCooldownTime();
            
            return 0;
        }

        public float GetRemainingDuration()
        {
            if (ability && ability.instanced)
                return ability.GetRemainingDuration();
            
            return 0;
        }

        public void GetCooldownTimeAndDuration(out float cooldownTime, out float duration)
        {
            if (ability && ability.instanced)
            {
                ability.GetCooldownTimeAndDuration(out cooldownTime, out duration);
                return;
            }
            
            cooldownTime = 0;
            duration = 0;
        }
    }
    
    public partial class AbilitySystemComponent
    {
        /// 简易事件，可以使用 Key 准确反注册
        private class EasyAction<TKey, T>
        {
            private event Action<T> ONAction;
            private readonly Dictionary<TKey, Action<T>> actionDictionary;    // 缓存，用于移除时反注册
            
            public EasyAction()
            {
                actionDictionary = new Dictionary<TKey, Action<T>>();
            }
            public void Clear()
            {
                ONAction = null;
                actionDictionary.Clear();
            }
            
            public void Register(TKey key, Action<T> onAddTag)
            {
                if(actionDictionary.ContainsKey(key)) return;
                ONAction += onAddTag;
                actionDictionary.Add(key, onAddTag);
            }
            public void Unregister(TKey key)
            {
                if(!actionDictionary.ContainsKey(key)) return;
                ONAction -= actionDictionary[key];
                actionDictionary.Remove(key);
            }
            public void Trigger(T value)
            {
                ONAction?.Invoke(value);
            }
        }
        
        private readonly GameplayTagCountContainer dynamicBlockAbilityTags = new GameplayTagCountContainer();
        private GameplayTagContainer BlockAbilityTags => dynamicBlockAbilityTags.GameplayTagContainer;
        // Ability Event Trigger
        private readonly EasyAction<GameplayAbilitySpecHandle, GameplayEventData> abilityEventTrigger = new EasyAction<GameplayAbilitySpecHandle, GameplayEventData>();
        // Ability OnTagAdd Trigger
        private readonly EasyAction<GameplayAbilitySpecHandle, GameplayTagHash> abilityOnTagAdd = new EasyAction<GameplayAbilitySpecHandle, GameplayTagHash>();
        // Ability OnTagPresent Trigger
        private readonly EasyAction<GameplayAbilitySpecHandle, GameplayTagHash> abilityOnTagPresentAdd = new EasyAction<GameplayAbilitySpecHandle, GameplayTagHash>();
        private readonly EasyAction<GameplayAbilitySpecHandle, GameplayTagHash> abilityOnTagPresentRemove = new EasyAction<GameplayAbilitySpecHandle, GameplayTagHash>();
        
        // add or remove GA callback
        private event Action<GameplayAbilitySpecHandle> ONAddAbility;
        private event Action<GameplayAbilitySpecHandle> ONRemoveAbility;
        
        /// 已获得能力列表
        private readonly Dictionary<GameplayAbilitySpecHandle, GameplayAbilitySpec> ownedAbilities = new Dictionary<GameplayAbilitySpecHandle, GameplayAbilitySpec>();
        /// 激活中的能力
        private readonly HashSet<GameplayAbilitySpecHandle> activatingAbilities = new HashSet<GameplayAbilitySpecHandle>();
        /// 等待执行完毕后移除的技能
        private readonly HashSet<GameplayAbilitySpecHandle> setRemoveAbilitySpecs = new HashSet<GameplayAbilitySpecHandle>();

        public List<GameplayAbilitySpecHandle> OwnedAbilities
        {
            get
            {
                var result = new List<GameplayAbilitySpecHandle>();
                if (ownedAbilities?.Count > 0)
                    result.AddRange(ownedAbilities.Select(pair => pair.Key));
                
                return result;
            }
        }

        private void InitAbilityCache()
        {
            
        }

        private void ReleaseAbilityInfo()
        {
            dynamicBlockAbilityTags.ClearTags();
            
            abilityEventTrigger.Clear();
            abilityOnTagAdd.Clear();
            abilityOnTagPresentAdd.Clear();
            abilityOnTagPresentRemove.Clear();

            ONAddAbility = null;
            ONRemoveAbility = null;
            
            ownedAbilities.Clear();
            activatingAbilities.Clear();
            setRemoveAbilitySpecs.Clear();
        }

        #region Ability Handle Operate
        
        internal bool AbilitySpecHandleIsValid(GameplayAbilitySpecHandle abilitySpecHandle)
        {
            return ownedAbilities.ContainsKey(abilitySpecHandle);
        }
        internal bool AbilitySpecIsActive(GameplayAbilitySpecHandle abilitySpecHandle)
        {
            return activatingAbilities.Contains(abilitySpecHandle);
        }
        internal void AbilityAddTarget(GameplayAbilitySpecHandle abilitySpecHandle, GameplayTarget gameplayTarget)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return;
            
            if(abilitySpec.ability.Instance != GameplayAbility.InstanceStrategy.InstancedPerActor) return;
            abilitySpec.ability.OnAddTargetData(gameplayTarget);
        }
        internal void AbilitySetLevel(GameplayAbilitySpecHandle abilitySpecHandle, int level)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return;

            abilitySpec.level = level;
            ownedAbilities[abilitySpecHandle] = abilitySpec;
        }

        internal void AbilitySetActivateCount(GameplayAbilitySpecHandle abilitySpecHandle, int activateCount)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return;
            if(abilitySpec.ability.activeCountPolicyLimit == GameplayAbility.ActiveCountPolicy.Unlimited) return;

            var max = abilitySpec.ability.maxActiveCount;
            abilitySpec.leftActivateCount = Mathf.Clamp(activateCount, 0, max);
            ownedAbilities[abilitySpecHandle] = abilitySpec;
        }
        internal GameplayAbilitySpec AbilityHandleGetData(GameplayAbilitySpecHandle handle)
        {
            return ownedAbilities[handle];
        }
        
        #endregion

        private bool TryGetOwnedAbility(GameplayAbilitySpecHandle abilitySpecHandle, out GameplayAbilitySpec abilitySpec)
        {
            var containsAbility = ownedAbilities.ContainsKey(abilitySpecHandle);

            abilitySpec = containsAbility ? ownedAbilities[abilitySpecHandle] : default;
            return containsAbility;
        }
        
        private GameplayAbilitySpecHandle GiveAbilityInternal(GameplayAbility ability, int level)
        {
            return GiveAbilityInternal(new GameplayAbilitySpec(ability){
                level = level,
                leftActivateCount = ability.maxActiveCount,
            });
        }
        
        private GameplayAbilitySpecHandle GiveAbilityAndActivateOnceInternal(GameplayAbility ability, int level)
        {
            var handle = GiveAbilityInternal(ability, level);
            ActivateAbilityInternal(handle);
            return handle;
        }
        
        private GameplayAbilitySpecHandle GiveAbilityInternal(GameplayAbilitySpec abilitySpec)
        {
            var handle = new GameplayAbilitySpecHandle(abilitySpec.ability, this);
            var ability = abilitySpec.ability;
            
            // 不重复添加能力
            if(TryGetOwnedAbility(handle, out var existAbilitySpec))
            {
                if (ability.activeCountPolicyLimit == GameplayAbility.ActiveCountPolicy.Unlimited)
                    return handle;
                // 添加能力执行次数
                existAbilitySpec.leftActivateCount = ability.maxActiveCount;
                ownedAbilities[handle] = existAbilitySpec;
                return handle;
            }
            
            switch (ability.Instance)
            {
                // case GameplayAbility.InstanceStrategy.NonInstanced:
                //     break;
                case GameplayAbility.InstanceStrategy.InstancedPerActor:
                    var addingAbility = ability.CloneAsInstance(handle);
                    addingAbility.SetOwner(this);
                    addingAbility.RegisterOnActivateAbility(OnActivateAbility);
                    addingAbility.RegisterOnEndAbility(OnEndActivateAbility);
                    
                    abilitySpec.ability = addingAbility;
                    addingAbility.OnInstanceEnable();
                    break;
            }
            
            ownedAbilities.Add(handle, abilitySpec);
            RegisterAbilityTrigger(handle);
            ONAddAbility?.Invoke(handle);
            return handle;
        }

        private void RemoveAbilityInternal(GameplayAbilitySpecHandle abilitySpecHandle)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return;
            
            // 如果技能已启用，关闭它
            if (AbilitySpecIsActive(abilitySpecHandle))
                CancelAbilityInternal(abilitySpecHandle);
            
            var ability = abilitySpec.ability;
            switch (ability.Instance)
            {
                case GameplayAbility.InstanceStrategy.NonInstanced:
                    ability.SetOwner(null);
                    break;
                case GameplayAbility.InstanceStrategy.InstancedPerActor:
                    ability.OnInstanceDisable();
                    ability.CleanInstanceData();
                    ability.UnregisterOnActivateAbility(OnActivateAbility);
                    ability.UnregisterOnEndAbility(OnEndActivateAbility);
                    Destroy(ability);
                    break;
            }
            
            ownedAbilities.Remove(abilitySpecHandle);
            UnregisterAbilityTrigger(abilitySpecHandle);
            ONRemoveAbility?.Invoke(abilitySpecHandle);
        }
        
        private void SetRemoveAbilityOnEndInternal(GameplayAbilitySpecHandle abilitySpecHandle)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return;

            var abilityInstance = abilitySpec.ability.Instance;
            switch (abilityInstance)
            {
                case GameplayAbility.InstanceStrategy.NonInstanced:
                    // 非实例化，不管理运行和结束，直接移除
                    RemoveAbilityInternal(abilitySpecHandle);
                    break;
                case GameplayAbility.InstanceStrategy.InstancedPerActor:
                    // 能力激活中，则等待执行完毕再移除
                    if(AbilitySpecIsActive(abilitySpecHandle))
                        setRemoveAbilitySpecs.Add(abilitySpecHandle);
                    else
                        RemoveAbilityInternal(abilitySpecHandle);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        private void HandleSetRemoveAbilityOnEndInternal(GameplayAbilitySpecHandle abilitySpecHandle)
        {
            if (!setRemoveAbilitySpecs.Contains(abilitySpecHandle)) return;
            setRemoveAbilitySpecs.Remove(abilitySpecHandle);
            RemoveAbilityInternal(abilitySpecHandle);
        }
        
        /// 激活能力时的处理（仅适用于实例化方式为 InstancedPerActor 的能力）
        private void OnActivateAbility(GameplayAbilitySpecHandle abilitySpecHandle)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return;

            if (AbilitySpecIsActive(abilitySpecHandle)) return;    // 能力已激活，不重复触发下文
            activatingAbilities.Add(abilitySpecHandle);

            var activationAbility = abilitySpec.ability;
            // 添加 activationOwnedTags 到 ActorTags
            AddActorTags(activationAbility.activationOwnedTags);
            // 添加 blockAbilitiesWithTags
            dynamicBlockAbilityTags.AddTag(activationAbility.blockAbilitiesWithTags);
        }

        /// 结束能力时的处理（仅适用于实例化方式为 InstancedPerActor 的能力）
        private void OnEndActivateAbility(GameplayAbilitySpecHandle abilitySpecHandle)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return;
            
            if (!AbilitySpecIsActive(abilitySpecHandle)) return;   // 能力没有激活，不执行下文的结束处理
            activatingAbilities.Remove(abilitySpecHandle);
            
            var activationAbility = abilitySpec.ability;
            // 移除 activationOwnedTags
            RemoveActorTags(activationAbility.activationOwnedTags);
            // 移除 blockAbilitiesWithTags
            dynamicBlockAbilityTags.RemoveTag(activationAbility.blockAbilitiesWithTags);
            // 处理 SetRemoveOnEnd
            HandleSetRemoveAbilityOnEndInternal(abilitySpecHandle);
        }

        #region Ability Trigger
        
        /// 添加 abilityTriggers
        private void RegisterAbilityTrigger(GameplayAbilitySpecHandle abilitySpecHandle)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return;
            
            var ability = abilitySpec.ability;
            foreach (var trigger in ability.abilityTriggers)
            {
                // 仅一个标签为有效设置
                var triggerTag = trigger.triggerTag;
                
                switch (trigger.triggerSource)
                {
                    case GameplayAbility.AbilityTrigger.TriggerSource.GameplayEvent:
                        void HandleEvent(GameplayEventData eventData)
                        {
                            if(!triggerTag.Contains(eventData.eventTag)) return;
                            
                            var triggeringHandle = abilitySpecHandle;
                            if(CanActivateAbilityFromEventInternal(triggeringHandle, eventData))
                                ActivateAbilityFromEventInternal(triggeringHandle, eventData);
                        }
                        abilityEventTrigger.Register(abilitySpecHandle, HandleEvent);
                        break;
                    
                    case GameplayAbility.AbilityTrigger.TriggerSource.OnTagAdd:
                        void HandleOnAddTag(GameplayTagHash addingTag)
                        {
                            if(!triggerTag.Contains(addingTag)) return;
                            
                            var triggeringHandle = abilitySpecHandle;
                            if(CanActivateAbilityInternal(triggeringHandle))
                                ActivateAbilityInternal(triggeringHandle);
                        }
                        abilityOnTagAdd.Register(abilitySpecHandle, HandleOnAddTag);
                        break;
                    
                    case GameplayAbility.AbilityTrigger.TriggerSource.OnTagPresent:
                        void HandleOnTagPresentAdd(GameplayTagHash tagPresent)
                        {
                            if(!triggerTag.Contains(tagPresent)) return;
                            
                            var triggeringHandle = abilitySpecHandle;
                            if(CanActivateAbilityInternal(triggeringHandle))
                                ActivateAbilityInternal(triggeringHandle);
                        }
                        void HandleOnTagPresentRemove(GameplayTagHash tagPresent)
                        {
                            if(!triggerTag.Contains(tagPresent)) return;
                            
                            var triggeringHandle = abilitySpecHandle;
                            CancelAbilityInternal(triggeringHandle);
                        }
                        abilityOnTagPresentAdd.Register(abilitySpecHandle, HandleOnTagPresentAdd);
                        abilityOnTagPresentRemove.Register(abilitySpecHandle, HandleOnTagPresentRemove);
                        break;
                }
            }
        }
        
        private void UnregisterAbilityTrigger(GameplayAbilitySpecHandle abilitySpecHandle)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return;
            
            var ability = abilitySpec.ability;
            foreach (var trigger in ability.abilityTriggers)
            {
                switch (trigger.triggerSource)
                {
                    case GameplayAbility.AbilityTrigger.TriggerSource.GameplayEvent:
                        abilityEventTrigger.Unregister(abilitySpecHandle);
                        break;
                    
                    case GameplayAbility.AbilityTrigger.TriggerSource.OnTagAdd:
                        abilityOnTagAdd.Unregister(abilitySpecHandle);
                        break;
                    
                    case GameplayAbility.AbilityTrigger.TriggerSource.OnTagPresent:
                        abilityOnTagPresentAdd.Unregister(abilitySpecHandle);
                        abilityOnTagPresentRemove.Unregister(abilitySpecHandle);
                        break;
                }
            }
        }

        // Ability Event
        private void TriggerAbilityEvent(GameplayEventData eventData)
        {
            abilityEventTrigger.Trigger(eventData);
        }
        // Ability OnTagAdd
        private void TriggerAbilityOnTagAdd(GameplayTagHash addingTag)
        {
            abilityOnTagAdd.Trigger(addingTag);
        }
        // Ability OnTagPresent
        private void TriggerAbilityOnTagPresentAdd(GameplayTagHash tagPresent)
        {
            abilityOnTagPresentAdd.Trigger(tagPresent);
        }
        private void TriggerAbilityOnTagPresentRemove(GameplayTagHash tagPresent)
        {
            abilityOnTagPresentRemove.Trigger(tagPresent);
        }
        
        #endregion

        #region Activate Ability

        /// 能力执行前的标签判断
        private bool CheckActorTagsSatisfyAbilityRequirement(GameplayAbility ability)
        {
            if (!ability.activationRequiredTags.IsEmpty() &&
                !ActorTags.Contains(ability.activationRequiredTags)) return false;
            if (!ability.activationBlockedTags.IsEmpty() &&
                ActorTags.ContainsAny(ability.activationBlockedTags)) return false;
            if (!BlockAbilityTags.IsEmpty() &&
                ability.abilityTags.ContainsAny(BlockAbilityTags)) return false;

            return true;
        }
        
        private bool CanActivateAbilityInternal(GameplayAbilitySpecHandle abilitySpecHandle)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return false;
            
            var ability = abilitySpec.ability;
            
            // 剩余执行次数检查
            if (ability.activeCountPolicyLimit == GameplayAbility.ActiveCountPolicy.Limited &&
                abilitySpec.leftActivateCount <= 0)
                return false;
            
            // 标签判断
            var tagsSatisfyRequirement = CheckActorTagsSatisfyAbilityRequirement(ability);
            if (!tagsSatisfyRequirement) return false;
            
            // 实例化的能力如果激活中，不允许重复激活
            if (ability.Instance == GameplayAbility.InstanceStrategy.InstancedPerActor)
                return !AbilitySpecIsActive(abilitySpecHandle) && ability.CanActivateAbility();
            
            ability.SetOwner(this);
            ability.SetLevel(abilitySpec.level);
            return ability.CanActivateAbility();
        }

        private void ActivateAbilityInternal(GameplayAbilitySpecHandle abilitySpecHandle)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return;
            
            var ability = abilitySpec.ability;

            // 执行次数处理
            if (ability.activeCountPolicyLimit == GameplayAbility.ActiveCountPolicy.Limited)
            {
                abilitySpec.leftActivateCount -= 1;
                ownedAbilities[abilitySpecHandle] = abilitySpec;
            }
            
            if (ability.Instance == GameplayAbility.InstanceStrategy.NonInstanced)
            {
                ability.SetOwner(this);
                ability.SetLevel(abilitySpec.level);
            }
            ability.CallActivateAbility();
            
            // 取消 cancelAbilitiesWithTags 标识的能力
            CancelAbilitiesWithTagsInternal(ability.cancelAbilitiesWithTags);
        }

        #endregion

        #region Activate Ability From Event

        private bool CanActivateAbilityFromEventInternal(GameplayAbilitySpecHandle abilitySpecHandle, GameplayEventData eventData)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return false;
            
            var ability = abilitySpec.ability;
            
            // 剩余执行次数检查
            if (ability.activeCountPolicyLimit == GameplayAbility.ActiveCountPolicy.Limited &&
                abilitySpec.leftActivateCount <= 0)
                return false;
            
            // 标签判断
            var tagsSatisfyRequirement = CheckActorTagsSatisfyAbilityRequirement(ability);
            if (!tagsSatisfyRequirement) return false;
            
            // 实例化的能力如果激活中，不允许重复激活
            if (ability.Instance == GameplayAbility.InstanceStrategy.InstancedPerActor)
                return !AbilitySpecIsActive(abilitySpecHandle) && ability.ShouldAbilityRespondToEvent(eventData);
            
            ability.SetOwner(this);
            ability.SetLevel(abilitySpec.level);
            return ability.ShouldAbilityRespondToEvent(eventData);
        }

        private void ActivateAbilityFromEventInternal(GameplayAbilitySpecHandle abilitySpecHandle,
            GameplayEventData eventData)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return;
            
            var ability = abilitySpec.ability;

            // 执行次数处理
            if (ability.activeCountPolicyLimit == GameplayAbility.ActiveCountPolicy.Limited)
            {
                abilitySpec.leftActivateCount -= 1;
                ownedAbilities[abilitySpecHandle] = abilitySpec;
            }
            
            if (ability.Instance == GameplayAbility.InstanceStrategy.NonInstanced)
            {
                ability.SetOwner(this);
                ability.SetLevel(abilitySpec.level);
            }
            ability.CallActivateAbilityFromEvent(eventData);
            
            // 取消 cancelAbilitiesWithTags 标识的能力
            CancelAbilitiesWithTagsInternal(ability.cancelAbilitiesWithTags);
        }

        #endregion

        private void CancelAbilityInternal(GameplayAbilitySpecHandle abilitySpecHandle)
        {
            if(!TryGetOwnedAbility(abilitySpecHandle, out var abilitySpec)) return;
            
            var ability = abilitySpec.ability;
            ability.CancelAbility();
            // CancelAbility 会触发 OnEndAbility 事件并从 activatingAbilities 中移除
        }

        private void CancelAbilitiesWithTagsInternal(GameplayTagContainer tagContainer)
        {
            var waitToCheckList = new List<GameplayAbilitySpecHandle>();
            waitToCheckList.AddRange(activatingAbilities);
            foreach (var handle in waitToCheckList)
            {
                if(!TryGetOwnedAbility(handle, out var abilitySpec)) continue;
                
                var abilityTags = abilitySpec.ability.abilityTags;
                if(abilityTags.ContainsAny(tagContainer))
                    CancelAbilityInternal(handle);
            }
        }
    }
}
