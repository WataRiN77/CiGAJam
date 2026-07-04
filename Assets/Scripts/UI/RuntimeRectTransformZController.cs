using UnityEngine;

[ExecuteAlways]
public class RuntimeRectTransformZController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform[] targetRoots;
    [SerializeField] private bool includeChildren = true;
    [SerializeField] private bool includeRootTransform;

    [Header("Z Positions")]
    [SerializeField] private float editModeZ = -6400f;
    [SerializeField] private float playModeZ = 0f;
    [SerializeField] private bool applyInEditMode = true;
    [SerializeField] private bool applyInPlayMode = true;

    private void OnEnable()
    {
        ApplyCurrentModeZ();
    }

    private void Start()
    {
        ApplyCurrentModeZ();
    }

    private void OnValidate()
    {
        ApplyCurrentModeZ();
    }

    private void ApplyCurrentModeZ()
    {
        bool isPlaying = Application.isPlaying;

        if ((isPlaying && !applyInPlayMode) || (!isPlaying && !applyInEditMode))
        {
            return;
        }

        float targetZ = isPlaying ? playModeZ : editModeZ;

        if (targetRoots != null && targetRoots.Length > 0)
        {
            for (int i = 0; i < targetRoots.Length; i++)
            {
                ApplyRoot(targetRoots[i], targetZ);
            }

            return;
        }

        ApplyRoot(transform, targetZ);
    }

    private void ApplyRoot(Transform root, float targetZ)
    {
        if (root == null)
        {
            return;
        }

        ApplyRecursive(root, targetZ, true);
    }

    private void ApplyRecursive(Transform current, float targetZ, bool isRoot)
    {
        if (current == null)
        {
            return;
        }

        RectTransform rectTransform = current as RectTransform;

        if (rectTransform != null && (!isRoot || includeRootTransform))
        {
            SetZ(rectTransform, targetZ);
        }

        if (isRoot && !includeChildren)
        {
            return;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            ApplyRecursive(current.GetChild(i), targetZ, false);
        }
    }

    private void SetZ(RectTransform rectTransform, float targetZ)
    {
        Vector3 anchoredPosition = rectTransform.anchoredPosition3D;

        if (Mathf.Approximately(anchoredPosition.z, targetZ))
        {
            return;
        }

        anchoredPosition.z = targetZ;
        rectTransform.anchoredPosition3D = anchoredPosition;
    }
}
