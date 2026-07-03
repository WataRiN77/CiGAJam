using UnityEngine;

public class GizmoEdgeHandle : MonoBehaviour
{
    public enum EdgeType { Top, Bottom, Left, Right }
    [SerializeField] private EdgeType edgeType;

    public DraggableOrgan TargetOrgan { get; private set; }

    private Vector3 originalScale;
    private Vector3 organWorldPos;
    private float initProj;
    private Camera cam;

    // 记录初始时计算用的世界轴方向（与器官对齐）
    private Vector3 worldAxisDir;

    public void SetTarget(DraggableOrgan organ) => TargetOrgan = organ;

    private void Start() => cam = Camera.main;

    public void OnBeginDrag(Camera _)
    {
        if (TargetOrgan == null) return;
        Transform organT = TargetOrgan.transform;
        originalScale = organT.localScale;
        organWorldPos = organT.position;

        // 根据边的类型确定世界轴方向
        switch (edgeType)
        {
            case EdgeType.Top: worldAxisDir = organT.up; break;
            case EdgeType.Bottom: worldAxisDir = -organT.up; break;
            case EdgeType.Left: worldAxisDir = -organT.right; break;
            case EdgeType.Right: worldAxisDir = organT.right; break;
        }

        Vector3 initialOffset = transform.position - organWorldPos;
        initProj = Vector3.Dot(initialOffset, worldAxisDir);
    }

    public void OnDrag(Camera _)
    {
        if (TargetOrgan == null) return;
        Transform organT = TargetOrgan.transform;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 currentOffset = mouseWorld - organT.position;

        // 重新获取当前的世界轴（如果器官被旋转）
        Vector3 currentAxis = GetWorldAxis(organT);
        float curProj = Vector3.Dot(currentOffset, currentAxis);

        float newScaleX = originalScale.x;
        float newScaleY = originalScale.y;

        if (Mathf.Abs(initProj) > 0.001f)
        {
            float factor = curProj / initProj;
            factor = Mathf.Max(factor, 0.01f);

            if (edgeType == EdgeType.Top || edgeType == EdgeType.Bottom)
                newScaleY = originalScale.y * factor;
            else
                newScaleX = originalScale.x * factor;
        }

        organT.localScale = new Vector3(
            Mathf.Max(newScaleX, 0.01f),
            Mathf.Max(newScaleY, 0.01f),
            1f);
    }

    private Vector3 GetWorldAxis(Transform organT)
    {
        switch (edgeType)
        {
            case EdgeType.Top: return organT.up;
            case EdgeType.Bottom: return -organT.up;
            case EdgeType.Left: return -organT.right;
            case EdgeType.Right: return organT.right;
        }
        return Vector3.zero;
    }

    public void OnEndDrag() { }
}