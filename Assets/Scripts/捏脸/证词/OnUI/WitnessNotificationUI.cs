using System.Collections;
using TMPro;
using UnityEngine;

public class WitnessNotificationUI : MonoBehaviour
{
    [SerializeField] private TMP_Text notificationText;
    [SerializeField] private GameObject notificationRoot;
    [SerializeField] private string newStatementMessage = "接收到新证词";
    [SerializeField] private string allCollectedMessage = "你已收集所有证词";
    [SerializeField] private float messageDuration = 3f;

    private Coroutine messageRoutine;
    private bool allCollectedShown;

    private void Awake()
    {
        if (notificationRoot == null && notificationText != null)
            notificationRoot = notificationText.gameObject;

        HideNotification();
    }

    private void OnEnable()
    {
        WitnessStatementUI.OnStatementShown += HandleStatementShown;
    }

    private void OnDisable()
    {
        WitnessStatementUI.OnStatementShown -= HandleStatementShown;
    }

    private void HandleStatementShown(int shownCount, int totalCount)
    {
        if (allCollectedShown)
            return;

        if (messageRoutine != null)
            StopCoroutine(messageRoutine);

        bool isAllCollected = totalCount > 0 && shownCount >= totalCount;
        messageRoutine = StartCoroutine(ShowMessageRoutine(isAllCollected));
    }

    private IEnumerator ShowMessageRoutine(bool isAllCollected)
    {
        ShowNotification(newStatementMessage);
        yield return new WaitForSeconds(messageDuration);

        if (isAllCollected)
        {
            allCollectedShown = true;
            ShowNotification(allCollectedMessage);
            messageRoutine = null;
            yield break;
        }

        HideNotification();
        messageRoutine = null;
    }

    private void ShowNotification(string message)
    {
        if (notificationText != null)
            notificationText.text = message;

        if (notificationRoot != null)
            notificationRoot.SetActive(true);
    }

    private void HideNotification()
    {
        if (notificationRoot != null)
            notificationRoot.SetActive(false);
    }
}
