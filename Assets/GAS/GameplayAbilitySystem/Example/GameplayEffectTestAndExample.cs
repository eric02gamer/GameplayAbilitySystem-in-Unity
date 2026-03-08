// using System;
// using System.Collections;
// using System.Collections.Generic;
// using Taco.Gameplay;
// using UnityEngine;
//
// namespace GAS.Editor
// {
//     public class GameplayEffectTestAndExample : MonoBehaviour
//     {
//         public AttributeRef healthAttr;
//         public AttributeRef attackAttr;
//
//         [Serializable]
//         public struct TestMission
//         {
//             public AbilitySystemComponent from;
//             public AbilitySystemComponent to;
//             public GameplayEffect gameplayEffect;
//             [Range(0,10)]public int level;
//             public int stackToAdd;
//         }
//         [Serializable]
//         public struct ActiveMission
//         {
//             public string name;
//             public ActiveEffectSpecHandle activeHandle;
//             public int stack;
//
//             public ActiveMission(ActiveEffectSpecHandle handle)
//             {
//                 name = handle.GetData().GameplayEffect.name;
//                 activeHandle = handle;
//                 stack = -1;
//             }
//         }
//         
//         [Header("测试配置")]
//         public int chooseMissionId;
//         public List<TestMission> testMissions;
//         public int chooseRemoveMissionId;
//         public List<ActiveMission> activeMissions;
//
//         #region 属性数据获取和监听
//
//         // [Header("数据展示")]
//         // public float showMaxHealth;
//         // public float showCurrentHealth;
//         // [Range(0,1)]public float showHealth;
//         // public float sourceMaxAttack;
//         // public float sourceCurrentAttack;
//         
//         // private void Start()
//         // {
//         //     if(targetAsc.TryGetAttribute(healthAttr, out var healthData))
//         //     {
//         //         showHealth = healthData.currentValue / healthData.baseValue;
//         //         showMaxHealth = healthData.baseValue;
//         //         showCurrentHealth = healthData.currentValue;
//         //         targetAsc.RegisterAttributeValueChangeCallback(healthAttr.AttributeHash, ShowHealth);
//         //     }
//         //     if(sourceAsc.TryGetAttribute(attackAttr, out var attackData))
//         //     {
//         //         sourceMaxAttack = attackData.baseValue;
//         //         sourceCurrentAttack = attackData.currentValue;
//         //         sourceAsc.RegisterAttributeValueChangeCallback(attackAttr.AttributeHash, ShowAttack);
//         //     }
//         // }
//         //
//         // private void ShowHealth(EventGameplayAttributeChangeArgs obj)
//         // {
//         //     showHealth = obj.newData.currentValue / obj.newData.baseValue;
//         //     showMaxHealth = obj.newData.baseValue;
//         //     showCurrentHealth = obj.newData.currentValue;
//         //     Debug.Log($"damage:{obj.newData - obj.oldData}");
//         // }
//         //
//         // private void ShowAttack(EventGameplayAttributeChangeArgs obj)
//         // {
//         //     sourceMaxAttack = obj.newData.baseValue;
//         //     sourceCurrentAttack = obj.newData.currentValue;
//         // }
//
//         #endregion
//
//         #region Test Gameplay Effect
//
//         [ContextMenu("Gameplay Effect Apply")]
//         private void GameplayEffectApply()
//         {
//             var mission = testMissions[chooseMissionId];
//             var applySource = mission.@from;
//             var applyTarget = mission.@to;
//
//             // // 创建 Context
//             // var contextHandle = applySource.MakeEffectContext();
//             // var context = contextHandle.GetData();
//             // context.SetOrigin(applySource.transform.position);
//             // contextHandle.SetData(context);
//             //
//             // // 创建 Spec
//             // var specHandle =
//             //     applyTarget.MakeOutgoingEffectSpec(mission.gameplayEffect, mission.level, contextHandle);
//             // var spec = specHandle.GetData();
//             // spec.SetStackCount(mission.stackToAdd);
//             // spec.SetLevel(mission.level);
//             // specHandle.SetData(spec);
//             //
//             // // （可选操作）数据转移给目标，清除自身数据
//             // var readyToApplySpecHandle = applyTarget.ReplicateEffectSpecToSelf(specHandle);
//             // contextHandle.ClearData();
//             // specHandle.ClearData();
//             //
//             // //  应用
//             // var activeHandle = applyTarget.ApplyGameplayEffectSpecToSelf(readyToApplySpecHandle);
//             
//             // 直接应用
//             var activeHandle = applyTarget.ApplyGameplayEffectToSelf(mission.gameplayEffect, applySource, mission.level, mission.stackToAdd);
//             
//             // 注意：如果是瞬时GE，必定是 IsValid == false
//             if (!activeHandle.IsValid)
//             {
//                 Debug.Log("Failed.");
//                 return;
//             }
//             
//             // （可选操作）监听添加和移除
//             void HandleAdding(ActiveEffectSpecHandle addingSpecHandle)
//             {
//                 if(!addingSpecHandle.Equals(activeHandle)) return;
//                 
//                 // Debug.Log($"Adding: {addingSpecHandle.GetData().GameplayEffect.name}");
//                 activeMissions.Add(new ActiveMission(addingSpecHandle));
//             }
//             void HandleRemove(ActiveEffectSpecHandle removingSpecHandle)
//             {
//                 if(!removingSpecHandle.Equals(activeHandle)) return;
//                 
//                 applyTarget.UnregisterOnAnyGameplayEffectAdded(HandleAdding);
//                 applyTarget.UnregisterOnAnyGameplayEffectRemoved(HandleRemove);
//                 var find = activeMissions.Find((activeMission => activeMission.activeHandle.Equals(removingSpecHandle)));
//                 Debug.Log($"Removing: {find.name}");
//                 activeMissions.Remove(find);
//             }
//             applyTarget.RegisterOnAnyGameplayEffectAdded(HandleAdding);
//             applyTarget.RegisterOnAnyGameplayEffectRemoved(HandleRemove);
//         }
//
//         [ContextMenu("Gameplay Effect Remove")]
//         private void GameplayEffectRemove()
//         {
//             var mission = activeMissions[chooseRemoveMissionId];
//
//             var targetAsc = mission.activeHandle.AscOwner;
//             // targetAsc.RemoveActiveEffectSpecWithHandle(mission.activeHandle, mission.stack);
//
//             targetAsc.RemoveActiveEffectSpecWithHandle(mission.activeHandle, targetAsc, mission.stack);
//
//             var source = mission.activeHandle.GetData().ContextHandle.GetData().Instigator;
//             targetAsc.RemoveActiveEffectSpecWithHandle(mission.activeHandle, targetAsc, mission.stack);
//         }
//
//         #endregion
//     }
// }
