using UnityEngine;
using UnityEngine.UI;

public class PartSelectorUI : MonoBehaviour
{
    [Header("UI 控件")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Text indexText;

    [Header("参数配置")]
    [SerializeField] private string parameterId = "hair_style";
    [SerializeField] private CharacterCustomizer2D customizer;

    private int currentIndex = 0;
    private int totalCount = 0;

    private void Start()
    {
        if (customizer == null)
            customizer = FindObjectOfType<CharacterCustomizer2D>();

        // 获取总选项数
        totalCount = GetPartCount();
        if (totalCount == 0)
        {
            Debug.LogError($"参数 {parameterId} 不是有效的 Part 参数，或无 Sprite");
            return;
        }

        // 从当前状态读取初始索引
        var indexes = customizer.GetPartIndexes();
        if (indexes.TryGetValue(parameterId, out int savedIndex))
            currentIndex = savedIndex;
        else
            currentIndex = 0;

        UpdateUI();

        prevButton.onClick.AddListener(OnPrev);
        nextButton.onClick.AddListener(OnNext);
    }

    private int GetPartCount()
    {
        var dataAsset = customizer.GetDataAsset();
        if (dataAsset == null) return 0;
        var param = dataAsset.parameters.Find(p => p.parameterId == parameterId);
        if (param is PartParameter partParam)
            return partParam.sprites.Length;
        return 0;
    }

    public void OnPrev()
    {
        if (totalCount == 0) return;
        currentIndex = (currentIndex - 1 + totalCount) % totalCount;
        Apply();
    }

    public void OnNext()
    {
        if (totalCount == 0) return;
        currentIndex = (currentIndex + 1) % totalCount;
        Apply();
    }

    private void Apply()
    {
        customizer.SetPart(parameterId, currentIndex);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (indexText != null)
            indexText.text = $"{currentIndex + 1}/{totalCount}";
    }
}