using System.Collections;
using UnityEngine;

public class KillUiBlinkController : MonoBehaviour
{
    [Header("Indicators")]
    [SerializeField] private GameObject[] indicators;
    [SerializeField] private bool resetIndicatorsOnEnable = true;

    [Header("Blink")]
    [SerializeField] private float blinkDuration = 0.45f;
    [SerializeField] private float blinkInterval = 0.08f;

    private int nextIndicatorIndex;

    private void OnEnable()
    {
        if (resetIndicatorsOnEnable)
        {
            ResetIndicators();
        }
    }

    public void ConsumeOne()
    {
        GameObject indicator = GetNextAvailableIndicator();

        if (indicator == null)
        {
            return;
        }

        StartCoroutine(BlinkThenDisable(indicator));
    }

    public void ResetIndicators()
    {
        nextIndicatorIndex = 0;

        if (indicators == null)
        {
            return;
        }

        for (int i = 0; i < indicators.Length; i++)
        {
            if (indicators[i] != null)
            {
                indicators[i].SetActive(true);
            }
        }
    }

    private GameObject GetNextAvailableIndicator()
    {
        if (indicators == null)
        {
            return null;
        }

        while (nextIndicatorIndex < indicators.Length)
        {
            GameObject indicator = indicators[nextIndicatorIndex];
            nextIndicatorIndex++;

            if (indicator != null && indicator.activeSelf)
            {
                return indicator;
            }
        }

        return null;
    }

    private IEnumerator BlinkThenDisable(GameObject indicator)
    {
        float duration = Mathf.Max(0.01f, blinkDuration);
        float interval = Mathf.Max(0.01f, blinkInterval);
        float elapsed = 0f;
        bool visible = true;

        indicator.SetActive(true);

        while (elapsed < duration)
        {
            visible = !visible;
            indicator.SetActive(visible);
            yield return new WaitForSecondsRealtime(interval);
            elapsed += interval;
        }

        indicator.SetActive(false);
    }

    private void OnValidate()
    {
        blinkDuration = Mathf.Max(0.01f, blinkDuration);
        blinkInterval = Mathf.Max(0.01f, blinkInterval);
    }
}
