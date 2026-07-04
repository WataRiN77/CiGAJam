using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class PostProcessedCanvasBinder : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Canvas[] canvases;
    [SerializeField] private bool findCanvasesOnStart = true;
    [SerializeField] private bool includeInactiveCanvases;
    [SerializeField] private bool keepBoundEveryFrame = true;
    [SerializeField] private bool includeCanvasLayerInCameraCullingMask = true;
    [SerializeField] private float planeDistance = 1f;

    private void OnEnable()
    {
        RefreshAndApply();
    }

    private void Start()
    {
        RefreshAndApply();
    }

    private void LateUpdate()
    {
        if (keepBoundEveryFrame)
        {
            RefreshAndApply();
        }
    }

    private void RefreshAndApply()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (findCanvasesOnStart || canvases == null || canvases.Length == 0)
        {
            canvases = FindObjectsOfType<Canvas>(includeInactiveCanvases);
        }

        Apply();
    }

    [ContextMenu("Apply")]
    public void Apply()
    {
        if (targetCamera == null || canvases == null)
        {
            return;
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];

            if (canvas == null)
            {
                continue;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = targetCamera;
                canvas.planeDistance = planeDistance;

                if (includeCanvasLayerInCameraCullingMask)
                {
                    targetCamera.cullingMask |= 1 << canvas.gameObject.layer;
                }
            }
        }
    }

    private void OnValidate()
    {
        planeDistance = Mathf.Max(0.01f, planeDistance);

        if (isActiveAndEnabled)
        {
            RefreshAndApply();
        }
    }

    public static void BindAllOverlayCanvasesToCamera(
        Camera camera,
        float planeDistance,
        bool includeInactive,
        bool includeCanvasLayerInCameraCullingMask
    )
    {
        if (camera == null)
        {
            return;
        }

        Canvas[] sceneCanvases = FindObjectsOfType<Canvas>(includeInactive);
        List<Canvas> validCanvases = new List<Canvas>(sceneCanvases.Length);

        for (int i = 0; i < sceneCanvases.Length; i++)
        {
            Canvas canvas = sceneCanvases[i];

            if (canvas == null)
            {
                continue;
            }

            validCanvases.Add(canvas);
        }

        BindCanvasesToCamera(validCanvases, camera, planeDistance, includeCanvasLayerInCameraCullingMask);
    }

    private static void BindCanvasesToCamera(
        IEnumerable<Canvas> targetCanvases,
        Camera camera,
        float planeDistance,
        bool includeCanvasLayerInCameraCullingMask
    )
    {
        foreach (Canvas canvas in targetCanvases)
        {
            if (canvas == null)
            {
                continue;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
            }

            if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                continue;
            }

            canvas.worldCamera = camera;
            canvas.planeDistance = Mathf.Max(0.01f, planeDistance);

            if (includeCanvasLayerInCameraCullingMask)
            {
                camera.cullingMask |= 1 << canvas.gameObject.layer;
            }
        }
    }
}
