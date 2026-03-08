using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    // /// <summary>
    // /// GE 扩展数据
    // /// </summary>
    // public interface IGameplayEffectContextExtensionData
    // {
    //     
    // }

    public struct HitResult
    {
        public Vector3 position;
        public Vector3 normal;
        public Collider colliderObject;
    }

    /// <summary>
    /// 用于为游戏效果的应用提供上下文信息，通过派生扩展数据的子类来扩展，可用于在（自定义计算类、属性、能力）之间传递数据
    /// </summary>
    public struct GameplayEffectContext
    {
        private GameplayEffectContextHandle handle;
        private readonly AbilitySystemComponent instigator;
        private bool isReplica;

        private GameObject effectCauser;
        
        private bool hasEffectOrigin;
        private Vector3 effectOrigin;

        private bool hasHitResult;
        private HitResult hitResult;
        
        // private IGameplayEffectContextExtensionData extensionData;
        
        private CapturedAttributesSpec capturedAttributesSnapshot;

        #region Property
        
        public GameplayEffectContextHandle Handle => handle;

        /// 引发 Effect 的对象，例如武器、道具，子弹或是角色本身
        public GameObject EffectCauser => effectCauser;

        /// 创建 GE 的源对象，一般是角色本身
        public GameObject SourceObject => instigator.gameObject;

        /// 创建 GE 的 ASC（SourceObject 的 ASC）
        public AbilitySystemComponent Instigator => instigator;

        /// 是否有起始位置信息
        public bool HasEffectOrigin => hasEffectOrigin;

        /// GE 的起始位置
        public Vector3 EffectOrigin => effectOrigin;

        public bool HasHitResult => hasHitResult;
        
        /// GE 的击中位置
        public HitResult HitResult => hitResult;

        /// 是否成功初始化
        public bool IsValid => instigator;

        /// 是否是副本
        public bool IsReplica => isReplica;

        // /// 扩展数据
        // public IGameplayEffectContextExtensionData ExtensionData => extensionData;

        /// 是否启用了属性快照
        public bool AttributesSnapshotCaptured => capturedAttributesSnapshot != null;

        /// 属性快照
        public CapturedAttributesSpec CapturedAttributesSnapshot => capturedAttributesSnapshot;
        
        #endregion

        public GameplayEffectContext(AbilitySystemComponent instigatorAsc)
        {
            instigator = instigatorAsc;
            handle = GameplayEffectContextHandle.NewHandle(instigatorAsc);
            isReplica = false;
            
            effectCauser = null;
            hasEffectOrigin = false;
            effectOrigin = default;
            hasHitResult = false;
            hitResult = new HitResult();
            // extensionData = null;
            
            capturedAttributesSnapshot = null;
        }

        // 仅用于直接创建托管副本
        private GameplayEffectContext(AbilitySystemComponent instigatorAsc, AbilitySystemComponent storageAsc)
        {
            instigator = instigatorAsc;
            handle = GameplayEffectContextHandle.NewHandle(storageAsc);
            isReplica = true;
            
            effectCauser = null;
            hasEffectOrigin = false;
            effectOrigin = default;
            hasHitResult = false;
            hitResult = new HitResult();
            // extensionData = null;
            
            capturedAttributesSnapshot = null;
        }

        public static GameplayEffectContext CreateAsReplica(AbilitySystemComponent instigatorAsc, AbilitySystemComponent storageAsc)
        {
            var context = new GameplayEffectContext(instigatorAsc, storageAsc);
            return context;
        }

        // 移交副本操作中为 Context 生成新句柄
        public GameplayEffectContext TransferToNewAsc(AbilitySystemComponent instigatorAsc)
        {
            handle = GameplayEffectContextHandle.NewHandle(instigatorAsc);
            isReplica = true;
            return this;
        }

        public void CreateCapturedAttributesSpec()
        {
            capturedAttributesSnapshot = new CapturedAttributesSpec(instigator);
        }
        
        public void SetEffectCauserObject(GameObject effectCauserObject)
        {
            effectCauser = effectCauserObject;
        }

        public void SetOrigin(Vector3 originData)
        {
            hasEffectOrigin = true;
            effectOrigin = originData;
        }

        public void SetHitResult(Vector3 hitPosition, Vector3 hitNormal, Collider collider = null)
        {
            hasHitResult = true;
            hitResult = new HitResult()
            {
                position = hitPosition,
                normal = hitNormal,
                colliderObject = collider,
            };
        }

        // public void SetExtensionData(IGameplayEffectContextExtensionData addingExtensionData)
        // {
        //     extensionData = addingExtensionData;
        // }
    }
}
 