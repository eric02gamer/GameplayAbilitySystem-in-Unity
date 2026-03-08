using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    [Serializable]
    public class LevelFloat
    {
        public bool useCurveData;
        public float constantValue;
        public AnimationCurve curveValue;

        #region 构造函数
        
        public LevelFloat(float initValue)
        {
            useCurveData = false;
            constantValue = initValue;
            curveValue = new AnimationCurve();
        }

        public LevelFloat(AnimationCurve initValue)
        {
            useCurveData = false;
            constantValue = 0;
            curveValue = initValue;
        }

        public LevelFloat()
        {
            useCurveData = false;
            constantValue = 0;
            curveValue = new AnimationCurve();
        }

        #endregion
        
        public float Evaluate(float level)
        {
            return !useCurveData ? constantValue : curveValue.Evaluate(level);
        }

        // 隐式转换运算符：float 到 LevelFloat
        public static implicit operator LevelFloat(float floatValue)
        {
            return new LevelFloat(floatValue);
        }
        
        // 隐式转换运算符：AnimationCurve 到 LevelFloat
        public static implicit operator LevelFloat(AnimationCurve floatValue)
        {
            return new LevelFloat(floatValue);
        }
    }
}
