using UnityEngine;

public class SelectionGizmo : MonoBehaviour
{
    [Header("目标")]
    private DraggableOrgan targetOrgan;
    private SpriteRenderer targetRenderer;

    [Header("四角拖拽手柄")]
    [SerializeField] private GizmoCornerHandle cornerTopLeft;
    [SerializeField] private GizmoCornerHandle cornerTopRight;
    [SerializeField] private GizmoCornerHandle cornerBottomLeft;
    [SerializeField] private GizmoCornerHandle cornerBottomRight;

    [Header("四边中点拖拽手柄")]
    [SerializeField] private GizmoEdgeHandle edgeTopHandle;
    [SerializeField] private GizmoEdgeHandle edgeBottomHandle;
    [SerializeField] private GizmoEdgeHandle edgeLeftHandle;
    [SerializeField] private GizmoEdgeHandle edgeRightHandle;

    [Header("矩形线框（LineRenderer）")]
    [SerializeField] private LineRenderer lineTop;
    [SerializeField] private LineRenderer lineBottom;
    [SerializeField] private LineRenderer lineLeft;
    [SerializeField] private LineRenderer lineRight;

    [Header("线框粗细")]
    [SerializeField] private float lineWidth = 0.05f;

    private void Awake()
    {
        // 自动查找（若命名符合且未手动拖入）
        if (cornerTopLeft == null) cornerTopLeft = transform.Find("Corner_TL")?.GetComponent<GizmoCornerHandle>();
        if (cornerTopRight == null) cornerTopRight = transform.Find("Corner_TR")?.GetComponent<GizmoCornerHandle>();
        if (cornerBottomLeft == null) cornerBottomLeft = transform.Find("Corner_BL")?.GetComponent<GizmoCornerHandle>();
        if (cornerBottomRight == null) cornerBottomRight = transform.Find("Corner_BR")?.GetComponent<GizmoCornerHandle>();

        if (edgeTopHandle == null) edgeTopHandle = transform.Find("Edge_Top")?.GetComponent<GizmoEdgeHandle>();
        if (edgeBottomHandle == null) edgeBottomHandle = transform.Find("Edge_Bottom")?.GetComponent<GizmoEdgeHandle>();
        if (edgeLeftHandle == null) edgeLeftHandle = transform.Find("Edge_Left")?.GetComponent<GizmoEdgeHandle>();
        if (edgeRightHandle == null) edgeRightHandle = transform.Find("Edge_Right")?.GetComponent<GizmoEdgeHandle>();

        if (lineTop == null) lineTop = transform.Find("Line_Top")?.GetComponent<LineRenderer>();
        if (lineBottom == null) lineBottom = transform.Find("Line_Bottom")?.GetComponent<LineRenderer>();
        if (lineLeft == null) lineLeft = transform.Find("Line_Left")?.GetComponent<LineRenderer>();
        if (lineRight == null) lineRight = transform.Find("Line_Right")?.GetComponent<LineRenderer>();

        // 初始化 LineRenderer 设置
        SetupLine(lineTop);
        SetupLine(lineBottom);
        SetupLine(lineLeft);
        SetupLine(lineRight);

        gameObject.SetActive(false);
    }

    private void SetupLine(LineRenderer lr)
    {
        if (lr == null) return;
        lr.positionCount = 2;
        lr.useWorldSpace = false;   // 使用本地坐标，跟随 Gizmo 根
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.sortingOrder = 9;     // 确保线框显示在角和边之上/之下？按需调整
        //lr.material = new Material(Shader.Find("Sprites/Default"));
        //lr.startColor = Color.white;
        //lr.endColor = Color.white;
    }

    public void SetTarget(DraggableOrgan organ)
    {
        targetOrgan = organ;
        targetRenderer = organ?.GetComponent<SpriteRenderer>();

        cornerTopLeft?.SetTarget(organ);
        cornerTopRight?.SetTarget(organ);
        cornerBottomLeft?.SetTarget(organ);
        cornerBottomRight?.SetTarget(organ);

        edgeTopHandle?.SetTarget(organ);
        edgeBottomHandle?.SetTarget(organ);
        edgeLeftHandle?.SetTarget(organ);
        edgeRightHandle?.SetTarget(organ);

        gameObject.SetActive(targetRenderer != null);
    }

    public void ClearTarget()
    {
        targetOrgan = null;
        targetRenderer = null;

        cornerTopLeft?.SetTarget(null);
        cornerTopRight?.SetTarget(null);
        cornerBottomLeft?.SetTarget(null);
        cornerBottomRight?.SetTarget(null);

        edgeTopHandle?.SetTarget(null);
        edgeBottomHandle?.SetTarget(null);
        edgeLeftHandle?.SetTarget(null);
        edgeRightHandle?.SetTarget(null);

        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (targetRenderer == null) return;
        Sprite sprite = targetRenderer.sprite;
        if (sprite == null) return;

        Bounds localBounds = sprite.bounds;
        Vector3 localExtents = localBounds.extents;
        Transform organT = targetRenderer.transform;
        Vector3 worldScale = organT.lossyScale;
        Vector3 worldExtents = Vector3.Scale(localExtents, worldScale);
        Vector3 worldCenterOffset = organT.TransformVector(localBounds.center);
        Vector3 organWorldCenter = organT.position + worldCenterOffset;

        transform.position = organWorldCenter;
        transform.rotation = organT.rotation;
        transform.localScale = Vector3.one;

        // 设置四角
        SetLocalPos(cornerTopLeft, -worldExtents.x, worldExtents.y);
        SetLocalPos(cornerTopRight, worldExtents.x, worldExtents.y);
        SetLocalPos(cornerBottomLeft, -worldExtents.x, -worldExtents.y);
        SetLocalPos(cornerBottomRight, worldExtents.x, -worldExtents.y);

        // 设置边中点
        SetLocalPos(edgeTopHandle, 0, worldExtents.y);
        SetLocalPos(edgeBottomHandle, 0, -worldExtents.y);
        SetLocalPos(edgeLeftHandle, -worldExtents.x, 0);
        SetLocalPos(edgeRightHandle, worldExtents.x, 0);

        // 更新线框顶点（本地坐标）
        float hw = worldExtents.x;
        float hh = worldExtents.y;

        SetLinePositions(lineTop, new Vector3(-hw, hh, 0), new Vector3(hw, hh, 0));
        SetLinePositions(lineBottom, new Vector3(-hw, -hh, 0), new Vector3(hw, -hh, 0));
        SetLinePositions(lineLeft, new Vector3(-hw, hh, 0), new Vector3(-hw, -hh, 0));
        SetLinePositions(lineRight, new Vector3(hw, hh, 0), new Vector3(hw, -hh, 0));
    }

    private void SetLocalPos(MonoBehaviour handle, float x, float y)
    {
        if (handle != null)
            handle.transform.localPosition = new Vector3(x, y, 0);
    }

    private void SetLinePositions(LineRenderer lr, Vector3 start, Vector3 end)
    {
        if (lr == null) return;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
    }
}