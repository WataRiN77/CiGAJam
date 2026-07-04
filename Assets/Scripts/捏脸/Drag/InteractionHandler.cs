using UnityEngine;

public class InteractionHandler : MonoBehaviour
{
    [Header("层设置")]
    [SerializeField] private LayerMask organLayer;      // FaceOrgan 层
    [SerializeField] private LayerMask gizmoLayer;      // Gizmo 层（角、边、旋转手柄）

    [Header("引用")]
    [SerializeField] private SelectionManager selectionManager;

    private Camera mainCamera;

    // 当前拖拽状态
    private DraggableOrgan currentDraggedOrgan;
    private GizmoCornerHandle currentDraggedCorner;
    private GizmoEdgeHandle currentDraggedEdge;
    private GizmoRotateHandle currentRotateHandle;

    private void Start()
    {
        mainCamera = Camera.main;
        if (selectionManager == null)
            selectionManager = SelectionManager.Instance;
    }

    private void Update()
    {
        Vector2 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);

        // 鼠标按下
        if (Input.GetMouseButtonDown(0))
        {
            // 1. 优先检测 Gizmo 层
            Collider2D gizmoHit = Physics2D.OverlapPoint(mouseWorld, gizmoLayer);
            if (gizmoHit != null)
            {
                // 旋转手柄
                GizmoRotateHandle rotate = gizmoHit.GetComponent<GizmoRotateHandle>();
                if (rotate != null && rotate.TargetOrgan != null)
                {
                    currentRotateHandle = rotate;
                    rotate.OnBeginDrag(mainCamera);
                    selectionManager?.SelectOrgan(rotate.TargetOrgan);
                    return;
                }

                // 角手柄
                GizmoCornerHandle corner = gizmoHit.GetComponent<GizmoCornerHandle>();
                if (corner != null && corner.TargetOrgan != null)
                {
                    currentDraggedCorner = corner;
                    corner.OnBeginDrag(mainCamera);
                    selectionManager?.SelectOrgan(corner.TargetOrgan);
                    return;
                }

                // 边手柄
                GizmoEdgeHandle edge = gizmoHit.GetComponent<GizmoEdgeHandle>();
                if (edge != null && edge.TargetOrgan != null)
                {
                    currentDraggedEdge = edge;
                    edge.OnBeginDrag(mainCamera);
                    selectionManager?.SelectOrgan(edge.TargetOrgan);
                    return;
                }
            }

            // 2. 检测器官层
            Collider2D[] organHits = Physics2D.OverlapPointAll(mouseWorld, organLayer);
            if (organHits.Length > 0)
            {
                Collider2D nearest = null;
                float minDist = float.MaxValue;
                foreach (var col in organHits)
                {
                    float dist = Vector2.Distance(mouseWorld, col.bounds.center);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = col;
                    }
                }

                DraggableOrgan organ = nearest?.GetComponent<DraggableOrgan>();
                if (organ != null)
                {
                    selectionManager?.SelectOrgan(organ);
                    Vector3 worldPoint = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                    worldPoint.z = organ.transform.position.z;
                    organ.BeginDrag(worldPoint);
                    currentDraggedOrgan = organ;
                    return;
                }
            }

            // 3. 点到空白 — 取消选中
            selectionManager?.DeselectCurrent();
        }

        // 鼠标拖拽中
        if (Input.GetMouseButton(0))
        {
            if (currentRotateHandle != null)
            {
                currentRotateHandle.OnDrag(mainCamera);
            }
            else if (currentDraggedCorner != null)
            {
                currentDraggedCorner.OnDrag(mainCamera);
            }
            else if (currentDraggedEdge != null)
            {
                currentDraggedEdge.OnDrag(mainCamera);
            }
            else if (currentDraggedOrgan != null)
            {
                Vector3 worldPoint = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                worldPoint.z = currentDraggedOrgan.transform.position.z;
                currentDraggedOrgan.OnDrag(worldPoint);
            }
        }

        // 鼠标释放
        if (Input.GetMouseButtonUp(0))
        {
            if (currentRotateHandle != null)
            {
                currentRotateHandle.OnEndDrag();
                currentRotateHandle = null;
            }
            if (currentDraggedCorner != null)
            {
                currentDraggedCorner.OnEndDrag();
                currentDraggedCorner = null;
            }
            if (currentDraggedEdge != null)
            {
                currentDraggedEdge.OnEndDrag();
                currentDraggedEdge = null;
            }
            if (currentDraggedOrgan != null)
            {
                currentDraggedOrgan.EndDrag();
                currentDraggedOrgan = null;
            }
        }

        // 滚轮缩放（仅选中器官）
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0 && selectionManager != null && selectionManager.SelectedOrgan != null)
        {
            selectionManager.SelectedOrgan.ApplyZoom(scroll);
        }
    }
}