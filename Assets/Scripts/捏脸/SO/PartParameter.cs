using UnityEngine;

[CreateAssetMenu(menuName = "Customization/Part Parameter")]
public class PartParameter : CustomizationParameter
{
    [Tooltip("所有可选 Sprite，按顺序对应 UI 选择器的索引")]
    public Sprite[] sprites;

    [Tooltip("目标骨骼名称（可多个，如：Eye_L,Eye_R 或单独 Nose）")]
    public string[] targetBoneNames;

    private void OnValidate()
    {
        type = ParameterType.Part;
    }
}