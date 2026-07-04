using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FaceSaveData
{
    public int seed;
    public List<WitnessStatement> witnessStatements = new List<WitnessStatement>();
    public List<OrganState> organs = new List<OrganState>();
}

[Serializable]
public class OrganState
{
    public string objectPath;
    public Vector3 localPosition;
    public Vector3 localScale;
    public Quaternion localRotation;
    public string partId;
    public int spriteIndex;
    public bool isVisible = true;
}
