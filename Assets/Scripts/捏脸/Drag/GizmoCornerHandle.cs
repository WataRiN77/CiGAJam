using UnityEngine;

public class GizmoCornerHandle : MonoBehaviour
{
    public DraggableOrgan TargetOrgan { get; private set; }

    private Vector3 originalScale;
    private Vector3 organWorldPos;
    private float initProjX, initProjY;   // 初始沿局部轴的分量（带符号）
    private Camera cam;

    public void SetTarget(DraggableOrgan organ) => TargetOrgan = organ;

    private void Start() => cam = Camera.main;

    public void OnBeginDrag(Camera _)
    {
        if (TargetOrgan == null) return;
        Transform organT = TargetOrgan.transform;
        originalScale = organT.localScale;
        organWorldPos = organT.position;

        // 器官的局部轴方向（世界空间）
        Vector3 right = organT.right;
        Vector3 up = organT.up;

        // 初始角到器官中心的偏移
        Vector3 initialOffset = transform.position - organWorldPos;
        initProjX = Vector3.Dot(initialOffset, right);
        initProjY = Vector3.Dot(initialOffset, up);
    }

    public void OnDrag(Camera _)
    {
        if (TargetOrgan == null) return;
        Transform organT = TargetOrgan.transform;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 currentOffset = mouseWorld - organT.position;

        // 投影到器官当前的局部轴（因为旋转可能不变，但为保险实时获取）
        float curProjX = Vector3.Dot(currentOffset, organT.right);
        float curProjY = Vector3.Dot(currentOffset, organT.up);

        float newScaleX = originalScale.x;
        float newScaleY = originalScale.y;

        if (Mathf.Abs(initProjX) > 0.001f)
        {
            float factorX = curProjX / initProjX;
            // 限制缩放不能为负，防止越过中心点
            factorX = Mathf.Max(factorX, 0.01f / originalScale.x);
            newScaleX = originalScale.x * factorX;
        }

        if (Mathf.Abs(initProjY) > 0.001f)
        {
            float factorY = curProjY / initProjY;
            factorY = Mathf.Max(factorY, 0.01f / originalScale.y);
            newScaleY = originalScale.y * factorY;
        }

        organT.localScale = new Vector3(newScaleX, newScaleY, 1f);
    }

    public void OnEndDrag() { }
}