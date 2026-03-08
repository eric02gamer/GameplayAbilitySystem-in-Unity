using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    public class CapturedAttributesSpec
    {
        private readonly Dictionary<int, GameplayAttributeData> dataDict;
        
        public CapturedAttributesSpec(AbilitySystemComponent asc)
        {
            dataDict = new Dictionary<int,GameplayAttributeData>();
            if(asc.AttributeRuntimeData == null) return;
            
            foreach (var dataPair in asc.AttributeRuntimeData)
                dataDict.Add(dataPair.Key, dataPair.Value);
        }

        private bool TryGetDataInternal(int hash, out GameplayAttributeData data)
        {
            if (dataDict == null || !dataDict.ContainsKey(hash))
            {
                data = new GameplayAttributeData(0);
                return false;
            }

            data = dataDict[hash];
            return true;
        }
        
        public bool TryGetData(AttributeRef attribute, out GameplayAttributeData data)
        {
            var hash = attribute.AttributeHash;
            return TryGetDataInternal(hash, out data);
        }

        public bool TryGetData(int hash, out GameplayAttributeData data)
        {
            return TryGetDataInternal(hash, out data);
        }
    }
}
