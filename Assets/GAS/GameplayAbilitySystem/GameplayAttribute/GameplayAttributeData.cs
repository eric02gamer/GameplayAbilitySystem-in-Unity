using System;
using UnityEngine;

namespace GAS
{
    /// 单个属性
    [Serializable]
    public struct GameplayAttributeData
    {
        public float baseValue;
        public float currentValue;

        public GameplayAttributeData(float initValue)
        {
            baseValue = initValue;
            currentValue = initValue;
        }

        public bool ApproximateEquals(GameplayAttributeData other, float threshold)
        {
            if (Mathf.Abs(baseValue - other.baseValue) < threshold &&
                Mathf.Abs(currentValue - other.currentValue) < threshold) return true;

            return false;
        }

        #region Operator

        public static GameplayAttributeData operator +(GameplayAttributeData a, GameplayAttributeData b)
        {
            return new GameplayAttributeData
            {
                baseValue = a.baseValue + b.baseValue,
                currentValue = a.currentValue + b.currentValue,
            };
        }
        public static GameplayAttributeData operator -(GameplayAttributeData a, GameplayAttributeData b)
        {
            return new GameplayAttributeData
            {
                baseValue = a.baseValue - b.baseValue,
                currentValue = a.currentValue - b.currentValue,
            };
        }

        public override string ToString()
        {
            return $"Attribute: <{baseValue}, {currentValue}>";
        }

        #endregion
    }
}
