// WitnessConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Customization/Witness Config")]
public class WitnessConfig : ScriptableObject
{
    [Header("目击者姓名库")]
    [Tooltip("随机抽取，每条证词分配一个不同的姓名")]
    public List<string> witnessNames = new List<string>()
    {
        "王先生", "李小姐", "张女士", "刘先生", "陈大爷", "赵阿姨"
    };

    [Header("器官证词库")]
    [Tooltip("每个器官对应多组证词，每组对应一个Sprite索引")]
    public List<OrganStatementGroup> organStatementGroups = new List<OrganStatementGroup>();

    [Serializable]
    public class OrganStatementGroup
    {
        [Tooltip("参数ID，如 hair_style")]
        public string partId;

        [Tooltip("每个Sprite索引对应的完整证词列表（顺序与PartParameter的Sprites一致）")]
        public List<SpriteStatementList> spriteStatements = new List<SpriteStatementList>();
    }

    [Serializable]
    public class SpriteStatementList
    {
        [Tooltip("该Sprite形状的可选完整证词，例如“我看到他的发型是清爽的短发。”")]
        public List<string> statements = new List<string>();
    }

    // 缓存剩余可选姓名（生成时用）
    private List<string> availableNames;

    /// <summary>
    /// 初始化姓名池（生成前调用）
    /// </summary>
    public void ResetNames()
    {
        availableNames = new List<string>(witnessNames);
        // 随机打乱，确保每次生成的姓名顺序不同
        for (int i = 0; i < availableNames.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, availableNames.Count);
            var tmp = availableNames[i];
            availableNames[i] = availableNames[j];
            availableNames[j] = tmp;
        }
    }

    /// <summary>
    /// 获取一个随机姓名并从池中移除（避免重复）
    /// </summary>
    public string GetNextName()
    {
        if (availableNames == null || availableNames.Count == 0)
            return "目击者";
        string name = availableNames[0];
        availableNames.RemoveAt(0);
        return name;
    }

    /// <summary>
    /// 根据器官参数ID和Sprite索引，随机获取一条完整证词句子
    /// </summary>
    public string GetRandomStatement(string partId, int spriteIndex)
    {
        OrganStatementGroup group = organStatementGroups.Find(g => g.partId == partId);
        if (group == null || spriteIndex < 0 || spriteIndex >= group.spriteStatements.Count)
            return "我没有看清他的" + partId;

        List<string> statements = group.spriteStatements[spriteIndex].statements;
        if (statements == null || statements.Count == 0)
            return "我记不太清他的" + partId;

        return statements[UnityEngine.Random.Range(0, statements.Count)];
    }
}