using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DraggableOrgan : MonoBehaviour
{
    [HideInInspector] public bool isDragging = false;   // 由管理器控制
    private Vector3 dragOffset;                         // 鼠标与物体的偏移

    [Tooltip("缩放速度")] public float zoomSpeed = 0.1f;
    [Tooltip("最小缩放")] public float minScale = 0.5f;
    [Tooltip("最大缩放")] public float maxScale = 2f;

    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    /// <summary>
    /// 开始拖拽，记录偏移
    /// </summary>
    public void BeginDrag(Vector3 mouseWorldPos)
    {
        isDragging = true;
        Vector3 worldPoint = mouseWorldPos;
        worldPoint.z = transform.position.z;
        dragOffset = transform.position - worldPoint;
    }

    /// <summary>
    /// 拖拽中，更新位置
    /// </summary>
    public void OnDrag(Vector3 mouseWorldPos)
    {
        if (!isDragging) return;
        Vector3 worldPoint = mouseWorldPos;
        worldPoint.z = transform.position.z;
        transform.position = worldPoint + dragOffset;
    }

    /// <summary>
    /// 结束拖拽
    /// </summary>
    public void EndDrag()
    {
        isDragging = false;
    }

    /// <summary>
    /// 执行缩放
    /// </summary>
    public void ApplyZoom(float scrollDelta)
    {
        float newScale = transform.localScale.x + scrollDelta * zoomSpeed;
        newScale = Mathf.Clamp(newScale, minScale, maxScale);
        transform.localScale = Vector3.one * newScale;
    }
}