using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class RandomizeButton : MonoBehaviour
{
    public CharacterCustomizer2D customizer;
    public AnswerGenerator answerGenerator;
    public SeededClothingSelector clothingSelector;
    public Button randomButton;
    public int seed = 1234;

    void Start()
    {
        if (clothingSelector == null && customizer != null)
        {
            clothingSelector = customizer.GetComponentInParent<SeededClothingSelector>();
        }

        //randomButton.onClick.AddListener(() => customizer.RandomizeAll());
        randomButton.onClick.AddListener(() =>
        {
            FaceSaveData faceData = answerGenerator.GenerateAnswerWithSeed(seed);
            string json = JsonUtility.ToJson(faceData);
            customizer.LoadFromJson(json);
            clothingSelector?.ApplySeed(seed);
        });
    }
}
