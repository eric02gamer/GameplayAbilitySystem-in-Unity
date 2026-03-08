using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GAS.Editor
{
    [CustomPropertyDrawer(typeof(Magnitude))]
    public class MagnitudeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new MagnitudeView(property);
        }
    }
    
    public class MagnitudeView: VisualElement
    {
        private readonly VisualElement floatViewElement;
        private readonly VisualElement attributeViewElement;
        private readonly VisualElement rootView;

        // 参考了 VisualElement.layout 获取的信息，通常一个字段的高度为 20 + 1
        private const float FloatTypeHeight = 48;
        private const float AttributeBasedTypeHeight = 167;

        private const float AttributeEnableHeight = 132;
        private const float FloatEnableHeight = 22;
        private const float DisableHeight = 0;

        public MagnitudeView(SerializedProperty property)
        {
            // 属性数据
            var magnitudeTypeProperty = property.FindPropertyRelative("magnitudeType");
            
            var floatProp = property.FindPropertyRelative("floatMagnitude");
            
            var attributeNameProp = property.FindPropertyRelative("attributeToCapture");
            var attributeSourceProp = property.FindPropertyRelative("attributeSource");
            var attributeCalculationTypeProp = property.FindPropertyRelative("attributeCalculationType");
            var coefficientProp = property.FindPropertyRelative("coefficient");
            var preMultiplyAddictiveValueProp = property.FindPropertyRelative("preMultiplyAddictiveValue");
            var postMultiplyAddictiveValueProp = property.FindPropertyRelative("postMultiplyAddictiveValue");
            
            // 界面元素
            var root = new Box {name = property.displayName};
            
            Add(root);
            
            style.paddingLeft = 5;
            
            var magnitudeEnumView = new EnumField("Magnitude Type", Magnitude.MagnitudeType.Float);
            magnitudeEnumView.BindProperty(magnitudeTypeProperty);

            // MagnitudeType 为 Float
            var floatView = new PropertyField()
            {
                style =
                {
                    position = new StyleEnum<Position>(Position.Absolute),
                    top = 22,
                    left = 20,
                    right = 5,
                }
            };
            floatView.BindProperty(floatProp);

            // MagnitudeType 为 AttributeBased
            var attributeView = new VisualElement()
            {
                style =
                {
                    position = new StyleEnum<Position>(Position.Absolute),
                    top = 22,
                    left = 20,
                    right = 5,
                }
            };
            
            // 添加界面元素
            var attrField = new PropertyField();
            attrField.BindProperty(attributeNameProp);
            var attrSourceField = new PropertyField();
            attrSourceField.BindProperty(attributeSourceProp);
            var attrCalculationField = new PropertyField();
            attrCalculationField.BindProperty(attributeCalculationTypeProp);
            var coefficientField = new PropertyField();
            coefficientField.BindProperty(coefficientProp);
            var preAddictiveField = new PropertyField();
            preAddictiveField.BindProperty(preMultiplyAddictiveValueProp);
            var postAddictiveField = new PropertyField();
            postAddictiveField.BindProperty(postMultiplyAddictiveValueProp);
            attributeView.Add(attrField);
            attributeView.Add(attrSourceField);
            attributeView.Add(attrCalculationField);
            attributeView.Add(coefficientField);
            attributeView.Add(preAddictiveField);
            attributeView.Add(postAddictiveField);

            // 控制 Attribute Name 显示和隐藏
            var isFloatType = magnitudeTypeProperty.intValue == (int)Magnitude.MagnitudeType.Float;
            rootView = root;
            floatViewElement = floatView;
            attributeViewElement = attributeView;
            
            if (isFloatType)
                FloatViewEnable();
            else
                AttributeViewEnable();
            magnitudeEnumView.RegisterValueChangedCallback(ChangeMagnitudeType);
            
            // 组建界面
            root.Add(magnitudeEnumView);
            root.Add(floatView);
            root.Add(attributeView);
        }
        
        private void ChangeMagnitudeType(ChangeEvent<Enum> evt)
        {
            if (evt.newValue is not Magnitude.MagnitudeType magnitudeType) return;
            
            var isFloatType = magnitudeType == (int)Magnitude.MagnitudeType.Float;
            if (isFloatType)
                FloatViewEnable();
            else
                AttributeViewEnable();
        }

        private void FloatViewEnable()
        {
            attributeViewElement.visible = false;
            // 有些元素不能正常隐藏，只能缩小了
            attributeViewElement.style.height = DisableHeight;
            floatViewElement.visible = true;
            floatViewElement.style.height = FloatEnableHeight;
            
            rootView.style.height = FloatTypeHeight;
        }

        private void AttributeViewEnable()
        {
            floatViewElement.visible = false;
            floatViewElement.style.height = DisableHeight;
            attributeViewElement.style.height = AttributeEnableHeight;
            attributeViewElement.visible = true;
            
            rootView.style.height = AttributeBasedTypeHeight;
        }

        /// 有些元素不能正常隐藏，MagnitudeDrawer改进的隐藏方法如下
        public void HideAll()
        {
            if (rootView == null || floatViewElement == null || attributeViewElement == null) return;
            
            floatViewElement.visible = false;
            floatViewElement.style.height = DisableHeight;
            attributeViewElement.visible = false;
            attributeViewElement.style.height = DisableHeight;
            rootView.visible = false;
            rootView.style.height = DisableHeight;
        }

        public void ShowAll(bool asFloatType)
        {
            if (rootView == null || floatViewElement == null || attributeViewElement == null) return;
            
            rootView.visible = true;
            if (asFloatType)
                FloatViewEnable();
            else
                AttributeViewEnable();
        }
    }
}
