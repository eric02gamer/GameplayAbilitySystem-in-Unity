using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    // /// <summary>
    // /// GAS框架中用于生成唯一句柄
    // /// </summary>
    // public readonly struct GameplayHandle: IEquatable<GameplayHandle>
    // {
    //     private readonly Guid guid;
    //     private GameplayHandle(Guid guidInit)
    //     {
    //         guid = guidInit;
    //     }
    //     public bool Equals(GameplayHandle other)
    //     {
    //         return guid.Equals(other.guid);
    //     }
    //     public override bool Equals(object obj)
    //     {
    //         return obj is GameplayHandle other && Equals(other);
    //     }
    //     public override int GetHashCode()
    //     {
    //         return guid.GetHashCode();
    //     }
    //     
    //     public static GameplayHandle NewHandle()
    //     {
    //         var newHandle = new GameplayHandle(Guid.NewGuid());
    //         return newHandle;
    //     }
    // }
    
    /// <summary>
    /// 能力实例对象的句柄
    /// </summary>
    public readonly struct GameplayAbilitySpecHandle: IEquatable<GameplayAbilitySpecHandle>
    {
        private readonly int guid;
        private readonly AbilitySystemComponent ascOwner;
        
        public AbilitySystemComponent AscOwner => ascOwner;
        public static GameplayAbilitySpecHandle Empty = new GameplayAbilitySpecHandle();

        #region Data
        
        public bool IsValid => ascOwner && ascOwner.AbilitySpecHandleIsValid(this);
        public bool IsActive => ascOwner && ascOwner.AbilitySpecIsActive(this);
        
        public void AddTarget(GameplayTarget target)
        {
            if(!ascOwner) return;
            
            ascOwner.AbilityAddTarget(this, target);
        }
        public GameplayAbilitySpec GetData()
        {
            return IsValid ? ascOwner.AbilityHandleGetData(this) : new GameplayAbilitySpec();
        }

        public void SetLevel(int newLevel)
        {
            if(!ascOwner) return;

            ascOwner.AbilitySetLevel(this, newLevel);
        }

        public void SetActivateCount(int activateCount)
        {
            if(!ascOwner) return;

            ascOwner.AbilitySetActivateCount(this, activateCount);
        }

        #endregion
        
        /// 从同一个 GameplayAbility 创建的 Handle， hash值相同
        internal GameplayAbilitySpecHandle(GameplayAbility ability, AbilitySystemComponent owner)
        {
            guid = ability.GetHashCode();
            ascOwner = owner;
        }
        public bool Equals(GameplayAbilitySpecHandle other)
        {
            return guid.Equals(other.guid);
        }
        // public override bool Equals(object obj)
        // {
        //     return obj is GameplayAbilitySpecHandle other && Equals(other);
        // }
        // public override int GetHashCode()
        // {
        //     return guid.GetHashCode();
        // }
    }
    
    
    /// <summary>
    /// Effect Context 句柄
    /// </summary>
    public readonly struct GameplayEffectContextHandle: IEquatable<GameplayEffectContextHandle>
    {
        private readonly Guid guid;
        private readonly AbilitySystemComponent ascOwner;
        
        /// 管理 Effect Context 的ASC组件，不一定是创建时的 ASC
        public AbilitySystemComponent AscOwner => ascOwner;

        #region Data
        
        public bool IsValid => ascOwner && ascOwner.GetEffectContext(this).IsValid;
        
        public GameplayEffectContext GetData()
        {
            return ascOwner.GetEffectContext(this);
        }
        public void SetData(GameplayEffectContext data)
        {
            if(!ascOwner) return;
            ascOwner.SetEffectContext(data);
        }
        public void ClearData()
        {
            if(!ascOwner) return;
            ascOwner.ClearEffectContext(this);
        }
        
        #endregion
        
        private GameplayEffectContextHandle(Guid guidInit, AbilitySystemComponent owner)
        {
            guid = guidInit;
            ascOwner = owner;
        }
        public bool Equals(GameplayEffectContextHandle other)
        {
            return guid.Equals(other.guid);
        }
        public override bool Equals(object obj)
        {
            return obj is GameplayEffectContextHandle other && Equals(other);
        }
        public override int GetHashCode()
        {
            return guid.GetHashCode();
        }
        internal static GameplayEffectContextHandle NewHandle(AbilitySystemComponent owner)
        {
            var newHandle = new GameplayEffectContextHandle(Guid.NewGuid(), owner);
            return newHandle;
        }
    }

    /// <summary>
    /// Gameplay Effect Spec的句柄
    /// </summary>
    public readonly struct GameplayEffectSpecHandle: IEquatable<GameplayEffectSpecHandle>
    {
        private readonly Guid guid;
        private readonly AbilitySystemComponent ascOwner;

        public AbilitySystemComponent AscOwner => ascOwner;
        
        #region Data
        
        public bool IsValid => ascOwner && ascOwner.GetEffectSpec(this).IsValid;
        
        public GameplayEffectSpec GetData()
        {
            return ascOwner.GetEffectSpec(this);
        }
        public void SetData(GameplayEffectSpec data)
        {
            if(!ascOwner) return;
            ascOwner.SetEffectSpec(data);
        }
        public void ClearData()
        {
            if(!ascOwner) return;
            ascOwner.ClearEffectSpec(this);
        }
        
        #endregion
        
        private GameplayEffectSpecHandle(Guid guidInit, AbilitySystemComponent owner)
        {
            guid = guidInit;
            ascOwner = owner;
        }
        public bool Equals(GameplayEffectSpecHandle other)
        {
            return guid.Equals(other.guid);
        }
        public override bool Equals(object obj)
        {
            return obj is GameplayEffectSpecHandle other && Equals(other);
        }
        public override int GetHashCode()
        {
            return guid.GetHashCode();
        }
        
        internal static GameplayEffectSpecHandle NewHandle(AbilitySystemComponent owner)
        {
            var newHandle = new GameplayEffectSpecHandle(Guid.NewGuid(), owner);
            return newHandle;
        }
    }
    
    
    /// <summary>
    /// Gameplay Effect Spec的句柄
    /// </summary>
    public readonly struct ActiveEffectSpecHandle: IEquatable<ActiveEffectSpecHandle>
    {
        private readonly Guid guid;
        private readonly AbilitySystemComponent ascOwner;

        public AbilitySystemComponent AscOwner => ascOwner;
        
        #region Data
        
        public bool IsValid => ascOwner && ascOwner.GetActiveEffectSpec(this).IsValid;

        public ActiveGameplayEffectSpec GetData()
        {
            return ascOwner.GetActiveEffectSpec(this);
        }
        // public void SetData(ActiveGameplayEffectSpec data)
        // {
        //     ascOwner.SetActiveEffectSpec(data);
        // }
        
        #endregion
        
        private ActiveEffectSpecHandle(Guid guidInit, AbilitySystemComponent owner)
        {
            guid = guidInit;
            ascOwner = owner;
        }
        public bool Equals(ActiveEffectSpecHandle other)
        {
            return guid.Equals(other.guid);
        }
        public override bool Equals(object obj)
        {
            return obj is ActiveEffectSpecHandle other && Equals(other);
        }
        public override int GetHashCode()
        {
            return guid.GetHashCode();
        }
        
        internal static ActiveEffectSpecHandle NewHandle(AbilitySystemComponent owner)
        {
            var newHandle = new ActiveEffectSpecHandle(Guid.NewGuid(), owner);
            return newHandle;
        }
    }
}
