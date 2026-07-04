using UnityEngine;

public class OrganInteractionManager : MonoBehaviour
{
    [Tooltip("器官所在的 Layer，只检测这一层")]
    [SerializeField] private LayerMask organLayerMask;

    private Camera mainCamera;
    private DraggableOrgan currentDraggedOrgan = null;

    private void Start()
    {
        mainCamera = Camera.main;
        if (organLayerMask == 0)
            organLayerMask = LayerMask.GetMask("FaceOrgan"); // 假设你已创建 FaceOrgan 层
    }

    private void Update()
    {
        Vector2 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);

        // 鼠标按下
        if (Input.GetMouseButtonDown(0))
        {
            // 获取鼠标位置所有器官碰撞体
            Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorld, organLayerMask);
            if (hits.Length > 0)
            {
                // 找出距离鼠标最近的碰撞体
                Collider2D nearest = null;
                float minDist = float.MaxValue;
                foreach (var col in hits)
                {
                    float dist = Vector2.Distance(mouseWorld, col.bounds.center);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = col;
                    }
                }

                // 选中最近的器官并开始拖拽
                DraggableOrgan organ = nearest.GetComponent<DraggableOrgan>();
                if (organ != null)
                {
                    // 通知选择管理器
                    SelectionManager.Instance?.SelectOrgan(organ);
                    // 开始拖拽
                    organ.BeginDrag(mainCamera.ScreenToWorldPoint(Input.mousePosition));
                    currentDraggedOrgan = organ;
                }
            }
            else
            {
                // 点击空白处取消选中
                SelectionManager.Instance?.DeselectCurrent();
            }
        }

        // 鼠标拖拽中
        if (Input.GetMouseButton(0) && currentDraggedOrgan != null)
        {
            currentDraggedOrgan.OnDrag(mainCamera.ScreenToWorldPoint(Input.mousePosition));
        }

        // 鼠标松开
        if (Input.GetMouseButtonUp(0))
        {
            if (currentDraggedOrgan != null)
            {
                currentDraggedOrgan.EndDrag();
                currentDraggedOrgan = null;
            }
        }

        // 鼠标滚轮缩放（仅对当前选中的器官生效）
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            // 需要知道鼠标悬停在哪个器官上（最近的那个）
            Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorld, organLayerMask);
            if (hits.Length > 0)
            {
                // 找到最近的
                Collider2D nearest = null;
                float minDist = float.MaxValue;
                foreach (var col in hits)
                {
                    float dist = Vector2.Distance(mouseWorld, col.bounds.center);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = col;
                    }
                }
                DraggableOrgan organ = nearest.GetComponent<DraggableOrgan>();
                // 只有选中的器官才响应缩放（可按需改为悬停的器官）
                if (organ != null && SelectionManager.Instance != null &&
                    SelectionManager.Instance.SelectedOrgan == organ)
                {
                    organ.ApplyZoom(scroll);
                }
            }
        }
    }
}