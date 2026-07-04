using System;
using UnityEngine;
using UnityEngine.UI;

public class FaceAssetSlotUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private Graphic selectedIndicator;

    private int spriteIndex;
    private Action<int> onClicked;

    public void Initialize(Sprite sprite, int index, bool selected, Action<int> clickCallback)
    {
        spriteIndex = index;
        onClicked = clickCallback;

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
            iconImage.preserveAspect = true;
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
        onClicked?.Invoke(spriteIndex);
    }
}
