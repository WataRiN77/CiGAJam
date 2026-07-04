using System.Collections.Generic;
using UnityEngine;

public class FaceGenerator : MonoBehaviour
{
    [SerializeField] private CharacterCustomizer2D templateCustomizer;
    [SerializeField] private AnswerGenerator answerGenerator;

    private void Awake()
    {
        if (templateCustomizer != null)
            templateCustomizer.gameObject.SetActive(false);
    }

    public List<FaceSaveData> GenerateFacesFromSeeds(List<int> seeds)
    {
        List<FaceSaveData> faces = new List<FaceSaveData>();

        if (seeds == null || seeds.Count == 0)
        {
            Debug.LogWarning("种子列表为空");
            return faces;
        }

        if (templateCustomizer == null || answerGenerator == null)
        {
            Debug.LogError("FaceGenerator 未配置");
            return faces;
        }

        // 重置模板到默认状态
        templateCustomizer.ResetAllOrgansToOriginal();

        foreach (int seed in seeds)
        {
            FaceSaveData face = answerGenerator.GenerateAnswerWithSeed(seed);
            faces.Add(face);
        }

        // 生成后再次重置，保持模板干净
        templateCustomizer.ResetAllOrgansToOriginal();
        return faces;
    }
}