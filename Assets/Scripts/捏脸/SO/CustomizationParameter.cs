using UnityEngine;

public abstract class CustomizationParameter : ScriptableObject
{
    public string parameterId;
    public string displayName;
    public ParameterType type;
}