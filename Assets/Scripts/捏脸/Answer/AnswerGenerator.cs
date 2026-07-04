// AnswerGenerator.cs
using System.Collections.Generic;
using UnityEngine;

public class AnswerGenerator : MonoBehaviour
{
    [SerializeField] private CharacterCustomizer2D customizer;

    /// <summary>
    /// 使用随机种子生成答案，返回携带种子的人脸数据。
    /// 相同种子可生成完全相同的人脸。
    /// </summary>
    public FaceSaveData GenerateAnswer()
    {
        // 1. 生成一个随机种子（用System.Random避免依赖Unity随机状态）
        int seed = new System.Random().Next();

        // 2. 保存Unity引擎当前的随机状态，生成完毕后恢复
        Random.State oldState = Random.state;

        // 3. 初始化随机种子
        Random.InitState(seed);

        // 4. 备份当前玩家捏脸状态（免得破坏玩家的操作）
        string originalJson = customizer.SaveToJson();

        // 5. 基于种子随机生成
        customizer.RandomizeAll();

        // 6. 获取生成结果并填充种子
        string answerJson = customizer.SaveToJson();
        FaceSaveData answer = JsonUtility.FromJson<FaceSaveData>(answerJson);
        answer.seed = seed;

        // 7. 恢复玩家原来的捏脸数据
        customizer.LoadFromJson(originalJson);

        // 8. 恢复Unity随机状态（避免影响后续游戏中的其他随机）
        Random.state = oldState;

        return answer;
    }
    public List<FaceSaveData> GenerateDistractors(int count, FaceSaveData targetFace, int maxRetries = 5)
    {
        List<FaceSaveData> distractors = new List<FaceSaveData>();
        System.Random sysRand = new System.Random();

        for (int i = 0; i < count; i++)
        {
            FaceSaveData distractor = null;
            int attempts = 0;
            bool isDifferent = false;

            while (!isDifferent && attempts < maxRetries)
            {
                // 生成独立种子
                int seed = sysRand.Next();
                // 必须与目标种子不同
                if (targetFace != null && seed == targetFace.seed)
                    seed += 1;  // 简单规避

                // 基于种子生成人脸
                distractor = GenerateAnswerWithSeed(seed);
                distractor.seed = seed;  // 确保种子写入

                // 差异检查：至少要求与目标脸有明显不同
                isDifferent = IsSignificantlyDifferent(targetFace, distractor);
                attempts++;
            }

            // 如果尝试多次仍无差异，也加入列表（极低概率）
            distractors.Add(distractor);
        }

        return distractors;
    }

    /// <summary>
    /// 判断两张脸是否有足够差异（这里简单比较部件索引、位置、旋转、缩放）
    /// 可根据需求调整阈值
    /// </summary>
    private bool IsSignificantlyDifferent(FaceSaveData a, FaceSaveData b)
    {
        if (a == null || b == null) return true;
        if (a.organs.Count == 0 || b.organs.Count == 0) return true;

        // 检查关键器官的部件索引是否不同
        foreach (var organA in a.organs)
        {
            if (string.IsNullOrEmpty(organA.partId)) continue;
            var organB = b.organs.Find(o => o.objectPath == organA.objectPath && o.partId == organA.partId);
            if (organB == null) continue;

            // 部件形状不同 ⇒ 明确差异
            if (organA.spriteIndex != organB.spriteIndex) return true;
        }

        // 所有部件都相同时，检查变换差异是否足够大
        float totalDiff = 0f;
        int compared = 0;
        foreach (var organA in a.organs)
        {
            var organB = b.organs.Find(o => o.objectPath == organA.objectPath);
            if (organB == null) continue;

            float posDiff = Vector3.Distance(organA.localPosition, organB.localPosition);
            float rotDiff = Mathf.Abs(organA.localRotation.eulerAngles.z - organB.localRotation.eulerAngles.z);
            rotDiff = Mathf.Min(rotDiff, 360 - rotDiff);
            float scaleDiff = Mathf.Abs(organA.localScale.x - organB.localScale.x) +
                              Mathf.Abs(organA.localScale.y - organB.localScale.y);

            totalDiff += posDiff * 10f + rotDiff * 0.1f + scaleDiff;
            compared++;
        }
        float avgDiff = compared > 0 ? totalDiff / compared : 0f;

        // 平均差异阈值（可调整）
        return avgDiff > 0.02f;
    }

    // 已在之前定义的 GenerateAnswerWithSeed
    public FaceSaveData GenerateAnswerWithSeed(int seed)
    {
        Random.State oldState = Random.state;
        Random.InitState(seed);

        string originalJson = customizer.SaveToJson();
        customizer.RandomizeAll();

        string answerJson = customizer.SaveToJson();
        FaceSaveData answer = JsonUtility.FromJson<FaceSaveData>(answerJson);
        answer.seed = seed;

        customizer.LoadFromJson(originalJson);
        Random.state = oldState;
        return answer;
    }
}