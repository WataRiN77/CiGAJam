using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-9000)]
public class SceneBStateJsonSaver : MonoBehaviour
{
    public static SceneBStateJsonSaver Instance { get; private set; }

    [Header("Save")]
    [SerializeField] private string saveDirectory = @"C:\CiGAJam";
    [SerializeField] private string saveFileName = "SceneBState.json";
    [SerializeField] private bool saveOnStart = true;
    [SerializeField] private bool saveOnChange = true;
    [SerializeField] private bool resetRuntimeStateOnInitialJsonLoad = true;
    [SerializeField] private bool logSavePath;

    [Header("References")]
    [SerializeField] private SeededNpcSpawnManager npcSpawnManager;
    [SerializeField] private GameSessionManager gameSessionManager;

    private readonly Dictionary<int, NpcRuntimeState> npcStates = new Dictionary<int, NpcRuntimeState>();
    private SceneBStateSaveData cachedState;
    private string SavePath => Path.Combine(saveDirectory, saveFileName);

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (npcSpawnManager == null)
        {
            npcSpawnManager = FindObjectOfType<SeededNpcSpawnManager>();
        }

        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance != null
                ? GameSessionManager.Instance
                : FindObjectOfType<GameSessionManager>();
        }

        RefreshNpcListFromScene();

        // 恢复原生逻辑：检测本地状态并正常加载缓存
        if (saveOnStart)
        {
            if (npcStates.Count > 0 || !TryReadState(out SceneBStateSaveData state) || !HasValidSeedConfig(state))
            {
                SaveNow();
            }
            else
            {
                cachedState = state;
                LoadNpcStatesIntoCache(state);
            }
        }
    }

    public bool TryGetInitialJsonForSpawn(out SceneBStateSaveData state)
    {
        EnsureReferences(null);

        if (TryReadState(out state) && HasValidSeedConfig(state))
        {
            if (resetRuntimeStateOnInitialJsonLoad)
            {
                ResetRuntimeState(state);
                WriteState(state);
            }

            cachedState = state;
            LoadNpcStatesIntoCache(state);
            return true;
        }

        return false;
    }

    public bool WriteInitialConfigFromFaceManager(int[] npcSeeds, int murdererSeed, int mapNumber, SeededNpcSpawnManager spawner = null)
    {
        EnsureReferences(spawner);

        if (spawner == null)
        {
            spawner = npcSpawnManager;
        }

        if (spawner == null)
        {
            return false;
        }

        if ((npcSeeds == null || npcSeeds.Length == 0) && murdererSeed < 0)
        {
            return false;
        }

        SceneBStateSaveData state = CreateInitialState(spawner, npcSeeds, murdererSeed >= 0 ? murdererSeed : (int?)null, mapNumber);
        cachedState = state;
        LoadNpcStatesIntoCache(state);
        WriteState(state);
        return true;
    }

    public bool TryReadState(out SceneBStateSaveData state)
    {
        state = null;

        if (!File.Exists(SavePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            state = JsonUtility.FromJson<SceneBStateSaveData>(json);
            return state != null;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to read SceneB state from '{SavePath}'. {exception.Message}", this);
            return false;
        }
    }

    public void RefreshNpcListFromScene()
    {
        SeededNpcIdentity[] identities = FindObjectsOfType<SeededNpcIdentity>(true);

        for (int i = 0; i < identities.Length; i++)
        {
            RegisterNpc(identities[i]);
        }
    }

    public void RegisterNpc(SeededNpcIdentity identity)
    {
        if (identity == null)
        {
            return;
        }

        int seed = identity.NpcSeed;

        if (!npcStates.TryGetValue(seed, out NpcRuntimeState state))
        {
            state = new NpcRuntimeState();
            npcStates.Add(seed, state);
        }

        state.seed = seed;
        state.positionSeed = identity.PositionSeed;
        state.initialPosition = SerializableVector3.FromVector3(identity.InitialPosition);
        state.isMurderer = identity.IsMurderer;
    }

    public void SetNpcFollowing(Transform npcRoot, bool isFollowing)
    {
        SetNpcState(npcRoot, state => state.isTracked = isFollowing);
    }

    public void ClearFollowing()
    {
        foreach (NpcRuntimeState state in npcStates.Values)
        {
            state.isTracked = false;
        }

        SaveIfNeeded();
    }

    public void SetNpcMarked(Transform npcRoot, bool isMarked)
    {
        SetNpcState(npcRoot, state => state.isMarked = isMarked);
    }

    public void SetNpcShot(Transform npcRoot, bool isShot)
    {
        SetNpcState(npcRoot, state => state.isShot = isShot);
    }

    public void SaveNow()
    {
        RefreshNpcListFromScene();

        SceneBStateSaveData saveData = BuildSaveData();
        cachedState = saveData;
        WriteState(saveData);
    }

    private void WriteState(SceneBStateSaveData saveData)
    {
        string compactJson = JsonUtility.ToJson(saveData, false);

        // 诊断数据是否正常
        Debug.Log($"[Saver-Debug] 正在进行状态打包。当前统计路人总数: {saveData.npcs?.Length ?? 0}, 嫌疑人种子: {saveData.murdererSeed}, 开枪数: {saveData.shotsFired}");

        if (Photon.Pun.PhotonNetwork.InRoom && AsymmetricSyncManager.Instance != null)
        {
            Debug.Log($"[Saver-Debug] 联网状态正常。正在通过网络发送 JSON 数据包 (大小: {compactJson.Length} 字节)...");
            AsymmetricSyncManager.Instance.SendSceneBStateToArtist(compactJson);
        }
        else
        {
            Debug.LogWarning($"[Saver-Debug] 警告：未执行网络同步发送。是否在房间中: {Photon.Pun.PhotonNetwork.InRoom}, SyncManager是否存在: {AsymmetricSyncManager.Instance != null}");
        }

        try
        {
            Directory.CreateDirectory(saveDirectory);
            string prettyJson = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(SavePath, prettyJson);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Saver-Debug] 磁盘备份保存失败: {exception.Message}", this);
        }
    }

    private SceneBStateSaveData BuildSaveData()
    {
        SceneBStateSaveData saveData = new SceneBStateSaveData
        {
            mapNumber = gameSessionManager != null ? gameSessionManager.SelectedTerrainNumber : 0,
            npcSeeds = GetKnownNpcSeeds(),
            murdererSeed = GetKnownMurdererSeed(),
            shotsFired = gameSessionManager != null ? gameSessionManager.ShotsFired : 0,
            gameEnded = gameSessionManager != null && gameSessionManager.HasEnded,
            gameSucceeded = gameSessionManager != null && gameSessionManager.LastGameSucceeded
        };

        List<NpcRuntimeState> states = new List<NpcRuntimeState>(npcStates.Values);
        states.Sort((a, b) => a.seed.CompareTo(b.seed));
        saveData.npcs = states.ToArray();
        return saveData;
    }

    private SceneBStateSaveData CreateInitialState(SeededNpcSpawnManager spawner, int[] npcSeeds, int? murdererSeed, int mapNumber)
    {
        int[] finalSeeds = spawner.BuildFinalSpawnSeedList(npcSeeds, murdererSeed);
        Vector3[] positions = spawner.GetInitialPointsForSeeds(npcSeeds, murdererSeed);
        NpcRuntimeState[] npcs = new NpcRuntimeState[finalSeeds.Length];

        for (int i = 0; i < finalSeeds.Length; i++)
        {
            int seed = finalSeeds[i];
            npcs[i] = new NpcRuntimeState
            {
                seed = seed,
                positionSeed = seed,
                initialPosition = SerializableVector3.FromVector3(i < positions.Length ? positions[i] : Vector3.zero),
                isMurderer = murdererSeed.HasValue && seed == murdererSeed.Value,
                isTracked = false,
                isShot = false,
                isMarked = false
            };
        }

        return new SceneBStateSaveData
        {
            mapNumber = Mathf.Max(1, mapNumber),
            npcSeeds = finalSeeds,
            murdererSeed = murdererSeed ?? -1,
            shotsFired = 0,
            gameEnded = false,
            gameSucceeded = false,
            npcs = npcs
        };
    }

    private void SetNpcState(Transform npcRoot, Action<NpcRuntimeState> apply)
    {
        SeededNpcIdentity identity = GetIdentity(npcRoot);

        if (identity == null)
        {
            return;
        }

        RegisterNpc(identity);

        if (npcStates.TryGetValue(identity.NpcSeed, out NpcRuntimeState state))
        {
            apply?.Invoke(state);
        }

        SaveIfNeeded();
    }

    private void SaveIfNeeded()
    {
        if (saveOnChange)
        {
            SaveNow();
        }
    }

    private static SeededNpcIdentity GetIdentity(Transform npcRoot)
    {
        if (npcRoot == null)
        {
            return null;
        }

        SeededNpcIdentity identity = npcRoot.GetComponent<SeededNpcIdentity>();
        return identity != null ? identity : npcRoot.GetComponentInParent<SeededNpcIdentity>();
    }

    private void EnsureReferences(SeededNpcSpawnManager spawner)
    {
        if (npcSpawnManager == null)
        {
            npcSpawnManager = spawner != null ? spawner : FindObjectOfType<SeededNpcSpawnManager>();
        }

        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance != null
                ? GameSessionManager.Instance
                : FindObjectOfType<GameSessionManager>();
        }
    }

    private bool HasValidSeedConfig(SceneBStateSaveData state)
    {
        return state != null && state.npcSeeds != null && state.npcSeeds.Length > 0;
    }

    private void ResetRuntimeState(SceneBStateSaveData state)
    {
        if (state == null)
        {
            return;
        }

        state.shotsFired = 0;
        state.gameEnded = false;
        state.gameSucceeded = false;

        if (state.npcs == null)
        {
            return;
        }

        for (int i = 0; i < state.npcs.Length; i++)
        {
            if (state.npcs[i] == null)
            {
                continue;
            }

            state.npcs[i].isTracked = false;
            state.npcs[i].isShot = false;
            state.npcs[i].isMarked = false;
        }
    }

    private void LoadNpcStatesIntoCache(SceneBStateSaveData state)
    {
        npcStates.Clear();

        if (state == null || state.npcs == null)
        {
            return;
        }

        for (int i = 0; i < state.npcs.Length; i++)
        {
            NpcRuntimeState npc = state.npcs[i];

            if (npc != null && !npcStates.ContainsKey(npc.seed))
            {
                npcStates.Add(npc.seed, npc);
            }
        }
    }

    private int[] GetKnownNpcSeeds()
    {
        if (SeededNpcSpawnManager.PendingNpcSeeds != null && SeededNpcSpawnManager.PendingNpcSeeds.Length > 0)
        {
            return SeededNpcSpawnManager.PendingNpcSeeds;
        }

        if (cachedState != null && cachedState.npcSeeds != null && cachedState.npcSeeds.Length > 0)
        {
            return cachedState.npcSeeds;
        }

        List<int> seeds = new List<int>(npcStates.Keys);
        seeds.Sort();
        return seeds.ToArray();
    }

    private int GetKnownMurdererSeed()
    {
        if (SeededNpcSpawnManager.PendingMurdererSeed.HasValue)
        {
            return SeededNpcSpawnManager.PendingMurdererSeed.Value;
        }

        if (cachedState != null && cachedState.murdererSeed >= 0)
        {
            return cachedState.murdererSeed;
        }

        foreach (NpcRuntimeState state in npcStates.Values)
        {
            if (state != null && state.isMurderer)
            {
                return state.seed;
            }
        }

        return -1;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(saveDirectory))
        {
            saveDirectory = @"C:\CiGAJam";
        }

        if (string.IsNullOrWhiteSpace(saveFileName))
        {
            saveFileName = "SceneBState.json";
        }
    }
}

[Serializable]
public class SceneBStateSaveData
{
    public int mapNumber;
    public int[] npcSeeds;
    public int murdererSeed = -1;
    public int shotsFired;
    public bool gameEnded;
    public bool gameSucceeded;
    public NpcRuntimeState[] npcs;
}

[Serializable]
public class NpcRuntimeState
{
    public int seed;
    public int positionSeed;
    public SerializableVector3 initialPosition;
    public bool isMurderer;
    public bool isTracked;
    public bool isShot;
    public bool isMarked;
}

[Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public static SerializableVector3 FromVector3(Vector3 value)
    {
        return new SerializableVector3
        {
            x = value.x,
            y = value.y,
            z = value.z
        };
    }
}
