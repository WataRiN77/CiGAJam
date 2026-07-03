using System;
using System.Collections.Generic;

[Serializable]
public class ParameterValue
{
    public string id;
    public string valueJson; // 使用 JsonUtility 序列化/反序列化具体值
}

[Serializable]
public class FacialSaveData2D
{
    public string dataAssetId;
    public List<ParameterValue> values;
}