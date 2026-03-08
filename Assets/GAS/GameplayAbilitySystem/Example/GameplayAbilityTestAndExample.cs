// using System;
// using System.Collections.Generic;
// using UnityEngine;
//
// namespace GAS
// {
//     public class GameplayAbilityTestAndExample : MonoBehaviour
//     {
//         [Serializable]
//         public struct TestMission
//         {
//             public AbilitySystemComponent from;
//             public AbilitySystemComponent to;
//             public GameplayAbility ability;
//             [Range(0,10)]public int level;
//         }
//         
//         [Serializable]
//         public struct OwnedAbility
//         {
//             public string name;
//             public GameplayAbilitySpecHandle handle;
//         }
//
//         [Header("测试配置")]
//         public int chooseMissionId;
//         public List<TestMission> testMissions;
//         public int chooseGivenAbilityId;
//         public List<OwnedAbility> givenAbilities;
//         
//         [ContextMenu("Give Gameplay Ability")]
//         private void GiveGameplayAbility()
//         {
//             var mission = testMissions[chooseMissionId];
//             
//             var handle = mission.to.GiveAbility(mission.ability, mission.level);
//             var oa = new OwnedAbility() {name = mission.ability.name, handle = handle};
//             givenAbilities.Add(oa);
//         }
//
//         [ContextMenu("Activate Gameplay Ability")]
//         private void ActivateGameplayAbility()
//         {
//             var ownedAbility = givenAbilities[chooseGivenAbilityId];
//
//             var asc = ownedAbility.handle.AscOwner;
//             var handle = ownedAbility.handle;
//             
//             handle.AddTarget(new GameplayTarget(){position = transform.position});
//             
//             if(!asc.TryActivateAbility(handle))
//                 Debug.Log("Cannot Activate.");
//         }
//     }
// }
