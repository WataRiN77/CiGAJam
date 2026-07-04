using UnityEngine;

public class FaceCustomizationGameManager : MonoBehaviour
{
    public static FaceCustomizationGameManager Instance { get; private set; }

    [Header("核心组件")]
    [SerializeField] private CharacterCustomizer2D customizer;  // A玩家的捏脸系统
    [SerializeField] private AnswerGenerator answerGenerator;    // 答案生成器

    // 运行时数据
    private FaceSaveData currentAnswer;   // 当前正确答案
    private FaceSaveData playerFace;      // 玩家提交的捏脸

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            GenerateNewAnswer();
        }
        else Destroy(gameObject);
    }

    /// <summary>
    /// 生成新一局的答案并保存到本地文件。返回生成的答案数据（供B侧直接使用或网络发送）。
    /// </summary>
    public FaceSaveData GenerateNewAnswer()
    {
        currentAnswer = answerGenerator.GenerateAnswer();
        SaveAnswerToFile(currentAnswer);
        return currentAnswer;
    }

    /// <summary>
    /// 提交当前捏脸数据，进行对比并返回百分制得分。
    /// </summary>
    public float SubmitCurrentFace()
    {
        if (currentAnswer == null)
        {
            Debug.LogError("尚未生成答案，请先调用 GenerateNewAnswer");
            return 0f;
        }

        // 获取当前玩家捏脸
        string json = customizer.SaveToJson();
        playerFace = JsonUtility.FromJson<FaceSaveData>(json);

        // 保存玩家数据（可选，用于复盘）
        SavePlayerFaceToFile(playerFace);

        // 计算并返回得分
        return FaceComparer.CompareFaces(currentAnswer, playerFace);
    }

    /// <summary>
    /// 获取当前答案（可被其他脚本或网络模块调用）
    /// </summary>
    public FaceSaveData GetCurrentAnswer() => currentAnswer;

    // ---- 文件存储（本地调试 + B侧接口） ----

    private void SaveAnswerToFile(FaceSaveData answer)
    {
        string path = Application.persistentDataPath + "/AnswerData/";
        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);
        string json = JsonUtility.ToJson(answer, true);
        System.IO.File.WriteAllText(path + "answer.json", json);
        Debug.Log($"答案已保存至 {path}answer.json");
    }

    private void SavePlayerFaceToFile(FaceSaveData face)
    {
        string path = Application.persistentDataPath + "/PlayerData/";
        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);
        string json = JsonUtility.ToJson(face, true);
        System.IO.File.WriteAllText(path + "player_face.json", json);
    }

    /// <summary>
    /// 静态方法：从本地文件加载答案（供B侧或外部测试使用）。
    /// 联机时，此方法可替换为网络请求。
    /// </summary>
    public static FaceSaveData LoadAnswerFromFile()
    {
        string filePath = Application.persistentDataPath + "/AnswerData/answer.json";
        if (System.IO.File.Exists(filePath))
        {
            string json = System.IO.File.ReadAllText(filePath);
            return JsonUtility.FromJson<FaceSaveData>(json);
        }
        return null;
    }
}