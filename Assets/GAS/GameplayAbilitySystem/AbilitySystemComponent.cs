using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using EGF;
using UnityEditor;
using UnityEngine;

namespace GAS
{
    // TODO: 经考虑，后续还是换回 float 的等级数值，目前暂时使用 int 等级
    [DisallowMultipleComponent]
    public partial class AbilitySystemComponent : MonoBehaviour
    {
        private bool initialized = false;
        
        [Header("Tag")]
        [SerializeField]private GameplayTagContainer actorTags = new GameplayTagContainer();
        private Dictionary<int, int> actorTagsCount = new Dictionary<int, int>();
        public GameplayTagContainer ActorTags => actorTags;

        private event Action<GameplayTagHash> ONAnyTagAdd;
        private event Action<GameplayTagHash> ONAnyTagRemove;

        /// Asc 中统一使用的浮点允许误差
        public const float FEpsilon = 0.001f;

        // /// TODO: 激活中的 Cues 表现
        // private Dictionary<string, Cue> inActiveCues;
        
        /// 初始化
        private void InitComponentInfo()
        {
            if(initialized) return;
            
            InitAttributeSetSpec();
            InitEffectCache();
            InitAbilityCache();
            initialized = true;
        }

        private void ReleaseComponentInfo()
        {
            actorTags.ClearTagRuntime();
            actorTagsCount.Clear();
            
            ReleaseAttributeSetSpec();
            ReleaseEffectCache();
            ReleaseAbilityInfo();
        }

        private void Awake()
        {
            InitComponentInfo();
        }

        private void OnDestroy()
        {
            ReleaseComponentInfo();
        }

        private void Update()
        {
            UpdateLoopActiveEffectSpecs();
        }

        #region Tags | 标签

        private void AddActorTags(GameplayTagContainer tagContainer)
        {
            void Visitor(GTagRuntimeTrieNode node)
            {
                // 跳过伴随标签
                if(!node.active) return;
                
                var addingTag = node.hash;
                var addingTagHash = addingTag.GetDictHashInt();
                TriggerAbilityOnTagAdd(addingTag);
                
                if (actorTagsCount.ContainsKey(addingTagHash))
                    actorTagsCount[addingTagHash] += 1;
                else
                {
                    actorTagsCount.Add(addingTagHash, 1);
                    actorTags.AddTagRuntime(addingTag);
                    TriggerAbilityOnTagPresentAdd(addingTag);
                    ONAnyTagAdd?.Invoke(addingTag);
                }
            }
            tagContainer.Traverse(Visitor);
        }
        private void RemoveActorTags(GameplayTagContainer tagContainer)
        {
            void Visitor(GTagRuntimeTrieNode node)
            {
                // 跳过伴随标签
                if(!node.active) return;
                
                var removingTag = node.hash;
                var removingTagHash = removingTag.GetDictHashInt();
                if (!actorTagsCount.ContainsKey(removingTagHash)) return;
                
                actorTagsCount[removingTagHash] -= 1;
                if (actorTagsCount[removingTagHash] <= 0)
                {
                    actorTags.RemoveTagRuntime(removingTag);
                    actorTagsCount.Remove(removingTagHash);
                    TriggerAbilityOnTagPresentRemove(removingTag);
                    ONAnyTagRemove?.Invoke(removingTag);
                }
            }
            tagContainer.Traverse(Visitor);
        }
        public void RegisterOnAnyTagAdd(Action<GameplayTagHash> onTagAdd)
        {
            ONAnyTagAdd += onTagAdd;
        }
        public void UnregisterOnAnyTagAdd(Action<GameplayTagHash> onTagAdd)
        {
            ONAnyTagAdd -= onTagAdd;
        }
        public void RegisterOnAnyTagRemove(Action<GameplayTagHash> onTagRemove)
        {
            ONAnyTagRemove += onTagRemove;
        }
        public void UnregisterOnAnyTagRemove(Action<GameplayTagHash> onTagRemove)
        {
            ONAnyTagRemove -= onTagRemove;
        }

        #endregion

        #region 属性API | Attribute

        /// 获取属性
        public bool TryGetAttribute(AttributeRef attribute, out GameplayAttributeData data)
        {
            var hash = attribute.AttributeHash;
            return TryGetAttributeInternal(hash, out data);
        }
        public bool TryGetAttribute(int hash, out GameplayAttributeData data)
        {
            return TryGetAttributeInternal(hash, out data);
        }

        // HACK: 待测试。直接设置属性
        public void SetAttribute(AttributeRef attribute, GameplayAttributeData newData, AbilitySystemComponent source = null)
        {
            var hash = attribute.AttributeHash;
            SetAttributeInternal(hash, newData);
        }
        public void SetAttribute(int hash, GameplayAttributeData newData, AbilitySystemComponent source = null)
        {
            SetAttributeInternal(hash, newData);
        }
        
        /// 注册监听属性变化
        public void RegisterAttributeValueChangeCallback(int hash,
            Action<EventGameplayAttributeChangeArgs> onAttributeChange)
        {
            RegisterAttributeValueChangeCallbackInternal(hash, onAttributeChange);
        }

        /// 取消监听属性变化
        public void UnregisterAttributeValueChangeCallback(int hash,
            Action<EventGameplayAttributeChangeArgs> onAttributeChange)
        {
            UnregisterAttributeValueChangeCallbackInternal(hash, onAttributeChange);
        }

        #endregion

        
        #region 效果 | Effect

        /// 创建 GE Context
        public GameplayEffectContextHandle MakeEffectContext()
        {
            GameplayEffectContext context = new GameplayEffectContext(this);
            effectContexts.Add(context.Handle, context);
            return context.Handle;
        }
        
        /// 创建 GE Spec
        public GameplayEffectSpecHandle MakeOutgoingEffectSpec(GameplayEffect gameplayEffect, int level, GameplayEffectContextHandle contextHandle)
        {
            if (!gameplayEffect) return new GameplayEffectSpecHandle();
            
            return MakeOutgoingEffectSpecInternal(gameplayEffect, level, contextHandle);
        }
        
        /// 复制 GE Spec（以及 Spec 包含的 GE Context） 的数据副本给新的 ASC 进行管理
        public GameplayEffectSpecHandle ReplicateEffectSpecToSelf(GameplayEffectSpecHandle newEffectSpec)
        {
            return ReplicateEffectSpec(newEffectSpec);
        }
        
        public ActiveEffectSpecHandle ApplyGameplayEffectSpecToSelf(GameplayEffectSpecHandle specHandle)
        {
            return ApplyGameplayEffectSpec(specHandle);
        }
        
        public ActiveEffectSpecHandle ApplyGameplayEffectSpecToTarget(AbilitySystemComponent targetAsc, GameplayEffectSpecHandle specHandle)
        {
            return targetAsc.ApplyGameplayEffectSpec(specHandle);
        }
        
        public ActiveEffectSpecHandle ApplyGameplayEffectToSelf(GameplayEffect gameplayEffect, AbilitySystemComponent sourceAsc, int level = 0, int stack = 1)
        {
            if (!gameplayEffect) return new ActiveEffectSpecHandle();
            
            return ApplyGameplayEffectInternal(gameplayEffect, sourceAsc, level, stack);
        }

        /// <summary>
        /// 强制移除指定 stack 数目的 ActiveEffectSpecHandle
        /// </summary>
        public void RemoveActiveEffectSpecWithHandle(ActiveEffectSpecHandle activeSpecHandle, int stacksToRemove = -1)
        {
            WaitChangeActiveEffectSpecStack(activeSpecHandle, null, stacksToRemove, true);
        }
        
        // HACK: 待测试。
        /// <summary>
        /// 移除指定 stack 数目的 ActiveEffectSpecHandle，只能移除由 sourceAsc 附加的 stack
        /// </summary>
        public void RemoveActiveEffectSpecWithHandle(ActiveEffectSpecHandle activeSpecHandle, AbilitySystemComponent sourceAsc, int stacksToRemove = -1)
        {
            WaitChangeActiveEffectSpecStack(activeSpecHandle, sourceAsc, stacksToRemove, true);
        }

        // HACK: 待测试。
        /// <summary>
        /// 根据 AssetTags 强制移除指定 stack 数目的 ActiveEffectSpecHandle
        /// </summary>
        public void RemoveActiveEffectSpecWithAssetTags(GameplayTagContainer withTags, int stacksToRemove = -1)
        {
            RemoveActiveEffectSpecWithTagsInternal(withTags, stacksToRemove, true, false);
        }
        
        // HACK: 待测试。
        /// <summary>
        /// 根据 GrantedTags 强制移除指定 stack 数目的 ActiveEffectSpecHandle
        /// </summary>
        public void RemoveActiveEffectSpecWithGrantedTags(GameplayTagContainer withTags, int stacksToRemove = -1)
        {
            RemoveActiveEffectSpecWithTagsInternal(withTags, stacksToRemove, false, true);
        }

        // HACK: 待测试。
        /// <summary>
        /// 凡是带有指定 Tags 的 ActiveEffectSpecHandle，强制移除指定 stack 数目
        /// </summary>
        public void RemoveActiveEffectSpecWithTags(GameplayTagContainer withTags, int stacksToRemove = -1)
        {
            RemoveActiveEffectSpecWithTagsInternal(withTags, stacksToRemove, true, true);
        }
        
        public void RegisterOnAnyGameplayEffectAdded(Action<ActiveEffectSpecHandle> onAddCallback)
        {
            if(onAddCallback == null) return;
            ONAddEffect += onAddCallback;
        }
        public void UnregisterOnAnyGameplayEffectAdded(Action<ActiveEffectSpecHandle> onAddCallback)
        {
            if(onAddCallback == null) return;
            ONAddEffect -= onAddCallback;
        }
        public void RegisterOnAnyGameplayEffectRemoved(Action<ActiveEffectSpecHandle> onRemoveCallback)
        {
            if(onRemoveCallback == null) return;
            ONRemoveEffect += onRemoveCallback;
        }
        public void UnregisterOnAnyGameplayEffectRemoved(Action<ActiveEffectSpecHandle> onRemoveCallback)
        {
            if(onRemoveCallback == null) return;
            ONRemoveEffect -= onRemoveCallback;
        }

        #endregion
        
        #region 能力 | Ability

        /*
         * 生命周期：
         * Give 添加 | Clear 移除
         * Active 启用 | End 停止
         */
        
        // 添加
        // ----------------------------------------------------------------------------------------------------
        
        // HACK: 待测试
        public GameplayAbilitySpecHandle GiveAbility(GameplayAbility ability, int level)
        {
            return GiveAbilityInternal(ability, level);
        }

        // HACK: 待测试
        public GameplayAbilitySpecHandle GiveAbilityAndActivateOnce(GameplayAbility ability, int level)
        {
            return GiveAbilityAndActivateOnceInternal(ability, level);
        }

        public List<GameplayAbilitySpecHandle> GetActivatableAbilities()
        {
            List<GameplayAbilitySpecHandle> result = new();
            foreach (var ownedAbility in ownedAbilities)
            {
                result.Add(ownedAbility.Key);
            }

            return result;
        }

        // 移除
        // ----------------------------------------------------------------------------------------------------
        
        // HACK: 待测试
        /// <summary>
        /// 技能标记为执行完毕后移除，如果不是正在执行则立即移除
        /// </summary>
        public void SetRemoveAbilityOnEnd(GameplayAbilitySpecHandle abilityHandle)
        {
            SetRemoveAbilityOnEndInternal(abilityHandle);
        }
        
        // HACK: 待测试
        public void RemoveAbility(GameplayAbilitySpecHandle abilityHandle)
        {
            RemoveAbilityInternal(abilityHandle);
        }

        // HACK: 待测试
        public void RemoveAllAbilities()
        {
            var waitToRemoveList = new List<GameplayAbilitySpecHandle>();
            foreach (var pair in ownedAbilities)
                waitToRemoveList.Add(pair.Key);
            foreach (var item in waitToRemoveList)
                RemoveAbilityInternal(item);
        }
        
        // 调用
        // ----------------------------------------------------------------------------------------------------

        // HACK: 待测试
        public bool CanActivateAbility(GameplayAbilitySpecHandle abilityHandle)
        {
            return CanActivateAbilityInternal(abilityHandle);
        }

        // HACK: 不考虑公开直接激活能力的方法
        // public void CallActivateAbility(GameplayAbilitySpecHandle abilityHandle)
        // {
        //     CheckInit();
        //     ActivateAbilityInternal(abilityHandle);
        // }

        // HACK: 待测试
        public bool TryActivateAbility(GameplayAbilitySpecHandle abilityHandle)
        {
            var canActivate = CanActivateAbilityInternal(abilityHandle);
            if(canActivate)
                ActivateAbilityInternal(abilityHandle);

            return canActivate;
        }

        // HACK：待测试，激活具有指定标签的所有技能
        /// <summary>
        /// 激活具有指定标签的所有技能
        /// </summary>
        public void ActivateAbilitiesWithTag(GameplayTagContainer gameplayTag)
        {
            var waitToActivateList = new List<GameplayAbilitySpecHandle>();
            foreach (var pair in ownedAbilities)
                waitToActivateList.Add(pair.Key);
            
            foreach (var handle in waitToActivateList)
            {
                if (CanActivateAbilityInternal(handle))
                    ActivateAbilityInternal(handle);
            }
        }
        
        // HACK：待测试，向自身发送 GameplayEvent
        /// 向自身发送 GameplayEvent
        public void SendGameplayEventToSelf(GameplayEventData eventData)
        {
            // 触发能力的 Trigger
            TriggerAbilityEvent(eventData);
            // TODO: 触发 Gameplay Cues（可能并不需要的功能）
            // ......
        }
        
        // HACK：待测试
        public void CancelAbilityWithHandle(GameplayAbilitySpecHandle abilityHandle)
        {
            CancelAbilityInternal(abilityHandle);
        }
        
        // HACK：待测试
        public void CancelAbilitiesWithTags(GameplayTagContainer tagContainer)
        {
            CancelAbilitiesWithTagsInternal(tagContainer);
        }
        
        public void RegisterOnAnyAbilityAdded(Action<GameplayAbilitySpecHandle> onAddCallback)
        {
            if(onAddCallback == null) return;
            ONAddAbility += onAddCallback;
        }
        public void UnregisterOnAnyAbilityAdded(Action<GameplayAbilitySpecHandle> onAddCallback)
        {
            if(onAddCallback == null) return;
            ONAddAbility -= onAddCallback;
        }
        public void RegisterOnAnyAbilityRemoved(Action<GameplayAbilitySpecHandle> onRemoveCallback)
        {
            if(onRemoveCallback == null) return;
            ONRemoveAbility += onRemoveCallback;
        }
        public void UnregisterOnAnyAbilityRemoved(Action<GameplayAbilitySpecHandle> onRemoveCallback)
        {
            if(onRemoveCallback == null) return;
            ONRemoveAbility -= onRemoveCallback;
        }

        #endregion

        #region EDITOR_ONLY
#if UNITY_EDITOR
        internal Dictionary<GameplayEffectContextHandle, GameplayEffectContext> EffectContexts => effectContexts;
        internal Dictionary<GameplayEffectSpecHandle, GameplayEffectSpec> EffectSpecs => effectSpecs;
        internal Dictionary<GameplayEffect, ActiveEffectSpecHandle> StackEffectHandles => stackEffectHandles;
        internal Dictionary<GameplayEffect, int> EffectStackSourceCount => effectStackSourceCount;
#endif
        #endregion
    }
}
