using System.Collections;
using UnityEngine;

/// <summary>
/// 挂在胶囊模型上，支持绕底部（底座连接处）摇晃。
/// </summary>
public class SimpleTarget : MonoBehaviour
{
    [Header("摇晃参数")]
    [Tooltip("最大倾斜角度（度）")]
    [SerializeField] private float maxTiltAngle = 30f;

    [Tooltip("从直立倾斜到最大角度所需时间（秒）")]
    [SerializeField] private float tiltDuration = 0.1f;

    [Tooltip("从最大角度弹回直立所需时间（秒）")]
    [SerializeField] private float returnDuration = 0.2f;

    [Header("枢轴点设置")]
    [Tooltip("胶囊中心到枢轴点（底座顶部）的本地偏移量。\n对于标准胶囊（高2，半径0.5），底部为 (0, -1, 0)")]
    [SerializeField] private Vector3 pivotLocalOffset = new Vector3(0, -1f, 0);

    // 初始状态缓存（世界空间）
    private Vector3 initialWorldPos;
    private Quaternion initialWorldRot;
    private Vector3 pivotWorldPos;   // 枢轴点世界坐标（固定）

    private Coroutine shakeCoroutine;

    private void Start()
    {
        // 保存初始位置和旋转
        initialWorldPos = transform.position;
        initialWorldRot = transform.rotation;

        // 计算枢轴点的世界坐标（基于初始姿态）
        pivotWorldPos = transform.TransformPoint(pivotLocalOffset);
    }

    /// <summary>
    /// 外部攻击调用，传入世界空间攻击方向。
    /// </summary>
    public void TakeDamage(Vector3 attackDirection)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            // 复位到初始状态（避免累积偏移）
            transform.position = initialWorldPos;
            transform.rotation = initialWorldRot;
        }

        shakeCoroutine = StartCoroutine(Shake(attackDirection));
    }

    private IEnumerator Shake(Vector3 attackDirection)
    {
        // 将攻击方向转换到本地空间（因为我们要相对于初始姿态计算轴向）
        Transform parent = transform.parent;
        Vector3 localAttackDir = parent != null
            ? parent.InverseTransformDirection(attackDirection)
            : attackDirection;

        // 水平方向（忽略垂直分量，确保绕Z轴或X轴旋转）
        Vector3 horizontalDir = Vector3.ProjectOnPlane(localAttackDir, Vector3.up).normalized;
        if (horizontalDir.sqrMagnitude < 0.001f)
        {
            // 纯垂直攻击不摇晃
            yield break;
        }

        // 计算旋转轴：在本地空间中，水平方向叉乘向上向量
        // 例如攻击来自右侧(+X)，则旋转轴为向前(Z)，物体会绕Z轴向X方向倾斜
        Vector3 axis = Vector3.Cross(horizontalDir, Vector3.up).normalized;
        if (axis.sqrMagnitude < 0.001f)
            yield break;

        // 获取初始状态
        Vector3 startPos = initialWorldPos;
        Quaternion startRot = initialWorldRot;
        Vector3 pivot = pivotWorldPos;

        // 从枢轴点到物体中心的方向向量（初始状态）
        Vector3 dirFromPivot = startPos - pivot;

        // 目标相对旋转（增量旋转）
        Quaternion targetDeltaRot = Quaternion.AngleAxis(maxTiltAngle, axis);

        // ----- 第一阶段：倾斜 -----
        float elapsed = 0f;
        while (elapsed < tiltDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / tiltDuration);

            // 计算当前增量旋转（从初始姿态到当前）
            Quaternion currentDeltaRot = Quaternion.Slerp(Quaternion.identity, targetDeltaRot, t);

            // 应用旋转到物体
            transform.rotation = currentDeltaRot * startRot;

            // 围绕枢轴点旋转方向向量，并更新位置
            Vector3 newDir = currentDeltaRot * dirFromPivot;
            transform.position = pivot + newDir;

            yield return null;
        }

        // 确保到达最大倾斜位置
        transform.rotation = targetDeltaRot * startRot;
        transform.position = pivot + (targetDeltaRot * dirFromPivot);

        // ----- 第二阶段：弹回直立 -----
        elapsed = 0f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / returnDuration);

            // 从最大倾斜 Slerp 到单位四元数（直立）
            Quaternion currentDeltaRot = Quaternion.Slerp(targetDeltaRot, Quaternion.identity, t);

            transform.rotation = currentDeltaRot * startRot;
            Vector3 newDir = currentDeltaRot * dirFromPivot;
            transform.position = pivot + newDir;

            yield return null;
        }

        // 完全复位到初始状态
        transform.position = startPos;
        transform.rotation = startRot;

        shakeCoroutine = null;
    }
}