using Game.Runtime;
using UnityEngine;
using UnityEngine.Pool;          // 引入官方对象池命名空间[reference:2][reference:3]
using UnityEngine.Events;
using UnityEngine.TextCore.Text;

public class SimpleFPSController : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("视角设置")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;

    [Header("射击设置")]
    [SerializeField] private float fireRate = 0.15f;
    [SerializeField] private Transform firePoint;          // 🔥 子弹发射点（可配置）
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float maxRayDistance = 1000f; // 瞄准射线最大距离

    [Header("对象池设置")]
    [SerializeField] private int defaultCapacity = 20;   // 默认容量[reference:4]
    [SerializeField] private int maxPoolSize = 50;       // 最大容量[reference:5]

    [Header("枪械模型")]

    // 组件引用
    private CharacterController characterController;
    private Camera playerCamera;
    private float verticalVelocity;
    private float xRotation = 0f;
    private float nextFireTime = 0f;

    // 官方对象池
    private ObjectPool<GameObject> bulletPool;

    public UnityEvent onShoot;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError("需要 CharacterController 组件！");
            enabled = false;
            return;
        }

        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            Debug.LogError("未找到 Camera 子物体！");
            enabled = false;
            return;
        }

        // 初始化官方对象池[reference:6][reference:7]
        bulletPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(bulletPrefab),                    // 创建新实例[reference:8]
            actionOnGet: (obj) => obj.SetActive(true),                     // 从池中取出时调用[reference:9]
            actionOnRelease: (obj) => obj.SetActive(false),                // 归还池中时调用[reference:10]
            actionOnDestroy: (obj) => Destroy(obj),                        // 池满被销毁时调用[reference:11]
            collectionCheck: true,                                         // Editor中启用安全检查[reference:12]
            defaultCapacity: defaultCapacity,                              // 默认容量[reference:13]
            maxSize: maxPoolSize                                           // 最大容量[reference:14]
        );

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMovement();
        HandleLook();
        HandleShoot();
        HandleJump();
    }

    #region 移动与视角（与之前完全相同）

    void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
        moveDirection.Normalize();
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
        Vector3 move = moveDirection * (currentSpeed * Time.deltaTime);
        characterController.Move(move);
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        transform.Rotate(Vector3.up * mouseX);
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void HandleJump()
    {
        if (characterController.isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f;
        if (Input.GetButtonDown("Jump") && characterController.isGrounded)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * Time.deltaTime;
        Vector3 verticalMove = new Vector3(0, verticalVelocity, 0) * Time.deltaTime;
        characterController.Move(verticalMove);
    }

    #endregion

    #region 射击（使用官方对象池）

    void HandleShoot()
    {
        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    void Shoot()
    {
        // 从对象池获取子弹
        GameObject bulletObj = bulletPool.Get();
        bulletObj.transform.SetParent(null);

        // 子弹设置位置和旋转
        if (playerCamera)
        {
            bulletObj.transform.position = firePoint.position;
            bulletObj.transform.rotation = Quaternion.LookRotation(GetShootDirection());
        }

        // 初始化 Bullet 组件
        if (bulletObj.TryGetComponent(out Bullet bullet))
        {
            bullet.Initialize(DoDamage);
            
            // 子弹击中时触发命中反馈
            bullet.onHitEvent.RemoveAllListeners();
            bullet.onHitEvent.AddListener(OnHitTarget);
            
            // 子弹销毁时归还池中
            bullet.onRelease.RemoveAllListeners(); // 清除旧监听，避免重复绑定
            bullet.onRelease.AddListener((b) =>
            {
                b.onHitEvent.RemoveAllListeners();
                b.onRelease.RemoveAllListeners();
                bulletPool.Release(b.gameObject);
            });
            
        }

        onShoot?.Invoke();
    }
    
    

    /// <summary>
    /// 计算从 FirePoint 指向相机准星瞄准点的方向
    /// </summary>
    private Vector3 GetShootDirection()
    {
        // 从相机中心发射一条射线
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // 尝试击中场景物体
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance))
        {
            // 有击中：方向指向击中点
            Vector3 targetPoint = hit.point;
            return (targetPoint - firePoint.position).normalized;
        }
        else
        {
            // 无击中：使用相机朝向（无限远）
            return playerCamera.transform.forward;
        }
    }

    private void DoDamage(GameObject hitTarget)
    {
        Debug.Log($"命中角色：{hitTarget.name}");
    }

    private void OnHitTarget(Bullet bullet, Transform hitTarget, Vector3 hitPos, Vector3 hitNormal)
    {
        
        if (hitTarget.gameObject.layer == LayerMask.NameToLayer("Character") && hitTarget.TryGetComponent(out SimpleTarget target))
        {
            bullet.transform.SetParent(hitTarget);
            
            Vector3 direction = (hitPos - firePoint.position).normalized;
            target.TakeDamage(-direction);
        }
    }

    #endregion
}