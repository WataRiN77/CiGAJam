// FaceSaveData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class OrganState
{
    public string objectPath;       // 相对于角色根节点的路径，如 "Hair/HairStyle"
    public Vector3 localPosition;
    public Vector3 localScale;
    public Quaternion localRotation;
    public string partId;           // 所属的 PartParameter ID，无则为空
    public int spriteIndex;         // 当前使用的部件 Sprite 索引
}

[Serializable]
public class FaceSaveData
{
    public List<OrganState> organs = new List<OrganState>();
}