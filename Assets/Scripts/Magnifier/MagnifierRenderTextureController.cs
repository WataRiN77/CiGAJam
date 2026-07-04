using UnityEngine;
using UnityEngine.UI;

public class MagnifierRenderTextureController : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera magnifierCamera;

    [Header("Lens Output")]
    [SerializeField] private Renderer lensRenderer;
    [SerializeField] private RawImage lensRawImage;
    [SerializeField] private string texturePropertyName = "_BaseMap";
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private Vector2Int runtimeTextureSize = new Vector2Int(1024, 1024);
    [SerializeField] private FilterMode runtimeTextureFilterMode = FilterMode.Bilinear;
    [SerializeField] private int runtimeTextureAntiAliasing = 1;
    [SerializeField] private bool forceOpaqueRenderTextureAlpha = true;

    [Header("Lens Image Correction")]
    [SerializeField] private bool mirrorOutputX;
    [SerializeField] private bool mirrorOutputY;
    [SerializeField] private bool forceLensMaterialWhite = true;

    [Header("Mouse Follow")]
    [SerializeField] private Transform magnifierRoot;
    [SerializeField] private RectTransform lensRectTransform;
    [SerializeField] private Canvas lensCanvas;
    [SerializeField] private LayerMask raycastMask = ~0;
    [SerializeField] private bool useStablePlaneForOrthographic = true;
    [SerializeField] private Vector3 orthographicPlaneNormal = Vector3.up;
    [SerializeField] private float orthographicPlaneHeight = 0f;
    [SerializeField] private float fallbackDistance = 10f;
    [SerializeField] private float surfaceOffset = 0.03f;
    [SerializeField] private float followSmoothTime = 0.04f;
    [SerializeField] private bool placeWorldLensAtFixedCameraDistance = true;
    [SerializeField] private float worldLensDistanceFromCamera = 3f;
    [SerializeField] private bool useFixedLensRotation = true;
    [SerializeField] private Vector3 fixedLensEulerAngles = new Vector3(45f, 0f, 0f);
    [SerializeField] private bool rotateLensToFaceCamera;

    [Header("Magnification")]
    [SerializeField] private float zoom = 2.5f;
    [SerializeField] private float minFieldOfView = 5f;
    [SerializeField] private float minOrthographicSize = 0.2f;

    [Header("Camera Copy")]
    [SerializeField] private bool copyMainCameraSettings = true;
    [SerializeField] private bool copyCullingMask = true;
    [SerializeField] private bool useCustomMagnifierCullingMask;
    [SerializeField] private LayerMask customMagnifierCullingMask = ~0;
    [SerializeField] private bool copyClearFlags = true;

    [Header("Screen Space Camera UI")]
    [SerializeField] private bool renderScreenSpaceCameraUiInMagnifier = true;
    [SerializeField] private Canvas[] screenSpaceCameraCanvases;
    [SerializeField] private bool findScreenSpaceCameraCanvasesOnStart = true;
    [SerializeField] private float stableCanvasPlaneDistance = 1f;
    [SerializeField] private bool forceCanvasOverrideSorting = true;
    [SerializeField] private int forcedCanvasSortingOrder = 500;
    [SerializeField] private bool includeCanvasLayerInCameraMasks = true;
    [SerializeField] private bool excludeLensCanvasFromMagnifierUi = true;

    private MaterialPropertyBlock propertyBlock;
    private RenderTexture ownedRenderTexture;
    private Vector3 followVelocity;
    private CanvasState[] canvasStates = new CanvasState[0];
    private bool isFollowLocked;
    private Vector2 lockedMousePosition;
    private bool isLockedToWorldPoint;
    private Vector3 lockedWorldPoint;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        if (!CanUpdate())
        {
            return;
        }

        Vector2 mousePosition = isFollowLocked ? lockedMousePosition : (Vector2)Input.mousePosition;
        Vector3 focusPoint;
        Vector3 focusNormal;
        float focusDistance;

        if (isLockedToWorldPoint)
        {
            focusPoint = GetLockedWorldFocusPoint(out focusNormal, out focusDistance);
        }
        else
        {
            focusPoint = GetMouseFocusPoint(mousePosition, out focusNormal, out focusDistance);
        }

        UpdateWorldLens(mousePosition, focusPoint, focusNormal);
        UpdateUiLens(mousePosition);
        UpdateMagnifierCamera(focusPoint, focusDistance);
        RenderMagnifierCamera();
    }

    public void LockAtCurrentMousePosition()
    {
        lockedMousePosition = Input.mousePosition;
        isFollowLocked = true;
        isLockedToWorldPoint = false;
        followVelocity = Vector3.zero;
    }

    public void LockToWorldPoint(Vector3 worldPoint)
    {
        SetLockedWorldPoint(worldPoint);
        isFollowLocked = true;
        isLockedToWorldPoint = true;
        followVelocity = Vector3.zero;
    }

    public void SetLockedWorldPoint(Vector3 worldPoint)
    {
        lockedWorldPoint = worldPoint;
        lockedMousePosition = mainCamera != null ? (Vector2)mainCamera.WorldToScreenPoint(worldPoint) : (Vector2)Input.mousePosition;
    }

    public void UnlockFollow()
    {
        isFollowLocked = false;
        isLockedToWorldPoint = false;
        followVelocity = Vector3.zero;
    }

    public void UnlockFollowAndSnapToMouse()
    {
        isFollowLocked = false;
        isLockedToWorldPoint = false;
        followVelocity = Vector3.zero;

        if (!CanUpdate())
        {
            return;
        }

        Vector2 mousePosition = Input.mousePosition;
        Vector3 focusPoint = GetMouseFocusPoint(mousePosition, out Vector3 focusNormal, out float focusDistance);

        SnapWorldLens(mousePosition, focusPoint, focusNormal);
        SnapUiLens(mousePosition);
        UpdateMagnifierCamera(focusPoint, focusDistance);
        RenderMagnifierCamera();
    }

    private void OnDisable()
    {
        if (magnifierCamera != null)
        {
            magnifierCamera.targetTexture = null;
        }

        RestoreCanvasStates();
    }

    private void OnDestroy()
    {
        if (ownedRenderTexture != null)
        {
            ownedRenderTexture.Release();
            Destroy(ownedRenderTexture);
        }
    }

    private void Initialize()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        EnsureRenderTexture();
        ApplyOutputTexture();

        if (magnifierCamera != null)
        {
            magnifierCamera.enabled = !renderScreenSpaceCameraUiInMagnifier;
            magnifierCamera.targetTexture = GetActiveRenderTexture();
        }

        CacheScreenSpaceCameraCanvases();
        ApplyStableCanvasSettings(mainCamera);
    }

    private bool CanUpdate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null || magnifierCamera == null)
        {
            return false;
        }

        EnsureRenderTexture();
        return GetActiveRenderTexture() != null;
    }

    private void EnsureRenderTexture()
    {
        if (renderTexture != null || ownedRenderTexture != null)
        {
            return;
        }

        int width = Mathf.Max(32, runtimeTextureSize.x);
        int height = Mathf.Max(32, runtimeTextureSize.y);
        ownedRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "Runtime Magnifier Render Texture",
            filterMode = runtimeTextureFilterMode,
            antiAliasing = GetSafeAntiAliasing(runtimeTextureAntiAliasing),
            useMipMap = false,
            autoGenerateMips = false
        };
        ownedRenderTexture.Create();
    }

    private RenderTexture GetActiveRenderTexture()
    {
        return renderTexture != null ? renderTexture : ownedRenderTexture;
    }

    private void ApplyOutputTexture()
    {
        RenderTexture activeTexture = GetActiveRenderTexture();

        if (activeTexture != null)
        {
            activeTexture.filterMode = runtimeTextureFilterMode;
        }

        if (lensRawImage != null)
        {
            lensRawImage.texture = activeTexture;
            lensRawImage.uvRect = GetCorrectedUvRect();
        }

        if (lensRenderer != null)
        {
            EnsurePropertyBlock();
            lensRenderer.GetPropertyBlock(propertyBlock);
            string textureName = GetTexturePropertyName();
            propertyBlock.SetTexture(textureName, activeTexture);
            propertyBlock.SetVector(textureName + "_ST", GetCorrectedTextureScaleOffset());

            if (forceLensMaterialWhite)
            {
                propertyBlock.SetColor("_BaseColor", Color.white);
                propertyBlock.SetColor("_Color", Color.white);
            }

            lensRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private string GetTexturePropertyName()
    {
        return string.IsNullOrWhiteSpace(texturePropertyName) ? "_BaseMap" : texturePropertyName;
    }

    private Rect GetCorrectedUvRect()
    {
        return new Rect(
            mirrorOutputX ? 1f : 0f,
            mirrorOutputY ? 1f : 0f,
            mirrorOutputX ? -1f : 1f,
            mirrorOutputY ? -1f : 1f
        );
    }

    private Vector4 GetCorrectedTextureScaleOffset()
    {
        return new Vector4(
            mirrorOutputX ? -1f : 1f,
            mirrorOutputY ? -1f : 1f,
            mirrorOutputX ? 1f : 0f,
            mirrorOutputY ? 1f : 0f
        );
    }

    private Vector3 GetMouseFocusPoint(Vector2 mousePosition, out Vector3 focusNormal, out float focusDistance)
    {
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);

        if (mainCamera.orthographic && useStablePlaneForOrthographic)
        {
            Vector3 planeNormal = GetSafeOrthographicPlaneNormal();
            Plane plane = new Plane(planeNormal, planeNormal * orthographicPlaneHeight);

            if (plane.Raycast(ray, out float planeDistance))
            {
                Vector3 point = ray.GetPoint(planeDistance);
                focusNormal = planeNormal;
                focusDistance = GetCameraForwardDistance(point);
                return point;
            }
        }

        Vector3 hitPoint;
        Vector3 hitNormal;
        float hitDistance;

        if (TryGetPhysicsFocusPoint(ray, out hitPoint, out hitNormal, out hitDistance))
        {
            focusNormal = hitNormal;
            focusDistance = hitDistance;
            return hitPoint;
        }

        focusNormal = -mainCamera.transform.forward;
        Vector3 fallbackPoint = ray.GetPoint(fallbackDistance);
        focusDistance = GetCameraForwardDistance(fallbackPoint);
        return fallbackPoint;
    }

    private bool TryGetPhysicsFocusPoint(Ray ray, out Vector3 point, out Vector3 normal, out float focusDistance)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, mainCamera.farClipPlane, raycastMask, QueryTriggerInteraction.Collide))
        {
            point = hit.point;
            normal = hit.normal;
            focusDistance = GetCameraForwardDistance(point);
            return true;
        }

        point = Vector3.zero;
        normal = Vector3.up;
        focusDistance = 0f;
        return false;
    }

    private Vector3 GetLockedWorldFocusPoint(out Vector3 focusNormal, out float focusDistance)
    {
        focusNormal = GetSafeOrthographicPlaneNormal();
        focusDistance = GetCameraForwardDistance(lockedWorldPoint);
        return lockedWorldPoint;
    }

    private void UpdateWorldLens(Vector2 mousePosition, Vector3 focusPoint, Vector3 focusNormal)
    {
        if (magnifierRoot == null)
        {
            return;
        }

        Vector3 targetPosition = GetWorldLensTargetPosition(mousePosition, focusPoint, focusNormal);

        if (followSmoothTime <= 0f)
        {
            magnifierRoot.position = targetPosition;
        }
        else
        {
            magnifierRoot.position = Vector3.SmoothDamp(
                magnifierRoot.position,
                targetPosition,
                ref followVelocity,
                followSmoothTime
            );
        }

        if (useFixedLensRotation)
        {
            magnifierRoot.rotation = Quaternion.Euler(fixedLensEulerAngles);
        }
        else if (rotateLensToFaceCamera)
        {
            Vector3 toCamera = mainCamera.transform.position - magnifierRoot.position;

            if (toCamera.sqrMagnitude > 0.0001f)
            {
                magnifierRoot.rotation = Quaternion.LookRotation(-toCamera.normalized, mainCamera.transform.up);
            }
        }
    }

    private void SnapWorldLens(Vector2 mousePosition, Vector3 focusPoint, Vector3 focusNormal)
    {
        if (magnifierRoot == null)
        {
            return;
        }

        magnifierRoot.position = GetWorldLensTargetPosition(mousePosition, focusPoint, focusNormal);
        followVelocity = Vector3.zero;

        if (useFixedLensRotation)
        {
            magnifierRoot.rotation = Quaternion.Euler(fixedLensEulerAngles);
        }
    }

    private Vector3 GetWorldLensTargetPosition(Vector2 mousePosition, Vector3 focusPoint, Vector3 focusNormal)
    {
        if (!placeWorldLensAtFixedCameraDistance || mainCamera == null)
        {
            return focusPoint + focusNormal * surfaceOffset;
        }

        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        return ray.GetPoint(worldLensDistanceFromCamera);
    }

    private void UpdateUiLens(Vector2 mousePosition)
    {
        if (lensRectTransform == null)
        {
            return;
        }

        if (lensCanvas == null || lensCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            lensRectTransform.position = mousePosition;
            return;
        }

        RectTransform canvasRect = lensCanvas.transform as RectTransform;
        Camera canvasCamera = lensCanvas.renderMode == RenderMode.ScreenSpaceCamera ? lensCanvas.worldCamera : null;

        if (canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mousePosition, canvasCamera, out Vector2 localPoint))
        {
            lensRectTransform.localPosition = localPoint;
        }
    }

    private void SnapUiLens(Vector2 mousePosition)
    {
        UpdateUiLens(mousePosition);
    }

    private void UpdateMagnifierCamera(Vector3 focusPoint, float focusDistance)
    {
        CopyCameraSettings();

        RenderTexture activeTexture = GetActiveRenderTexture();
        magnifierCamera.targetTexture = activeTexture;
        magnifierCamera.aspect = activeTexture != null ? (float)activeTexture.width / activeTexture.height : mainCamera.aspect;

        float safeZoom = Mathf.Max(0.01f, zoom);

        if (mainCamera.orthographic)
        {
            Vector3 viewDirection = mainCamera.transform.forward;

            magnifierCamera.orthographic = true;
            magnifierCamera.transform.rotation = mainCamera.transform.rotation;
            magnifierCamera.transform.position = focusPoint - viewDirection * focusDistance;
            magnifierCamera.orthographicSize = Mathf.Max(minOrthographicSize, mainCamera.orthographicSize / safeZoom);
        }
        else
        {
            Vector3 direction = focusPoint - mainCamera.transform.position;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = mainCamera.transform.forward;
            }

            magnifierCamera.orthographic = false;
            magnifierCamera.transform.position = mainCamera.transform.position;
            magnifierCamera.transform.rotation = Quaternion.LookRotation(direction.normalized, mainCamera.transform.up);
            magnifierCamera.fieldOfView = Mathf.Max(minFieldOfView, mainCamera.fieldOfView / safeZoom);
        }
    }

    private void CopyCameraSettings()
    {
        if (!copyMainCameraSettings)
        {
            return;
        }

        magnifierCamera.nearClipPlane = mainCamera.nearClipPlane;
        magnifierCamera.farClipPlane = mainCamera.farClipPlane;
        magnifierCamera.backgroundColor = mainCamera.backgroundColor;
        magnifierCamera.allowHDR = mainCamera.allowHDR;
        magnifierCamera.allowMSAA = mainCamera.allowMSAA;
        magnifierCamera.transparencySortMode = mainCamera.transparencySortMode;
        magnifierCamera.transparencySortAxis = mainCamera.transparencySortAxis;

        if (forceOpaqueRenderTextureAlpha)
        {
            Color backgroundColor = magnifierCamera.backgroundColor;
            backgroundColor.a = 1f;
            magnifierCamera.backgroundColor = backgroundColor;
        }

        if (useCustomMagnifierCullingMask)
        {
            magnifierCamera.cullingMask = customMagnifierCullingMask;
        }
        else if (copyCullingMask)
        {
            magnifierCamera.cullingMask = mainCamera.cullingMask;
        }

        if (copyClearFlags)
        {
            magnifierCamera.clearFlags = mainCamera.clearFlags;
        }

        if (forceOpaqueRenderTextureAlpha && magnifierCamera.clearFlags == CameraClearFlags.Depth)
        {
            magnifierCamera.clearFlags = CameraClearFlags.SolidColor;
        }
    }

    private void RenderMagnifierCamera()
    {
        if (!renderScreenSpaceCameraUiInMagnifier || magnifierCamera == null)
        {
            return;
        }

        magnifierCamera.enabled = false;
        ApplyStableCanvasSettings(magnifierCamera);
        magnifierCamera.Render();
        ApplyStableCanvasSettings(mainCamera);
    }

    private void CacheScreenSpaceCameraCanvases()
    {
        if (!findScreenSpaceCameraCanvasesOnStart || (screenSpaceCameraCanvases != null && screenSpaceCameraCanvases.Length > 0))
        {
            return;
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        int count = 0;

        for (int i = 0; i < canvases.Length; i++)
        {
            if (ShouldUseCanvasForMagnifierUi(canvases[i]))
            {
                count++;
            }
        }

        screenSpaceCameraCanvases = new Canvas[count];
        int index = 0;

        for (int i = 0; i < canvases.Length; i++)
        {
            if (ShouldUseCanvasForMagnifierUi(canvases[i]))
            {
                screenSpaceCameraCanvases[index] = canvases[i];
                index++;
            }
        }
    }

    private void ApplyStableCanvasSettings(Camera targetCamera)
    {
        if (screenSpaceCameraCanvases == null || targetCamera == null)
        {
            return;
        }

        EnsureCanvasStateCapacity();

        for (int i = 0; i < screenSpaceCameraCanvases.Length; i++)
        {
            Canvas canvas = screenSpaceCameraCanvases[i];

            if (!ShouldUseCanvasForMagnifierUi(canvas))
            {
                continue;
            }

            RememberCanvasState(i, canvas);
            canvas.worldCamera = targetCamera;
            canvas.planeDistance = stableCanvasPlaneDistance;

            if (forceCanvasOverrideSorting)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = forcedCanvasSortingOrder;
            }

            if (includeCanvasLayerInCameraMasks)
            {
                mainCamera.cullingMask |= 1 << canvas.gameObject.layer;
                magnifierCamera.cullingMask |= 1 << canvas.gameObject.layer;
            }
        }
    }

    private void RememberCanvasState(int index, Canvas canvas)
    {
        if (index < 0 || index >= canvasStates.Length || canvasStates[index].HasValue)
        {
            return;
        }

        canvasStates[index] = new CanvasState(canvas);
    }

    private void EnsureCanvasStateCapacity()
    {
        int neededLength = screenSpaceCameraCanvases != null ? screenSpaceCameraCanvases.Length : 0;

        if (canvasStates != null && canvasStates.Length >= neededLength)
        {
            return;
        }

        CanvasState[] newStates = new CanvasState[neededLength];

        if (canvasStates != null)
        {
            for (int i = 0; i < canvasStates.Length; i++)
            {
                newStates[i] = canvasStates[i];
            }
        }

        canvasStates = newStates;
    }

    private bool ShouldUseCanvasForMagnifierUi(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceCamera)
        {
            return false;
        }

        return !excludeLensCanvasFromMagnifierUi || canvas != lensCanvas;
    }

    private void RestoreCanvasStates()
    {
        for (int i = 0; i < canvasStates.Length; i++)
        {
            if (!canvasStates[i].HasValue)
            {
                continue;
            }

            canvasStates[i].Restore();
            canvasStates[i] = default;
        }
    }

    private void OnValidate()
    {
        if (orthographicPlaneNormal.sqrMagnitude < 0.0001f)
        {
            orthographicPlaneNormal = Vector3.up;
        }

        runtimeTextureSize.x = Mathf.Max(32, runtimeTextureSize.x);
        runtimeTextureSize.y = Mathf.Max(32, runtimeTextureSize.y);
        runtimeTextureAntiAliasing = GetSafeAntiAliasing(runtimeTextureAntiAliasing);
        zoom = Mathf.Max(0.01f, zoom);
        minFieldOfView = Mathf.Max(1f, minFieldOfView);
        minOrthographicSize = Mathf.Max(0.01f, minOrthographicSize);
        fallbackDistance = Mathf.Max(0.1f, fallbackDistance);
        followSmoothTime = Mathf.Max(0f, followSmoothTime);
        worldLensDistanceFromCamera = Mathf.Max(0.01f, worldLensDistanceFromCamera);
        stableCanvasPlaneDistance = Mathf.Max(0.01f, stableCanvasPlaneDistance);
    }

    private Vector3 GetSafeOrthographicPlaneNormal()
    {
        return orthographicPlaneNormal.sqrMagnitude < 0.0001f ? Vector3.up : orthographicPlaneNormal.normalized;
    }

    private float GetCameraForwardDistance(Vector3 worldPoint)
    {
        return Vector3.Dot(worldPoint - mainCamera.transform.position, mainCamera.transform.forward);
    }

    private int GetSafeAntiAliasing(int antiAliasing)
    {
        if (antiAliasing <= 1)
        {
            return 1;
        }

        if (antiAliasing <= 2)
        {
            return 2;
        }

        if (antiAliasing <= 4)
        {
            return 4;
        }

        return 8;
    }

    private struct CanvasState
    {
        public readonly bool HasValue;
        private readonly Canvas canvas;
        private readonly Camera worldCamera;
        private readonly float planeDistance;
        private readonly bool overrideSorting;
        private readonly int sortingOrder;

        public CanvasState(Canvas canvas)
        {
            HasValue = true;
            this.canvas = canvas;
            worldCamera = canvas.worldCamera;
            planeDistance = canvas.planeDistance;
            overrideSorting = canvas.overrideSorting;
            sortingOrder = canvas.sortingOrder;
        }

        public void Restore()
        {
            if (canvas == null)
            {
                return;
            }

            canvas.worldCamera = worldCamera;
            canvas.planeDistance = planeDistance;
            canvas.overrideSorting = overrideSorting;
            canvas.sortingOrder = sortingOrder;
        }
    }
}
