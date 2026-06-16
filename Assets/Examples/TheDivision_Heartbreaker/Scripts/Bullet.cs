using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Runtime
{
    public class Bullet : MonoBehaviour
    {
        /// <summary>
        /// 击中处理
        /// </summary>
        private Action<GameObject> onAttackSuccess;
        /// <summary>
        /// 击中事件 参数：WeaponAmmo弹药对象，Transform命中的对象，Vector3命中位置，Vector3命中位置的法线
        /// </summary>
        public UnityEvent<Bullet, Transform, Vector3, Vector3> onHitEvent;

        public UnityEvent<Bullet> onRelease;
        
        private float lifeTimer;
        private float travelDistance;
        private float maxTravelDistance;
        private bool releasing;
        
        public float speed = 800;
        public float maxLifeTime = 0.5f;
        public float destroyDelayTime = 0.2f;

        // Update is called once per frame
        void FixedUpdate()
        {
            if(releasing)
                return;
            
            var moveDistance = speed * Time.fixedDeltaTime;
            var remainingDistance = maxTravelDistance - travelDistance;
            if (remainingDistance <= 0f)
            {
                MarkAsRelease();
                return;
            }
            moveDistance = Mathf.Min(moveDistance, remainingDistance);
            var transformRef = transform;
            lifeTimer += Time.fixedDeltaTime;
            
            Ray damageRay = new Ray(transformRef.position, transformRef.forward);
            if (Physics.Raycast(damageRay, out var hitInfo, moveDistance * 1.2f, (int)PhysicsSetting.PhysicsLayerMask.All))
            {
                var targetCollider = hitInfo.collider;
                
                // 造成伤害
                if (targetCollider.gameObject.layer == (int)PhysicsSetting.PhysicsLayer.Character)
                {
                    onAttackSuccess?.Invoke(targetCollider.gameObject);
                }
                
                // 击中后特效信息
                onHitEvent?.Invoke(this, targetCollider.transform, hitInfo.point, hitInfo.normal);
                
                MarkAsRelease();
                
                // 移动
                transformRef.Translate(hitInfo.point - transformRef.position, Space.World);
                travelDistance = maxTravelDistance;
            }
            else
            {
                // 移动
                transformRef.Translate(0, 0, moveDistance);
                travelDistance += moveDistance;
            }

            // 强制回收
            if (lifeTimer > maxLifeTime || travelDistance >= maxTravelDistance)
                MarkAsRelease();
        }

        /// <summary>
        /// 标记为待回收，等待 destroyDelayTime 后执行
        /// </summary>
        void MarkAsRelease()
        {
            if(releasing) return;
            releasing = true;
            StartCoroutine(ReleaseCoroutine());
        }

        public void Initialize(Action<GameObject> attackSuccessHandler)
        {
            onAttackSuccess = attackSuccessHandler;
            lifeTimer = 0;
            travelDistance = 0;
            maxTravelDistance = Mathf.Max(0, speed * maxLifeTime);
            releasing = false;
        }
        
        private IEnumerator ReleaseCoroutine()
        {
            yield return new WaitForSeconds(destroyDelayTime);
            onRelease?.Invoke(this);
        }
    }
}
