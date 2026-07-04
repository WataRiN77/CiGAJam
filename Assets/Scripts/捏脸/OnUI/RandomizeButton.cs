using UnityEngine;
using UnityEngine.UI;

public class RandomizeButton : MonoBehaviour
{
    public CharacterCustomizer2D customizer;
    public Button randomButton;

    void Start()
    {
        randomButton.onClick.AddListener(() => customizer.RandomizeAll());
    }
}