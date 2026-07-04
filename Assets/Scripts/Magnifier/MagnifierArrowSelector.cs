using System.Collections.Generic;
using UnityEngine;

public class MagnifierArrowSelector : MonoBehaviour
{
    [Header("Click Camera")]
    [SerializeField] private Camera magnifierCamera;
    [SerializeField] private LayerMask selectableMask = ~0;
    [SerializeField] private float maxClickDistance = 100f;
    [SerializeField] private float clickSphereRadius = 0.35f;
    [SerializeField] private bool closeSingleActiveArrowWhenMiss;

    [Header("Left Click Kill")]
    [SerializeField] private bool killOnLeftClick = true;

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

    private void Start()
    {
        if (magnifierCamera == null)
        {
            Debug.LogWarning($"{nameof(MagnifierArrowSelector)} needs a Magnifier Camera reference.", this);
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            TryToggleArrowThroughMagnifier();
        }

        if (killOnLeftClick && Input.GetMouseButtonDown(0))
        {
            TryKillThroughMagnifier();
        }

        UpdateActiveArrows();
    }

    private void OnDisable()
    {
        RestoreAllArrows();
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
        floatHeight = Mathf.Max(0f, floatHeight);
        floatSpeed = Mathf.Max(0f, floatSpeed);
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
