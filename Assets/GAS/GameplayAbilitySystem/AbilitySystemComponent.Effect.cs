using System;
using System.Collections.Generic;
using System.Linq;
using EGF;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GAS
{
    public partial class AbilitySystemComponent
    {
        private readonly Dictionary<GameplayEffectContextHandle, GameplayEffectContext> effectContexts =
            new Dictionary<GameplayEffectContextHandle, GameplayEffectContext>();

        private readonly Dictionary<GameplayEffectSpecHandle, GameplayEffectSpec> effectSpecs =
            new Dictionary<GameplayEffectSpecHandle, GameplayEffectSpec>();
        // add remove GE callback
        private event Action<ActiveEffectSpecHandle> ONAddEffect;
        private event Action<ActiveEffectSpecHandle> ONRemoveEffect;

        /// 激活中的 active GE Spec，
        /// 修改需要使用 ActiveSpecChangeCacheClear()、ActiveSpecChangeCacheAddOrUpdate()、ActiveSpecChangeCacheApply()
        private readonly Dictionary<ActiveEffectSpecHandle, ActiveGameplayEffectSpec> activeEffectSpecs =
            new Dictionary<ActiveEffectSpecHandle, ActiveGameplayEffectSpec>();
        
        // stack
        private readonly Dictionary<GameplayEffect, ActiveEffectSpecHandle> stackEffectHandles =
            new Dictionary<GameplayEffect, ActiveEffectSpecHandle>();
        private readonly Dictionary<GameplayEffect, int> effectStackSourceCount = new Dictionary<GameplayEffect, int>();
        
        // GE granted ability
        private readonly Dictionary<ActiveEffectSpecHandle, List<GrantedAbilityDataFromEffect>> effectGrantedAbilityHandles =
            new Dictionary<ActiveEffectSpecHandle, List<GrantedAbilityDataFromEffect>>();
        
        // 临时缓存减少GC
        // --------------------------------------------------
        // 属性修改器计算缓存
        private readonly Dictionary<ActiveEffectSpecHandle,ActiveGameplayEffectSpec> activeEffectSpecsChangeCache = new Dictionary<ActiveEffectSpecHandle,ActiveGameplayEffectSpec>();
        private readonly HashSet<int> modifierTargetAttrs = new();
        private readonly Dictionary<int, GameplayAttributeData> modifierTargetAttrsSnapshot = new ();
        private readonly Dictionary<int, GameplayAttributeData> attrsLinkChangeRecords = new (); // 由于关联属性修改引发的属性变化，记录并手动触发变化事件
        private readonly List<(int, float)> modifierChange = new ();
        private readonly Dictionary<int, float> modifierOverride = new ();
        
        
        // stack 更改任务数据
        private struct ActiveEffectSpecStackChange
        {
            public ActiveEffectSpecHandle handle;
            public AbilitySystemComponent stackChangeSource;
            /// <summary>
            /// 如果更改值为负数，则添加到极限或是全部移除
            /// </summary>
            public int stackChangeCount;
            public bool removeMod;
        }
        /*
         * TODO: 现阶段时序仍有漏洞：
         * UpdateLoopActiveEffectSpecs 期间，GAS扩展代码有更改下面对列的可能性，引发报错：
         * InvalidOperationException: Collection was modified; enumeration operation may not execute.
         * 例如在 GameplayAbility 扩展时，刚激活技能（ActivateAbility）就调用 ApplyGameplayEffectToSelf 操作。
         */
        // active GE Spec 更改队列
        private readonly List<ActiveGameplayEffectSpec> waitToAddActiveEffectSpecs = new ();
        private readonly List<ActiveEffectSpecStackChange> waitToChangeActiveEffectSpecsStack = new ();
        private readonly HashSet<ActiveEffectSpecHandle> waitToRemoveActiveEffectSpecs = new ();
        private readonly HashSet<GameplayEffect> removedGameplayEffectConfigs = new();
        
        private void InitEffectCache()
        {
            
        }

        private void ReleaseEffectCache()
        {
            effectContexts.Clear();
            effectSpecs.Clear();
            ONAddEffect = null;
            ONRemoveEffect = null;
            
            activeEffectSpecs.Clear();
            
            stackEffectHandles.Clear();
            effectStackSourceCount.Clear();
            
            effectGrantedAbilityHandles.Clear();
            
            activeEffectSpecsChangeCache.Clear();
            modifierTargetAttrs.Clear();
            modifierTargetAttrsSnapshot.Clear();
            modifierChange.Clear();
            modifierOverride.Clear();
            
            waitToAddActiveEffectSpecs.Clear();
            waitToChangeActiveEffectSpecsStack.Clear();
            waitToRemoveActiveEffectSpecs.Clear();
        }

        private ActiveEffectSpecHandle ApplyGameplayEffectInternal(GameplayEffect gameplayEffect, AbilitySystemComponent sourceAsc, int level, int stack)
        {
            var gameplayEffectSpecHandle = MakeReplicaOutgoingEffectSpec(gameplayEffect, sourceAsc, level, stack);
            return ApplyGameplayEffectSpec(gameplayEffectSpecHandle);
        }
        
        private ActiveEffectSpecHandle ApplyGameplayEffectInternal(GameplayEffect gameplayEffect, GameplayEffectContextHandle contextHandle, int level, int stack)
        {
            var gameplayEffectSpecHandle = MakeReplicaOutgoingEffectSpec(gameplayEffect, contextHandle, level, stack);
            return ApplyGameplayEffectSpec(gameplayEffectSpecHandle);
        }

        private ActiveEffectSpecHandle ApplyConditionalGameplayEffectInternal(ConditionalEffect conditionalEffect, GameplayEffectContextHandle contextHandle, int level)
        {
            var requiredTags = conditionalEffect.requiredSourceTags;
            if(!requiredTags.IsEmpty() && !ActorTags.Contains(requiredTags)) return new ActiveEffectSpecHandle();

            return ApplyGameplayEffectInternal(conditionalEffect.effectClass, contextHandle, level, 1);
        }

        #region Tag & Immunity
        
        /// 更新 Tag 和 ImmunityList
        // private void RefreshEffectTagsAndImmunityList()
        // {
        //     dynamicEffectGrantTags.ClearTagRuntime();
        //     immunityTagList.Clear();
        //     foreach (var element in activeEffectSpecs)
        //     {
        //         var effect = element.Value.GameplayEffect;
        //         dynamicEffectGrantTags.AddContainerTagsRuntime(effect.grantedTags);
        //         immunityTagList.Add(effect);
        //     }
        //
        //     RefreshActorTags();
        // }
        private bool ImmunityContainsTag(GameplayTagContainer tagContainer)
        {
            foreach (var item in activeEffectSpecs)
            {
                var requirement = item.Value.GameplayEffect.grantedImmunityTags;
                if (requirement.CheckRequirement(tagContainer, false))
                    return true;
            }

            return false;
        }

        private void RemoveActiveEffectSpecWithTagsInternal(GameplayTagContainer tags, int stackToRemove, bool withAssetTags, bool withGrantedTags)
        {
            if(!withAssetTags && !withGrantedTags) return;
            
            foreach (var pair in activeEffectSpecs)
            {
                var activeSpecEffect = pair.Value.GameplayEffect;
                if (withAssetTags && tags.ContainsAny(activeSpecEffect.assetTags) ||
                    withGrantedTags && tags.ContainsAny(activeSpecEffect.grantedTags))
                {
                    // Remove Stack
                    WaitChangeActiveEffectSpecStack(pair.Key, null, stackToRemove, true);
                }
            }
        }

        #endregion
        
        #region Effect Context

        internal void ClearEffectContext(GameplayEffectContextHandle contextHandle)
        {
            if (effectContexts.ContainsKey(contextHandle))
                effectContexts.Remove(contextHandle);
        }
        internal GameplayEffectContext GetEffectContext(GameplayEffectContextHandle handle)
        {
            return effectContexts.ContainsKey(handle) ? effectContexts[handle] : new GameplayEffectContext();
        }
        internal void SetEffectContext(GameplayEffectContext effectContext)
        {
            var handle = effectContext.Handle;
            if (effectContexts.ContainsKey(handle))
                effectContexts[handle] = effectContext;
        }

        #endregion

        #region Effect Spec

        internal void ClearEffectSpec(GameplayEffectSpecHandle specHandle)
        {
            if (effectSpecs.ContainsKey(specHandle))
                effectSpecs.Remove(specHandle);
        }
        internal GameplayEffectSpec GetEffectSpec(GameplayEffectSpecHandle handle)
        {
            return effectSpecs.ContainsKey(handle) ? effectSpecs[handle] : new GameplayEffectSpec();
        }
        internal void SetEffectSpec(GameplayEffectSpec effectSpec)
        {
            var handle = effectSpec.Handle;
            if (effectSpecs.ContainsKey(handle))
                effectSpecs[handle] = effectSpec;
        }
        
        private GameplayEffectSpecHandle MakeOutgoingEffectSpecInternal(GameplayEffect gameplayEffect, int level, GameplayEffectContextHandle contextHandle)
        {
            var spec = new GameplayEffectSpec();
            spec.Initialize(this, gameplayEffect, contextHandle, level);
            
            var handle = spec.Handle;
            effectSpecs.Add(handle, spec);
            return handle;
        }
        
        #endregion

        #region Replica GE Data
        
        private GameplayEffectContextHandle ReplicateEffectContext(GameplayEffectContextHandle contextHandle)
        {
            var addingContext = contextHandle.GetData().TransferToNewAsc(this);
            effectContexts.Add(addingContext.Handle, addingContext);
            
            return addingContext.Handle;
        }
        
        /// <summary>
        /// 复制和保存 GE Context 和 GE Spec 副本
        /// </summary>
        private GameplayEffectSpecHandle ReplicateEffectSpec(GameplayEffectSpecHandle specHandle)
        {
            var oldSpec = specHandle.GetData();
            var addingContextHandle = ReplicateEffectContext(oldSpec.ContextHandle);
            
            var addingSpec = oldSpec.TransferToNewAsc(this, addingContextHandle);
            effectSpecs.Add(addingSpec.Handle, addingSpec);
            
            return addingSpec.Handle;
        }

        /// <summary>
        /// 在 GE Context 基础上创建副本 GE Spec，用于 Conditional GE
        /// </summary>
        private GameplayEffectSpecHandle MakeReplicaOutgoingEffectSpec(GameplayEffect gameplayEffect, GameplayEffectContextHandle contextHandle, int level, int stack)
        {
            var context = contextHandle.GetData().TransferToNewAsc(this);
            effectContexts.Add(context.Handle, context);

            var spec = GameplayEffectSpec.CreateAsReplica(this, gameplayEffect, context.Handle, level);
            spec.SetStackCount(stack);
            var specHandle = spec.Handle;
            effectSpecs.Add(specHandle, spec);
            
            return specHandle;
        }

        /// <summary>
        /// 以副本形式创建默认 GE Context 和 GE Spec，用于自动生成的 GE 上下文
        /// </summary>
        private GameplayEffectSpecHandle MakeReplicaOutgoingEffectSpec(GameplayEffect gameplayEffect, AbilitySystemComponent sourceAsc, int level, int stack)
        {
            var context = GameplayEffectContext.CreateAsReplica(sourceAsc, this);
            context.SetEffectCauserObject(sourceAsc.gameObject);
            context.SetOrigin(sourceAsc.transform.position);
            effectContexts.Add(context.Handle, context);

            var spec = GameplayEffectSpec.CreateAsReplica(this, gameplayEffect, context.Handle, level);
            spec.SetStackCount(stack);
            var specHandle = spec.Handle;
            effectSpecs.Add(specHandle, spec);
            
            return specHandle;
        }
        
        #endregion
        
        #region Active Effect Spec
        
        private bool TryGetRuntimeActiveEffectSpec(ActiveEffectSpecHandle handle, out ActiveGameplayEffectSpec spec)
        {
            // 检查运行时缓存
            var containsKey = activeEffectSpecs.ContainsKey(handle);
            spec = containsKey? activeEffectSpecs[handle]: new ActiveGameplayEffectSpec();
            return containsKey;
        }
        internal ActiveGameplayEffectSpec GetActiveEffectSpec(ActiveEffectSpecHandle handle)
        {
            // 排除移除队列
            if(waitToRemoveActiveEffectSpecs.Contains(handle))
                return new ActiveGameplayEffectSpec();
            // 检查运行时缓存
            if (activeEffectSpecs.ContainsKey(handle))
                return activeEffectSpecs[handle];
            // 检查添加队列
            foreach (var checkingSpec in waitToAddActiveEffectSpecs)
            {
                if (checkingSpec.Handle.Equals(handle))
                    return checkingSpec;
            }
            // 查找失败
            return new ActiveGameplayEffectSpec();
        }
        /// 检查和清理 Replica 数据
        private static void CheckAndRemoveReplicaEffectSpec(GameplayEffectSpecHandle effectSpecHandle)
        {
            var effectSpec = effectSpecHandle.GetData();
            var context = effectSpec.ContextHandle.GetData();
            if(effectSpec.IsReplica)
                effectSpec.Handle.ClearData();
            if(context.IsReplica)
                context.Handle.ClearData();
        }
        
        private ActiveEffectSpecHandle ApplyGameplayEffectSpec(GameplayEffectSpecHandle effectSpecHandle)
        {
            ActiveEffectSpecHandle failure = new ActiveEffectSpecHandle();
            
            var effectSpec = effectSpecHandle.GetData();
            if (!effectSpec.IsValid) return failure;
            
            // 默认无效的处理
            ActiveEffectSpecHandle Fail()
            {
                CheckAndRemoveReplicaEffectSpec(effectSpec.Handle);
                return failure;
            }
            var effect = effectSpec.GameplayEffect;
            
            // 判断能否应用GE：标签检查
            if (effect.removalTagsRequirement.CheckRequirement(ActorTags, false)) return Fail();
            if (ImmunityContainsTag(effect.assetTags)) return Fail();
            if (ImmunityContainsTag(effect.grantedTags)) return Fail();
            // 判断能否应用GE：应用条件
            if (effect.applicationRequirements.Any(requirement => !requirement.CanApplyGameplayEffect(effectSpec, this)))
                return Fail();
            // 判断能否应用GE：应用概率
            var chance = Random.Range(0, 1f) - FEpsilon;
            if (chance > effectSpec.ChanceToApply) return Fail();
            
            // 应用成功
            
            // 分类型应用GE修改
            ActiveGameplayEffectSpec activeEffectSpec = new ActiveGameplayEffectSpec();
            switch (effectSpec.DurationPolicy)
            {
                case DurationSetting.DurationPolicy.Instant:
                    InstantEffectApplyAttributesModify(effectSpec);
                    break;
                
                case DurationSetting.DurationPolicy.Infinite:
                    activeEffectSpec = activeEffectSpec.Initialize(this, effectSpec);
                    break;
                
                case DurationSetting.DurationPolicy.HasDuration:
                    if (effectSpec.GetApplyDuration(this) > FEpsilon)
                        activeEffectSpec = activeEffectSpec.Initialize(this, effectSpec);
                    break;
                
                default:break;
            }
            
            // 移除 removeGameplayEffectsWithTags 标记的 Effect
            RemoveActiveEffectSpecWithTagsInternal(effect.removeGameplayEffectsWithTags, -1, true, true);
            
            // 触发条件GE
            foreach (var conditionalEffect in effect.conditionalGameplayEffects)
                ApplyConditionalGameplayEffectInternal(conditionalEffect, effectSpec.ContextHandle, effectSpec.Level);
            
            // 赋予能力后 DoNothing，在这一阶段添加，允许非持续GE添加
            foreach (var grantAbility in effect.grantAbilities)
            {
                if(grantAbility.removalPolicy != EffectGrantAbility.RemovalPolicy.DoNothing) continue;

                var ability = grantAbility.ability;
                if (ability.AutoActiveOnEffectGranted)
                    GiveAbilityAndActivateOnceInternal(ability, effectSpec.Level);
                else
                    GiveAbilityInternal(ability, effectSpec.Level);
            }
            
            // 非持续性 GE 不添加 Active Effect Spec
            if (!activeEffectSpec.IsValid) return Fail();
            
            // 添加 Active Effect Spec
            WaitAddActiveEffectSpec(activeEffectSpec);
            if (activeEffectSpec.EnableStack && TryGetStackEffectHandleByEffect(effect, out var stackHandle))
                return stackHandle;
            return activeEffectSpec.Handle;
        }

        #endregion

        #region [Modify Attributes Fixed]

        private void InstantEffectApplyAttributesModify(GameplayEffectSpec effectSpec)
        {
            // 收集 Modifier 并记录可能修改的属性的 Snapshot
            modifierTargetAttrs.Clear();
            modifierTargetAttrsSnapshot.Clear();
            CollectGEModifierTargetAttributeHash(effectSpec.GameplayEffect, modifierTargetAttrs);
            CollectActiveGEModifierTargetAttributeHash(modifierTargetAttrs);
            MakeAttributesSnapshot(modifierTargetAttrs, modifierTargetAttrsSnapshot);
            
            modifierChange.Clear();
            modifierOverride.Clear();
            
            var spec = effectSpec;
            var sourceAsc = spec.ContextHandle.GetData().Instigator;
            EffectCalculateModifiers(spec, modifierChange, modifierOverride);
            EffectApplyAttributesModify(modifierChange, modifierOverride, sourceAsc, true); // 改动 Base Value
            EffectExecutionsTriggerConditionalGE(spec);

            EffectRefreshActivateGameplayModifiers();
            
            // 对比可能修改的属性的 Snapshot，如果改变则触发改变动画
            CheckAttributesChangeAndTriggerChangeEvent(modifierTargetAttrsSnapshot, sourceAsc);
        }

        private void EffectCalculateModifiers(
            GameplayEffectSpec effectSpec,
            List<(int, float)> change,
            Dictionary<int, float> @override)
        {
            var level = effectSpec.Level;
            var effect = effectSpec.GameplayEffect;
            var context = effectSpec.ContextHandle.GetData();
            var sourceAsc = context.Instigator;
            
            // modifiers
            var modifiers = effect.modifiers;
            foreach (var modifier in modifiers)
            {
                var modifierAttributeHash = modifier.ModifierAttribute.AttributeHash;
                
                // modifier condition
                if (!TryGetAttributeInternal(modifierAttributeHash, out var data)) continue;
                if(!modifier.sourceTagRequirement.CheckRequirement(sourceAsc.ActorTags, true)) continue;
                if(!modifier.targetTagRequirement.CheckRequirement(ActorTags,true)) continue;
                
                var magnitude = modifier.magnitude;
                float modifierValue = 0;
                switch (magnitude.magnitudeType)
                {
                    case Magnitude.MagnitudeType.Float:
                        modifierValue = magnitude.GetValueFloat(level);
                        break;
                    
                    case Magnitude.MagnitudeType.AttributeBased when magnitude.attributeSource is Magnitude.AttributeSource.Source:
                        modifierValue = context.AttributesSnapshotCaptured
                            ? magnitude.GetValueCapturedAttribute(context.CapturedAttributesSnapshot, level)
                            : magnitude.GetValueAttributeSource(sourceAsc, level);
                        break;
                    
                    case Magnitude.MagnitudeType.AttributeBased when magnitude.attributeSource is Magnitude.AttributeSource.Target:
                        modifierValue = magnitude.GetValueAttributeTarget(this, level);
                        break;
                }
                
                var beforeModifierValue = data.baseValue;
                var applyStackCount = modifier.isStackingModifier ? effectSpec.StackCount : 1;
                switch (modifier.modifierOp)
                {
                    case GameplayEffectModifier.ModifierOperation.Multiply:
                        modifierValue *= applyStackCount;
                        modifierValue = beforeModifierValue * modifierValue - beforeModifierValue;
                        change.Add((modifierAttributeHash, modifierValue));
                        break;
                    
                    case GameplayEffectModifier.ModifierOperation.Divide:
                        modifierValue *= applyStackCount;
                        modifierValue = Mathf.Abs(modifierValue) > 0.001f? (beforeModifierValue / modifierValue - beforeModifierValue): 0;
                        change.Add((modifierAttributeHash, modifierValue));
                        break;
                    
                    case GameplayEffectModifier.ModifierOperation.Override:
                        var overrideModify = modifierValue;
                        @override.Add(modifierAttributeHash, overrideModify); // 改为添加到 override
                        break;
                    
                    case GameplayEffectModifier.ModifierOperation.Add:
                        modifierValue *= applyStackCount;
                        change.Add((modifierAttributeHash, modifierValue));
                        break;
                    
                    default:
                        break;
                }
            }

            foreach (var execution in effect.executions)
            {
                var calculation = execution.executionCalculation;
                if (calculation && calculation is CustomEffectExecutionModifierCalculation modifierCalculation)
                {
                    modifierCalculation.ModifierExecute(effectSpec, this, change, @override);
                }
            }
        }
        /// 根据属性修改缓存来应用 GE 属性修改
        private void EffectApplyAttributesModify(List<(int, float)> change, Dictionary<int, float> overrideChange, AbilitySystemComponent source, bool modifyBase = false)
        {
            foreach (var valueTuple in change)
            {
                var key = valueTuple.Item1;
                if(overrideChange.ContainsKey(key)) continue;
                
                var modifyValue = valueTuple.Item2;
                if (!TryGetAttributeInternal(key, out var data)) continue;
                
                var modifyAttribute = new GameplayAttributeData()
                {
                    baseValue = modifyBase? modifyValue: 0,
                    currentValue = modifyBase? 0: modifyValue,
                };
                data += modifyAttribute;
                SetAttributeInternal(key, data);
            }

            foreach (var pair in overrideChange)
            {
                var key = pair.Key;
                if (!TryGetAttributeInternal(key, out var data)) continue;
                
                var modifyAttribute = new GameplayAttributeData()
                {
                    baseValue = modifyBase? pair.Value: 0,
                    currentValue = modifyBase? 0: pair.Value,
                };
                data = modifyAttribute;
                SetAttributeInternal(key, data);
            }
        }

        /// Activate GE 修饰器刷新 Current Value
        private void EffectRefreshActivateGameplayModifiers()
        {
            ResetAllAttributesCurrentValue();
            
            foreach (var activeEffectSpecDataPair in activeEffectSpecs)
            {
                modifierChange.Clear();
                modifierOverride.Clear();
                
                var activeSpec = activeEffectSpecDataPair.Value;
                var spec = activeSpec.SpecHandle.GetData();
                var sourceAsc = spec.ContextHandle.GetData().Instigator;
                
                EffectCalculateModifiers(spec, modifierChange, modifierOverride);
                EffectApplyAttributesModify(modifierChange, modifierOverride, sourceAsc);
            }
        }

        
        /// <summary>
        /// 周期性时机执行 Execution
        /// </summary>
        /// <param name="effectSpec"></param>
        private void EffectExecutionsTriggerConditionalGE(GameplayEffectSpec effectSpec)
        {
            var level = effectSpec.Level;
            var effect = effectSpec.GameplayEffect;
            var executions = effect.executions;
            
            foreach (var execution in executions)
            {
                if (execution.executionCalculation)
                {
                    var executionSuccess = execution.executionCalculation.ConditionalExecuteCheck(effectSpec, this);
                    if(!executionSuccess) continue; // 有 executionCalculation 且执行失败时，阻止 conditionalEffects 执行，但是没有 executionCalculation 时默认允许执行 conditionalEffects
                }
                
                // 触发 Execution Conditional Gameplay effect
                foreach (var conditionalEffect in execution.conditionalEffects)
                {
                    ApplyConditionalGameplayEffectInternal(conditionalEffect, effectSpec.ContextHandle, level);
                }
            }
        }
        
        #endregion

        #region [Attributes Snapshot And change check]

        private void CollectGEModifierTargetAttributeHash(GameplayEffect effectConfig, HashSet<int> hashCache)
        {
            foreach (var modifier in effectConfig.modifiers)
            {
                var hash = modifier.ModifierAttribute.AttributeHash;
                hashCache.Add(hash);
            }

            foreach (var execution in effectConfig.executions)
            {
                var calculation = execution.executionCalculation;
                if (calculation && calculation is CustomEffectExecutionModifierCalculation modifierCalculation)
                {
                    modifierCalculation.ListModifierTargetAttrs(hashCache);
                }
            }
        }
        
        private void CollectActiveGEModifierTargetAttributeHash(HashSet<int> hashCache)
        {
            foreach (var activeEffectSpecDataPair in activeEffectSpecs)
            {
                var activeSpec = activeEffectSpecDataPair.Value;
                var effectConfig = activeSpec.GameplayEffect;

                CollectGEModifierTargetAttributeHash(effectConfig, hashCache);
            }
        }

        private void MakeAttributesSnapshot(HashSet<int> attrHashs, Dictionary<int, GameplayAttributeData> snapshot)
        {
            foreach (var hash in attrHashs)
            {
                if (TryGetAttributeInternal(hash, out var data))
                    snapshot.Add(hash, data);
            }
        }
        
        private void CheckAttributesChangeAndTriggerChangeEvent(Dictionary<int, GameplayAttributeData> snapshot, AbilitySystemComponent source = null)
        {
            if(attributeSetSpec is not IGameplayAttributeSet attrSet) return;
            
            attrsLinkChangeRecords.Clear();
            attrSet.ClearAttributeLinkChangeRecord();
            
            foreach (var snapshotDataPair in snapshot)
            {
                var hash = snapshotDataPair.Key;
                if(!attrSet.GetAttribute(hash, out var newData)) continue;

                var oldData = snapshotDataPair.Value;
                // 更改值并记录为需要触发监听事件
                if (attrSet.ValidationCheckAndSetAttribute(hash, oldData, newData, out var appliedAttribute) 
                    && !oldData.ApproximateEquals(appliedAttribute, FEpsilon))
                {
                    attrsLinkChangeRecords[hash] = oldData;
                }
            }
            // 获取关联修改属性需要触发的监听事件
            attrSet.GetAttributeLinkChangeRecord(attrsLinkChangeRecords);
            
            foreach (var pair in attrsLinkChangeRecords)
            {
                var hash = pair.Key;
                if(!attrSet.GetAttribute(hash, out var newData)) continue;
                if(!attributeValueChangeActions.TryGetValue(hash, out var valueChangeAction)) continue;
                
                var oldData = pair.Value;
                // Debug.Log($"debug [Asc] CheckAttributesChange: hash: {hash}, changed?:{!oldData.ApproximateEquals(newData, FEpsilon)}.");
                // Debug.Log($"debug [Asc] attr detail: hash: {hash}, old:{oldData.baseValue},{oldData.currentValue}; new:{newData.baseValue},{newData.currentValue};");
                if (!oldData.ApproximateEquals(newData, FEpsilon))
                {
                    EventGameplayAttributeChangeArgs args = new EventGameplayAttributeChangeArgs() {oldData = oldData, newData = newData, source = source, target = this}; 
                    valueChangeAction.Invoke(args);
                }
            }
        }

        #endregion
        
        
        #region Stack
        
        /// 查找已叠加的 ActiveEffectSpec
        private bool TryGetStackEffectHandleByEffect(GameplayEffect gameplayEffect, out ActiveEffectSpecHandle activeEffectSpecHandle)
        {
            activeEffectSpecHandle = stackEffectHandles.TryGetValue(gameplayEffect, out var handle)
                ? handle
                : new ActiveEffectSpecHandle();
            
            return activeEffectSpecHandle.IsValid;
        }

        private void AddStackEffectHandle(GameplayEffect gameplayEffect, ActiveEffectSpecHandle activeEffectSpecHandle)
        {
            if(stackEffectHandles.ContainsKey(gameplayEffect)) return;
            
            stackEffectHandles.Add(gameplayEffect, activeEffectSpecHandle);
        }

        /// 检查和清理stack索引数据
        private void RemoveStackEffectHandle(GameplayEffect gameplayEffect)
        {
            if(!stackEffectHandles.ContainsKey(gameplayEffect)) return;
            
            stackEffectHandles.Remove(gameplayEffect);
        }

        // 用于给 AggregateBySource 模式的 GE 记录源对象的叠加数量
        internal int GetSourceStackCount(GameplayEffect gameplayEffect)
        {
            var dataExist = effectStackSourceCount.ContainsKey(gameplayEffect);
            return dataExist ? effectStackSourceCount[gameplayEffect] : 0;
        }
        internal void AddSourceStackCount(GameplayEffect gameplayEffect, int stackToAdd)
        {
            if(stackToAdd < 1) return;
            if (effectStackSourceCount.ContainsKey(gameplayEffect))
                effectStackSourceCount[gameplayEffect] += stackToAdd;
            else
                effectStackSourceCount.Add(gameplayEffect, stackToAdd);
        }
        internal void RemoveSourceStackCount(GameplayEffect gameplayEffect, int stackToRemove)
        {
            if(stackToRemove < 1 || !effectStackSourceCount.ContainsKey(gameplayEffect)) return;

            var afterRemove = effectStackSourceCount[gameplayEffect] - stackToRemove;
            if (afterRemove < 1)
                effectStackSourceCount.Remove(gameplayEffect);
            else
                effectStackSourceCount[gameplayEffect] = afterRemove;
        }

        #endregion

        #region Effect Granted Ability

        private struct GrantedAbilityDataFromEffect
        {
            public GameplayAbilitySpecHandle abilitySpecHandle;
            public EffectGrantAbility.RemovalPolicy removalPolicy;
        }
        
        private void GiveAbilitiesFromActiveEffectSpec(ActiveEffectSpecHandle specHandle)
        {
            if(!TryGetRuntimeActiveEffectSpec(specHandle, out var spec)) return;
            if(effectGrantedAbilityHandles.ContainsKey(specHandle)) return;
            
            var grantAbilities = spec.GameplayEffect.grantAbilities;
            if (!(grantAbilities?.Length > 0)) return;
            
            List<GrantedAbilityDataFromEffect> abilitySpecHandles = new List<GrantedAbilityDataFromEffect>();
            var level = spec.SpecHandle.GetData().Level;
            foreach (var effectGrantAbility in grantAbilities)
            {
                // DoNothing 类型已在刚应用 GE 时就添加
                if(effectGrantAbility.removalPolicy == EffectGrantAbility.RemovalPolicy.DoNothing) continue;

                var ability = effectGrantAbility.ability;
                GameplayAbilitySpecHandle abilityHandle = ability.AutoActiveOnEffectGranted
                    ? GiveAbilityAndActivateOnceInternal(ability, level)
                    : GiveAbilityInternal(ability, level);
                
                if(abilityHandle.IsValid)
                    abilitySpecHandles.Add(new GrantedAbilityDataFromEffect()
                    {
                        abilitySpecHandle = abilityHandle,
                        removalPolicy = effectGrantAbility.removalPolicy,
                    });
            }
            
            if(abilitySpecHandles.Count > 0)
                effectGrantedAbilityHandles.Add(specHandle, abilitySpecHandles);
        }

        private void RemoveAbilitiesFromActiveEffectSpec(ActiveEffectSpecHandle specHandle)
        {
            if(!TryGetRuntimeActiveEffectSpec(specHandle, out var spec)) return;
            if(!effectGrantedAbilityHandles.ContainsKey(specHandle)) return;

            var abilitySpecHandles = effectGrantedAbilityHandles[specHandle];
            foreach (var abilityData in abilitySpecHandles)
            {
                switch (abilityData.removalPolicy)
                {
                    case EffectGrantAbility.RemovalPolicy.RemoveAbilityOnEnd:
                        SetRemoveAbilityOnEndInternal(abilityData.abilitySpecHandle);
                        break;
                    case EffectGrantAbility.RemovalPolicy.CancelAbilityImmediate:
                        RemoveAbilityInternal(abilityData.abilitySpecHandle);
                        break;
                }
            }
            effectGrantedAbilityHandles.Remove(specHandle);
        }

        #endregion
        
        #region Handle Active Spec

        /// 添加 ActiveEffectSpec
        private void WaitAddActiveEffectSpec(ActiveGameplayEffectSpec activeEffectSpec)
        {
            waitToAddActiveEffectSpecs.Add(activeEffectSpec);
        }
        private void WaitRemoveActiveEffectSpec(ActiveEffectSpecHandle activeEffectSpecHandle)
        {
            waitToRemoveActiveEffectSpecs.Add(activeEffectSpecHandle);
        }
        private void WaitChangeActiveEffectSpecStack(ActiveEffectSpecHandle handle, AbilitySystemComponent sourceAsc, int stackToChange, bool removeMod)
        {
            var stackChange = new ActiveEffectSpecStackChange()
            {
                handle = handle,
                stackChangeSource = sourceAsc,
                stackChangeCount = stackToChange,
                removeMod = removeMod,
            };
            waitToChangeActiveEffectSpecsStack.Add(stackChange);
        }
        private void InstantAddActiveEffectSpec(ActiveGameplayEffectSpec addingSpec)
        {
            activeEffectSpecs.Add(addingSpec.Handle, addingSpec);
            // 赋予标签
            AddActorTags(addingSpec.GameplayEffect.grantedTags);
            // 赋予 grantAbilities
            GiveAbilitiesFromActiveEffectSpec(addingSpec.Handle);
            ONAddEffect?.Invoke(addingSpec.Handle);
        }
        private void InstantRemoveActiveEffectSpec(ActiveEffectSpecHandle handle)
        {
            if(!TryGetRuntimeActiveEffectSpec(handle, out var activeSpec)) return;
            
            // 记录移除GE的配置文件，用于 Modifier 修改属性恢复时触发
            removedGameplayEffectConfigs.Add(activeSpec.GameplayEffect);
            // 移除赋予的 grantAbilities
            RemoveAbilitiesFromActiveEffectSpec(handle);
            // 移除对 stack 的统计数据
            activeSpec.RemoveStack(null, activeSpec.Stack);
            RemoveStackEffectHandle(activeSpec.GameplayEffect);
            // 检查并删除 Replica 上下文
            CheckAndRemoveReplicaEffectSpec(activeSpec.SpecHandle);
            // 移除
            activeEffectSpecs.Remove(activeSpec.Handle);
            // 删除赋予标签
            RemoveActorTags(activeSpec.GameplayEffect.grantedTags);
            ONRemoveEffect?.Invoke(activeSpec.Handle);
        }

        /*
         * 为安全修改 activeEffectSpecs 中的 ActiveGESpec
         * 在 foreach 前使用 ActiveSpecChangeCacheClear 清理以往记录
         * 在 foreach 期间使用 ActiveSpecChangeCacheAddOrUpdate 记录需要的更新信息
         * 在 foreach 结束后 ActiveSpecChangeCacheApply 应用更改
         *
         * TODO: 目前因为spec使用的是普通结构体。所以修改完毕spec的副本后需要存回去才能更新，后续用 {引用结构体} 来简化处理过程和提高效率
         */
        private void ActiveSpecChangeCacheClear()
        {
            activeEffectSpecsChangeCache.Clear();
        }
        private void ActiveSpecChangeCacheAddOrUpdate(ActiveGameplayEffectSpec activeEffectSpec)
        {
            var handle = activeEffectSpec.Handle;
            if (activeEffectSpecsChangeCache.ContainsKey(handle))
                activeEffectSpecsChangeCache[handle] = activeEffectSpec;
            else
                activeEffectSpecsChangeCache.Add(handle, activeEffectSpec);
        }
        private void ActiveSpecChangeCacheApply()
        {
            foreach (var pair in activeEffectSpecsChangeCache)
            {
                var handle = pair.Key;
                if (activeEffectSpecs.ContainsKey(handle))
                    activeEffectSpecs[handle] = pair.Value;
            }
        }
        
        // 应用添加和删除操作
        private void ActiveEffectSpecsAddOrRemove(out bool needRefreshModifiers)
        {
            needRefreshModifiers = false;
            
            // 添加
            foreach (var addingSpec in waitToAddActiveEffectSpecs)
            {
                // 如果在移除队列中，则阻止添加
                if(waitToRemoveActiveEffectSpecs.Contains(addingSpec.Handle)) continue;

                var effect = addingSpec.GameplayEffect;
                // GE 未启用叠加
                if (!addingSpec.EnableStack)
                {
                    // 直接添加
                    if(activeEffectSpecs.ContainsKey(addingSpec.Handle)) continue;
                    InstantAddActiveEffectSpec(addingSpec);
                    if (effect.modifiers.Length > 0) needRefreshModifiers = true;
                    continue;
                }
                
                // 获取正在添加的 SourceAsc
                var stackCount = addingSpec.SpecHandle.GetData().StackCount;
                var addingSourceAsc = addingSpec.ContextHandle.GetData().Instigator;
                
                // 检查已有的 GE 叠加
                if (TryGetStackEffectHandleByEffect(effect, out var stackHandle))
                {
                    // TODO：后续如果希望高等级的效果覆盖低等级的效果，可以从这里入手修改
                    // 添加 stack
                    WaitChangeActiveEffectSpecStack(stackHandle, addingSourceAsc, stackCount, false);
                    // 移除使用完毕的上下文副本
                    CheckAndRemoveReplicaEffectSpec(addingSpec.SpecHandle);
                }
                // 添加新 GE 叠加
                else
                {
                    if(activeEffectSpecs.ContainsKey(addingSpec.Handle)) continue;
                    
                    // 添加 stack
                    WaitChangeActiveEffectSpecStack(addingSpec.Handle, addingSourceAsc, stackCount, false);
                    // 添加 stack 索引
                    AddStackEffectHandle(addingSpec.GameplayEffect, addingSpec.Handle);
                    
                    InstantAddActiveEffectSpec(addingSpec);
                    if (effect.modifiers.Length > 0) needRefreshModifiers = true;
                }
            }
            waitToAddActiveEffectSpecs.Clear();

            // 统一处理叠加计数更改
            foreach (var change in waitToChangeActiveEffectSpecsStack)
            {
                var handle = change.handle;
                // 跳过操作判定
                if (change.stackChangeCount == 0 || !TryGetRuntimeActiveEffectSpec(handle, out var activeSpec) || 
                    waitToRemoveActiveEffectSpecs.Contains(handle)) continue;
                
                // 检查是否存在涉及堆叠计算的modifier
                var hasStackModifier = false;
                foreach (var modifier in activeSpec.GameplayEffect.modifiers)
                {
                    if (!modifier.isStackingModifier) continue;
                    
                    hasStackModifier = true;
                    break;
                }
                
                // 移除
                if (change.removeMod)
                {
                    if(change.stackChangeCount < 0)
                        WaitRemoveActiveEffectSpec(handle);
                    else
                    {
                        activeSpec.RemoveStack(change.stackChangeSource, change.stackChangeCount);
                        activeEffectSpecs[handle] = activeSpec;
                        if(activeSpec.Stack < 1)
                            WaitRemoveActiveEffectSpec(handle);
                        else if (hasStackModifier)
                            needRefreshModifiers = true;
                    }
                }
                // 添加
                else
                {
                    var addingStack = change.stackChangeCount < 0 ? activeSpec.StackLimit : change.stackChangeCount;
                    var addingSourceAsc = change.stackChangeSource;
                    // 处理现有的 Spec
                    var overflow = activeSpec.AddStack(addingSourceAsc, addingStack);
                    activeEffectSpecs[handle] = activeSpec;
                    // 如果 hasStackModifier 刷新
                    if (hasStackModifier) needRefreshModifiers = true;
                    
                    // Overflow
                    if (overflow) continue;
                    var stackEffect = activeSpec.GameplayEffect;
                    if (stackEffect.clearStackOnOverflow)
                    {
                        WaitRemoveActiveEffectSpec(handle);
                        continue;
                    }
                    // 触发 Overflow Effects
                    if (stackEffect.denyOverflowApplication) continue;
                    var effectSpec = activeSpec.SpecHandle.GetData();
                    foreach (var overflowEffect in stackEffect.overflowEffects)
                    {
                        ApplyGameplayEffectInternal(overflowEffect, effectSpec.ContextHandle, effectSpec.Level, activeSpec.Stack);
                    }
                }
            }
            waitToChangeActiveEffectSpecsStack.Clear();
            
            // 处理手动移除
            foreach (var handle in waitToRemoveActiveEffectSpecs)
            {
                if (!activeEffectSpecs.TryGetValue(handle, out var activeSpec)) continue;

                // 触发 prematureExpirationEffects
                var spec = activeSpec.SpecHandle.GetData();
                var effect = activeSpec.GameplayEffect;
                var contextHandle = spec.ContextHandle;
                foreach (var expirationEffect in effect.prematureExpirationEffects)
                {
                    ApplyGameplayEffectInternal(expirationEffect, contextHandle, spec.Level, 1);
                }
                
                InstantRemoveActiveEffectSpec(handle);
                if (effect.modifiers.Length > 0) needRefreshModifiers = true;
            }
            waitToRemoveActiveEffectSpecs.Clear();
        }
        
        private void ActiveEffectSpecsTick()
        {
            var deltaTime = Time.deltaTime;
            
            ActiveSpecChangeCacheClear();
            foreach (var pair in activeEffectSpecs)
            {
                var activeSpec = pair.Value;
                var effect = activeSpec.GameplayEffect;
                var effectSpec = activeSpec.SpecHandle.GetData();
                var context = effectSpec.ContextHandle.GetData();
                
                // 移除判定（检查源对象有效性、检查移除标签）
                var sourceAsc = context.Instigator;
                if (!sourceAsc && !context.AttributesSnapshotCaptured 
                    || effect.removalTagsRequirement.CheckRequirement(ActorTags, false))
                {
                    WaitRemoveActiveEffectSpec(activeSpec.Handle);
                    continue;
                }
                // 持续时间
                if (effect.durationSetting.durationPolicy != DurationSetting.DurationPolicy.Infinite)
                {
                    activeSpec.durationTimer -= deltaTime;
                    // 过期移除在后续的 ActiveEffectSpecsExpire() 处理......略
                    ActiveSpecChangeCacheAddOrUpdate(activeSpec);
                }
                // 周期
                if (activeSpec.EnablePeriod)
                {
                    // 周期的执行和暂停判定
                    if (effect.ongoingTagsRequirement.CheckRequirement(ActorTags, true))
                    {
                        activeSpec.periodTimer -= deltaTime;
                        if (activeSpec.paused)
                        {
                            activeSpec.paused = false;
                            switch (effect.periodicInhibitionPolicy)
                            {
                                case GameplayEffect.PeriodicInhibitionPolicy.ExecuteAndReset:
                                    activeSpec.periodTimer = 0;
                                    break;
                                case GameplayEffect.PeriodicInhibitionPolicy.Reset:
                                    activeSpec.periodTimer = activeSpec.Period;
                                    break;
                                // case GameplayEffect.PeriodicInhibitionPolicy.NeverReset:
                                // default:
                                //     break;
                            }
                        }
                    }
                    else if (!activeSpec.paused) activeSpec.paused = true;
                    ActiveSpecChangeCacheAddOrUpdate(activeSpec);
                    
                    if (activeSpec.periodTimer > 0) continue; // 周期性 GE 的周期结束检查
                    // 周期结束时，重置计时器
                    activeSpec.periodTimer = activeSpec.Period;
                    ActiveSpecChangeCacheAddOrUpdate(activeSpec);
                    // 应用 Executions
                    EffectExecutionsTriggerConditionalGE(effectSpec);
                }
            }
            
            ActiveSpecChangeCacheApply();
        }

        private void ActiveEffectSpecsExpire(out bool needRefreshModifiers)
        {
            needRefreshModifiers = false;
            List<ActiveEffectSpecHandle> expireList = new List<ActiveEffectSpecHandle>();
            
            ActiveSpecChangeCacheClear();
            foreach (var pair in activeEffectSpecs)
            {
                var activeSpec = pair.Value;
                if (activeSpec.GameplayEffect.durationSetting.durationPolicy == DurationSetting.DurationPolicy.Infinite) continue;

                if (activeSpec.durationTimer > 0) continue;
                
                var removeConfirm = false;
                if (!activeSpec.EnableStack)
                    removeConfirm = true;
                else
                {
                    // 区分 Stack 失效策略
                    var effect = activeSpec.GameplayEffect;
                    switch (effect.stackExpirationPolicy)
                    {
                        case GameplayEffect.StackExpirationPolicy.ClearEntireStack:
                            removeConfirm = true;
                            break;
                        case GameplayEffect.StackExpirationPolicy.RemoveSingleStackAndRefreshDuration:
                        {
                            activeSpec.RemoveStack(null, 1);
                            if (activeSpec.Stack > 0)
                                activeSpec.durationTimer = activeSpec.Duration;
                            else
                                removeConfirm = true;

                            ActiveSpecChangeCacheAddOrUpdate(activeSpec);
                            break;
                        }
                        case GameplayEffect.StackExpirationPolicy.RefreshDuration:
                            activeSpec.durationTimer = activeSpec.Duration;
                            // TODO: 触发手动更新 Stack 方法
                            // ......
                            ActiveSpecChangeCacheAddOrUpdate(activeSpec);
                            break;
                    }
                }

                if (!removeConfirm) continue;
                // 触发 routineExpirationEffects
                var spec = activeSpec.SpecHandle.GetData();
                var contextHandle = spec.ContextHandle;
                foreach (var expirationEffect in activeSpec.GameplayEffect.routineExpirationEffects)
                {
                    ApplyGameplayEffectInternal(expirationEffect, contextHandle, spec.Level, 1);
                }
                // 等待移除
                expireList.Add(activeSpec.Handle);
            }
            // 对确认失效的Spec执行移除
            foreach (var handle in expireList)
            {
                if (handle.GetData().GameplayEffect.modifiers.Length > 0) needRefreshModifiers = true;
                InstantRemoveActiveEffectSpec(handle);
            }
            ActiveSpecChangeCacheApply();
        }
        
        private void UpdateLoopActiveEffectSpecs()
        {
            ActiveEffectSpecsAddOrRemove(out bool needRefreshModifiersStep1);
            ActiveEffectSpecsTick();
            ActiveEffectSpecsExpire(out bool needRefreshModifiersStep3);

            if (needRefreshModifiersStep1 || needRefreshModifiersStep3)
            {
                // 收集 Modifier 并记录可能修改的属性的 Snapshot
                modifierTargetAttrs.Clear();
                modifierTargetAttrsSnapshot.Clear();
                CollectActiveGEModifierTargetAttributeHash(modifierTargetAttrs);
                foreach (var gameplayEffect in removedGameplayEffectConfigs) CollectGEModifierTargetAttributeHash(gameplayEffect, modifierTargetAttrs);
                MakeAttributesSnapshot(modifierTargetAttrs, modifierTargetAttrsSnapshot);
                
                EffectRefreshActivateGameplayModifiers();
                
                // 对比可能修改的属性的 Snapshot，如果改变则触发改变动画
                CheckAttributesChangeAndTriggerChangeEvent(modifierTargetAttrsSnapshot);
            }
        }

        #endregion
    }
}
