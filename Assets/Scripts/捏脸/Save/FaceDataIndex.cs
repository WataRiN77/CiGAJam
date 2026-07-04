// FaceDataIndex.cs
using System;
using System.Collections.Generic;

[Serializable]
public class FaceSaveIndex
{
    public List<FaceSaveEntry> entries = new List<FaceSaveEntry>();
}

[Serializable]
public class FaceSaveEntry
{
    public string saveName;     // 存档显示名称
    public string fileName;     // 实际文件名（不含路径），如 "face_001.json"
    public string createdAt;    // 创建时间（可选）
}