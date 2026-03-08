// using UnityEngine;
//
// namespace GAS
// {
//     [CreateAssetMenu(menuName = CreatePath + "Gameplay AttributeSet Sample")]
//     public class GameplayAttributeSetExample : GameplayAttributeSet
//     {
//         private readonly int staminaHash = GameplayUtilities.GetHash("Stamina");
//         
//         // 创建时默认添加属性 Stamina
//         public GameplayAttributeSetExample()
//         {
//             data = new InitAttributeData[1];
//             data[0] = new InitAttributeData()
//             {
//                 attributeRef = new AttributeRef("Stamina"),
//                 baseValue = 100,
//             };
//         }
//         
//         protected override GameplayAttributeData PreAttributeChange(int changingHash, GameplayAttributeData oldValue,
//             GameplayAttributeData newValue)
//         {
//             if (changingHash == staminaHash)
//             {
//                 newValue.currentValue = Mathf.Clamp(newValue.currentValue, 0, newValue.baseValue);
//                 return newValue;
//             }
//
//             return base.PreAttributeChange(changingHash, oldValue, newValue);
//         }
//     }
// }
