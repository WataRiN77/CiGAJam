using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Customization/Character Customization Data 2D")]
public class CharacterCustomizationData2D : ScriptableObject
{
    [Tooltip("按配置顺序排列，UI 会依此生成控件")]
    public List<CustomizationParameter> parameters;
}