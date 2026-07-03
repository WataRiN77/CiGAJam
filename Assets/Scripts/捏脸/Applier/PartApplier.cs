using UnityEngine;

public class PartApplier : IFeatureApplier
{
    public void Apply(GameObject target, CustomizationParameter param, object value)
    {
        PartParameter partParam = param as PartParameter;
        if (partParam == null) return;

        int index = (int)value;
        if (index < 0 || index >= partParam.sprites.Length) return;

        foreach (string boneName in partParam.targetBoneNames)
        {
            if (string.IsNullOrWhiteSpace(boneName)) continue;
            Transform bone = target.transform.Find(boneName.Trim());
            if (bone == null)
            {
                Debug.LogWarning($"Bone not found: {boneName}");
                continue;
            }

            SpriteRenderer sr = bone.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sprite = partParam.sprites[index];
        }
    }
}