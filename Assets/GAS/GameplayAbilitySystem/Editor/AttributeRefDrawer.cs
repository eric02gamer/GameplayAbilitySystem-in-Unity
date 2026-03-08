using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GAS.Editor
{
    [CustomPropertyDrawer(typeof(AttributeRef))]
    public class AttributeRefDrawer : PropertyDrawer
    {
        private SerializedProperty attributeHashProperty;
        private const float LayoutHeight = 30;
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var attributeNameProp = property.FindPropertyRelative("attributeName");
            attributeHashProperty = property.FindPropertyRelative("attributeHash");
            
            var view = new TextField($"<{property.displayName}>");
            view.BindProperty(attributeNameProp);
            view.style.height = LayoutHeight;
            
            view.RegisterValueChangedCallback(ChangeAttributeName);
            
            return view;
        }

        private void ChangeAttributeName(ChangeEvent<string> evt)
        {
            if(attributeHashProperty == null) return;
            if(evt.newValue == evt.previousValue) return;

            attributeHashProperty.intValue =
                string.IsNullOrEmpty(evt.newValue) ? AttributeRef.InvalidHash : GameplayUtilities.GetHash(evt.newValue);
            attributeHashProperty.serializedObject.ApplyModifiedProperties();
        }
    }
}
