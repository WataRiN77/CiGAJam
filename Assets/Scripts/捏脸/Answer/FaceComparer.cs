using System.Collections.Generic;
using UnityEngine;

public static class FaceComparer
{
    // 权重分配（总和为1）
    private const float PART_WEIGHT = 0.4f;        // 部件形状匹配
    private const float POSITION_WEIGHT = 0.3f;    // 位置偏差
    private const float ROTATION_WEIGHT = 0.15f;   // 旋转偏差
    private const float SCALE_WEIGHT = 0.15f;      // 缩放偏差

    /// <summary>
    /// 对比答案脸和玩家脸，返回 0~100 的相似度分数
    /// </summary>
    public static float CompareFaces(FaceSaveData answer, FaceSaveData player)
    {
        if (answer == null || player == null || answer.organs.Count == 0)
            return 0f;

        // 转为字典便于查找
        Dictionary<string, OrganState> answerDict = new Dictionary<string, OrganState>();
        foreach (var o in answer.organs)
            answerDict[o.objectPath] = o;

        Dictionary<string, OrganState> playerDict = new Dictionary<string, OrganState>();
        foreach (var o in player.organs)
            playerDict[o.objectPath] = o;

        float totalScore = 0f;
        int count = 0;

        foreach (var kvp in answerDict)
        {
            string path = kvp.Key;
            OrganState a = kvp.Value;
            if (!playerDict.TryGetValue(path, out OrganState p))
                continue;   // 玩家缺少某个器官？可改为扣分，这里先忽略

            // 1. 部件匹配分（如果该器官有 partId 则表示受 PartParameter 控制）
            float partScore = 1f;
            if (!string.IsNullOrEmpty(a.partId) && a.partId == p.partId)
            {
                partScore = (a.spriteIndex == p.spriteIndex) ? 1f : 0f;
            }

            // 2. 位置相似度（欧氏距离，映射到0~1）
            float posError = Vector3.Distance(a.localPosition, p.localPosition);
            float posMaxError = 0.1f;   // 可调整，取决于坐标范围
            float posScore = Mathf.Clamp01(1f - (posError / posMaxError));

            // 3. 旋转相似度（只比较Z轴）
            float rotError = Mathf.Abs(a.localRotation.eulerAngles.z - p.localRotation.eulerAngles.z);
            rotError = Mathf.Min(rotError, 360 - rotError); // 处理角度环绕
            float rotMaxError = 30f;
            float rotScore = Mathf.Clamp01(1f - (rotError / rotMaxError));

            // 4. 缩放相似度（取X、Y平均差值）
            float scaleError = (Mathf.Abs(a.localScale.x - p.localScale.x) + Mathf.Abs(a.localScale.y - p.localScale.y)) / 2f;
            float scaleMaxError = 0.5f;
            float scaleScore = Mathf.Clamp01(1f - (scaleError / scaleMaxError));

            // 加权综合
            float organScore = partScore * PART_WEIGHT +
                               posScore * POSITION_WEIGHT +
                               rotScore * ROTATION_WEIGHT +
                               scaleScore * SCALE_WEIGHT;

            totalScore += organScore;
            count++;
        }

        if (count == 0) return 0f;
        return (totalScore / count) * 100f;
    }
}