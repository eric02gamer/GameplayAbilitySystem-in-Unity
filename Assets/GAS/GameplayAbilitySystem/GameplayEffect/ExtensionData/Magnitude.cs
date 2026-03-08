using System;
using UnityEngine;

namespace GAS
{
    [Serializable]
    public class Magnitude
    {
        public enum MagnitudeType
        {
            Float,
            AttributeBased,
            // SetByCaller,     // 由外部主动调用并更改值，暂不考虑制作的功能（使用场景例如根据按键时间改变伤害量的场合）
        }
        
        public enum AttributeSource
        {
            Source,
            Target,
        }
        
        public enum AttributeCalculationType
        {
            Base,
            Current,
            Bonus,// Bonus = Current - Base
        }

        public MagnitudeType magnitudeType;
        
        // Float
        public LevelFloat floatMagnitude;

        // AttributeBased
        // FinalModifierValue = (AttributeValue + preMultiplyAddictiveValue) * coefficient + postMultiplyAddictiveValue
        public AttributeRef attributeToCapture;
        public AttributeSource attributeSource;
        public AttributeCalculationType attributeCalculationType;
        
        public LevelFloat coefficient = 1;
        public LevelFloat preMultiplyAddictiveValue;
        public LevelFloat postMultiplyAddictiveValue;

        public float GetValueFloat(int level)
        {
            return floatMagnitude.Evaluate(level);
        }

        public float GetValueAttributeSource(AbilitySystemComponent source, int level)
        {
            if (!source.TryGetAttribute(attributeToCapture, out var data)) return 0;

            return CalculateAttributeMagnitude(data, level);
        }

        public float GetValueAttributeTarget(AbilitySystemComponent target, int level)
        {
            if (!target.TryGetAttribute(attributeToCapture, out var data)) return 0;
            
            return CalculateAttributeMagnitude(data, level);
        }

        public float GetValueCapturedAttribute(CapturedAttributesSpec capturedAttributesSpec, int level)
        {
            if (!capturedAttributesSpec.TryGetData(attributeToCapture, out var data)) return 0;
            
            return CalculateAttributeMagnitude(data, level);
        }

        private float CalculateAttributeMagnitude(GameplayAttributeData attributeData, int level)
        {
            float magnitude;
            switch (attributeCalculationType)
            {
                case AttributeCalculationType.Current:
                    magnitude = attributeData.currentValue;
                    break;
                case AttributeCalculationType.Bonus:
                    magnitude = attributeData.currentValue - attributeData.baseValue;
                    break;
                case AttributeCalculationType.Base:
                default:
                    magnitude = attributeData.baseValue;
                    break;
            }
            
            var preValue = preMultiplyAddictiveValue.Evaluate(level);
            var corValue = coefficient.Evaluate(level);
            var postValue = postMultiplyAddictiveValue.Evaluate(level);
            return (preValue + magnitude) * corValue + postValue;
        }
    }
}
