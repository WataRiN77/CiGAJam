using UnityEngine;

public class TransformApplier : IFeatureApplier
{
    public void Apply(GameObject target, CustomizationParameter param, object value)
    {
        FloatRangeParameter floatParam = param as FloatRangeParameter;
        if (floatParam == null) return;

        float val = (float)value;
        string[] names = floatParam.targetBoneNames;

        for (int i = 0; i < names.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(names[i])) continue;
            Transform bone = target.transform.Find(names[i].Trim());
            if (bone == null) continue;

            switch (floatParam.affectMode)
            {
                case FloatAffectMode.PositionY:
                    Vector3 pos = bone.localPosition;
                    pos.y = val;
                    bone.localPosition = pos;
                    break;

                case FloatAffectMode.SymmetricX:
                    float sign = (i == 0) ? -1f : 1f; // 假设第一个是 L
                    Vector3 posX = bone.localPosition;
                    posX.x = val * sign;
                    bone.localPosition = posX;
                    break;

                case FloatAffectMode.ScaleUniform:
                    bone.localScale = Vector3.one * val;
                    break;

                case FloatAffectMode.RotationZ_Symmetric:
                    float rotSign = (i == 0) ? -1f : 1f;
                    bone.localEulerAngles = new Vector3(0, 0, val * rotSign);
                    break;

                case FloatAffectMode.RotationZ:
                    bone.localEulerAngles = new Vector3(0, 0, val);
                    break;
            }
        }
    }
}