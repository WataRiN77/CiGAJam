using UnityEngine;

[CreateAssetMenu(menuName = "Customization/Part Parameter")]
public class PartParameter : CustomizationParameter
{
    [Tooltip("所有可选 Sprite")]
    public Sprite[] sprites;

    [Tooltip("目标骨骼路径（可多个，如 Eye_L,Eye_R）")]
    public string[] targetBoneNames;

    [Header("随机范围覆盖（不勾选则使用全局设置）")]
    [SerializeField] private bool overrideRandomRange = false;
    [SerializeField] private float randomPosRange = 0.05f;
    [SerializeField] private float randomRotationRange = 15f;
    [SerializeField] private float randomScaleMin = 0.8f;
    [SerializeField] private float randomScaleMax = 1.2f;

    // 公共属性，方便外部获取
    public bool OverrideRandomRange => overrideRandomRange;
    public float RandomPosRange => randomPosRange;
    public float RandomRotationRange => randomRotationRange;
    public float RandomScaleMin => randomScaleMin;
    public float RandomScaleMax => randomScaleMax;

    private void OnValidate()
    {
        type = ParameterType.Part;
    }
}