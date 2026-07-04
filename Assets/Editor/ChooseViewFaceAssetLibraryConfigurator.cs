using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ChooseViewFaceAssetLibraryConfigurator
{
    private const string ScenePath = "Assets/Scenes/A_捏脸.unity";
    private const string PrefabFolder = "Assets/Prefabs/UI/FaceAssetLibrary";
    private const string SlotPrefabPath = PrefabFolder + "/FaceAssetSlot.prefab";
    private const string TabPrefabPath = PrefabFolder + "/FaceAssetTab.prefab";

    [MenuItem("Tools/CiGA/Configure ChooseView Face Asset Library")]
    public static void ConfigureFromMenu()
    {
        ConfigureTargetScene(showDialog: true);
    }

    private static void ConfigureTargetScene(bool showDialog)
    {
        Scene scene = GetLoadedTargetScene();
        bool openedScene = false;
        if (!scene.IsValid())
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedScene = true;
        }

        GameObject chooseView = FindInScene(scene, "ChooseView");
        if (chooseView == null)
        {
            Debug.LogError("Face asset library setup failed: ChooseView was not found in the active scene.");
            return;
        }

        CharacterCustomizer2D customizer = FindSceneComponent<CharacterCustomizer2D>(scene);
        if (customizer == null)
        {
            Debug.LogError("Face asset library setup failed: CharacterCustomizer2D was not found in the active scene.");
            return;
        }

        EnsureFolders();
        FaceAssetSlotUI slotPrefab = EnsureSlotPrefab();
        FaceAssetTabUI tabPrefab = EnsureTabPrefab();
        if (slotPrefab == null || tabPrefab == null)
        {
            Debug.LogError("Face asset library setup failed: prefab creation failed.");
            return;
        }

        SetSerializedBool(customizer, "startWithPartsHidden", true);
        DisableOldPartSelectors(chooseView.transform);
        CreateOrReplaceLibraryPanel(chooseView.transform, customizer, tabPrefab, slotPrefab);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (openedScene)
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        string message = "ChooseView face asset library configured.";
        Debug.Log(message);
        if (showDialog)
        {
            EditorUtility.DisplayDialog("CiGA", message, "OK");
        }
    }

    private static Scene GetLoadedTargetScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path == ScenePath)
            {
                return scene;
            }
        }

        return default;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs/UI", "FaceAssetLibrary");
        }
    }

    private static FaceAssetSlotUI EnsureSlotPrefab()
    {
        FaceAssetSlotUI existing = AssetDatabase.LoadAssetAtPath<FaceAssetSlotUI>(SlotPrefabPath);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = CreateUIObject("FaceAssetSlot", null);
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(72f, 72f);

        Image background = root.AddComponent<Image>();
        background.color = new Color(0.12f, 0.12f, 0.12f, 0.78f);

        Button button = root.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        GameObject selected = CreateUIObject("SelectedIndicator", rootRt);
        RectTransform selectedRt = selected.GetComponent<RectTransform>();
        Stretch(selectedRt, 0f, 0f, 0f, 0f);
        Image selectedImage = selected.AddComponent<Image>();
        selectedImage.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        selected.SetActive(false);

        GameObject icon = CreateUIObject("Icon", rootRt);
        RectTransform iconRt = icon.GetComponent<RectTransform>();
        Stretch(iconRt, 8f, 8f, 8f, 8f);
        Image iconImage = icon.AddComponent<Image>();
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        FaceAssetSlotUI slot = root.AddComponent<FaceAssetSlotUI>();
        SetSerializedReference(slot, "button", button);
        SetSerializedReference(slot, "iconImage", iconImage);
        SetSerializedReference(slot, "selectedIndicator", selectedImage);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
        Object.DestroyImmediate(root);
        return prefab != null ? prefab.GetComponent<FaceAssetSlotUI>() : null;
    }

    private static FaceAssetTabUI EnsureTabPrefab()
    {
        FaceAssetTabUI existing = AssetDatabase.LoadAssetAtPath<FaceAssetTabUI>(TabPrefabPath);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = CreateUIObject("FaceAssetTab", null);
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(78f, 34f);

        Image background = root.AddComponent<Image>();
        background.color = new Color(0.16f, 0.16f, 0.16f, 0.82f);
        Button button = root.AddComponent<Button>();

        GameObject indicator = CreateUIObject("SelectedIndicator", rootRt);
        RectTransform indicatorRt = indicator.GetComponent<RectTransform>();
        indicatorRt.anchorMin = new Vector2(0f, 0f);
        indicatorRt.anchorMax = new Vector2(1f, 0f);
        indicatorRt.pivot = new Vector2(0.5f, 0f);
        indicatorRt.anchoredPosition = Vector2.zero;
        indicatorRt.sizeDelta = new Vector2(0f, 4f);
        Image indicatorImage = indicator.AddComponent<Image>();
        indicatorImage.color = new Color(0.2f, 0.8f, 1f, 1f);
        indicator.SetActive(false);

        GameObject label = CreateUIObject("Label", rootRt);
        RectTransform labelRt = label.GetComponent<RectTransform>();
        Stretch(labelRt, 4f, 4f, 4f, 4f);
        Text text = label.AddComponent<Text>();
        text.text = "Tab";
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontSize = 16;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.raycastTarget = false;

        FaceAssetTabUI tab = root.AddComponent<FaceAssetTabUI>();
        SetSerializedReference(tab, "button", button);
        SetSerializedReference(tab, "labelText", text);
        SetSerializedReference(tab, "selectedIndicator", indicatorImage);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TabPrefabPath);
        Object.DestroyImmediate(root);
        return prefab != null ? prefab.GetComponent<FaceAssetTabUI>() : null;
    }

    private static void CreateOrReplaceLibraryPanel(Transform chooseView, CharacterCustomizer2D customizer, FaceAssetTabUI tabPrefab, FaceAssetSlotUI slotPrefab)
    {
        Transform existing = chooseView.Find("FaceAssetLibraryPanel");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject panel = CreateUIObject("FaceAssetLibraryPanel", chooseView);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 0f);
        panelRt.anchorMax = new Vector2(1f, 0f);
        panelRt.pivot = new Vector2(1f, 0f);
        panelRt.anchoredPosition = new Vector2(-30f, 30f);
        panelRt.sizeDelta = new Vector2(520f, 300f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.05f, 0.05f, 0.82f);

        GameObject tabBar = CreateUIObject("TabBar", panelRt);
        RectTransform tabBarRt = tabBar.GetComponent<RectTransform>();
        tabBarRt.anchorMin = new Vector2(0f, 1f);
        tabBarRt.anchorMax = new Vector2(1f, 1f);
        tabBarRt.pivot = new Vector2(0.5f, 1f);
        tabBarRt.anchoredPosition = new Vector2(0f, -8f);
        tabBarRt.sizeDelta = new Vector2(-16f, 38f);
        HorizontalLayoutGroup tabLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 6f;
        tabLayout.childControlWidth = false;
        tabLayout.childControlHeight = false;
        tabLayout.childForceExpandWidth = false;
        tabLayout.childForceExpandHeight = false;
        tabLayout.childAlignment = TextAnchor.MiddleLeft;

        GameObject scrollView = CreateUIObject("AssetScrollView", panelRt);
        RectTransform scrollRt = scrollView.GetComponent<RectTransform>();
        Stretch(scrollRt, 10f, 10f, 10f, 54f);
        Image scrollImage = scrollView.AddComponent<Image>();
        scrollImage.color = new Color(0f, 0f, 0f, 0.18f);
        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 24f;

        GameObject viewport = CreateUIObject("Viewport", scrollRt);
        RectTransform viewportRt = viewport.GetComponent<RectTransform>();
        Stretch(viewportRt, 0f, 0f, 0f, 0f);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = CreateUIObject("Content", viewportRt);
        RectTransform contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(72f, 72f);
        grid.spacing = new Vector2(8f, 8f);
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;

        FaceAssetLibraryUI library = panel.AddComponent<FaceAssetLibraryUI>();
        SetSerializedReference(library, "customizer", customizer);
        SetSerializedReference(library, "tabParent", tabBar.transform);
        SetSerializedReference(library, "tabPrefab", tabPrefab);
        SetSerializedReference(library, "itemParent", content.transform);
        SetSerializedReference(library, "slotPrefab", slotPrefab);
    }

    private static void DisableOldPartSelectors(Transform chooseView)
    {
        PartSelectorUI[] selectors = chooseView.GetComponentsInChildren<PartSelectorUI>(true);
        foreach (PartSelectorUI selector in selectors)
        {
            selector.gameObject.SetActive(false);
            EditorUtility.SetDirty(selector.gameObject);
        }
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        if (parent != null)
        {
            rt.SetParent(parent, false);
        }

        return go;
    }

    private static void Stretch(RectTransform rt, float left, float right, float bottom, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject found = FindChildRecursive(root.transform, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static GameObject FindChildRecursive(Transform transform, string objectName)
    {
        if (transform.name == objectName)
        {
            return transform.gameObject;
        }

        foreach (Transform child in transform)
        {
            GameObject found = FindChildRecursive(child, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component.gameObject.scene == scene && !EditorUtility.IsPersistent(component))
            {
                return component;
            }
        }

        return null;
    }

    private static void SetSerializedReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"Serialized property {propertyName} was not found on {target.name}.");
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetSerializedBool(Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"Serialized property {propertyName} was not found on {target.name}.");
            return;
        }

        property.boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
