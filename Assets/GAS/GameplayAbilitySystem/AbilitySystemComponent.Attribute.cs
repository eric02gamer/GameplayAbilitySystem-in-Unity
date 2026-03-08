using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    /// GAS属性变化事件参数
    public struct EventGameplayAttributeChangeArgs
    {
        public AbilitySystemComponent source;
        public AbilitySystemComponent target;
        
        public GameplayAttributeData oldData;
        public GameplayAttributeData newData;
    }
    
    public partial class AbilitySystemComponent
    {
        /// 属性集
        [SerializeField]private GameplayAttributeSet attributeSet;

        private GameplayAttributeSet attributeSetSpec
        {
            get
            {
                if (!attributeSet) return null;
        
                if (!attributeSet.Specified) attributeSet = GameplayAttributeSet.CloneAsSpec(attributeSet, this);
                return attributeSet;
            }
        }

        /// 监听属性变动事件
        private readonly Dictionary<int, Action<EventGameplayAttributeChangeArgs>> attributeValueChangeActions =
            new Dictionary<int, Action<EventGameplayAttributeChangeArgs>>();

        public Dictionary<int, GameplayAttributeData> AttributeRuntimeData => attributeSetSpec ? attributeSetSpec.RuntimeData : null;
        public GameplayAttributeSet.InitAttributeData[] InitAttributeData => attributeSetSpec ? attributeSetSpec.data : null;

        /// 初始化属性集
        private void InitAttributeSetSpec()
        {
            // 检查数据
            if(!attributeSet) return;
            
            attributeSet = GameplayAttributeSet.CloneAsSpec(attributeSet, this);
        }

        private void ReleaseAttributeSetSpec()
        {
            if(attributeSet && attributeSet.Specified)
                attributeSet.Release();
            attributeValueChangeActions?.Clear();
        }

        public void ResetAttributeSet()
        {
            if(attributeSet)
            {
                attributeSet.ResetSet();

                // 手动触发所有属性事件
                foreach (var pair in attributeValueChangeActions)
                {
                    var key = pair.Key;
                    var action = pair.Value;
                    if (TryGetAttribute(key, out var curData))
                    {
                        action?.Invoke(new EventGameplayAttributeChangeArgs(){newData = curData, source = null});
                    }
                }
            }
        }

        #region [Modify Attributes]
        
        private void ResetAllAttributesCurrentValue()
        {
            if(attributeSetSpec is not IGameplayAttributeSet attrSet) return;
            attrSet.ResetCurrentValue(); // 注意：不会触发属性更改事件
        }

        /// 设置属性值（会触发监听事件）
        private void SetAttributeInternal(int hash, GameplayAttributeData newData)
        {
            if(attributeSetSpec is not IGameplayAttributeSet attrSet) return;
            attrSet.SetAttribute(hash, newData);
        }
        
        /// 获取属性值
        private bool TryGetAttributeInternal(int hash, out GameplayAttributeData data)
        {
            if (attributeSetSpec is IGameplayAttributeSet attrSet) return attrSet.GetAttribute(hash, out data);
            
            data = default;
            return false;
        }

        #endregion
        

        #region 监听属性变化
        
        /// 注册监听属性变化
        private void RegisterAttributeValueChangeCallbackInternal(int hash, Action<EventGameplayAttributeChangeArgs> onAttributeChange)
        {
            if(attributeValueChangeActions == null || onAttributeChange == null) return;
            // 检查有没有对应的属性
            if(attributeSetSpec is not IGameplayAttributeSet attrSet || !attrSet.GetAttribute(hash, out _)) return;

            if (!attributeValueChangeActions.TryAdd(hash, onAttributeChange))
                attributeValueChangeActions[hash] = attributeValueChangeActions[hash] + onAttributeChange;
        }

        /// 取消监听属性变化
        private void UnregisterAttributeValueChangeCallbackInternal(int hash, Action<EventGameplayAttributeChangeArgs> onAttributeChange)
        {
            if(attributeValueChangeActions == null || onAttributeChange == null) return;
            if(!attributeValueChangeActions.ContainsKey(hash)) return;
            
            attributeValueChangeActions[hash] = attributeValueChangeActions[hash] - onAttributeChange;
            if (attributeValueChangeActions[hash] == null)
                attributeValueChangeActions.Remove(hash);
        }
        
        #endregion
    }
}
