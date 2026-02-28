using UnityEngine;

/// <summary>横板通关 - 摄像机跟随</summary>
public class PlatformerCameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;
    public Vector3 offset = new Vector3(0f, 2f, -10f);

    [Header("水平边界限制")]
    public bool useBounds = false;
    public float minX = -50f;
    public float maxX = 50f;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;

        if (useBounds)
            desired.x = Mathf.Clamp(desired.x, minX, maxX);

        desired.z = offset.z; // 保持 Z 不变

        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}
