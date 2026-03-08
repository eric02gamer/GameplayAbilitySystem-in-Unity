// using UnityEngine;
// using UnityEngine.Assertions;
//
// namespace GAS
// {
//     [CreateAssetMenu(menuName = CreatePath+"TestAbility", fileName = "Test Ability")]
//     public class GameplayAbilityExample : GameplayAbility
//     {
//         private bool targetSet;
//         private Vector3 position;
//         public override InstanceStrategy Instance => InstanceStrategy.InstancedPerActor;
//
//         [Header("能力配置")]
//         public GameplayEffect pauseStaminRecover;
//         
//         public override void OnAddTargetData(GameplayTarget target)
//         {
//             position = target.position;
//             targetSet = true;
//         }
//
//         public override bool CanActivateAbility()
//         {
//             return targetSet;
//         }
//
//         protected override void ActivateAbility()
//         {
//             var owner = GetOwner();
//             if (!owner)
//                 throw new AssertionException("No Asc set", this.name);
//             ApplyCostGameplayEffect();
//             if(pauseStaminRecover)
//                 owner.ApplyGameplayEffectToSelf(pauseStaminRecover, owner, GetLevel());
//             
//             Debug.Log($"Test Ability, {targetSet}, target:{position}");
//             EndAbility();
//         }
//
//         protected override void OnEndAbility(bool cancelled)
//         {
//             targetSet = false;
//         }
//     }
// }
