// WitnessStatementUI.cs
using UnityEngine;
using UnityEngine.UI;

public class WitnessStatementUI : MonoBehaviour
{
    [SerializeField] private GameObject statementPrefab;
    [SerializeField] private Transform statementParent;
    [SerializeField] private string nameTextName = "NameText";       // 显示目击者姓名的Text
    [SerializeField] private string contentTextName = "ContentText"; // 显示证词内容的Text

    private void Start()
    {
        if (FaceCustomizationGameManager.Instance == null) return;
        FaceCustomizationGameManager.Instance.OnNewStatement += AddStatement;

        // 加载已存在的初始证词
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
            nameTr.GetComponent<Text>().text = statement.witnessName;

        Transform contentTr = entry.transform.Find(contentTextName);
        if (contentTr != null)
            contentTr.GetComponent<Text>().text = statement.content;
    }
}