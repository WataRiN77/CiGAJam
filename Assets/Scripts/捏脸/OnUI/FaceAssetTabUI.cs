using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FaceAssetTabUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Graphic selectedIndicator;

    private int tabIndex;
    private Action<int> onClicked;

    public void Initialize(string label, int index, bool selected, Action<int> clickCallback)
    {
        tabIndex = index;
        onClicked = clickCallback;

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (labelText != null)
        {
            labelText.text = label;
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
        }

        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectedIndicator != null)
        {
            selectedIndicator.gameObject.SetActive(selected);
        }
    }

    private void HandleClick()
    {
        onClicked?.Invoke(tabIndex);
    }
}
