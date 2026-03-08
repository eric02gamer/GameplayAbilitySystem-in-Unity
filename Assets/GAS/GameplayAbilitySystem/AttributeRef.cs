using System;
using UnityEngine;

namespace GAS
{
    /// 属性的引用名，在 Inspector 配置的字符串，将自动计算 hash
    [Serializable]
    public class AttributeRef
    {
        public static int InvalidHash => 0;
        
        public string attributeName;
        
        // AttributeRefDrawer 会自动计算 attributeName 的 hash
        [SerializeField] private int attributeHash;

        public int AttributeHash => attributeHash;

        public AttributeRef(string newAttributeName)
        {
            attributeName = newAttributeName;
            attributeHash = GameplayUtilities.GetHash(newAttributeName);
        }
    }
}
