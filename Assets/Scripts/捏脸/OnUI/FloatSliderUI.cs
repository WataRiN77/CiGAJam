//using UnityEngine;
//using UnityEngine.UI;
//public class FloatSliderUI : MonoBehaviour
//{
//    [Header("UI 控件")]
//    [SerializeField] private Slider slider;
//    [SerializeField] private Text  valueText;

//    [Header("参数配置")]
//    [SerializeField] private string parameterId = "eyebrow_spacing";
//    [SerializeField] private CharacterCustomizer2D customizer;

//    // 这两个字段现在变为“后备值”，仅当 dataAsset 中找不到参数时使用
//    [Header("后备范围（当 dataAsset 无对应参数时生效）")]
//    [SerializeField] private float fallbackMin = 0f;
//    [SerializeField] private float fallbackMax = 1f;

//    private FloatRangeParameter cachedParam;

//    private void Start()
//    {
//        if (customizer == null)
//            customizer = FindObjectOfType<CharacterCustomizer2D>();

//        // 尝试从 dataAsset 获取参数范围
//        float min = fallbackMin;
//        float max = fallbackMax;
//        float def = 0f;

//        if (customizer != null)
//        {
//            var dataAsset = customizer.GetDataAsset();
//            if (dataAsset != null)
//            {
//                var param = dataAsset.parameters.Find(p => p.parameterId == parameterId);
//                if (param is FloatRangeParameter floatParam)
//                {
//                    cachedParam = floatParam;
//                    min = floatParam.minValue;
//                    max = floatParam.maxValue;
//                    def = floatParam.defaultValue;
//                }
//                else
//                {
//                    Debug.LogWarning($"FloatSliderUI: 参数 '{parameterId}' 未找到或不是 FloatRangeParameter，将使用后备范围");
//                }
//            }
//        }

//        // 配置滑块范围
//        slider.minValue = min;
//        slider.maxValue = max;
//        slider.wholeNumbers = false;

//        // 同步初始值
//        if (customizer != null && customizer.GetCurrentValues().TryGetValue(parameterId, out object val))
//        {
//            slider.value = (float)val;
//        }
//        else
//        {
//            slider.value = def;
//        }

//        UpdateValueText(slider.value);
//        slider.onValueChanged.AddListener(OnSliderChanged);
//    }

//    private void OnSliderChanged(float value)
//    {
//        customizer?.SetParameter(parameterId, value);
//        UpdateValueText(value);
//    }

//    private void UpdateValueText(float value)
//    {
//        if (valueText != null)
//            valueText.text = value.ToString("F2");
//    }

//    /// <summary>
//    /// 外部修改参数时同步滑块（不触发回调）
//    /// </summary>
//    public void SetValueWithoutNotify(float value)
//    {
//        slider.SetValueWithoutNotify(value);
//        UpdateValueText(value);
//    }
//}