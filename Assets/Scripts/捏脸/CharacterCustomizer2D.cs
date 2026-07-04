using System.Collections.Generic;
using UnityEngine;

public class CharacterCustomizer2D : MonoBehaviour
{
    [Header("数据资产")]
    [SerializeField] private CharacterCustomizationData2D dataAsset;

    [Header("保存设置")]
    [SerializeField] private bool autoSaveAllDraggableOrgans = true;

    [Header("全局随机范围（未被器官覆盖时使用）")]
    [SerializeField] private float randomPosRange = 0.05f;
    [SerializeField] private float randomRotationRange = 15f;
    [SerializeField] private float randomScaleMin = 0.8f;
    [SerializeField] private float randomScaleMax = 1.2f;

    private Dictionary<string, int> partIndexes = new Dictionary<string, int>();
    private Dictionary<ParameterType, IFeatureApplier> appliers;
    private Dictionary<string, TransformData> originalTransforms = new Dictionary<string, TransformData>();

    private void Awake()
    {
        appliers = new Dictionary<ParameterType, IFeatureApplier>
        {
            { ParameterType.Part, new PartApplier() }
        };

        if (dataAsset != null)
        {
            foreach (var param in dataAsset.parameters)
            {
                if (param is PartParameter partParam)
                {
                    partIndexes[param.parameterId] = 0;
                    ApplyPart(param.parameterId, 0);
                }
            }

            // 记录初始 Transform（用于重置）
            foreach (var param in dataAsset.parameters)
            {
                if (param is PartParameter part)
                {
                    foreach (string path in part.targetBoneNames)
                    {
                        Transform t = transform.Find(path);
                        if (t != null && !originalTransforms.ContainsKey(path))
                        {
                            originalTransforms[path] = new TransformData
                            {
                                localPosition = t.localPosition,
                                localScale = t.localScale,
                                localRotation = t.localRotation
                            };
                        }
                    }
                }
            }
        }
    }

    #region 部件切换

    public void SetPart(string id, int index)
    {
        if (!partIndexes.ContainsKey(id))
        {
            Debug.LogWarning($"部件参数 {id} 不存在");
            return;
        }
        if (partIndexes[id] == index) return;

        partIndexes[id] = index;
        ApplyPart(id, index);
    }

    private void ApplyPart(string id, int index)
    {
        var param = dataAsset.parameters.Find(p => p.parameterId == id);
        if (param != null && appliers.TryGetValue(param.type, out var applier))
        {
            applier.Apply(gameObject, param, index);
        }
    }

    public CharacterCustomizationData2D GetDataAsset() => dataAsset;
    public Dictionary<string, int> GetPartIndexes() => partIndexes;

    #endregion

    #region 重置

    public void ResetAllOrgansToOriginal()
    {
        foreach (var kvp in originalTransforms)
        {
            Transform t = transform.Find(kvp.Key);
            if (t != null)
            {
                t.localPosition = kvp.Value.localPosition;
                t.localScale = kvp.Value.localScale;
                t.localRotation = kvp.Value.localRotation;
            }
        }

        foreach (var param in dataAsset.parameters)
        {
            if (param is PartParameter)
                SetPart(param.parameterId, 0);
        }

        SelectionManager.Instance?.DeselectCurrent();
    }

    #endregion

    #region 保存与加载

    public string SaveToJson()
    {
        FaceSaveData save = new FaceSaveData();
        HashSet<string> savedPaths = new HashSet<string>();

        foreach (var param in dataAsset.parameters)
        {
            if (param is PartParameter part)
            {
                int index = partIndexes.ContainsKey(part.parameterId) ? partIndexes[part.parameterId] : 0;
                foreach (string path in part.targetBoneNames)
                {
                    Transform target = transform.Find(path);
                    if (target == null || savedPaths.Contains(path)) continue;

                    save.organs.Add(new OrganState
                    {
                        objectPath = path,
                        localPosition = target.localPosition,
                        localScale = target.localScale,
                        localRotation = target.localRotation,
                        partId = part.parameterId,
                        spriteIndex = index
                    });
                    savedPaths.Add(path);
                }
            }
        }

        if (autoSaveAllDraggableOrgans)
        {
            DraggableOrgan[] allDraggables = GetComponentsInChildren<DraggableOrgan>(true);
            foreach (var draggable in allDraggables)
            {
                string path = GetRelativePath(transform, draggable.transform);
                if (savedPaths.Contains(path)) continue;

                save.organs.Add(new OrganState
                {
                    objectPath = path,
                    localPosition = draggable.transform.localPosition,
                    localScale = draggable.transform.localScale,
                    localRotation = draggable.transform.localRotation,
                    partId = "",
                    spriteIndex = 0
                });
                savedPaths.Add(path);
            }
        }

        return JsonUtility.ToJson(save, true);
    }

    public void LoadFromJson(string json)
    {
        FaceSaveData save = JsonUtility.FromJson<FaceSaveData>(json);
        if (save == null) return;

        SelectionManager.Instance?.DeselectCurrent();

        foreach (var state in save.organs)
        {
            Transform target = transform.Find(state.objectPath);
            if (target == null) continue;

            if (!string.IsNullOrEmpty(state.partId))
            {
                if (partIndexes.ContainsKey(state.partId))
                {
                    partIndexes[state.partId] = state.spriteIndex;
                    ApplyPart(state.partId, state.spriteIndex);
                }
            }

            target.localPosition = state.localPosition;
            target.localScale = state.localScale;
            target.localRotation = state.localRotation;
        }
    }

    private string GetRelativePath(Transform root, Transform target)
    {
        string path = "";
        while (target != root && target != null)
        {
            path = target.name + (string.IsNullOrEmpty(path) ? "" : "/" + path);
            target = target.parent;
        }
        return path;
    }

    #endregion

    #region 随机生成

    public void RandomizeAll()
    {
        HashSet<Transform> processed = new HashSet<Transform>();

        foreach (var param in dataAsset.parameters)
        {
            if (param is PartParameter part)
            {
                // 随机部件索引
                if (part.sprites != null && part.sprites.Length > 0)
                {
                    int randomIndex = Random.Range(0, part.sprites.Length);
                    SetPart(part.parameterId, randomIndex);
                }

                // 获取随机范围
                float posRange = randomPosRange;
                float rotRange = randomRotationRange;
                float scaleMin = randomScaleMin;
                float scaleMax = randomScaleMax;
                if (part.OverrideRandomRange)
                {
                    posRange = part.RandomPosRange;
                    rotRange = part.RandomRotationRange;
                    scaleMin = part.RandomScaleMin;
                    scaleMax = part.RandomScaleMax;
                }

                foreach (string path in part.targetBoneNames)
                {
                    Transform t = transform.Find(path);
                    if (t == null) continue;
                    RandomizeTransform(t, posRange, rotRange, scaleMin, scaleMax);
                    processed.Add(t);
                }
            }
        }

        // 处理未通过 PartParameter 管理的器官（如脸底图）
        DraggableOrgan[] allDraggables = GetComponentsInChildren<DraggableOrgan>(true);
        foreach (var draggable in allDraggables)
        {
            if (processed.Contains(draggable.transform)) continue;
            RandomizeTransform(draggable.transform, randomPosRange, randomRotationRange, randomScaleMin, randomScaleMax);
        }

        SelectionManager.Instance?.DeselectCurrent();
    }

    private void RandomizeTransform(Transform t, float posRange, float rotRange, float scaleMin, float scaleMax)
    {
        t.localPosition = new Vector3(
            Random.Range(-posRange, posRange),
            Random.Range(-posRange, posRange),
            t.localPosition.z);

        t.localEulerAngles = new Vector3(0, 0, Random.Range(-rotRange, rotRange));

        float scale = Random.Range(scaleMin, scaleMax);
        t.localScale = new Vector3(scale, scale, 1f);
    }

    #endregion

    [System.Serializable]
    public struct TransformData
    {
        public Vector3 localPosition;
        public Vector3 localScale;
        public Quaternion localRotation;
    }
}