using UnityEngine;
using UnityEngine.UI;

public class WitnessTimerUI : MonoBehaviour
{
    [Header("进度条")]
    [SerializeField] private Slider progressSlider;        // 进度条 0~1
    [SerializeField] private Text timeText;                // 可选：显示剩余秒数

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

        // 从 Manager 获取间隔（需要在 Inspector 中保持 public 或添加属性，如果 statementInterval 是 private serialized，可通过反射或改成 public）
        // 简便方法：在 FaceCustomizationGameManager 中添加一个公共方法或属性获取间隔
        // 我们这里假设已经添加了 public float StatementInterval => statementInterval;
        totalInterval = gameManager.StatementInterval; // 需要在 Manager 中添加此属性

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

        //// 全部证词生成完毕时可隐藏进度条
        //if (progress >= 1f)
        //{
        //    gameObject.SetActive(false);   // 或禁用 Slider
        //}
    }
}