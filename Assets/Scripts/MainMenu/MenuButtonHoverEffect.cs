using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MenuButtonHoverEffect : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler // 🌟 新增按下和抬起接口
{
    [Header("高亮目标")]
    [SerializeField] private TMP_Text descText;             // 下方的说明文本
    [SerializeField] private float hoverScale = 1.05f;      // 悬浮放大倍数 (轻微放大)
    [SerializeField] private float pressScale = 0.95f;      // 按下缩小倍数 (实现下陷回弹感)
    [SerializeField] private float transitionSpeed = 12f;   // 渐变过渡速度 (调大一点，反馈更灵敏)

    private Vector3 originalScale;
    private Color originalColor;
    private Vector3 targetScale;
    private Color targetColor;
    private bool isHovering = false; // 记录当前鼠标是否悬浮在按钮上

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

    // ==========================================
    // 1. 鼠标悬浮与移开
    // ==========================================
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        targetScale = originalScale * hoverScale;
        targetColor = Color.white; // 悬浮变纯白
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        targetScale = originalScale;
        targetColor = originalColor; // 恢复灰色
    }

    // ==========================================
    // 2. 新增：鼠标点击与松开（回弹效果）
    // ==========================================
    public void OnPointerDown(PointerEventData eventData)
    {
        // 鼠标按下：按钮瞬间向内下陷缩小
        targetScale = originalScale * pressScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 鼠标松开（抬起）：根据当前鼠标是否还在按钮内，决定回弹到放大状态还是初始状态
        if (isHovering)
        {
            targetScale = originalScale * hoverScale; // 回弹到悬浮大小 (Q弹)
        }
        else
        {
            targetScale = originalScale; // 回弹到正常大小
        }
    }
}