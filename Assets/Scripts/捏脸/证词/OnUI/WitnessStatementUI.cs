// WitnessStatementUI.cs
using TMPro;
using UnityEngine;

public class WitnessStatementUI : MonoBehaviour
{
    [SerializeField] private GameObject statementPrefab;
    [SerializeField] private Transform statementParent;
    [SerializeField] private string nameTextName = "NameText";       // 显示目击者姓名的 TMP 文本
    [SerializeField] private string contentTextName = "ContentText"; // 显示证词内容的 TMP 文本

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

        GameObject entry = Instantiate(statementPrefab, statementParent);

        Transform nameTr = entry.transform.Find(nameTextName);
        if (nameTr != null)
            SetText(nameTr, statement.witnessName);

        Transform contentTr = entry.transform.Find(contentTextName);
        if (contentTr != null)
            SetText(contentTr, statement.content);
    }

    private void SetText(Transform textTransform, string value)
    {
        TMP_Text text = textTransform.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.text = value;
        }
    }
}
