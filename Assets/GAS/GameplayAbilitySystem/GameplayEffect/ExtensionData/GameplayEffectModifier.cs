using System;
using UnityEngine;

namespace GAS
{
    [Serializable]
    public class GameplayEffectModifier
    {
        public enum ModifierOperation
        {
            Add,
            Multiply,
            Divide,
            Override,
        }

        [SerializeField] private AttributeRef modifierAttribute;
        public AttributeRef ModifierAttribute => modifierAttribute;
        
        public ModifierOperation modifierOp;

        public bool isStackingModifier;

        [Header("Magnitude")]
        public Magnitude magnitude;

        [Space(5),Header("Source Tags")]
        public GameplayTagsRequirement sourceTagRequirement;
        
        [Space(5),Header("Target Tags")]
        public GameplayTagsRequirement targetTagRequirement;
    }
}
