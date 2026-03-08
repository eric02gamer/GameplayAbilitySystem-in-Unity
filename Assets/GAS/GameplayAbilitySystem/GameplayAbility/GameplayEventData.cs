using System;
using System.Collections;
using System.Collections.Generic;
using EGF;
using UnityEngine;

namespace GAS
{
    public struct GameplayEventData
    {
        public GameplayTagHash eventTag;
        public AbilitySystemComponent instigator;
        public Vector3 origin;
        public float eventMagnitude;
        
        private IGameplayEventExtensionData extensionData;
        public void SetExtensionData(IGameplayEventExtensionData data)
        {
            extensionData = data;
        }
        public bool TryGetExtensionData<T>(out T data) where T: IGameplayEventExtensionData
        {
            if (extensionData is not T dataValue)
            {
                data = default;
                return false;
            }

            data = dataValue;
            return true;
        }
    }

    public interface IGameplayEventExtensionData
    {
        
    }
    
    public struct GameplayTarget
    {
        public Vector3 position;
        public Vector3 direction;
        public GameObject gameObject;
        public AbilitySystemComponent targetAsc;
    }
}
