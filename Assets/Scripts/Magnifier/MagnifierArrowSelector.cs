using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MagnifierArrowSelector : MonoBehaviour
{
    [Header("Click Camera")]
    [SerializeField] private Camera magnifierCamera;
    [SerializeField] private MagnifierRenderTextureController magnifierController;
    [SerializeField] private LayerMask selectableMask = ~0;
    [SerializeField] private float maxClickDistance = 100f;
    [SerializeField] private float clickSphereRadius = 0.35f;
    [SerializeField] private bool closeSingleActiveArrowWhenMiss;

    [Header("Left Click Kill")]
    [SerializeField] private bool killOnLeftClick;

    [Header("Left Click Focus")]
    [SerializeField] private bool focusOnLeftClick = true;
    [SerializeField] private Camera focusCamera;
    [SerializeField] private Transform cameraChildCharacterRoot;
    [SerializeField] private string cameraChildCharacterRootName = "Character_Root";
    [SerializeField] private string shotPointChildName = "shotpoint";
    [SerializeField] private string[] headFocusChildNames = { "面部", "Head", "头部", "Face" };
    [SerializeField] private Vector3 headFocusOffset;
    [SerializeField] private float focusMoveDuration = 0.2f;
    [SerializeField] private float focusZoomDelay = 0.1f;
    [SerializeField] private float focusZoomDuration = 0.2f;
    [SerializeField] private float focusedOrthographicSize = 5f;
    [SerializeField] private Vector2 focusedTargetViewportPoint = new Vector2(0.63f, 0.5f);
    [SerializeField] private Vector3 focusedCameraWorldOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private Vector3 focusedCharacterRootLocalPosition = new Vector3(-5.5f, 0f, 16f);
    [SerializeField] private float magnifierReturnToMouseDuration = 0.3f;

    [Header("Focus Buttons")]
    [SerializeField] private GameObject[] focusModeButtons;
    [SerializeField] private Transform magnifierShotObject;
    [SerializeField] private string magnifierShotChildName = "shot";

    [Header("Focused Shoot")]
    [SerializeField] private float shootSequenceDuration = 1f;
    [SerializeField] private float shootSlowMotionScale = 0.2f;
    [SerializeField] private float shootCameraShakeStrength = 0.35f;
    [SerializeField] private float shootCameraShakeFrequency = 35f;

    [Header("Kill UI")]
    [SerializeField] private KillUiBlinkController killUiBlinkController;

    [Header("Game Session")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [Header("Hit FX")]
    [SerializeField] private string bloodFxChildName = "blood";
    [SerializeField] private bool detachBloodFxOnPlay;

    [Header("Arrow Target")]
    [SerializeField] private string arrowChildName = "arrows";
    [SerializeField] private string fallbackArrowChildName = "Arrow";

    [Header("Arrow Float")]
    [SerializeField] private float floatHeight = 0.18f;
    [SerializeField] private float floatSpeed = 2.5f;

    private readonly Dictionary<Transform, ArrowState> activeArrows = new Dictionary<Transform, ArrowState>();
    private readonly RaycastHit[] clickHits = new RaycastHit[64];
    private FocusState focusState = FocusState.Idle;
    private Transform focusedPerson;
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private float originalCameraOrthographicSize;
    private Vector3 originalCharacterRootLocalPosition;
    private Vector3 transitionCameraStartPosition;
    private Quaternion transitionCameraStartRotation;
    private float transitionCameraStartSize;
    private Vector3 transitionCharacterRootStartLocalPosition;
    private float focusForwardDistance;
    private float focusTransitionTimer;
    private Vector3 focusCameraShakeOffset;
    private bool isShootingFocusedPerson;
    private float defaultFixedDeltaTime;

    private enum FocusState
    {
        Idle,
        MovingIn,
        Focused,
        MovingOut
    }

    private void Start()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;

        if (magnifierCamera == null)
        {
            Debug.LogWarning($"{nameof(MagnifierArrowSelector)} needs a Magnifier Camera reference.", this);
        }

        if (focusCamera == null)
        {
            focusCamera = Camera.main;
        }

        if (cameraChildCharacterRoot == null && focusCamera != null)
        {
            cameraChildCharacterRoot = FindChildRecursive(focusCamera.transform, cameraChildCharacterRootName);
        }

        if (magnifierController == null)
        {
            magnifierController = FindObjectOfType<MagnifierRenderTextureController>();
        }

        if (magnifierShotObject == null)
        {
            magnifierShotObject = FindMagnifierShotObject();
        }

        if (killUiBlinkController == null)
        {
            killUiBlinkController = FindObjectOfType<KillUiBlinkController>();
        }

        if (gameSessionManager == null)
        {
            gameSessionManager = FindObjectOfType<GameSessionManager>();
        }

        SetMagnifierShotVisible(false);
        SetFocusButtonsVisible(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            BeginReturnFromFocus();
        }

        if (Input.GetMouseButtonDown(1))
        {
            TryToggleArrowThroughMagnifier();
        }

        if (focusOnLeftClick && focusState == FocusState.Idle && Input.GetMouseButtonDown(0) && !IsPointerOverUi())
        {
            TryFocusThroughMagnifier();
        }
        else if (killOnLeftClick && focusState == FocusState.Idle && Input.GetMouseButtonDown(0) && !IsPointerOverUi())
        {
            TryKillThroughMagnifier();
        }

        UpdateFocusTransition();
        UpdateActiveArrows();
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime > 0f ? defaultFixedDeltaTime : 0.02f;
        focusCameraShakeOffset = Vector3.zero;
        isShootingFocusedPerson = false;
        SetFocusButtonsVisible(false);
        SetMagnifierShotVisible(false);
        UnlockMagnifierFollow();
        RestoreAllArrows();
        RestoreFocusImmediately();
    }

    public void ToggleFocusedArrowButton()
    {
        if (focusedPerson == null || focusState == FocusState.Idle)
        {
            return;
        }

        Transform arrow = FindArrow(focusedPerson);

        if (arrow != null && IsArrowOwnerAlive(arrow))
        {
            ToggleArrow(arrow);
        }

        BeginReturnFromFocus();
    }

    public void ExitFocusButton()
    {
        BeginReturnFromFocus();
    }

    public void ShootFocusedButton()
    {
        if (isShootingFocusedPerson || focusedPerson == null || focusState == FocusState.Idle)
        {
            return;
        }

        StartCoroutine(ShootFocusedPersonRoutine());
    }

    private IEnumerator ShootFocusedPersonRoutine()
    {
        isShootingFocusedPerson = true;
        SetFocusButtonsVisible(false);

        Transform target = focusedPerson;
        RandomWanderFloat wander = target != null ? target.GetComponent<RandomWanderFloat>() : null;
        Transform arrow = target != null ? FindArrow(target) : null;

        if (target != null && wander != null && wander.IsAlive)
        {
            GetFocusedShootPoint(target, out Vector3 hitPoint, out Vector3 hitNormal);
            CloseArrow(arrow);
            PlayBloodFx(target, hitPoint, hitNormal);
            wander.Die(GetFocusedShootBackwardDirection(target));
            NotifyKillUi();
            NotifyShot(target);
        }

        float originalTimeScale = Time.timeScale;
        float originalFixedDeltaTime = Time.fixedDeltaTime;
        float duration = Mathf.Max(0.01f, shootSequenceDuration);
        float elapsed = 0f;

        Time.timeScale = Mathf.Clamp(shootSlowMotionScale, 0.01f, 1f);
        Time.fixedDeltaTime = originalFixedDeltaTime * Time.timeScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float intensity = 1f - normalizedTime;
            float wave = Mathf.Sin(elapsed * shootCameraShakeFrequency);
            Vector2 randomCircle = Random.insideUnitCircle * shootCameraShakeStrength * intensity;
            Vector3 cameraRight = focusCamera != null ? focusCamera.transform.right : Vector3.right;
            Vector3 cameraUp = focusCamera != null ? focusCamera.transform.up : Vector3.up;
            focusCameraShakeOffset =
                cameraRight * randomCircle.x +
                cameraUp * (randomCircle.y + wave * shootCameraShakeStrength * 0.35f * intensity);
            yield return null;
        }

        focusCameraShakeOffset = Vector3.zero;
        Time.timeScale = originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;
        isShootingFocusedPerson = false;
        BeginReturnFromFocus();
    }

    private void TryToggleArrowThroughMagnifier()
    {
        if (magnifierCamera == null)
        {
            return;
        }

        Ray ray = magnifierCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!TryGetClickedArrow(ray, out Transform arrow))
        {
            if (closeSingleActiveArrowWhenMiss && TryCloseSingleActiveArrow())
            {
                return;
            }

            return;
        }

        ToggleArrow(arrow);
    }

    private void TryKillThroughMagnifier()
    {
        if (magnifierCamera == null)
        {
            return;
        }

        Ray ray = magnifierCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!TryGetClickedPerson(ray, out RandomWanderFloat wander, out Transform arrow, out RaycastHit hit))
        {
            return;
        }

        CloseArrow(arrow);
        PlayBloodFx(wander.transform, hit.point, hit.normal);
        wander.Die(GetBackwardDirectionFromRay(ray));
        NotifyKillUi();
        NotifyShot(wander.transform);
    }

    private void TryFocusThroughMagnifier()
    {
        if (magnifierCamera == null || focusCamera == null)
        {
            return;
        }

        Ray ray = magnifierCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!TryGetClickedPerson(ray, out RandomWanderFloat wander, out Transform arrow, out _))
        {
            return;
        }

        BeginFocus(wander.transform);
    }

    private bool TryGetClickedArrow(Ray ray, out Transform arrow)
    {
        arrow = null;

        int hitCount = GetHitCount(ray);

        if (hitCount <= 0)
        {
            return false;
        }

        SortHitsByDistance(hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            arrow = FindArrowFromHit(clickHits[i].collider.transform);

            if (arrow != null && IsArrowOwnerAlive(arrow))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetClickedPerson(Ray ray, out RandomWanderFloat wander, out Transform arrow, out RaycastHit hit)
    {
        wander = null;
        arrow = null;
        hit = default;

        int hitCount = GetHitCount(ray);

        if (hitCount <= 0)
        {
            return false;
        }

        SortHitsByDistance(hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            Transform hitTransform = clickHits[i].collider.transform;
            wander = hitTransform.GetComponentInParent<RandomWanderFloat>();

            if (wander == null || wander.IsDead)
            {
                continue;
            }

            arrow = FindArrow(wander.transform);
            hit = clickHits[i];
            return true;
        }

        return false;
    }

    private void BeginFocus(Transform person)
    {
        if (person == null || focusCamera == null)
        {
            return;
        }

        focusedPerson = person;

        if (focusState == FocusState.Idle)
        {
            originalCameraPosition = focusCamera.transform.position;
            originalCameraRotation = focusCamera.transform.rotation;
            originalCameraOrthographicSize = focusCamera.orthographicSize;

            if (cameraChildCharacterRoot != null)
            {
                originalCharacterRootLocalPosition = cameraChildCharacterRoot.localPosition;
            }
        }

        transitionCameraStartPosition = focusCamera.transform.position;
        transitionCameraStartRotation = focusCamera.transform.rotation;
        transitionCameraStartSize = focusCamera.orthographicSize;

        if (cameraChildCharacterRoot != null)
        {
            transitionCharacterRootStartLocalPosition = cameraChildCharacterRoot.localPosition;
        }

        Vector3 focusTargetPosition = person.position;
        focusForwardDistance = Vector3.Dot(focusTargetPosition - focusCamera.transform.position, focusCamera.transform.forward);

        if (focusForwardDistance <= 0.01f)
        {
            focusForwardDistance = 10f;
        }

        focusTransitionTimer = 0f;
        focusCameraShakeOffset = Vector3.zero;
        isShootingFocusedPerson = false;
        SetFocusButtonsVisible(false);
        LockMagnifierFollow(person);
        SetMagnifierShotVisible(true);
        focusState = FocusState.MovingIn;
    }

    private void BeginReturnFromFocus()
    {
        if (focusState == FocusState.Idle || focusCamera == null)
        {
            return;
        }

        if (focusState == FocusState.MovingOut)
        {
            return;
        }

        SetFocusButtonsVisible(false);
        SetMagnifierShotVisible(false);
        UnlockMagnifierFollowTowardMouse();
        transitionCameraStartPosition = focusCamera.transform.position;
        transitionCameraStartRotation = focusCamera.transform.rotation;
        transitionCameraStartSize = focusCamera.orthographicSize;

        if (cameraChildCharacterRoot != null)
        {
            transitionCharacterRootStartLocalPosition = cameraChildCharacterRoot.localPosition;
        }

        focusTransitionTimer = 0f;
        focusCameraShakeOffset = Vector3.zero;
        focusState = FocusState.MovingOut;
    }

    private void UpdateFocusTransition()
    {
        if (focusState == FocusState.Idle || focusCamera == null)
        {
            return;
        }

        if (focusState == FocusState.Focused)
        {
            if (focusedPerson != null)
            {
                UpdateMagnifierHeadLock();
                focusCamera.transform.position = GetFocusedCameraPosition(focusedPerson.position, focusedOrthographicSize) + focusCameraShakeOffset;
            }

            return;
        }

        float duration = GetFocusTransitionTotalDuration();
        focusTransitionTimer += Time.deltaTime;
        float moveT = Mechanical01(GetSegmentT(focusTransitionTimer, 0f, focusMoveDuration));
        float zoomT = Mechanical01(GetSegmentT(focusTransitionTimer, focusZoomDelay, focusZoomDuration));

        if (focusState == FocusState.MovingIn)
        {
            Vector3 targetCameraPosition = focusedPerson != null
                ? GetFocusedCameraPosition(focusedPerson.position, focusedOrthographicSize)
                : transitionCameraStartPosition;

            UpdateMagnifierHeadLock();
            focusCamera.transform.position = Vector3.Lerp(transitionCameraStartPosition, targetCameraPosition, moveT) + focusCameraShakeOffset;
            focusCamera.transform.rotation = Quaternion.Slerp(transitionCameraStartRotation, originalCameraRotation, moveT);
            focusCamera.orthographicSize = Mathf.Lerp(transitionCameraStartSize, focusedOrthographicSize, zoomT);

            if (cameraChildCharacterRoot != null)
            {
                cameraChildCharacterRoot.localPosition = Vector3.Lerp(
                    transitionCharacterRootStartLocalPosition,
                    focusedCharacterRootLocalPosition,
                    moveT
                );
            }

            if (focusTransitionTimer >= duration)
            {
                focusState = FocusState.Focused;
                SetFocusButtonsVisible(true);
            }

            return;
        }

        if (focusState == FocusState.MovingOut)
        {
            focusCamera.transform.position = Vector3.Lerp(transitionCameraStartPosition, originalCameraPosition, moveT);
            focusCamera.transform.rotation = Quaternion.Slerp(transitionCameraStartRotation, originalCameraRotation, moveT);
            focusCamera.orthographicSize = Mathf.Lerp(transitionCameraStartSize, originalCameraOrthographicSize, zoomT);

            if (cameraChildCharacterRoot != null)
            {
                cameraChildCharacterRoot.localPosition = Vector3.Lerp(
                    transitionCharacterRootStartLocalPosition,
                    originalCharacterRootLocalPosition,
                    moveT
                );
            }

            if (focusTransitionTimer >= duration)
            {
                focusedPerson = null;
                focusCameraShakeOffset = Vector3.zero;
                isShootingFocusedPerson = false;
                SetFocusButtonsVisible(false);
                focusState = FocusState.Idle;
            }
        }
    }

    private Vector3 GetFocusedCameraPosition(Vector3 focusTargetPosition, float orthographicSize)
    {
        Vector3 viewportOffset = new Vector3(
            (focusedTargetViewportPoint.x - 0.5f) * 2f * orthographicSize * focusCamera.aspect,
            (focusedTargetViewportPoint.y - 0.5f) * 2f * orthographicSize,
            0f
        );

        return focusTargetPosition
            + focusedCameraWorldOffset
            - focusCamera.transform.right * viewportOffset.x
            - focusCamera.transform.up * viewportOffset.y
            - focusCamera.transform.forward * focusForwardDistance;
    }

    private float GetFocusTransitionTotalDuration()
    {
        return Mathf.Max(0.01f, focusMoveDuration, focusZoomDelay + focusZoomDuration);
    }

    private float GetSegmentT(float elapsed, float delay, float segmentDuration)
    {
        return Mathf.Clamp01((elapsed - delay) / Mathf.Max(0.01f, segmentDuration));
    }

    private float Mechanical01(float value)
    {
        return Mathf.Clamp01(value);
    }

    private void SetFocusButtonsVisible(bool visible)
    {
        if (focusModeButtons == null)
        {
            return;
        }

        for (int i = 0; i < focusModeButtons.Length; i++)
        {
            if (focusModeButtons[i] != null)
            {
                focusModeButtons[i].SetActive(visible);
            }
        }
    }

    private void SetMagnifierShotVisible(bool visible)
    {
        if (magnifierShotObject != null)
        {
            magnifierShotObject.gameObject.SetActive(visible);
        }
    }

    private void NotifyKillUi()
    {
        if (killUiBlinkController != null)
        {
            killUiBlinkController.ConsumeOne();
        }
    }

    private void NotifyShot(Transform target)
    {
        if (gameSessionManager != null)
        {
            gameSessionManager.RegisterShot(target);
        }
    }

    private Transform FindMagnifierShotObject()
    {
        if (!string.IsNullOrWhiteSpace(magnifierShotChildName))
        {
            if (magnifierController != null)
            {
                Transform shot = FindChildRecursive(magnifierController.transform, magnifierShotChildName);

                if (shot != null)
                {
                    return shot;
                }
            }

            GameObject magnifier = GameObject.Find("Magnifier");

            if (magnifier != null)
            {
                Transform shot = FindChildRecursive(magnifier.transform, magnifierShotChildName);

                if (shot != null)
                {
                    return shot;
                }
            }
        }

        return null;
    }

    private bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void LockMagnifierFollow(Transform person)
    {
        if (magnifierController != null)
        {
            magnifierController.LockToWorldPoint(GetMagnifierHeadFocusPoint(person));
        }
    }

    private void UnlockMagnifierFollow()
    {
        if (magnifierController != null)
        {
            magnifierController.UnlockFollow();
        }
    }

    private void UnlockMagnifierFollowAndSnapToMouse()
    {
        if (magnifierController != null)
        {
            magnifierController.UnlockFollowAndSnapToMouse();
        }
    }

    private void UnlockMagnifierFollowTowardMouse()
    {
        if (magnifierController != null)
        {
            magnifierController.UnlockFollowTowardMouse(magnifierReturnToMouseDuration);
        }
    }

    private void UpdateMagnifierHeadLock()
    {
        if (magnifierController != null && focusedPerson != null)
        {
            magnifierController.SetLockedWorldPoint(GetMagnifierHeadFocusPoint(focusedPerson));
        }
    }

    private Vector3 GetMagnifierHeadFocusPoint(Transform person)
    {
        if (person == null)
        {
            return Vector3.zero;
        }

        Transform shotPoint = FindChildRecursive(person, shotPointChildName);

        if (shotPoint != null)
        {
            return shotPoint.position;
        }

        Transform head = FindHeadFocusTransform(person);

        if (head != null)
        {
            return head.position + headFocusOffset;
        }

        Collider[] colliders = person.GetComponentsInChildren<Collider>();

        if (colliders.Length <= 0)
        {
            return person.position + headFocusOffset;
        }

        Bounds bounds = colliders[0].bounds;

        for (int i = 1; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
        }

        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + headFocusOffset;
    }

    private Transform FindHeadFocusTransform(Transform person)
    {
        if (headFocusChildNames == null)
        {
            return null;
        }

        for (int i = 0; i < headFocusChildNames.Length; i++)
        {
            Transform result = FindChildRecursive(person, headFocusChildNames[i]);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void GetFocusedShootPoint(Transform target, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = target.position;
        hitNormal = focusCamera != null ? -focusCamera.transform.forward : Vector3.up;

        Collider[] colliders = target.GetComponentsInChildren<Collider>();

        if (colliders.Length <= 0 || focusCamera == null)
        {
            return;
        }

        Bounds bounds = colliders[0].bounds;

        for (int i = 1; i < colliders.Length; i++)
        {
            bounds.Encapsulate(colliders[i].bounds);
        }

        Vector3 rayOrigin = focusCamera.transform.position;
        Vector3 rayDirection = (bounds.center - rayOrigin).normalized;
        Ray ray = new Ray(rayOrigin, rayDirection);
        float closestDistance = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].Raycast(ray, out RaycastHit hit, maxClickDistance) && hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                hitPoint = hit.point;
                hitNormal = hit.normal;
            }
        }

        if (closestDistance == float.MaxValue)
        {
            hitPoint = bounds.center;
        }
    }

    private Vector3 GetFocusedShootBackwardDirection(Transform target)
    {
        Vector3 direction = focusCamera != null ? focusCamera.transform.forward : target.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = target.forward;
            direction.y = 0f;
        }

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.back;
    }

    private int GetHitCount(Ray ray)
    {
        int hitCount = clickSphereRadius > 0f
            ? Physics.SphereCastNonAlloc(
                ray,
                clickSphereRadius,
                clickHits,
                maxClickDistance,
                selectableMask,
                QueryTriggerInteraction.Collide
            )
            : 0;

        if (hitCount <= 0)
        {
            hitCount = Physics.RaycastNonAlloc(
                ray,
                clickHits,
                maxClickDistance,
                selectableMask,
                QueryTriggerInteraction.Collide
            );
        }

        return hitCount;
    }

    private Transform FindArrowFromHit(Transform hitTransform)
    {
        Transform current = hitTransform;

        while (current != null)
        {
            if (IsArrow(current))
            {
                return current;
            }

            Transform arrow = FindArrow(current);

            if (arrow != null)
            {
                return arrow;
            }

            current = current.parent;
        }

        return null;
    }

    private bool IsArrowOwnerAlive(Transform arrow)
    {
        RandomWanderFloat owner = arrow.GetComponentInParent<RandomWanderFloat>();
        return owner == null || owner.IsAlive;
    }

    private bool IsArrow(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        return target.name == arrowChildName ||
            (!string.IsNullOrWhiteSpace(fallbackArrowChildName) && target.name == fallbackArrowChildName);
    }

    private Transform FindArrow(Transform root)
    {
        Transform arrow = FindChildRecursive(root, arrowChildName);

        if (arrow == null && !string.IsNullOrWhiteSpace(fallbackArrowChildName))
        {
            arrow = FindChildRecursive(root, fallbackArrowChildName);
        }

        return arrow;
    }

    private Transform FindBloodFx(Transform root)
    {
        return FindChildRecursive(root, bloodFxChildName);
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform result = FindChildRecursive(child, childName);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void ToggleArrow(Transform arrow)
    {
        if (activeArrows.TryGetValue(arrow, out ArrowState state))
        {
            CloseArrow(arrow, state);
            return;
        }

        activeArrows.Add(arrow, new ArrowState(arrow));
        arrow.gameObject.SetActive(true);
    }

    private void CloseArrow(Transform arrow)
    {
        if (arrow == null)
        {
            return;
        }

        if (activeArrows.TryGetValue(arrow, out ArrowState state))
        {
            CloseArrow(arrow, state);
            return;
        }

        arrow.gameObject.SetActive(false);
    }

    private void CloseArrow(Transform arrow, ArrowState state)
    {
        RestoreArrow(state);
        activeArrows.Remove(arrow);
    }

    private bool TryCloseSingleActiveArrow()
    {
        if (activeArrows.Count != 1)
        {
            return false;
        }

        Transform arrow = null;
        ArrowState state = null;

        foreach (KeyValuePair<Transform, ArrowState> pair in activeArrows)
        {
            arrow = pair.Key;
            state = pair.Value;
            break;
        }

        if (arrow == null || state == null)
        {
            activeArrows.Clear();
            return false;
        }

        RestoreArrow(state);
        activeArrows.Remove(arrow);
        return true;
    }

    private void SortHitsByDistance(int hitCount)
    {
        for (int i = 0; i < hitCount - 1; i++)
        {
            for (int j = i + 1; j < hitCount; j++)
            {
                if (clickHits[j].distance < clickHits[i].distance)
                {
                    RaycastHit temp = clickHits[i];
                    clickHits[i] = clickHits[j];
                    clickHits[j] = temp;
                }
            }
        }
    }

    private void UpdateActiveArrows()
    {
        foreach (ArrowState state in activeArrows.Values)
        {
            if (state.Arrow == null)
            {
                continue;
            }

            Vector3 position = state.OriginalLocalPosition;
            position.y += Mathf.Sin((Time.time - state.StartTime) * floatSpeed) * floatHeight;
            state.Arrow.localPosition = position;
        }
    }

    private void RestoreAllArrows()
    {
        foreach (ArrowState state in activeArrows.Values)
        {
            RestoreArrow(state);
        }

        activeArrows.Clear();
    }

    private void RestoreArrow(ArrowState state)
    {
        if (state.Arrow == null)
        {
            return;
        }

        state.Arrow.localPosition = state.OriginalLocalPosition;
        state.Arrow.localRotation = state.OriginalLocalRotation;
        state.Arrow.localScale = state.OriginalLocalScale;
        state.Arrow.gameObject.SetActive(false);
    }

    private void OnValidate()
    {
        maxClickDistance = Mathf.Max(0.1f, maxClickDistance);
        clickSphereRadius = Mathf.Max(0f, clickSphereRadius);
        focusMoveDuration = Mathf.Max(0.01f, focusMoveDuration);
        focusZoomDelay = Mathf.Max(0f, focusZoomDelay);
        focusZoomDuration = Mathf.Max(0.01f, focusZoomDuration);
        magnifierReturnToMouseDuration = Mathf.Max(0.01f, magnifierReturnToMouseDuration);
        focusedOrthographicSize = Mathf.Max(0.01f, focusedOrthographicSize);
        shootSequenceDuration = Mathf.Max(0.01f, shootSequenceDuration);
        shootSlowMotionScale = Mathf.Clamp(shootSlowMotionScale, 0.01f, 1f);
        shootCameraShakeStrength = Mathf.Max(0f, shootCameraShakeStrength);
        shootCameraShakeFrequency = Mathf.Max(0f, shootCameraShakeFrequency);
        floatHeight = Mathf.Max(0f, floatHeight);
        floatSpeed = Mathf.Max(0f, floatSpeed);
    }

    private void RestoreFocusImmediately()
    {
        if (focusState == FocusState.Idle || focusCamera == null)
        {
            return;
        }

        focusCamera.transform.position = originalCameraPosition;
        focusCamera.transform.rotation = originalCameraRotation;
        focusCamera.orthographicSize = originalCameraOrthographicSize;

        if (cameraChildCharacterRoot != null)
        {
            cameraChildCharacterRoot.localPosition = originalCharacterRootLocalPosition;
        }

        focusedPerson = null;
        focusCameraShakeOffset = Vector3.zero;
        isShootingFocusedPerson = false;
        SetFocusButtonsVisible(false);
        SetMagnifierShotVisible(false);
        UnlockMagnifierFollowAndSnapToMouse();
        focusState = FocusState.Idle;
    }

    private void PlayBloodFx(Transform personRoot, Vector3 hitPoint, Vector3 hitNormal)
    {
        Transform bloodFx = FindBloodFx(personRoot);

        if (bloodFx == null)
        {
            return;
        }

        if (detachBloodFxOnPlay)
        {
            bloodFx.SetParent(null, true);
        }

        bloodFx.position = hitPoint;

        if (hitNormal.sqrMagnitude > 0.0001f)
        {
            bloodFx.rotation = Quaternion.LookRotation(hitNormal.normalized, Vector3.up);
        }

        bloodFx.gameObject.SetActive(true);

        ParticleSystem[] particleSystems = bloodFx.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        PlayVisualEffectIfPresent(bloodFx);
    }

    private void PlayVisualEffectIfPresent(Transform fxRoot)
    {
        Component[] components = fxRoot.GetComponentsInChildren<Component>(true);

        foreach (Component component in components)
        {
            if (component == null || component.GetType().FullName != "UnityEngine.VFX.VisualEffect")
            {
                continue;
            }

            component.gameObject.SetActive(true);
            component.GetType().GetMethod("Reinit")?.Invoke(component, null);
            component.GetType().GetMethod("Play", System.Type.EmptyTypes)?.Invoke(component, null);
        }
    }

    private Vector3 GetBackwardDirectionFromRay(Ray ray)
    {
        Vector3 direction = ray.direction;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.back;
        }

        return direction.normalized;
    }

    private class ArrowState
    {
        public readonly Transform Arrow;
        public readonly Vector3 OriginalLocalPosition;
        public readonly Quaternion OriginalLocalRotation;
        public readonly Vector3 OriginalLocalScale;
        public readonly float StartTime;

        public ArrowState(Transform arrow)
        {
            Arrow = arrow;
            OriginalLocalPosition = arrow.localPosition;
            OriginalLocalRotation = arrow.localRotation;
            OriginalLocalScale = arrow.localScale;
            StartTime = Time.time;
        }
    }
}
