using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LegacyTextToTmpMigrator
{
    private const string TargetScenePath = "Assets/Scenes/A_捏脸.unity";
    private static readonly string[] TargetPrefabPaths =
    {
        "Assets/Prefabs/UI/FaceAssetLibrary/FaceAssetTab.prefab",
        "Assets/Resources/Prefab/证词.prefab",
    };

    [MenuItem("Tools/CiGA/Convert A Scene Text To TMP")]
    public static void ConvertFromMenu()
    {
        int converted = ConvertACharacterSceneAndFaceAssetPrefabs();
        EditorUtility.DisplayDialog("TextMesh Pro Migration", $"Converted {converted} Legacy Text component(s).", "OK");
    }

    public static int ConvertACharacterSceneAndFaceAssetPrefabs()
    {
        int converted = 0;

        Scene previousScene = EditorSceneManager.GetActiveScene();
        bool previousSceneWasDirty = previousScene.IsValid() && previousScene.isDirty;

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        converted += ConvertLegacyTextsInScene(scene);
        RebindReferencesInScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        foreach (string prefabPath in TargetPrefabPaths)
        {
            converted += ConvertLegacyTextsInPrefab(prefabPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (previousScene.IsValid() && previousScene.path != TargetScenePath && !previousSceneWasDirty)
        {
            EditorSceneManager.OpenScene(previousScene.path, OpenSceneMode.Single);
        }

        return converted;
    }

    public static int NormalizeConvertedTextObjectNames()
    {
        int renamed = 0;

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            renamed += NormalizeConvertedTextObjectNamesUnder(root);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        foreach (string prefabPath in TargetPrefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            renamed += NormalizeConvertedTextObjectNamesUnder(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return renamed;
    }

    private static int ConvertLegacyTextsInScene(Scene scene)
    {
        int converted = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            converted += ConvertLegacyTextsUnder(root);
        }

        return converted;
    }

    private static int ConvertLegacyTextsInPrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        int converted = ConvertLegacyTextsUnder(root);
        RebindReferencesUnder(root);
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        return converted;
    }

    private static int ConvertLegacyTextsUnder(GameObject root)
    {
        int converted = 0;
        Text[] legacyTexts = root.GetComponentsInChildren<Text>(true);

        foreach (Text legacy in legacyTexts)
        {
            if (legacy == null)
            {
                continue;
            }

            TextSnapshot snapshot = new TextSnapshot(legacy);
            GameObject target = legacy.gameObject;
            Object.DestroyImmediate(legacy, true);

            TextMeshProUGUI tmp = target.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                tmp = target.AddComponent<TextMeshProUGUI>();
            }

            snapshot.ApplyTo(tmp);
            RenameConvertedTextObject(target);
            converted++;
        }

        return converted;
    }

    private static void RenameConvertedTextObject(GameObject target)
    {
        if (target.name.StartsWith("Text (Legacy)"))
        {
            target.name = target.name.Replace("Text (Legacy)", "Text (TMP)");
            EditorUtility.SetDirty(target);
        }
    }

    private static int NormalizeConvertedTextObjectNamesUnder(GameObject root)
    {
        int renamed = 0;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!text.gameObject.name.StartsWith("Text (Legacy)"))
            {
                continue;
            }

            RenameConvertedTextObject(text.gameObject);
            renamed++;
        }

        return renamed;
    }

    private static void RebindReferencesInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            RebindReferencesUnder(root);
        }
    }

    private static void RebindReferencesUnder(GameObject root)
    {
        foreach (FaceAssetTabUI tab in root.GetComponentsInChildren<FaceAssetTabUI>(true))
        {
            SetSerializedReference(tab, "labelText", FindPreferredText(tab.transform, "Label"));
        }

        foreach (PartSelectorUI selector in root.GetComponentsInChildren<PartSelectorUI>(true))
        {
            SetSerializedReference(selector, "indexText", selector.GetComponentInChildren<TMP_Text>(true));
        }

        foreach (WitnessTimerUI timer in root.GetComponentsInChildren<WitnessTimerUI>(true))
        {
            SetSerializedReference(timer, "timeText", FindPreferredText(timer.transform, "TimerText"));
        }

        foreach (CountdownTimerUI countdown in root.GetComponentsInChildren<CountdownTimerUI>(true))
        {
            TMP_Text text = countdown.GetComponent<TMP_Text>();
            if (text == null)
            {
                text = countdown.GetComponentInChildren<TMP_Text>(true);
            }

            SetSerializedReference(countdown, "tmpText", text);
            SetSerializedReference(countdown, "legacyText", null);
        }
    }

    private static TMP_Text FindPreferredText(Transform root, string preferredName)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text.name == preferredName)
            {
                return text;
            }
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    private static void SetSerializedReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private readonly struct TextSnapshot
    {
        private readonly string text;
        private readonly int fontSize;
        private readonly float lineSpacing;
        private readonly FontStyle fontStyle;
        private readonly TextAnchor alignment;
        private readonly bool richText;
        private readonly bool resizeTextForBestFit;
        private readonly int resizeTextMinSize;
        private readonly int resizeTextMaxSize;
        private readonly HorizontalWrapMode horizontalOverflow;
        private readonly VerticalWrapMode verticalOverflow;
        private readonly Color color;
        private readonly bool raycastTarget;
        private readonly bool maskable;
        private readonly bool enabled;

        public TextSnapshot(Text source)
        {
            text = source.text;
            fontSize = source.fontSize;
            lineSpacing = source.lineSpacing;
            fontStyle = source.fontStyle;
            alignment = source.alignment;
            richText = source.supportRichText;
            resizeTextForBestFit = source.resizeTextForBestFit;
            resizeTextMinSize = source.resizeTextMinSize;
            resizeTextMaxSize = source.resizeTextMaxSize;
            horizontalOverflow = source.horizontalOverflow;
            verticalOverflow = source.verticalOverflow;
            color = source.color;
            raycastTarget = source.raycastTarget;
            maskable = source.maskable;
            enabled = source.enabled;
        }

        public void ApplyTo(TextMeshProUGUI target)
        {
            target.text = text;
            target.fontSize = fontSize;
            target.lineSpacing = lineSpacing;
            target.fontStyle = ConvertFontStyle(fontStyle);
            target.alignment = ConvertAlignment(alignment);
            target.richText = richText;
            target.enableAutoSizing = resizeTextForBestFit;
            target.fontSizeMin = resizeTextMinSize;
            target.fontSizeMax = resizeTextMaxSize;
            target.enableWordWrapping = horizontalOverflow == HorizontalWrapMode.Wrap;
            target.overflowMode = verticalOverflow == VerticalWrapMode.Overflow || horizontalOverflow == HorizontalWrapMode.Overflow
                ? TextOverflowModes.Overflow
                : TextOverflowModes.Truncate;
            target.color = color;
            target.raycastTarget = raycastTarget;
            target.maskable = maskable;
            target.enabled = enabled;

            if (TMP_Settings.defaultFontAsset != null)
            {
                target.font = TMP_Settings.defaultFontAsset;
            }
        }
    }

    private static FontStyles ConvertFontStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold:
                return FontStyles.Bold;
            case FontStyle.Italic:
                return FontStyles.Italic;
            case FontStyle.BoldAndItalic:
                return FontStyles.Bold | FontStyles.Italic;
            default:
                return FontStyles.Normal;
        }
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft:
                return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter:
                return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight:
                return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft:
                return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter:
                return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight:
                return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft:
                return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter:
                return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight:
                return TextAlignmentOptions.BottomRight;
            default:
                return TextAlignmentOptions.Center;
        }
    }
}
