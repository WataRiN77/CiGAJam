using UnityEngine;

[CreateAssetMenu(menuName = "Customization/Float Range Parameter")]
public class FloatRangeParameter : CustomizationParameter
{
    public float minValue;
    public float maxValue;
    public float defaultValue;

    [Tooltip("如何应用这个浮点数到 Transform")]
    public FloatAffectMode affectMode;

    [Tooltip("目标骨骼名称（可多个）")]
    public string[] targetBoneNames;

    private void OnValidate()
    {
        type = ParameterType.FloatRange;
    }
}