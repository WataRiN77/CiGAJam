using System;
using System.Collections.Generic;
using UnityEngine;

public class FaceAssetLibraryUI : MonoBehaviour
{
    [Serializable]
    public class FaceAssetCategory
    {
        public string label;
        public string parameterId;
    }

    [Header("References")]
    [SerializeField] private CharacterCustomizer2D customizer;
    [SerializeField] private Transform tabParent;
    [SerializeField] private FaceAssetTabUI tabPrefab;
    [SerializeField] private Transform itemParent;
    [SerializeField] private FaceAssetSlotUI slotPrefab;

    [Header("Categories")]
    [SerializeField] private List<FaceAssetCategory> categories = new List<FaceAssetCategory>();

    private readonly List<FaceAssetTabUI> spawnedTabs = new List<FaceAssetTabUI>();
    private readonly List<FaceAssetSlotUI> spawnedSlots = new List<FaceAssetSlotUI>();
    private int currentCategoryIndex = -1;

    private void Start()
    {
        if (customizer == null)
        {
            customizer = FindObjectOfType<CharacterCustomizer2D>();
        }

        if (customizer == null)
        {
            Debug.LogError("FaceAssetLibraryUI: CharacterCustomizer2D is not assigned.");
            return;
        }

        EnsureCategories();
        BuildTabs();

        if (categories.Count > 0)
        {
            ShowCategory(0);
        }
    }

    public void ShowCategory(int categoryIndex)
    {
        if (categoryIndex < 0 || categoryIndex >= categories.Count)
        {
            return;
        }

        currentCategoryIndex = categoryIndex;
        RefreshTabSelection();
        BuildSlots(categories[categoryIndex]);
    }

    private void EnsureCategories()
    {
        if (categories.Count == 0)
        {
            AddDefaultCategory("\u53d1\u578b", "hairstyle");
            AddDefaultCategory("\u5de6\u773c", "eyestylel");
            AddDefaultCategory("\u53f3\u773c", "eyestyler");
            AddDefaultCategory("\u5de6\u7709", "eyebrowstylel");
            AddDefaultCategory("\u53f3\u7709", "eyebrowstyler");
            AddDefaultCategory("\u9f3b\u5b50", "nosestyle");
            AddDefaultCategory("\u5634\u5df4", "mouthstyle");
        }

        CharacterCustomizationData2D dataAsset = customizer.GetDataAsset();
        if (dataAsset == null)
        {
            return;
        }

        foreach (var param in dataAsset.parameters)
        {
            if (param is PartParameter && !HasCategory(param.parameterId))
            {
                categories.Add(new FaceAssetCategory
                {
                    label = string.IsNullOrEmpty(param.displayName) ? param.parameterId : param.displayName,
                    parameterId = param.parameterId
                });
            }
        }
    }

    private void AddDefaultCategory(string label, string parameterId)
    {
        if (HasCategory(parameterId))
        {
            return;
        }

        categories.Add(new FaceAssetCategory
        {
            label = label,
            parameterId = parameterId
        });
    }

    private bool HasCategory(string parameterId)
    {
        return categories.Exists(category => category.parameterId == parameterId);
    }

    private void BuildTabs()
    {
        ClearTabs();

        if (tabParent == null || tabPrefab == null)
        {
            return;
        }

        for (int i = 0; i < categories.Count; i++)
        {
            FaceAssetTabUI tab = Instantiate(tabPrefab, tabParent);
            tab.Initialize(categories[i].label, i, i == currentCategoryIndex, ShowCategory);
            spawnedTabs.Add(tab);
        }
    }

    private void BuildSlots(FaceAssetCategory category)
    {
        ClearSlots();

        if (itemParent == null || slotPrefab == null)
        {
            return;
        }

        PartParameter part = FindPartParameter(category.parameterId);
        if (part == null || part.sprites == null)
        {
            return;
        }

        int selectedIndex = customizer.GetSelectedPartIndex(category.parameterId);
        for (int i = 0; i < part.sprites.Length; i++)
        {
            Sprite sprite = part.sprites[i];
            if (sprite == null)
            {
                continue;
            }

            FaceAssetSlotUI slot = Instantiate(slotPrefab, itemParent);
            slot.Initialize(sprite, i, selectedIndex == i, HandleSlotClicked);
            spawnedSlots.Add(slot);
        }
    }

    private void HandleSlotClicked(int spriteIndex)
    {
        if (currentCategoryIndex < 0 || currentCategoryIndex >= categories.Count)
        {
            return;
        }

        string parameterId = categories[currentCategoryIndex].parameterId;
        int selectedIndex = customizer.GetSelectedPartIndex(parameterId);
        if (selectedIndex == spriteIndex)
        {
            customizer.HidePart(parameterId);
        }
        else
        {
            customizer.SetPart(parameterId, spriteIndex);
        }

        RefreshSlotSelection();
    }

    private void RefreshTabSelection()
    {
        for (int i = 0; i < spawnedTabs.Count; i++)
        {
            spawnedTabs[i].SetSelected(i == currentCategoryIndex);
        }
    }

    private void RefreshSlotSelection()
    {
        if (currentCategoryIndex < 0 || currentCategoryIndex >= categories.Count)
        {
            return;
        }

        string parameterId = categories[currentCategoryIndex].parameterId;
        int selectedIndex = customizer.GetSelectedPartIndex(parameterId);

        PartParameter part = FindPartParameter(parameterId);
        if (part == null || part.sprites == null)
        {
            return;
        }

        int slotIndex = 0;
        for (int i = 0; i < part.sprites.Length && slotIndex < spawnedSlots.Count; i++)
        {
            if (part.sprites[i] == null)
            {
                continue;
            }

            spawnedSlots[slotIndex].SetSelected(selectedIndex == i);
            slotIndex++;
        }
    }

    private PartParameter FindPartParameter(string parameterId)
    {
        CharacterCustomizationData2D dataAsset = customizer.GetDataAsset();
        if (dataAsset == null)
        {
            return null;
        }

        return dataAsset.parameters.Find(param => param.parameterId == parameterId) as PartParameter;
    }

    private void ClearTabs()
    {
        foreach (FaceAssetTabUI tab in spawnedTabs)
        {
            if (tab != null)
            {
                Destroy(tab.gameObject);
            }
        }

        spawnedTabs.Clear();
    }

    private void ClearSlots()
    {
        foreach (FaceAssetSlotUI slot in spawnedSlots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }

        spawnedSlots.Clear();
    }
}
