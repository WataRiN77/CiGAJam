using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    [Header("高亮材质")]
    [SerializeField] private Material outlineMaterial;

    [Header("Gizmo 预制体")]
    [SerializeField] private GameObject gizmoPrefab;

    public DraggableOrgan SelectedOrgan { get; private set; }
    private SpriteRenderer selectedRenderer;
    private Material originalMaterial;
    private GameObject currentGizmo;

    private void Awake()
    {
        Instance = this;
    }

    public void SelectOrgan(DraggableOrgan organ)
    {
        if (SelectedOrgan == organ) return;
        DeselectCurrent();

        SelectedOrgan = organ;
        selectedRenderer = organ.GetComponent<SpriteRenderer>();
        if (selectedRenderer != null)
        {
            originalMaterial = selectedRenderer.sharedMaterial;
            selectedRenderer.material = outlineMaterial; // 实例化材质，避免影响其他对象
        }

        // 创建 Gizmo
        if (gizmoPrefab != null)
        {
            currentGizmo = Instantiate(gizmoPrefab, transform); // 放在角色根下或直接放在世界
            SelectionGizmo gizmoScript = currentGizmo.GetComponent<SelectionGizmo>();
            if (gizmoScript != null)
            {
                Debug.Log("1");
                gizmoScript.SetTarget(organ);
            }
        }
    }

    public void DeselectCurrent()
    {
        if (selectedRenderer != null)
        {
            // 恢复材质
            if (originalMaterial != null)
                selectedRenderer.material = originalMaterial;
            selectedRenderer = null;
        }

        if (currentGizmo != null)
        {
            SelectionGizmo gizmoScript = currentGizmo.GetComponent<SelectionGizmo>();
            if (gizmoScript != null)
                gizmoScript.ClearTarget();
            Destroy(currentGizmo);
            currentGizmo = null;
        }

        SelectedOrgan = null;
    }
}