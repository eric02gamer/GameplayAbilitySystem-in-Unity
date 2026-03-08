using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR

namespace GAS
{
    /*
     * Ability System Helper 用于为 AbilitySystemComponent 提供可视化的内部数据展示。
     */
    public class AbilitySystemHelper : MonoBehaviour
    {
        private AbilitySystemComponent asc;
        private bool initialized;

        private void Awake()
        {
            asc = GetComponent<AbilitySystemComponent>();
            if(!asc) return;
            InitGameplayEffectWatch();
            InitAbilityWatch();
            InitContextWatch();
            
            initialized = true;
        }

        private void OnDestroy()
        {
            if(!initialized) return;
            
            ReleaseGameplayEffectWatch();
            ReleaseAbilityWatch();
            ReleaseContextWatch();
        }

        private void Update()
        {
            if(!initialized) return;
            RuntimeDataUpdate();
        }

        #region Attributes
        
        [Serializable]
        public struct ShowAttributeData
        {
            public string name;
            public string value;
            [Range(0,1)]public float slider;

            public ShowAttributeData(AttributeRef attributeRef, GameplayAttributeData attributeData)
            {
                name = attributeRef.attributeName;
                value = attributeData.currentValue + "/" + attributeData.baseValue;
                slider = attributeData.currentValue / attributeData.baseValue;
            }
        }
        [Tooltip("运行时的属性数据")]public List<ShowAttributeData> runtimeAttributeData;

        private void AttributesWatch()
        {
            runtimeAttributeData ??= new List<ShowAttributeData>();
            
            runtimeAttributeData.Clear();
            if(asc.InitAttributeData == null) return;
            
            foreach (var data in asc.InitAttributeData)
            {
                var attributeRef = data.attributeRef;
                if(!asc.TryGetAttribute(attributeRef, out var att)) continue;
                
                var showAtt = new ShowAttributeData(attributeRef, att);
                runtimeAttributeData.Add(showAtt);
            }
        }

        #endregion
        
        #region GE
        
        [Serializable]
        public struct ShowEffectData
        {
            public string name;
            public GameplayEffect effect;
            public int stack;
            [Range(0,1)]public float period;
            [Range(0,1)]public float duration;

            public ShowEffectData(ActiveGameplayEffectSpec effectSpec)
            {
                name = effectSpec.GameplayEffect.name;
                effect = effectSpec.GameplayEffect;
                stack = effectSpec.Stack;
                period = effectSpec.periodTimer / effectSpec.Period;
                duration = effectSpec.durationTimer / effectSpec.Duration;
            }
        }
        private HashSet<ActiveEffectSpecHandle> activeEffectHandles;
        [Tooltip("运行时的 GE 数据")]public List<ShowEffectData> runtimeEffectData;

        private void InitGameplayEffectWatch()
        {
            activeEffectHandles = new HashSet<ActiveEffectSpecHandle>();
            runtimeEffectData = new List<ShowEffectData>();
            
            asc.RegisterOnAnyGameplayEffectAdded(OnAddEffect);
            asc.RegisterOnAnyGameplayEffectRemoved(OnRemoveEffect);
        }

        private void ReleaseGameplayEffectWatch()
        {
            asc.UnregisterOnAnyGameplayEffectAdded(OnAddEffect);
            asc.UnregisterOnAnyGameplayEffectRemoved(OnRemoveEffect);
            
            activeEffectHandles.Clear();
            runtimeEffectData.Clear();
        }
        private void OnAddEffect(ActiveEffectSpecHandle obj)
        {
            activeEffectHandles.Add(obj);
        }
        private void OnRemoveEffect(ActiveEffectSpecHandle obj)
        {
            activeEffectHandles.Remove(obj);
        }
        private void GameplayEffectWatch()
        {
            ActiveEffectSpecHandle[] activeEffectSpecs = new ActiveEffectSpecHandle[activeEffectHandles.Count];
            activeEffectHandles.CopyTo(activeEffectSpecs);
            
            runtimeEffectData.Clear();
            foreach (var handle in activeEffectSpecs)
            {
                var effectSpec = handle.GetData();
                if(!effectSpec.IsValid) continue;
                
                var showData = new ShowEffectData(effectSpec);
                runtimeEffectData.Add(showData);
            }
        }
        
        #endregion

        #region GA
        
        [Serializable]
        public struct ShowAbilityData
        {
            public string name;
            public int level;
            public GameplayAbility.InstanceStrategy instanceStrategy;
            public bool active;
        }
        private HashSet<GameplayAbilitySpecHandle> ownedAbilitySpecHandles;
        [Header("实时能力")]
        public List<ShowAbilityData> runtimeAbilities;
        
        private void InitAbilityWatch()
        {
            var abilities = asc.GetActivatableAbilities();
            ownedAbilitySpecHandles = new HashSet<GameplayAbilitySpecHandle>(abilities);
            runtimeAbilities = new List<ShowAbilityData>();
            
            asc.RegisterOnAnyAbilityAdded(OnAddAbility);
            asc.RegisterOnAnyAbilityRemoved(OnRemoveAbility);
        }
        private void ReleaseAbilityWatch()
        {
            asc.UnregisterOnAnyAbilityAdded(OnAddAbility);
            asc.UnregisterOnAnyAbilityRemoved(OnRemoveAbility);
            
            ownedAbilitySpecHandles.Clear();
            runtimeAbilities.Clear();
        }
        private void OnAddAbility(GameplayAbilitySpecHandle obj)
        {
            ownedAbilitySpecHandles.Add(obj);
        }
        private void OnRemoveAbility(GameplayAbilitySpecHandle obj)
        {
            ownedAbilitySpecHandles.Remove(obj);
        }
        private void AbilitiesWatch()
        {
            GameplayAbilitySpecHandle[] abilitySpecHandles = new GameplayAbilitySpecHandle[ownedAbilitySpecHandles.Count];
            ownedAbilitySpecHandles.CopyTo(abilitySpecHandles);
            
            runtimeAbilities.Clear();
            foreach (var handle in abilitySpecHandles)
            {
                var abilitySpec = handle.GetData();

                var showData = new ShowAbilityData()
                {
                    name = abilitySpec.ability.name,
                    active = handle.IsActive,
                    instanceStrategy = abilitySpec.ability.Instance,
                    level = abilitySpec.level,
                };
                runtimeAbilities.Add(showData);
            }
        }
        
        #endregion

        #region Context
        
        [Serializable]
        public struct ContextCountData
        {
            [Tooltip("运行时的 Context 数目")]public int rContextCount;
            [Tooltip("运行时的 GE Spec 数目")]public int rSpecCount;
            [Tooltip("运行时的 叠加GE信息 数目")]public int rStackHandleCount;
        }

        [Space] [Header("GE 上下文数据数目")]
        public ContextCountData contextCountData;
        [Tooltip("运行时的 叠加GE源 的数量")]public List<int> runtimeSourceStack;
        
        [Conditional("UNITY_EDITOR")]
        private void InitContextWatch()
        {
            runtimeSourceStack = new List<int>();
        }
        
        [Conditional("UNITY_EDITOR")]
        private void ReleaseContextWatch()
        {
            runtimeSourceStack.Clear();
        }

        [Conditional("UNITY_EDITOR")]
        private void ContextWatch()
        {
            if(Selection.activeObject != gameObject) return;
            
            // GE 数据缓存
            contextCountData = new ContextCountData()
            {
                rContextCount = asc.EffectContexts.Count,
                rSpecCount = asc.EffectSpecs.Count,
                rStackHandleCount = asc.StackEffectHandles.Count,
            };
            // 以自身为Source施加的可叠加GE
            runtimeSourceStack.Clear();
            foreach (var pair in asc.EffectStackSourceCount)
            {
                runtimeSourceStack.Add(pair.Value);
            }
        }

        #endregion
        
        private void RuntimeDataUpdate()
        {
            if(Selection.activeObject != gameObject) return;
            
            // 属性
            AttributesWatch();
            // GE
            GameplayEffectWatch();
            // GA
            AbilitiesWatch();
            // Context
            ContextWatch();
        }
    }
}

#endif
