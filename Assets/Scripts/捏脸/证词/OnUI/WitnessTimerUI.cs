using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WitnessTimerUI : MonoBehaviour
{
    [Header("进度条")]
    [SerializeField] private Slider progressSlider;        // 进度条 0~1
    [SerializeField] private TMP_Text timeText;            // 可选：显示剩余秒数

    private FaceCustomizationGameManager gameManager;
    private float totalInterval;

    private void Start()
    {
        gameManager = FaceCustomizationGameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogWarning("FaceCustomizationGameManager 未找到");
            return;
        }

        totalInterval = gameManager.StatementInterval;

        gameManager.OnTimerProgress += UpdateProgress;
        UpdateProgress(0f);
    }

    private void OnDestroy()
    {
        if (gameManager != null)
            gameManager.OnTimerProgress -= UpdateProgress;
    }

    private void UpdateProgress(float progress)
    {
        if (progressSlider != null)
            progressSlider.value = progress;

        if (timeText != null)
        {
            float remaining = (1f - progress) * totalInterval;
            timeText.text = $"下一条证词: {remaining:F1}秒";
        }
    }
}
