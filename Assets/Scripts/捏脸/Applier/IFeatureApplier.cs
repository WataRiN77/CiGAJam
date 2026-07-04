using UnityEngine;

public interface IFeatureApplier
{
    void Apply(GameObject target, CustomizationParameter param, object value);
}