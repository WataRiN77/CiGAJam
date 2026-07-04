// WitnessStatementUI.cs
using System;
using System.Collections;
using Febucci.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WitnessStatementUI : MonoBehaviour
{
    public static event Action<int, int> OnStatementShown;

    [Header("证词列表")]
    [SerializeField] private GameObject statementPrefab;
    [SerializeField] private Transform statementParent;
    [SerializeField] private ScrollRect statementScrollRect;
    [SerializeField] private string nameTextName = "NameText";
    [SerializeField] private string contentTextName = "ContentText";
    [SerializeField] private string fallbackContentTextName = "ContextText";

    [Header("Auto Scroll")]
    [SerializeField] private bool autoScrollToBottom = true;

    [Header("出现动画")]
    [SerializeField] private float firstStatementDelay = 3f;
    [SerializeField] private float slideOffsetY = 80f;
    [SerializeField] private float slideDuration = 0.35f;

    [Header("正文延迟")]
    [SerializeField] private string pendingContentText = "...";
    [SerializeField] private float revealDelayMin = 2f;
    [SerializeField] private float revealDelayMax = 3f;

    private float startTime;
    private bool hasScheduledStatement;
    private int shownStatementCount;

    private void Awake()
    {
        startTime = Time.time;
        EnsureScrollRect();
    }

    private void Start()
    {
        if (FaceCustomizationGameManager.Instance == null) return;
        FaceCustomizationGameManager.Instance.OnNewStatement += AddStatement;

        var existing = FaceCustomizationGameManager.Instance.GetStatements();
        if (existing != null)
        {
            foreach (var stmt in existing)
                AddStatement(stmt);
        }
    }

    private void OnDestroy()
    {
        if (FaceCustomizationGameManager.Instance != null)
            FaceCustomizationGameManager.Instance.OnNewStatement -= AddStatement;
    }

    private void AddStatement(WitnessStatement statement)
    {
        if (statementPrefab == null || statementParent == null) return;

        float delay = 0f;
        if (!hasScheduledStatement)
            delay = Mathf.Max(0f, firstStatementDelay - (Time.time - startTime));

        hasScheduledStatement = true;
        StartCoroutine(AddStatementAfterDelay(statement, delay));
    }

    private IEnumerator AddStatementAfterDelay(WitnessStatement statement, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        GameObject entry = CreateHiddenEntry();
        if (entry == null) yield break;

        CanvasGroup canvasGroup = entry.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = entry.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        Transform nameTr = FindChildByName(entry.transform, nameTextName);
        if (nameTr != null)
            SetPlainText(nameTr, statement.witnessName);

        Transform contentTr = FindContentText(entry.transform);
        if (contentTr != null)
            SetPendingContent(contentTr);

        entry.SetActive(true);
        if (contentTr != null)
            SetPendingContent(contentTr);

        Canvas.ForceUpdateCanvases();
        ForceRebuildStatementLayout();
        yield return ScrollToBottomNextFrame();

        yield return PlayStatementRoutine(entry, canvasGroup, contentTr, statement.content);
    }

    private GameObject CreateHiddenEntry()
    {
        bool prefabWasActive = statementPrefab.activeSelf;

        if (prefabWasActive)
            statementPrefab.SetActive(false);

        GameObject entry = Instantiate(statementPrefab, statementParent);

        if (prefabWasActive)
            statementPrefab.SetActive(true);

        entry.SetActive(false);
        return entry;
    }

    private IEnumerator PlayStatementRoutine(GameObject entry, CanvasGroup canvasGroup, Transform contentTr, string content)
    {
        RectTransform rectTransform = entry.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            Vector2 targetPosition = rectTransform.anchoredPosition;
            Vector2 startPosition = targetPosition + Vector2.down * slideOffsetY;

            rectTransform.anchoredPosition = startPosition;
            canvasGroup.alpha = 1f;

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, slideDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutCubic(t);
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
                yield return null;
            }

            rectTransform.anchoredPosition = targetPosition;
        }
        else
        {
            canvasGroup.alpha = 1f;
        }

        ScrollToBottom();
        NotifyStatementShown();

        float minDelay = Mathf.Max(0f, Mathf.Min(revealDelayMin, revealDelayMax));
        float maxDelay = Mathf.Max(minDelay, Mathf.Max(revealDelayMin, revealDelayMax));
        yield return new WaitForSeconds(UnityEngine.Random.Range(minDelay, maxDelay));

        if (contentTr != null)
        {
            ShowContentWithTypewriter(contentTr, content);
            Canvas.ForceUpdateCanvases();
            ForceRebuildStatementLayout();
            yield return ScrollToBottomNextFrame();
        }
    }

    private void ForceRebuildStatementLayout()
    {
        RectTransform parentRect = statementParent as RectTransform;
        if (parentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
    }

    private void EnsureScrollRect()
    {
        if (statementScrollRect != null || statementParent == null) return;
        statementScrollRect = statementParent.GetComponentInParent<ScrollRect>();
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        if (!autoScrollToBottom) yield break;

        yield return null;
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        if (!autoScrollToBottom) return;

        EnsureScrollRect();
        if (statementScrollRect == null) return;

        Canvas.ForceUpdateCanvases();
        ForceRebuildStatementLayout();
        statementScrollRect.verticalNormalizedPosition = 0f;
    }

    private void NotifyStatementShown()
    {
        shownStatementCount++;

        int totalCount = shownStatementCount;
        FaceCustomizationGameManager manager = FaceCustomizationGameManager.Instance;
        if (manager != null)
            totalCount = manager.TotalStatementCount;

        OnStatementShown?.Invoke(shownStatementCount, totalCount);
    }

    private Transform FindContentText(Transform root)
    {
        Transform content = FindChildByName(root, contentTextName);
        if (content == null && !string.IsNullOrEmpty(fallbackContentTextName))
            content = FindChildByName(root, fallbackContentTextName);
        return content;
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;
        if (root.name == childName) return root;

        foreach (Transform child in root)
        {
            Transform result = FindChildByName(child, childName);
            if (result != null) return result;
        }

        return null;
    }

    private void SetPendingContent(Transform textTransform)
    {
        TypewriterCore typewriter = textTransform.GetComponent<TypewriterCore>();
        if (typewriter != null)
        {
            if (typewriter.enabled)
                typewriter.StopShowingText();

            typewriter.enabled = false;
        }

        TAnimCore textAnimator = textTransform.GetComponent<TAnimCore>();
        if (textAnimator != null)
        {
            textAnimator.enabled = true;
            textAnimator.SetText(pendingContentText, false);
            ForceTextMeshUpdate(textTransform);
            return;
        }

        SetPlainText(textTransform, pendingContentText);
    }

    private void ShowContentWithTypewriter(Transform textTransform, string value)
    {
        string content = value ?? string.Empty;
        TAnimCore textAnimator = textTransform.GetComponent<TAnimCore>();
        TypewriterCore typewriter = textTransform.GetComponent<TypewriterCore>();

        if (textAnimator != null)
            textAnimator.enabled = true;

        if (typewriter != null)
        {
            typewriter.enabled = true;
            typewriter.ShowText(content);
            return;
        }

        SetPlainText(textTransform, content);
    }

    private void SetTextAnimatorEnabled(Transform textTransform, bool enabled)
    {
        TypewriterCore typewriter = textTransform.GetComponent<TypewriterCore>();
        if (typewriter != null)
        {
            if (typewriter.enabled)
                typewriter.StopShowingText();

            typewriter.enabled = enabled;
        }

        TAnimCore textAnimator = textTransform.GetComponent<TAnimCore>();
        if (textAnimator != null)
            textAnimator.enabled = enabled;
    }

    private void SetPlainText(Transform textTransform, string value)
    {
        TMP_Text text = textTransform.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.maxVisibleCharacters = int.MaxValue;
            text.text = value ?? string.Empty;
            text.SetAllDirty();
            text.ForceMeshUpdate(true, true);
        }
    }

    private void ForceTextMeshUpdate(Transform textTransform)
    {
        TMP_Text text = textTransform.GetComponent<TMP_Text>();
        if (text == null) return;

        text.maxVisibleCharacters = int.MaxValue;
        text.SetAllDirty();
        text.ForceMeshUpdate(true, true);
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}
