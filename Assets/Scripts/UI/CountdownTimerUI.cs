using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CountdownTimerUI : MonoBehaviour
{
    [Header("Time")]
    [SerializeField] private float durationSeconds = 60f;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool useUnscaledTime;

    [Header("Display")]
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private Text legacyText;
    [SerializeField] private string numberFormat = "0.00";
    [SerializeField] private string suffix = "s";

    [Header("Shake")]
    [SerializeField] private RectTransform shakeTarget;
    [SerializeField] private float baseShakeStrength = 2f;
    [SerializeField] private float finalTenSecondsShakeStrength = 24f;
    [SerializeField] private float shakeFrequency = 45f;

    public event Action CountdownFinished;

    private Vector2 originalAnchoredPosition;
    private float remainingSeconds;
    private bool isRunning;
    private bool hasFinished;

    public float RemainingSeconds => remainingSeconds;
    public float ElapsedSeconds => Mathf.Max(0f, durationSeconds - remainingSeconds);
    public bool IsRunning => isRunning;
    public bool HasFinished => hasFinished;

    private void Awake()
    {
        if (shakeTarget == null)
        {
            shakeTarget = transform as RectTransform;
        }

        if (tmpText == null)
        {
            tmpText = GetComponent<TMP_Text>();
        }

        if (legacyText == null)
        {
            legacyText = GetComponent<Text>();
        }

        if (shakeTarget != null)
        {
            originalAnchoredPosition = shakeTarget.anchoredPosition;
        }

        ResetTimer();
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            StartCountdown();
        }
    }

    private void OnDisable()
    {
        RestoreShakePosition();
    }

    private void Update()
    {
        if (!isRunning || hasFinished)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        remainingSeconds = Mathf.Max(0f, remainingSeconds - deltaTime);

        UpdateText();
        UpdateShake();

        if (remainingSeconds <= 0f)
        {
            FinishCountdown();
        }
    }

    public void StartCountdown()
    {
        isRunning = true;
        hasFinished = false;
        UpdateText();
    }

    public void StopCountdown()
    {
        isRunning = false;
        RestoreShakePosition();
    }

    public void ResetTimer()
    {
        remainingSeconds = Mathf.Max(0f, durationSeconds);
        isRunning = false;
        hasFinished = false;
        RestoreShakePosition();
        UpdateText();
    }

    public void SetDuration(float seconds, bool resetTimer)
    {
        durationSeconds = Mathf.Max(0f, seconds);

        if (resetTimer)
        {
            ResetTimer();
        }
    }

    private void FinishCountdown()
    {
        isRunning = false;
        hasFinished = true;
        RestoreShakePosition();
        UpdateText();
        CountdownFinished?.Invoke();
    }

    private void UpdateText()
    {
        string text = remainingSeconds.ToString(numberFormat) + suffix;

        if (tmpText != null)
        {
            tmpText.text = text;
        }

        if (legacyText != null)
        {
            legacyText.text = text;
        }
    }

    private void UpdateShake()
    {
        if (shakeTarget == null || durationSeconds <= 0f)
        {
            return;
        }

        float progress = 1f - Mathf.Clamp01(remainingSeconds / durationSeconds);
        float strength = Mathf.Lerp(baseShakeStrength, finalTenSecondsShakeStrength, progress * progress);

        if (remainingSeconds <= 10f)
        {
            float finalTenProgress = 1f - Mathf.Clamp01(remainingSeconds / 10f);
            strength = Mathf.Lerp(strength, finalTenSecondsShakeStrength, finalTenProgress);
        }

        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        Vector2 shake = UnityEngine.Random.insideUnitCircle * strength;
        shake += new Vector2(
            Mathf.Sin(time * shakeFrequency),
            Mathf.Cos(time * shakeFrequency * 1.37f)
        ) * strength * 0.35f;

        Vector3 anchoredPosition = shakeTarget.anchoredPosition3D;
        anchoredPosition.x = originalAnchoredPosition.x;
        anchoredPosition.y = originalAnchoredPosition.y;
        anchoredPosition.x += shake.x;
        anchoredPosition.y += shake.y;
        shakeTarget.anchoredPosition3D = anchoredPosition;
    }

    private void RestoreShakePosition()
    {
        if (shakeTarget != null)
        {
            Vector3 anchoredPosition = shakeTarget.anchoredPosition3D;
            anchoredPosition.x = originalAnchoredPosition.x;
            anchoredPosition.y = originalAnchoredPosition.y;
            shakeTarget.anchoredPosition3D = anchoredPosition;
        }
    }

    private void OnValidate()
    {
        durationSeconds = Mathf.Max(0f, durationSeconds);
        baseShakeStrength = Mathf.Max(0f, baseShakeStrength);
        finalTenSecondsShakeStrength = Mathf.Max(0f, finalTenSecondsShakeStrength);
        shakeFrequency = Mathf.Max(0f, shakeFrequency);
    }
}
