using UnityEngine;

public class RandomWanderFloat : MonoBehaviour
{
    private static event System.Action<RandomWanderFloat, Vector3> AnyNpcDied;

    public enum LifeState
    {
        Alive,
        Dead
    }

    [Header("Move Range")]
    [SerializeField] private Vector3 rangeCenterOffset = Vector3.zero;
    [SerializeField] private Vector3 moveRange = new Vector3(5f, 0f, 5f);

    [Header("Scene Gizmos")]
    [SerializeField] private bool alwaysShowMoveRange = true;
    [SerializeField] private Color rangeColor = Color.cyan;
    [SerializeField] private bool showTargetPoint = true;

    [Header("Random Control")]
    [SerializeField] private bool useFixedSeed = false;
    [SerializeField] private int fixedSeed = 12345;

    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private Vector2 moveTimeRange = new Vector2(2f, 5f);
    [SerializeField] private Vector2 stopTimeRange = new Vector2(1f, 3f);
    [SerializeField] private float arriveDistance = 0.08f;

    [Header("Transition Settings")]
    [SerializeField] private float accelerationTime = 0.6f;
    [SerializeField] private float decelerationTime = 0.8f;
    [SerializeField] private float floatTransitionSharpness = 6f;

    [Header("Floating While Moving")]
    [SerializeField] private float moveFloatHeight = 0.3f;
    [SerializeField] private float moveFloatSpeed = 3f;

    [Header("Rotation Shake While Moving")]
    [SerializeField] private float moveRotationYAmount = 8f;
    [SerializeField] private float moveRotationZAmount = 5f;
    [SerializeField] private float moveRotationSpeed = 4f;

    [Header("Rotation Shake While Stopped")]
    [SerializeField] private float stopRotationYAmount = 2f;
    [SerializeField] private float stopRotationZAmount = 1.2f;
    [SerializeField] private float stopRotationSpeed = 2f;

    [Header("Life State")]
    [SerializeField] private LifeState lifeState = LifeState.Alive;
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private float deathBackwardForce = 6f;
    [SerializeField] private float deathUpwardForce = 1.5f;
    [SerializeField] private ForceMode deathForceMode = ForceMode.Impulse;

    [Header("Flee From Death")]
    [SerializeField] private bool fleeWhenNearbyDeath = true;
    [SerializeField] private float deathAlertRadius = 7f;
    [SerializeField] private float fleeDistance = 4f;
    [SerializeField] private float fleeDuration = 1.5f;
    [SerializeField] private float fleeSpeedMultiplier = 2.6f;

    private Vector3 startPosition;
    private Vector3 rangeCenter;
    private Vector3 targetPosition;
    private Quaternion startRotation;
    private float baseY;
    private float stateTimer;
    private float moveBlend;
    private float floatOffsetY;
    private float rotationPhaseY;
    private float rotationPhaseZ;
    private bool isMoving;
    private bool isFleeing;
    private System.Random random;

    public LifeState CurrentLifeState => lifeState;
    public bool IsAlive => lifeState == LifeState.Alive;
    public bool IsDead => lifeState == LifeState.Dead;

    private void OnEnable()
    {
        AnyNpcDied += HandleAnyNpcDied;
    }

    private void OnDisable()
    {
        AnyNpcDied -= HandleAnyNpcDied;
    }

    public void SetFixedSeed(int seed, bool reinitializeIfPlaying = false)
    {
        useFixedSeed = true;
        fixedSeed = seed;

        if (Application.isPlaying && reinitializeIfPlaying && random != null)
        {
            InitializeRandom();
        }
    }

    private void Start()
    {
        startPosition = transform.position;
        rangeCenter = startPosition + rangeCenterOffset;
        baseY = startPosition.y;
        startRotation = transform.rotation;

        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }

        if (targetRigidbody != null && lifeState == LifeState.Alive)
        {
            targetRigidbody.velocity = Vector3.zero;
            targetRigidbody.angularVelocity = Vector3.zero;
            targetRigidbody.isKinematic = true;
            targetRigidbody.useGravity = false;
        }

        InitializeRandom();

        BeginMove();
    }

    private void Update()
    {
        if (lifeState == LifeState.Dead)
        {
            return;
        }

        stateTimer -= Time.deltaTime;
        UpdateMoveBlend();

        if (isMoving)
        {
            MoveToTarget();

            if (stateTimer <= 0f || IsArrived())
            {
                BeginStop();
            }
        }
        else
        {
            if (moveBlend > 0.001f)
            {
                MoveToTarget();
            }

            if (stateTimer <= 0f)
            {
                BeginMove();
            }
        }

        ApplyFloatHeight();
        ApplyRotationShake();
    }

    private void BeginMove()
    {
        if (lifeState == LifeState.Dead)
        {
            return;
        }

        isFleeing = false;
        isMoving = true;
        stateTimer = GetRandomRange(moveTimeRange);
        targetPosition = GetRandomPointInRange();
    }

    private void BeginStop()
    {
        isFleeing = false;
        isMoving = false;
        stateTimer = GetRandomRange(stopTimeRange);
    }

    public void Die(Vector3 backwardDirection)
    {
        if (lifeState == LifeState.Dead)
        {
            return;
        }

        lifeState = LifeState.Dead;
        isMoving = false;
        isFleeing = false;
        moveBlend = 0f;
        AnyNpcDied?.Invoke(this, transform.position);

        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }

        if (targetRigidbody == null)
        {
            enabled = false;
            return;
        }

        Vector3 forceDirection = backwardDirection;
        forceDirection.y = 0f;

        if (forceDirection.sqrMagnitude < 0.0001f)
        {
            forceDirection = -transform.forward;
        }

        forceDirection.Normalize();

        targetRigidbody.isKinematic = false;
        targetRigidbody.useGravity = true;
        targetRigidbody.velocity = Vector3.zero;
        targetRigidbody.angularVelocity = Vector3.zero;
        targetRigidbody.AddForce(forceDirection * deathBackwardForce + Vector3.up * deathUpwardForce, deathForceMode);
    }

    private void MoveToTarget()
    {
        Vector3 current = transform.position;
        Vector3 target = targetPosition;
        current.y = baseY;
        target.y = baseY;

        float currentMoveSpeed = moveSpeed * (isFleeing ? fleeSpeedMultiplier : 1f);
        Vector3 next = Vector3.MoveTowards(current, target, currentMoveSpeed * moveBlend * Time.deltaTime);
        transform.position = new Vector3(next.x, transform.position.y, next.z);
    }

    private void HandleAnyNpcDied(RandomWanderFloat deadNpc, Vector3 deathPosition)
    {
        if (!fleeWhenNearbyDeath || deadNpc == this || lifeState == LifeState.Dead)
        {
            return;
        }

        Vector3 current = transform.position;
        Vector3 planarDeathPosition = deathPosition;
        current.y = 0f;
        planarDeathPosition.y = 0f;

        float distance = Vector3.Distance(current, planarDeathPosition);

        if (distance > deathAlertRadius)
        {
            return;
        }

        Vector3 fleeDirection = current - planarDeathPosition;

        if (fleeDirection.sqrMagnitude < 0.0001f)
        {
            float angle = random != null ? GetRandomRange(0f, Mathf.PI * 2f) : Random.Range(0f, Mathf.PI * 2f);
            fleeDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
        else
        {
            fleeDirection.Normalize();
        }

        Vector3 fleeTarget = transform.position + fleeDirection * fleeDistance;
        targetPosition = ClampPointToMoveRange(fleeTarget);
        targetPosition.y = baseY;
        isFleeing = true;
        isMoving = true;
        stateTimer = Mathf.Max(0.1f, fleeDuration);
    }

    private void UpdateMoveBlend()
    {
        float targetBlend = isMoving ? 1f : 0f;
        float transitionTime = targetBlend > moveBlend ? accelerationTime : decelerationTime;

        if (transitionTime <= 0f)
        {
            moveBlend = targetBlend;
            return;
        }

        moveBlend = Mathf.MoveTowards(moveBlend, targetBlend, Time.deltaTime / transitionTime);
    }

    private void ApplyFloatHeight()
    {
        float targetOffsetY = Mathf.Sin(Time.time * moveFloatSpeed) * moveFloatHeight * moveBlend;
        float smoothFactor = 1f - Mathf.Exp(-floatTransitionSharpness * Time.deltaTime);
        floatOffsetY = Mathf.Lerp(floatOffsetY, targetOffsetY, smoothFactor);

        Vector3 position = transform.position;
        position.y = baseY + floatOffsetY;
        transform.position = position;
    }

    private void ApplyRotationShake()
    {
        float yAmount = Mathf.Lerp(stopRotationYAmount, moveRotationYAmount, moveBlend);
        float zAmount = Mathf.Lerp(stopRotationZAmount, moveRotationZAmount, moveBlend);
        float speed = Mathf.Lerp(stopRotationSpeed, moveRotationSpeed, moveBlend);

        rotationPhaseY += speed * Time.deltaTime;
        rotationPhaseZ += speed * 1.37f * Time.deltaTime;

        float y = Mathf.Sin(rotationPhaseY) * yAmount;
        float z = Mathf.Sin(rotationPhaseZ) * zAmount;
        transform.rotation = startRotation * Quaternion.Euler(0f, y, z);
    }

    private bool IsArrived()
    {
        Vector3 current = transform.position;
        Vector3 target = targetPosition;
        current.y = baseY;
        target.y = baseY;

        return Vector3.Distance(current, target) <= arriveDistance;
    }

    private Vector3 GetRandomPointInRange()
    {
        float halfX = Mathf.Abs(moveRange.x) * 0.5f;
        float halfZ = Mathf.Abs(moveRange.z) * 0.5f;

        return rangeCenter + new Vector3(
            GetRandomRange(-halfX, halfX),
            0f,
            GetRandomRange(-halfZ, halfZ)
        );
    }

    private Vector3 ClampPointToMoveRange(Vector3 point)
    {
        float halfX = Mathf.Abs(moveRange.x) * 0.5f;
        float halfZ = Mathf.Abs(moveRange.z) * 0.5f;
        Vector3 clamped = point;
        clamped.x = Mathf.Clamp(clamped.x, rangeCenter.x - halfX, rangeCenter.x + halfX);
        clamped.z = Mathf.Clamp(clamped.z, rangeCenter.z - halfZ, rangeCenter.z + halfZ);
        return clamped;
    }

    private void InitializeRandom()
    {
        int seed = useFixedSeed ? fixedSeed : Random.Range(0, int.MaxValue);
        random = new System.Random(seed);
    }

    private float GetRandomRange(Vector2 range)
    {
        return GetRandomRange(range.x, range.y);
    }

    private float GetRandomRange(float min, float max)
    {
        if (min > max)
        {
            float temp = min;
            min = max;
            max = temp;
        }

        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private void OnDrawGizmos()
    {
        if (!alwaysShowMoveRange)
        {
            return;
        }

        DrawMoveRangeGizmos(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawMoveRangeGizmos(true);
    }

    private void DrawMoveRangeGizmos(bool isSelected)
    {
        Vector3 center = Application.isPlaying ? rangeCenter : transform.position + rangeCenterOffset;
        Vector3 size = new Vector3(Mathf.Abs(moveRange.x), 0.05f, Mathf.Abs(moveRange.z));

        Color fillColor = rangeColor;
        fillColor.a = isSelected ? 0.18f : 0.08f;

        Gizmos.color = fillColor;
        Gizmos.DrawCube(center, size);

        Color wireColor = rangeColor;
        wireColor.a = isSelected ? 1f : 0.45f;

        Gizmos.color = wireColor;
        Gizmos.DrawWireCube(center, size);

        if (Application.isPlaying && showTargetPoint)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(targetPosition, isSelected ? 0.16f : 0.1f);
            Gizmos.DrawLine(transform.position, targetPosition);
        }
    }

    private void OnValidate()
    {
        deathBackwardForce = Mathf.Max(0f, deathBackwardForce);
        deathUpwardForce = Mathf.Max(0f, deathUpwardForce);
        deathAlertRadius = Mathf.Max(0f, deathAlertRadius);
        fleeDistance = Mathf.Max(0f, fleeDistance);
        fleeDuration = Mathf.Max(0.1f, fleeDuration);
        fleeSpeedMultiplier = Mathf.Max(1f, fleeSpeedMultiplier);
    }
}
