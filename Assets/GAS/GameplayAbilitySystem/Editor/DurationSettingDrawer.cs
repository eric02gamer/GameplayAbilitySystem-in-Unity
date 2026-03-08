using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GAS.Editor
{
    [CustomPropertyDrawer(typeof(DurationSetting))]
    public class DurationSettingDrawer : PropertyDrawer
    {
        private MagnitudeView magnitudeView;
        private SerializedProperty magnitudeTypeProp;
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var policyProp = property.FindPropertyRelative("durationPolicy");
            var magnitudeProp = property.FindPropertyRelative("durationMagnitude");
            magnitudeTypeProp = magnitudeProp.FindPropertyRelative("magnitudeType");
            
            var root = new VisualElement();

            var propField1 = new EnumField(policyProp.displayName);
            propField1.BindProperty(policyProp);
            magnitudeView = new MagnitudeView(magnitudeProp);
            var propField2 = magnitudeView;

            bool hasDuration = policyProp.intValue == (int) DurationSetting.DurationPolicy.HasDuration;
            
            if(hasDuration)
                MagnitudeEnable();
            else
                MagnitudeDisable();
            propField1.RegisterValueChangedCallback(ChangeDurationPolicy);

            root.Add(propField1);
            root.Add(propField2);

            return root;
        }

        private void ChangeDurationPolicy(ChangeEvent<Enum> evt)
        {
            if(evt.newValue is not DurationSetting.DurationPolicy policy) return;
            
            if(policy == DurationSetting.DurationPolicy.HasDuration)
                MagnitudeEnable();
            else
                MagnitudeDisable();
        }

        private void MagnitudeEnable()
        {
            bool magnitudeIsFloatType = magnitudeTypeProp.intValue == (int) Magnitude.MagnitudeType.Float;
            magnitudeView.ShowAll(magnitudeIsFloatType);
        }

        private void MagnitudeDisable()
        {
            magnitudeView.HideAll();
        }
    }
}
