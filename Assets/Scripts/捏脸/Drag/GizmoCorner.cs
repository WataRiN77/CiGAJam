using UnityEngine;
using UnityEngine.EventSystems; // 如果使用 UI 射线，可继承相应接口

public class GizmoCorner : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private DraggableOrgan targetOrgan;
    private Camera mainCamera;

    // 记录初始状态
    private Vector3 originalScale;
    private Vector3 originalMouseWorld;
    private Vector3 originalCornerLocalPos; // 相对于中心的方向
    private float originalDistance;

    public void Initialize(DraggableOrgan organ)
    {
        targetOrgan = organ;
    }

    private void Start()
    {
        mainCamera = Camera.main;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (targetOrgan == null) return;
        // 记录拖拽开始时的数据
        originalScale = targetOrgan.transform.localScale;
        originalMouseWorld = mainCamera.ScreenToWorldPoint(eventData.position);
        // 计算角相对于器官中心的方向（世界坐标）
        Vector3 cornerWorldPos = transform.position;
        Vector3 centerWorldPos = targetOrgan.transform.position;
        originalCornerLocalPos = cornerWorldPos - centerWorldPos; // 这是初始方向向量
        originalDistance = originalCornerLocalPos.magnitude;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetOrgan == null) return;
        Vector3 currentMouseWorld = mainCamera.ScreenToWorldPoint(eventData.position);
        // 计算鼠标相对于器官中心的新向量
        Vector3 centerWorld = targetOrgan.transform.position;
        Vector3 newDir = currentMouseWorld - centerWorld;
        // 沿初始方向计算投影距离，避免非均匀缩放时变形
        float newDistance = Vector3.Dot(newDir, originalCornerLocalPos.normalized);
        // 确定缩放因子（相对于初始距离）
        if (originalDistance > 0.001f)
        {
            float factor = newDistance / originalDistance;
            // 应用到初始缩放上
            targetOrgan.transform.localScale = originalScale * factor;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 无需操作
    }
}