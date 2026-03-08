using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GAS.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTagCountContainer))]
    public class GameplayTagCountContainerDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var gameplayTagsProp = property.FindPropertyRelative("gameplayTags");
            
            var root = new PropertyField(){label = property.name};
            root.BindProperty(gameplayTagsProp);

            return root;
        }
    }
}
