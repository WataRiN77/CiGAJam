using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MenuButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("高亮目标")]
    [SerializeField] private TMP_Text descText;             // 下方的说明文本
    [SerializeField] private float hoverScale = 1.05f;      // 悬浮放大倍数
    [SerializeField] private float transitionSpeed = 8f;    // 渐变过渡速度

    private Vector3 originalScale;
    private Color originalColor;
    private Vector3 targetScale;
    private Color targetColor;


    private void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;

        if (descText != null)
        {
            originalColor = descText.color; // 默认为面板里调好的灰色
            targetColor = originalColor;
        }
    }

    private void Update()
    {
        // 平滑过渡大小和颜色
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * transitionSpeed);

        if (descText != null)
        {
            descText.color = Color.Lerp(descText.color, targetColor, Time.deltaTime * transitionSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * hoverScale;
        targetColor = Color.white; // 悬浮变纯白
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
        targetColor = originalColor; // 恢复灰色
    }
}