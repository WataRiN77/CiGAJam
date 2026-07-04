using System.Collections.Generic;
using UnityEngine;

public class CharacterCustomizer2D : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterCustomizationData2D dataAsset;

    [Header("Save")]
    [SerializeField] private bool autoSaveAllDraggableOrgans = true;

    [Header("Initial Visibility")]
    [SerializeField] private bool startWithPartsHidden = true;

    [Header("Global Random Range")]
    [SerializeField] private float randomPosRange = 0.05f;
    [SerializeField] private float randomRotationRange = 15f;
    [SerializeField] private float randomScaleMin = 0.8f;
    [SerializeField] private float randomScaleMax = 1.2f;

    private readonly Dictionary<string, int> partIndexes = new Dictionary<string, int>();
    private readonly Dictionary<string, bool> partVisible = new Dictionary<string, bool>();
    private readonly Dictionary<string, TransformData> originalTransforms = new Dictionary<string, TransformData>();
    private Dictionary<ParameterType, IFeatureApplier> appliers;

    private void Awake()
    {
        appliers = new Dictionary<ParameterType, IFeatureApplier>
        {
            { ParameterType.Part, new PartApplier() }
        };

        if (dataAsset == null)
        {
            return;
        }

        foreach (var param in dataAsset.parameters)
        {
            if (param is PartParameter part)
            {
                partIndexes[param.parameterId] = 0;
                partVisible[param.parameterId] = !startWithPartsHidden;
                ApplyPart(param.parameterId, 0);
                SetPartRenderersVisible(part, !startWithPartsHidden);
            }
        }

        foreach (var param in dataAsset.parameters)
        {
            if (param is PartParameter part)
            {
                RecordOriginalTransforms(part);
            }
        }
    }

    #region Part selection

    public void SetPart(string id, int index)
    {
        PartParameter part = GetPartParameter(id);
        if (part == null)
        {
            Debug.LogWarning($"Part parameter {id} does not exist.");
            return;
        }

        if (part.sprites == null || index < 0 || index >= part.sprites.Length || part.sprites[index] == null)
        {
            Debug.LogWarning($"Part parameter {id} index {index} is invalid.");
            return;
        }

        bool wasVisible = IsPartVisible(id);
        if (!wasVisible)
        {
            RestorePartDefaultTransforms(part);
        }

        partIndexes[id] = index;
        partVisible[id] = true;
        ApplyPart(id, index);
        SetPartRenderersVisible(part, true);
    }

    public void HidePart(string id)
    {
        PartParameter part = GetPartParameter(id);
        if (part == null)
        {
            Debug.LogWarning($"Part parameter {id} does not exist.");
            return;
        }

        partVisible[id] = false;
        SetPartRenderersVisible(part, false);
        SelectionManager.Instance?.DeselectCurrent();
    }

    public bool IsPartVisible(string id)
    {
        return partVisible.TryGetValue(id, out bool visible) && visible;
    }

    public int GetSelectedPartIndex(string id)
    {
        if (!IsPartVisible(id))
        {
            return -1;
        }

        return partIndexes.TryGetValue(id, out int index) ? index : -1;
    }

    private void ApplyPart(string id, int index)
    {
        var param = dataAsset.parameters.Find(p => p.parameterId == id);
        if (param != null && appliers.TryGetValue(param.type, out var applier))
        {
            applier.Apply(gameObject, param, index);
        }
    }

    private PartParameter GetPartParameter(string id)
    {
        if (dataAsset == null)
        {
            return null;
        }

        return dataAsset.parameters.Find(p => p.parameterId == id) as PartParameter;
    }

    public CharacterCustomizationData2D GetDataAsset() => dataAsset;
    public Dictionary<string, int> GetPartIndexes() => partIndexes;

    #endregion

    #region Reset

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

        if (dataAsset != null)
        {
            foreach (var param in dataAsset.parameters)
            {
                if (param is PartParameter part)
                {
                    partIndexes[param.parameterId] = 0;
                    ApplyPart(param.parameterId, 0);
                    partVisible[param.parameterId] = !startWithPartsHidden;
                    SetPartRenderersVisible(part, !startWithPartsHidden);
                }
            }
        }

        SelectionManager.Instance?.DeselectCurrent();
    }

    #endregion

    #region Save and load

    public string SaveToJson()
    {
        FaceSaveData save = new FaceSaveData();
        HashSet<string> savedPaths = new HashSet<string>();

        if (dataAsset != null)
        {
            foreach (var param in dataAsset.parameters)
            {
                if (param is PartParameter part)
                {
                    bool visible = IsPartVisible(part.parameterId);
                    int index = visible && partIndexes.ContainsKey(part.parameterId)
                        ? partIndexes[part.parameterId]
                        : -1;

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
                            spriteIndex = index,
                            isVisible = visible
                        });
                        savedPaths.Add(path);
                    }
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
                    spriteIndex = 0,
                    isVisible = draggable.gameObject.activeInHierarchy
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
                PartParameter part = GetPartParameter(state.partId);
                if (part != null)
                {
                    bool visible = state.isVisible && state.spriteIndex >= 0;
                    if (visible)
                    {
                        partIndexes[state.partId] = state.spriteIndex;
                        ApplyPart(state.partId, state.spriteIndex);
                    }

                    partVisible[state.partId] = visible;
                    SetPartRenderersVisible(part, visible);
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

    #region Randomize

    public void RandomizeAll()
    {
        if (dataAsset == null)
        {
            return;
        }

        HashSet<Transform> processed = new HashSet<Transform>();

        foreach (var param in dataAsset.parameters)
        {
            if (param is PartParameter part)
            {
                int randomIndex = GetRandomValidSpriteIndex(part);
                if (randomIndex >= 0)
                {
                    SetPart(part.parameterId, randomIndex);
                }

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

    private int GetRandomValidSpriteIndex(PartParameter part)
    {
        if (part.sprites == null || part.sprites.Length == 0)
        {
            return -1;
        }

        List<int> validIndexes = new List<int>();
        for (int i = 0; i < part.sprites.Length; i++)
        {
            if (part.sprites[i] != null)
            {
                validIndexes.Add(i);
            }
        }

        if (validIndexes.Count == 0)
        {
            return -1;
        }

        return validIndexes[Random.Range(0, validIndexes.Count)];
    }

    private void RecordOriginalTransforms(PartParameter part)
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

    private void RestorePartDefaultTransforms(PartParameter part)
    {
        foreach (string path in part.targetBoneNames)
        {
            Transform t = transform.Find(path);
            if (t != null && originalTransforms.TryGetValue(path, out TransformData data))
            {
                t.localPosition = data.localPosition;
                t.localScale = data.localScale;
                t.localRotation = data.localRotation;
            }
        }
    }

    private void SetPartRenderersVisible(PartParameter part, bool visible)
    {
        foreach (string path in part.targetBoneNames)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            Transform target = transform.Find(path.Trim());
            if (target == null) continue;

            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    [System.Serializable]
    public struct TransformData
    {
        public Vector3 localPosition;
        public Vector3 localScale;
        public Quaternion localRotation;
    }
}
