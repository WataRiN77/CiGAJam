using System.Collections.Generic;
using UnityEngine;

public static class FaceComparer
{
    private const float PART_WEIGHT = 0.4f;
    private const float POSITION_WEIGHT = 0.3f;
    private const float ROTATION_WEIGHT = 0.15f;
    private const float SCALE_WEIGHT = 0.15f;

    public static float CompareFaces(FaceSaveData answer, FaceSaveData player)
    {
        if (answer == null || player == null || answer.organs.Count == 0)
        {
            return 0f;
        }

        Dictionary<string, OrganState> answerDict = new Dictionary<string, OrganState>();
        foreach (var organ in answer.organs)
        {
            answerDict[organ.objectPath] = organ;
        }

        Dictionary<string, OrganState> playerDict = new Dictionary<string, OrganState>();
        foreach (var organ in player.organs)
        {
            playerDict[organ.objectPath] = organ;
        }

        float totalScore = 0f;
        int count = 0;

        foreach (var kvp in answerDict)
        {
            string path = kvp.Key;
            OrganState answerOrgan = kvp.Value;
            if (!playerDict.TryGetValue(path, out OrganState playerOrgan))
            {
                continue;
            }

            bool answerVisible = answerOrgan.isVisible && answerOrgan.spriteIndex >= 0;
            bool playerVisible = playerOrgan.isVisible && playerOrgan.spriteIndex >= 0;
            if (answerVisible != playerVisible)
            {
                count++;
                continue;
            }

            if (!answerVisible && !playerVisible)
            {
                totalScore += 1f;
                count++;
                continue;
            }

            float partScore = 1f;
            if (!string.IsNullOrEmpty(answerOrgan.partId) && answerOrgan.partId == playerOrgan.partId)
            {
                partScore = answerOrgan.spriteIndex == playerOrgan.spriteIndex ? 1f : 0f;
            }

            float posError = Vector3.Distance(answerOrgan.localPosition, playerOrgan.localPosition);
            float posScore = Mathf.Clamp01(1f - posError / 0.1f);

            float rotError = Mathf.Abs(answerOrgan.localRotation.eulerAngles.z - playerOrgan.localRotation.eulerAngles.z);
            rotError = Mathf.Min(rotError, 360f - rotError);
            float rotScore = Mathf.Clamp01(1f - rotError / 30f);

            float scaleError = (
                Mathf.Abs(answerOrgan.localScale.x - playerOrgan.localScale.x) +
                Mathf.Abs(answerOrgan.localScale.y - playerOrgan.localScale.y)
            ) / 2f;
            float scaleScore = Mathf.Clamp01(1f - scaleError / 0.5f);

            float organScore =
                partScore * PART_WEIGHT +
                posScore * POSITION_WEIGHT +
                rotScore * ROTATION_WEIGHT +
                scaleScore * SCALE_WEIGHT;

            totalScore += organScore;
            count++;
        }

        if (count == 0)
        {
            return 0f;
        }

        return totalScore / count * 100f;
    }
}
