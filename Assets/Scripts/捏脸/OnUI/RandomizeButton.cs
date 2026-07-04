using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class RandomizeButton : MonoBehaviour
{
    public CharacterCustomizer2D customizer;
    public AnswerGenerator answerGenerator;
    public Button randomButton;
    public int seed = 1234;

    void Start()
    {
        //randomButton.onClick.AddListener(() => customizer.RandomizeAll());
        randomButton.onClick.AddListener(() =>
        {
            FaceSaveData faceData = answerGenerator.GenerateAnswerWithSeed(seed);
            string json = JsonUtility.ToJson(faceData);
            customizer.LoadFromJson(json);
        });
    }
}