using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SceneBStateJsonSaver : MonoBehaviour
{
    public static SceneBStateJsonSaver Instance { get; private set; }

    [Header("Save")]
    [SerializeField] private string saveDirectory = @"C:\CiGAJam";
    [SerializeField] private string saveFileName = "SceneBState.json";
    [SerializeField] private bool saveOnStart = true;
    [SerializeField] private bool saveOnChange = true;
    [SerializeField] private bool logSavePath;

    [Header("References")]
    [SerializeField] private SeededNpcSpawnManager npcSpawnManager;
    [SerializeField] private GameSessionManager gameSessionManager;

    private readonly Dictionary<int, NpcRuntimeState> npcStates = new Dictionary<int, NpcRuntimeState>();
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

        if (saveOnStart)
        {
            SaveNow();
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
        string json = JsonUtility.ToJson(saveData, true);

        try
        {
            Directory.CreateDirectory(saveDirectory);
            File.WriteAllText(SavePath, json);

            if (logSavePath)
            {
                Debug.Log($"SceneB state saved to: {SavePath}", this);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to save SceneB state to '{SavePath}'. {exception.Message}", this);
        }
    }

    private SceneBStateSaveData BuildSaveData()
    {
        SceneBStateSaveData saveData = new SceneBStateSaveData
        {
            mapNumber = gameSessionManager != null ? gameSessionManager.SelectedTerrainNumber : 0,
            shotsFired = gameSessionManager != null ? gameSessionManager.ShotsFired : 0,
            gameEnded = gameSessionManager != null && gameSessionManager.HasEnded,
            gameSucceeded = gameSessionManager != null && gameSessionManager.LastGameSucceeded
        };

        List<NpcRuntimeState> states = new List<NpcRuntimeState>(npcStates.Values);
        states.Sort((a, b) => a.seed.CompareTo(b.seed));
        saveData.npcs = states.ToArray();
        return saveData;
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
