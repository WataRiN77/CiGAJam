using UnityEngine;

public class DiagonalGridScroller : MonoBehaviour
{
    [Header("Position")]
    [SerializeField] private bool useLocalPosition = true;
    [SerializeField] private bool useCurrentPositionOnAwake = true;
    [SerializeField] private Vector3 initialPosition;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 20f;
    [SerializeField, Min(0.01f)] private float loopDuration = 8f;

    private static readonly Vector3 MoveDirection = new Vector3(1f, -1f, 0f).normalized;
    private float elapsedTime;

    private void Awake()
    {
        if (useCurrentPositionOnAwake)
            initialPosition = GetPosition();

        SetPosition(initialPosition);
    }

    private void OnEnable()
    {
        elapsedTime = 0f;
        SetPosition(initialPosition);
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        float loopTime = Mathf.Max(0.01f, loopDuration);
        float currentLoopTime = Mathf.Repeat(elapsedTime, loopTime);
        Vector3 loopOffset = MoveDirection * moveSpeed * currentLoopTime;

        SetPosition(initialPosition + loopOffset);
    }

    public void SetInitialPosition(Vector3 position)
    {
        initialPosition = position;
        elapsedTime = 0f;
        SetPosition(initialPosition);
    }

    private Vector3 GetPosition()
    {
        return useLocalPosition ? transform.localPosition : transform.position;
    }

    private void SetPosition(Vector3 position)
    {
        if (useLocalPosition)
            transform.localPosition = position;
        else
            transform.position = position;
    }
}
