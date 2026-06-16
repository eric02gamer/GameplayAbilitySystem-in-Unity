using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PhysicsSetting
{
    public enum PhysicsLayer
    {
        Default = 1,
        Character = 6,
    }
    
    public enum PhysicsLayerMask
    {
        DefaultMask = 1 << PhysicsLayer.Default,
        CharacterMask = 1<<PhysicsLayer.Character,
        
        All = DefaultMask | CharacterMask,
    }
}
