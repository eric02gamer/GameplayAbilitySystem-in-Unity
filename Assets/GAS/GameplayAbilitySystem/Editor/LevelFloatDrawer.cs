using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GAS.Editor
{
    [CustomPropertyDrawer(typeof(LevelFloat))]
    public class LevelFloatDrawer : PropertyDrawer
    {
        private VisualElement floatViewElement;
        private VisualElement curveViewElement;

        private SerializedProperty curveValuePropRef;
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var useCurveProp = property.FindPropertyRelative("useCurveData");
            var constantValueProp = property.FindPropertyRelative("constantValue");
            var curveValueProp = property.FindPropertyRelative("curveValue");
            curveValuePropRef = curveValueProp;
            
            var root = new VisualElement();
            root.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);

            var label = new Label($"<{property.displayName}>");
            label.style.alignSelf = new StyleEnum<Align>(Align.Center);
            label.style.marginRight = 30;
            var toggle = new Toggle();
            toggle.style.marginRight = 5;
            toggle.style.marginLeft = 5;
            toggle.BindProperty(useCurveProp);
            var tip = new Label("Level Curve");
            
            var floatView = new PropertyField(){label = ""};
            floatView.BindProperty(constantValueProp);
            floatViewElement = floatView;
            var curveFieldView = new PropertyField {label = ""};
            curveFieldView.BindProperty(curveValueProp);
            curveViewElement = curveFieldView;
            
            if (useCurveProp.boolValue)
                CurveViewEnable();
            else
                FloatViewEnable();
            toggle.RegisterValueChangedCallback(ChangeUseCurve);

            root.Add(label);
            root.Add(floatView);
            root.Add(curveFieldView);
            root.Add(toggle);
            root.Add(tip);
            
            return root;
        }

        private void ChangeUseCurve(ChangeEvent<bool> evt)
        {
            if(evt.newValue)
                CurveViewEnable();
            else
                FloatViewEnable();
        }

        private void FloatViewEnable()
        {
            floatViewElement.style.flexGrow = 1;
            floatViewElement.visible = true;
            
            curveViewElement.style.flexGrow = -1;
            curveViewElement.visible = false;
            AnimationCurve curve = GetObjectFromProperty(curveValuePropRef) as AnimationCurve;
            curve?.ClearKeys();
        }

        private void CurveViewEnable()
        {
            curveViewElement.style.flexGrow = 1;
            curveViewElement.visible = true;
            
            floatViewElement.style.flexGrow = -1;
            floatViewElement.visible = false;
        }

        #region 反射获取 SerializedProperty 的字段对象
        // TODO: 后续加入工具代码集
        
        private readonly struct FieldStringData
        {
            public const int FieldInt = 0;
            public const int ArrayFieldInt = 1;
            
            public readonly int dataTypeInt;
            public readonly string dataName;
            public readonly int arrayIndex;

            public FieldStringData(int initDataTypeInt, string initDataName, int initArrayIndex)
            {
                dataTypeInt = initDataTypeInt;
                dataName = initDataName;
                arrayIndex = initArrayIndex;
            }
        }
        
        private object GetObjectFromProperty(SerializedProperty property)
        {
            var rootObject = property.serializedObject.targetObject;
            var propertyPath = property.propertyPath;

            string[] pathElements = propertyPath.Split('.');
            
            // 解析字段路径和类型
            List<FieldStringData> pathParseData = new List<FieldStringData>();
            int skipElement = 0;
            for (int i = 0; i < pathElements.Length; i++)
            {
                // 跳过集合的元素
                if (skipElement > 0)
                {
                    skipElement--;
                    continue;
                }
                
                // 集合字段
                if (i + 1 < pathElements.Length && pathElements[i + 1] == "Array")
                {
                    skipElement = 2;
                    var dataIndex = pathElements[i + 2];
                    dataIndex = dataIndex.Replace("data[", "");
                    dataIndex = dataIndex.Replace("]", "");
                    
                    var arrayIndex = int.Parse(dataIndex);
                    pathParseData.Add(new FieldStringData(FieldStringData.ArrayFieldInt, pathElements[i], arrayIndex));
                    continue;
                }
                
                pathParseData.Add(new FieldStringData(FieldStringData.FieldInt, pathElements[i], 0));
            }

            // 获取字段
            object cacheObject = rootObject;
            Type cacheType = rootObject.GetType();
            FieldInfo fieldInfo;
            foreach (var parseData in pathParseData)
            {
                if (parseData.dataTypeInt == FieldStringData.ArrayFieldInt)
                {
                    var parseName = parseData.dataName;
                    var parseIndex = parseData.arrayIndex;
                    fieldInfo = cacheType.GetField(parseName);
                    var listObj = fieldInfo.GetValue(cacheObject);
                    if (listObj is not IEnumerable list)
                    {
                        return null;
                    }
            
                    cacheObject = GetObjectFromArray(list, parseIndex);
                    cacheType = cacheObject.GetType();
                }
                else
                {
                    var parseName = parseData.dataName;
                    fieldInfo = cacheType.GetField(parseName);
                    cacheObject = fieldInfo.GetValue(cacheObject);
                    cacheType = cacheObject.GetType();
                }
            }
            
            return cacheObject;
        }
        
        private object GetObjectFromArray(IEnumerable array, int index)
        {
            int id = 0;
            foreach (var data in array)
            {
                if (id == index)
                {
                    return data;
                }

                id++;
            }

            Debug.LogWarning($"Function [GetObjectFromArray] index is out of range. index = {index}");
            return null;
        }
        
        #endregion
    }
}
