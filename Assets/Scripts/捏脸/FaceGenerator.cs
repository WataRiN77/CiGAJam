using System.Collections.Generic;
using UnityEngine;

public class FaceGenerator : MonoBehaviour
{
    [SerializeField] private CharacterCustomizer2D targetCustomizer;
    [SerializeField] private CharacterCustomizer2D templateCustomizer;
    [SerializeField] private AnswerGenerator answerGenerator;
    [SerializeField] private bool generateAppliedFaceFromTargetCustomizer = true;

    private void Awake()
    {
        if (templateCustomizer != null && templateCustomizer != targetCustomizer)
        {
            templateCustomizer.gameObject.SetActive(false);
        }
    }

    public List<FaceSaveData> GenerateFacesFromSeeds(List<int> seeds)
    {
        List<FaceSaveData> faces = new List<FaceSaveData>();

        if (seeds == null || seeds.Count == 0)
        {
            Debug.LogWarning("Seed list is empty.");
            return faces;
        }

        if (templateCustomizer == null || answerGenerator == null)
        {
            Debug.LogError("FaceGenerator is not configured.");
            return faces;
        }

        templateCustomizer.ResetAllOrgansToOriginal();

        foreach (int seed in seeds)
        {
            FaceSaveData face = answerGenerator.GenerateAnswerWithSeed(seed);
            faces.Add(face);
        }

        templateCustomizer.ResetAllOrgansToOriginal();
        return faces;
    }

    public FaceSaveData GenerateAndApplyFace(int seed)
    {
        CharacterCustomizer2D customizer = GetTargetCustomizer();

        if (customizer == null)
        {
            Debug.LogError("FaceGenerator needs a target CharacterCustomizer2D to apply the generated face.", this);
            return null;
        }

        FaceSaveData face = GenerateFaceData(seed);

        if (face == null)
        {
            return null;
        }

        customizer.LoadFromJson(JsonUtility.ToJson(face));
        return face;
    }

    public FaceSaveData GenerateFaceData(int seed)
    {
        CharacterCustomizer2D customizer = GetTargetCustomizer();

        if (!generateAppliedFaceFromTargetCustomizer && answerGenerator != null)
        {
            return answerGenerator.GenerateAnswerWithSeed(seed);
        }

        if (customizer == null)
        {
            Debug.LogError("FaceGenerator cannot find CharacterCustomizer2D.", this);
            return null;
        }

        Random.State oldState = Random.state;
        Random.InitState(seed);

        string originalJson = customizer.SaveToJson();
        customizer.RandomizeAll();

        string faceJson = customizer.SaveToJson();
        FaceSaveData face = JsonUtility.FromJson<FaceSaveData>(faceJson);
        face.seed = seed;

        customizer.LoadFromJson(originalJson);
        Random.state = oldState;
        return face;
    }

    private CharacterCustomizer2D GetTargetCustomizer()
    {
        if (targetCustomizer != null)
        {
            return targetCustomizer;
        }

        targetCustomizer = GetComponentInParent<CharacterCustomizer2D>();

        if (targetCustomizer == null)
        {
            targetCustomizer = GetComponentInChildren<CharacterCustomizer2D>(true);
        }

        return targetCustomizer;
    }
}
