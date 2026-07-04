using UnityEngine;

public class GizmoRotateHandle : MonoBehaviour
{
    public DraggableOrgan TargetOrgan { get; private set; }

    private Camera cam;
    private Quaternion originalRotation;
    private float angleOffset;    // 初始时鼠标方向与器官旋转的差值

    public void SetTarget(DraggableOrgan organ) => TargetOrgan = organ;

    private void Start() => cam = Camera.main;

    public void OnBeginDrag(Camera _)
    {
        if (TargetOrgan == null) return;
        Transform organT = TargetOrgan.transform;
        originalRotation = organT.rotation;

        Vector3 organPos = organT.position;
        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 dir = mouseWorld - organPos;
        float mouseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        angleOffset = organT.eulerAngles.z - mouseAngle;
    }

    public void OnDrag(Camera _)
    {
        if (TargetOrgan == null) return;
        Transform organT = TargetOrgan.transform;
        Vector3 organPos = organT.position;
        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 dir = mouseWorld - organPos;
        float mouseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float newAngle = mouseAngle + angleOffset;
        organT.eulerAngles = new Vector3(0, 0, newAngle);
    }

    public void OnEndDrag() { }
}