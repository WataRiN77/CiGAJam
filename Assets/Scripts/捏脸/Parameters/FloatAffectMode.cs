public enum FloatAffectMode
{
    PositionY,            // 设置 localPosition.y（所有目标）
    SymmetricX,           // 对称水平移动（L减，R加）
    ScaleUniform,         // 统一缩放（localScale = val * Vector3.one）
    RotationZ_Symmetric,  // 对称旋转（L转+val，R转-val）
    RotationZ             // 直接设置 localEulerAngles.z
}