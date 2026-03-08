using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// TODO: 当前在 TrySetAttribute 函数中，对于属性的修改存在问题，PreAttributeChange 和 PostAttributeChange 应该在每次完整的属性更新后最终调用一次，便于统一处理属性变动的后续逻辑。

namespace GAS
{
    public interface IGameplayAttributeSet
    {
        void ResetCurrentValue();
        bool GetAttribute(int hash, out GameplayAttributeData attribute);
        void SetAttribute(int hash, GameplayAttributeData attribute);
        bool ValidationCheckAndSetAttribute(int hash, 
            GameplayAttributeData oldAttr,
            GameplayAttributeData newAttr,
            out GameplayAttributeData finalAppliedAttribute);

        void ClearAttributeLinkChangeRecord();
        void GetAttributeLinkChangeRecord(Dictionary<int, GameplayAttributeData> attributeLinkChanges);
    }
    /// 用于生成属性集实例的 SO配置文件
    [CreateAssetMenu(menuName = "GAS/Attribute Set/Default Attribute Set", fileName = "New Default Attribute Set")]
    public class GameplayAttributeSet : ScriptableObject, IGameplayAttributeSet
    {
        protected const string CreatePath = "GAS/Attribute Set/";
        protected AbilitySystemComponent GetOwner() => abilitySystemComponent;
        
        private Dictionary<int, GameplayAttributeData> runtimeData;
        private Dictionary<int, GameplayAttributeData> runtimeAttributeLinkChanges;
        private AbilitySystemComponent abilitySystemComponent;
        
        [Serializable]
        public class InitAttributeData
        {
            public AttributeRef attributeRef;
            public float baseValue;
        }
        
        [Header("Custom Attributes | 自定义属性")]
        public InitAttributeData[] data;

        public Dictionary<int, GameplayAttributeData> RuntimeData => runtimeData;
        public bool Specified => abilitySystemComponent;

        private static GameplayAttributeSet CloneAsSpecInternal(GameplayAttributeSet prototype, AbilitySystemComponent owner)
        {
            var spec = Instantiate(prototype);

            spec.abilitySystemComponent = owner;
            spec.runtimeData = new Dictionary<int, GameplayAttributeData>();
            spec.runtimeAttributeLinkChanges = new Dictionary<int, GameplayAttributeData>();
            
            void AddInitData(InitAttributeData attribute)
            {
                var hash = attribute.attributeRef.AttributeHash;
                if (hash == 0)
                {
                    Debug.LogWarning("检测到无效空属性, 属性添加失败");
                    return;
                }
                if (spec.runtimeData.ContainsKey(hash))
                {
                    Debug.LogWarning($"重复的属性名：{attribute.attributeRef.attributeName}, 属性添加失败");
                    return;
                }
                
                var baseValue = attribute.baseValue;
                spec.runtimeData.Add(hash, new GameplayAttributeData(baseValue));
            }
            
            foreach (var attribute in spec.data)
            {
                AddInitData(attribute);
            }

            var extraData = spec.ExtraAttribute();
            if (extraData == null) return spec;
            
            List<InitAttributeData> dataList = spec.data.ToList();
            // 添加扩展属性
            foreach (var attribute in extraData)
            {
                // 已有属性不覆盖
                if(spec.runtimeData.ContainsKey(attribute.attributeRef.AttributeHash)) continue;
                
                AddInitData(attribute);
                dataList.Add(attribute);
            }
            // 更新 spec.data数据（用于后续数据处理时的索引，协助 AbilitySystemHelper 进行数据可视化）
            spec.data = dataList.ToArray();
            
            return spec;
        }
        public static GameplayAttributeSet CloneAsSpec(GameplayAttributeSet prototype, AbilitySystemComponent owner)
        {
            return CloneAsSpecInternal(prototype, owner);
        }

        public void ResetSet()
        {
            void ResetData(InitAttributeData attribute)
            {
                var hash = attribute.attributeRef.AttributeHash;
                if (hash == 0)
                {
                    Debug.LogWarning("检测到无效空属性, 属性添加失败");
                    return;
                }
                if (runtimeData.ContainsKey(hash))
                {
                    var baseValue = attribute.baseValue;
                    runtimeData[hash] = new GameplayAttributeData(baseValue);
                }
            }
            
            foreach (var attribute in data)
            {
                ResetData(attribute);
            }
        }
        
        #region Runtime
        
        public void Release()
        {
            runtimeData?.Clear();
            runtimeAttributeLinkChanges?.Clear();
        }

        private bool GetAttributeInternal(int hash, out GameplayAttributeData attribute)
        {
            if (!runtimeData.TryGetValue(hash, out var value))
            {
                attribute = default;
                return false;
            }

            attribute = value;
            return true;
        }

        private void SetAttributeInternal(int hash, GameplayAttributeData attribute)
        {
            if (!runtimeData.ContainsKey(hash)) return;
            runtimeData[hash] = attribute;
        }
        
        // public bool TryGetAttribute(int hash, out GameplayAttributeData attribute)
        // {
        //     if (!runtimeData.TryGetValue(hash, out var value))
        //     {
        //         attribute = default;
        //         return false;
        //     }
        //
        //     attribute = value;
        //     return true;
        // }
        
        // public bool TrySetAttribute(int hash, GameplayAttributeData attribute, out GameplayAttributeData appliedAttribute)
        // {
        //     appliedAttribute = attribute;
        //     if (!runtimeData.TryGetValue(hash, out var oldValue)) return false;
        //
        //     attribute = PreAttributeChange(hash, oldValue, attribute);
        //     runtimeData[hash] = attribute;
        //     appliedAttribute = attribute;
        //     PostAttributeChange(hash, attribute);
        //     return true;
        // }

        
        #endregion

        #region [Set Attributes API]

        void IGameplayAttributeSet.ResetCurrentValue()
        {
            foreach (var initAttribute in data)
            {
                var key = initAttribute.attributeRef.AttributeHash;
                if (!runtimeData.TryGetValue(key, out var oldData)) continue;
                
                // Debug.Log($"debug [GameplayAttributeSet] before reset: hash:{key}, base:{runtimeData[key].baseValue}, current:{runtimeData[key].currentValue}");
                var newData = new GameplayAttributeData
                {
                    baseValue = oldData.baseValue,
                    currentValue = oldData.baseValue, // 重置为 baseValue
                };
                runtimeData[key] = newData;
                // Debug.Log($"debug [GameplayAttributeSet] after reset: hash:{key}, base:{runtimeData[key].baseValue}, current:{runtimeData[key].currentValue}");
            }
        }
        
        bool IGameplayAttributeSet.GetAttribute(int hash, out GameplayAttributeData attribute)
        {
            return GetAttributeInternal(hash, out attribute);
        }

        void IGameplayAttributeSet.SetAttribute(int hash, GameplayAttributeData attribute)
        {
            SetAttributeInternal(hash, attribute);
        }
        
        bool IGameplayAttributeSet.ValidationCheckAndSetAttribute(
            int hash, 
            GameplayAttributeData oldAttr, 
            GameplayAttributeData newAttr,
            out GameplayAttributeData finalAppliedAttribute)
        {
            if (!GetRuntimeAttribute(hash, out _))
            {
                finalAppliedAttribute = default;
                return false;
            }
            
            finalAppliedAttribute = PreAttributeChange(hash, oldAttr, newAttr);
            runtimeData[hash] = finalAppliedAttribute;
            PostAttributeChange(hash, oldAttr, finalAppliedAttribute);
            return true;
        }

        public void ClearAttributeLinkChangeRecord()
        {
            if(runtimeAttributeLinkChanges != null) runtimeAttributeLinkChanges.Clear();
        }

        public void GetAttributeLinkChangeRecord(Dictionary<int, GameplayAttributeData> attributeLinkChanges)
        {
            if(runtimeAttributeLinkChanges == null) return;
            
            foreach (var pair in runtimeAttributeLinkChanges)
            {
                var key = pair.Key;
                var value = pair.Value;

                attributeLinkChanges[key] = value;
            }
        }

        #endregion

        #region [Set Attributes API for subclass]
        /*
         * 供子类使用
         */

        protected bool GetRuntimeAttribute(int hash, out GameplayAttributeData attribute)
        {
            return GetAttributeInternal(hash, out attribute);
        }

        protected void SetRuntimeAttribute(int hash, GameplayAttributeData attribute)
        {
            if(runtimeData.TryGetValue(hash, out var oldValue))
                runtimeAttributeLinkChanges[hash] = oldValue;
            SetAttributeInternal(hash, attribute);
        }

        #endregion
        
        /*
         * 如果希望创建配置文件时自动添加序列化属性，可在构造函数中进行，这些配置可被删除
         * exp.
         * public HumanAttributes()
         * {
         *     data = new InitAttributeData[]
         *     {
         *         new InitAttributeData() {
         *             attributeRef = new AttributeRef(CharacterAttributes.Durability),
         *             baseValue = 100,
         *         },
         *         // ...
         *     };
         * }
         *
         * 如果希望默认配置不受序列化数据影响，在 ExtraAttribute 中处理。
         */
        /// 扩展的预设属性信息，实例化时执行
        protected virtual InitAttributeData[] ExtraAttribute() => null;
        /// 数据修改预处理，属性值实际发生改变之前调用，可以用来调整或限制当前属性的属性值。（在 PreAttributeChange 中可调用 GetRuntimeAttribute 获取其它属性值信息，但不要在此处调用 SetRuntimeAttribute）
        protected virtual GameplayAttributeData PreAttributeChange(int changingHash, GameplayAttributeData oldValue, GameplayAttributeData newValue)
        {
            return newValue;
        }
        /// 数据约束后处理，可用于触发连锁属性修改，（在 PostAttributeChange 中可调用 GetRuntimeAttribute 获取其它属性值信息，调用 SetRuntimeAttribute 设置其它属性值）
        protected virtual void PostAttributeChange(int changingHash, GameplayAttributeData oldValue, GameplayAttributeData newValue){ }
    }
}
