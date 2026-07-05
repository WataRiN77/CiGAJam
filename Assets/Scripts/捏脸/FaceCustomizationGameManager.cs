using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class FaceCustomizationGameManager : MonoBehaviour
{
    public static FaceCustomizationGameManager Instance { get; private set; }

    [Header("核心组件")]
    [SerializeField] private CharacterCustomizer2D customizer;
    [SerializeField] private AnswerGenerator answerGenerator;
    [SerializeField] private WitnessStatementGenerator statementGenerator;

    [Header("干扰项配置（可选）")]
    [SerializeField] private bool useDistractors = true;
    [SerializeField] private int defaultDistractorCount = 4;

    [Header("SceneB Json Output")]
    [SerializeField] private bool writeSceneBJsonOnNewRound = true;
    [SerializeField] private bool preferSyncedGameplaySeeds = true;
    [SerializeField] private int sceneMapCount = 4;
    [SerializeField] private SceneBStateJsonSaver sceneBStateJsonSaver;
    [SerializeField] private SeededNpcSpawnManager sceneBNpcSpawnManager;

    [Header("证词定时生成")]
    [SerializeField] private int totalStatementCount = 5;
    [SerializeField] private float statementInterval = 10f;
    [SerializeField] private int initialStatements = 1;
    public float StatementInterval => statementInterval;
    public int TotalStatementCount => totalStatementCount;

    private FaceSaveData currentAnswer;
    private List<WitnessStatement> witnessStatements = new List<WitnessStatement>();
    private List<FaceSaveData> currentDistractors = new List<FaceSaveData>();

    private Coroutine statementCoroutine;

    public event Action<WitnessStatement> OnNewStatement;
    public event Action<float> OnTimerProgress;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 开始新一局：生成目标答案、初始证词、干扰项，并启动定时证词生成。
    /// </summary>
    public FaceSaveData GenerateNewRound()
    {
        int[] authoritativeNpcSeeds = null;
        int authoritativeMurdererSeed = -1;
        bool hasAuthoritativeSeeds = TryGetAuthoritativeGameplaySeeds(out authoritativeNpcSeeds, out authoritativeMurdererSeed);

        currentAnswer = hasAuthoritativeSeeds
            ? answerGenerator.GenerateAnswerWithSeed(authoritativeMurdererSeed)
            : answerGenerator.GenerateAnswer();

        SaveAnswerToFile(currentAnswer);
        SyncCurrentAnswerForSettlement();

        if (useDistractors && currentAnswer != null)
        {
            currentDistractors = hasAuthoritativeSeeds
                ? GenerateDistractorsFromSyncedSeeds(authoritativeNpcSeeds, authoritativeMurdererSeed)
                : answerGenerator.GenerateDistractors(defaultDistractorCount, currentAnswer);
            SaveDistractorsToFile(currentDistractors);
        }
        else
        {
            currentDistractors.Clear();
        }

        WriteSceneBInitialJsonIfNeeded(authoritativeNpcSeeds, hasAuthoritativeSeeds ? authoritativeMurdererSeed : -1);

        UnityEngine.Random.State oldState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(currentAnswer.seed);

        witnessStatements.Clear();
        List<WitnessStatement> initial = statementGenerator.GenerateStatements(currentAnswer, initialStatements);
        witnessStatements.AddRange(initial);
        currentAnswer.witnessStatements = new List<WitnessStatement>(witnessStatements);

        UnityEngine.Random.state = oldState;

        if (statementCoroutine != null) StopCoroutine(statementCoroutine);
        statementCoroutine = StartCoroutine(GenerateStatementsOverTime());

        return currentAnswer;
    }

    /// <summary>
    /// 定时生成新证词，直到达到 totalStatementCount。
    /// </summary>
    private IEnumerator GenerateStatementsOverTime()
    {
        while (witnessStatements.Count < totalStatementCount)
        {
            float timer = 0f;

            while (timer < statementInterval && witnessStatements.Count < totalStatementCount)
            {
                timer += Time.deltaTime;
                float progress = timer / statementInterval;
                OnTimerProgress?.Invoke(progress);
                yield return null;
            }

            UnityEngine.Random.State oldState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(currentAnswer.seed + witnessStatements.Count * 100);

            WitnessStatement newStmt = statementGenerator.GenerateSingleStatement(currentAnswer, witnessStatements);
            if (newStmt != null)
            {
                witnessStatements.Add(newStmt);
                currentAnswer.witnessStatements.Add(newStmt);
                OnNewStatement?.Invoke(newStmt);
            }

            UnityEngine.Random.state = oldState;
        }

        OnTimerProgress?.Invoke(1f);
    }

    /// <summary>
    /// A 玩家提交捏脸，返回与答案的相似度分数（0~100）。
    /// </summary>
    public float SubmitCurrentFace()
    {
        if (currentAnswer == null)
        {
            Debug.LogError("尚未生成答案，请先调用 GenerateNewRound");
            return 0f;
        }

        string json = customizer.SaveToJson();
        FaceSaveData playerFace = JsonUtility.FromJson<FaceSaveData>(json);
        SavePlayerFaceToFile(playerFace);
        return FaceComparer.CompareFaces(currentAnswer, playerFace);
    }

    public FaceSaveData GetCurrentAnswer() => currentAnswer;
    public List<WitnessStatement> GetStatements() => witnessStatements;
    public List<FaceSaveData> GetDistractors() => currentDistractors;
    public int GetCurrentSeed() => currentAnswer != null ? currentAnswer.seed : -1;

    public List<int> GetAllSeeds()
    {
        List<int> seeds = new List<int>();
        if (currentAnswer != null) seeds.Add(currentAnswer.seed);
        if (useDistractors)
            foreach (var d in currentDistractors) seeds.Add(d.seed);
        return seeds;
    }

    private void SyncCurrentAnswerForSettlement()
    {
        if (currentAnswer == null)
        {
            return;
        }

        string answerJson = JsonUtility.ToJson(currentAnswer);
        GameSessionData.MurdererSeed = currentAnswer.seed;
        GameSessionData.SuspectCodename = $"Seed {currentAnswer.seed}";
        GameSessionData.SuspectFaceJson = answerJson;

        if (AsymmetricSyncManager.Instance != null)
        {
            AsymmetricSyncManager.Instance.BroadcastRoundAnswerData(currentAnswer.seed, answerJson);
        }
    }
    private bool TryGetAuthoritativeGameplaySeeds(out int[] npcSeeds, out int murdererSeed)
    {
        npcSeeds = null;
        murdererSeed = -1;

        if (!preferSyncedGameplaySeeds)
        {
            return false;
        }

        if (AsymmetricSyncManager.Instance != null &&
            AsymmetricSyncManager.Instance.SyncedNpcSeeds != null &&
            AsymmetricSyncManager.Instance.SyncedNpcSeeds.Length > 0 &&
            AsymmetricSyncManager.Instance.SyncedMurdererSeed >= 0)
        {
            npcSeeds = AsymmetricSyncManager.Instance.SyncedNpcSeeds;
            murdererSeed = AsymmetricSyncManager.Instance.SyncedMurdererSeed;
            return true;
        }

        if (SeededNpcSpawnManager.PendingNpcSeeds != null &&
            SeededNpcSpawnManager.PendingNpcSeeds.Length > 0 &&
            SeededNpcSpawnManager.PendingMurdererSeed.HasValue)
        {
            npcSeeds = SeededNpcSpawnManager.PendingNpcSeeds;
            murdererSeed = SeededNpcSpawnManager.PendingMurdererSeed.Value;
            return true;
        }

        return false;
    }

    private List<FaceSaveData> GenerateDistractorsFromSyncedSeeds(int[] npcSeeds, int murdererSeed)
    {
        List<FaceSaveData> distractors = new List<FaceSaveData>();

        if (npcSeeds == null)
        {
            return distractors;
        }

        for (int i = 0; i < npcSeeds.Length; i++)
        {
            int seed = npcSeeds[i];

            if (seed == murdererSeed)
            {
                continue;
            }

            FaceSaveData distractor = answerGenerator.GenerateAnswerWithSeed(seed);
            distractor.seed = seed;
            distractors.Add(distractor);
        }

        return distractors;
    }

    private void WriteSceneBInitialJsonIfNeeded(int[] authoritativeNpcSeeds, int authoritativeMurdererSeed)
    {
        if (!writeSceneBJsonOnNewRound || currentAnswer == null)
        {
            return;
        }

        SceneBStateJsonSaver saver = GetSceneBStateJsonSaver();

        if (saver == null)
        {
            Debug.LogWarning("FaceCustomizationGameManager could not find or create SceneBStateJsonSaver.");
            return;
        }

        List<int> seeds = authoritativeNpcSeeds != null && authoritativeNpcSeeds.Length > 0
            ? new List<int>(authoritativeNpcSeeds)
            : GetAllSeeds();

        if (!seeds.Contains(currentAnswer.seed))
        {
            seeds.Insert(0, currentAnswer.seed);
        }

        int murdererSeed = authoritativeMurdererSeed >= 0 ? authoritativeMurdererSeed : currentAnswer.seed;
        int mapNumber = GetAuthoritativeMapNumber();
        saver.WriteInitialConfigFromFaceManager(
            seeds.ToArray(),
            murdererSeed,
            mapNumber,
            GetSceneBNpcSpawnManager()
        );

        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.SetTerrainByNumber(mapNumber);
        }
    }

    private int GetAuthoritativeMapNumber()
    {
        if (preferSyncedGameplaySeeds &&
            AsymmetricSyncManager.Instance != null &&
            AsymmetricSyncManager.Instance.SyncedMapNumber >= 1)
        {
            return AsymmetricSyncManager.Instance.SyncedMapNumber;
        }

        return UnityEngine.Random.Range(1, Mathf.Max(1, sceneMapCount) + 1);
    }

    private SceneBStateJsonSaver GetSceneBStateJsonSaver()
    {
        if (sceneBStateJsonSaver != null)
        {
            return sceneBStateJsonSaver;
        }

        sceneBStateJsonSaver = SceneBStateJsonSaver.Instance != null
            ? SceneBStateJsonSaver.Instance
            : FindObjectOfType<SceneBStateJsonSaver>();

        if (sceneBStateJsonSaver != null)
        {
            return sceneBStateJsonSaver;
        }

        GameObject saverObject = new GameObject("SceneB State Json Saver");
        sceneBStateJsonSaver = saverObject.AddComponent<SceneBStateJsonSaver>();
        return sceneBStateJsonSaver;
    }

    private SeededNpcSpawnManager GetSceneBNpcSpawnManager()
    {
        if (sceneBNpcSpawnManager == null)
        {
            sceneBNpcSpawnManager = FindObjectOfType<SeededNpcSpawnManager>();
        }

        return sceneBNpcSpawnManager;
    }

    private void SaveAnswerToFile(FaceSaveData answer)
    {
        string path = Application.persistentDataPath + "/AnswerData/";
        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);
        string json = JsonUtility.ToJson(answer, true);
        System.IO.File.WriteAllText(path + "answer.json", json);
        Debug.Log($"答案保存至 {path}answer.json");
    }

    private void SavePlayerFaceToFile(FaceSaveData face)
    {
        string path = Application.persistentDataPath + "/PlayerData/";
        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);
        string json = JsonUtility.ToJson(face, true);
        System.IO.File.WriteAllText(path + "player_face.json", json);
    }

    private void SaveDistractorsToFile(List<FaceSaveData> distractors)
    {
        string path = Application.persistentDataPath + "/GameData/";
        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);
        string json = JsonUtility.ToJson(new DistractorList { items = distractors }, true);
        System.IO.File.WriteAllText(path + "distractors.json", json);
        Debug.Log($"干扰项保存至 {path}distractors.json");
    }

    [Serializable]
    private class DistractorList
    {
        public List<FaceSaveData> items;
    }

    /// <summary>
    /// 游戏结束时调用：保存当前捏脸、与答案比对、将相似值存入文件并返回。
    /// </summary>
    public float EndGameAndSubmit()
    {
        // 1. 提交当前捏脸并获取分数（内部会保存玩家脸部数据）
        float score = SubmitCurrentFace();

        // 2. 将分数保存到独立文件，供B侧读取
        SaveResultToFile(score);

        return score;
    }

    private void SaveResultToFile(float score)
    {
        string dir = Path.Combine(Application.persistentDataPath, "GameResult");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // 使用简单的JSON包装
        var result = new GameResultData { similarity = score };
        string json = JsonUtility.ToJson(result, true);
        File.WriteAllText(Path.Combine(dir, "result.json"), json);
        Debug.Log($"游戏结果已保存：相似度 {score}");
    }

    [Serializable]
    private class GameResultData
    {
        public float similarity;
    }

    private void OnValidate()
    {
        defaultDistractorCount = Mathf.Max(0, defaultDistractorCount);
        sceneMapCount = Mathf.Max(1, sceneMapCount);
    }
}
