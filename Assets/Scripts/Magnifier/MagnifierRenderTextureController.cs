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

    private MaterialPropertyBlock propertyBlock;
    private RenderTexture ownedRenderTexture;
    private Vector3 followVelocity;

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

        Vector2 mousePosition = Input.mousePosition;
        Vector3 focusPoint = GetMouseFocusPoint(mousePosition, out Vector3 focusNormal, out float focusDistance);

        UpdateWorldLens(mousePosition, focusPoint, focusNormal);
        UpdateUiLens(mousePosition);
        UpdateMagnifierCamera(focusPoint, focusDistance);
    }

    private void OnDisable()
    {
        if (magnifierCamera != null)
        {
            magnifierCamera.targetTexture = null;
        }
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
            magnifierCamera.enabled = true;
            magnifierCamera.targetTexture = GetActiveRenderTexture();
        }
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
}
