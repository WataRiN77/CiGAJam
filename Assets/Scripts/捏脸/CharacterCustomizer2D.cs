using System.Collections.Generic;
using UnityEngine;

public class CharacterCustomizer2D : MonoBehaviour
{
    [SerializeField] private CharacterCustomizationData2D dataAsset;

    // 只存储部件的索引值
    private Dictionary<string, int> partIndexes = new Dictionary<string, int>();
    private Dictionary<ParameterType, IFeatureApplier> appliers;

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
        }
    }

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

    // 获取所有部件参数ID
    public List<string> GetPartIds()
    {
        List<string> ids = new List<string>();
        foreach (var p in dataAsset.parameters)
            if (p is PartParameter) ids.Add(p.parameterId);
        return ids;
    }

    // 保存/加载会用到
    public Dictionary<string, int> GetPartIndexes() => partIndexes;
    public void LoadPartIndexes(Dictionary<string, int> loaded)
    {
        foreach (var kvp in loaded)
            SetPart(kvp.Key, kvp.Value);
    }
}