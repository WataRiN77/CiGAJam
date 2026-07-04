// WitnessStatementGenerator.cs
using System.Collections.Generic;
using UnityEngine;

public class WitnessStatementGenerator : MonoBehaviour
{
    [SerializeField] private WitnessConfig witnessConfig;
    [SerializeField] private CharacterCustomizer2D customizer; // 用于获取器官显示名（可选）

    /// <summary>
    /// 根据目标人脸生成多条证词，每条包含目击者姓名和完整内容
    /// </summary>
    public List<WitnessStatement> GenerateStatements(FaceSaveData targetFace, int count)
    {
        // 初始化姓名池
        witnessConfig.ResetNames();

        // 收集所有可能的证词（每个器官当前Sprite下可选的句子）
        List<WitnessStatement> pool = new List<WitnessStatement>();
        foreach (var organ in targetFace.organs)
        {
            if (string.IsNullOrEmpty(organ.partId)) continue;
            string sentence = witnessConfig.GetRandomStatement(organ.partId, organ.spriteIndex);
            // 一个器官只取一句（也可取多句，按需）
            pool.Add(new WitnessStatement
            {
                witnessName = "", // 稍后分配
                content = sentence
            });
        }

        // 随机打乱并取前 count 条
        for (int i = 0; i < pool.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, pool.Count);
            var tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }

        int actualCount = Mathf.Min(count, pool.Count);
        List<WitnessStatement> result = pool.GetRange(0, actualCount);

        // 分配姓名
        foreach (var stmt in result)
        {
            stmt.witnessName = witnessConfig.GetNextName();
        }

        return result;
    }

    public WitnessStatement GenerateSingleStatement(FaceSaveData targetFace, List<WitnessStatement> existing)
    {
        if (targetFace == null) return null;

        // 收集所有可用器官及其句子
        var organSentences = new List<(OrganState organ, string sentence)>();
        foreach (var organ in targetFace.organs)
        {
            if (string.IsNullOrEmpty(organ.partId)) continue;
            string sentence = witnessConfig.GetRandomStatement(organ.partId, organ.spriteIndex);
            organSentences.Add((organ, sentence));
        }

        // 打乱
        for (int i = 0; i < organSentences.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, organSentences.Count);
            var tmp = organSentences[i];
            organSentences[i] = organSentences[j];
            organSentences[j] = tmp;
        }

        // 寻找一句尚未出现过的句子（避免内容完全重复）
        foreach (var (organ, sentence) in organSentences)
        {
            if (existing == null || !existing.Exists(s => s.content == sentence))
            {
                string name = witnessConfig.GetNextName();
                return new WitnessStatement { witnessName = name, content = sentence };
            }
        }

        // 若所有句子都出现过了，返回null
        return null;
    }
}

/// <summary>
/// 一条目击证词的数据结构
/// </summary>
[System.Serializable]
public class WitnessStatement
{
    public string witnessName;   // 目击者姓名
    public string content;       // 证词内容
}