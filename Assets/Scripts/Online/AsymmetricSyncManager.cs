using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

public class AsymmetricSyncManager : MonoBehaviourPunCallbacks
{
    public static AsymmetricSyncManager Instance { get; private set; }

    [Header("Role State")]
    public bool isPlayerA_Artist = false;

    [Header("Runtime Face Binding")]
    private CharacterCustomizer2D activeCustomizer;

    public int[] SyncedNpcSeeds { get; private set; }
    public int SyncedMurdererSeed { get; private set; } = -1;
    public int SyncedMapNumber { get; private set; } = -1;
    public string SyncedMurdererFaceJson { get; private set; } = "";
    public string LatestArtistFaceJson { get; private set; } = "";

    private bool isLoadingResultScene;

    private void Awake()
    {
        Debug.Log($"[Sync-Awake] AsymmetricSyncManager started on '{gameObject.name}'.");

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RegisterLocalCustomizer(CharacterCustomizer2D customizer)
    {
        if (customizer == null)
        {
            Debug.LogWarning("[Sync] Refused to register a null CharacterCustomizer2D.");
            return;
        }

        if (activeCustomizer != null)
        {
            activeCustomizer.OnFaceChanged -= OnLocalFaceChanged;
        }

        activeCustomizer = customizer;
        activeCustomizer.OnFaceChanged += OnLocalFaceChanged;

        if (!isPlayerA_Artist)
        {
            if (!string.IsNullOrEmpty(LatestArtistFaceJson))
            {
                activeCustomizer.LoadFromJson(LatestArtistFaceJson);
            }
            else
            {
                LatestArtistFaceJson = activeCustomizer.SaveToJson();
            }

            GameSessionData.ArtistFaceJson = LatestArtistFaceJson;
        }

        Debug.Log($"[Sync] Registered local customizer '{customizer.gameObject.name}'. isPlayerA={isPlayerA_Artist}");
    }

    private void OnLocalFaceChanged()
    {
        if (activeCustomizer == null)
        {
            return;
        }

        string faceJson = activeCustomizer.SaveToJson();

        if (isPlayerA_Artist)
        {
            LatestArtistFaceJson = faceJson;
            GameSessionData.ArtistFaceJson = faceJson;
            SendFullFaceSync(faceJson);
        }
        else
        {
            LatestArtistFaceJson = faceJson;
            GameSessionData.ArtistFaceJson = faceJson;
        }
    }

    public void BroadcastSeeds(int[] npcSeeds, int murdererSeed)
    {
        BroadcastSeeds(npcSeeds, murdererSeed, -1);
    }

    public void BroadcastSeeds(int[] npcSeeds, int murdererSeed, int mapNumber)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log($"[Sync] Broadcasting seeds. npcCount={npcSeeds?.Length ?? 0}, murdererSeed={murdererSeed}, map={mapNumber}");
            photonView.RPC(nameof(RPC_SyncGameplaySeeds), RpcTarget.AllBuffered, npcSeeds, murdererSeed, mapNumber);
        }
        else
        {
            Debug.LogWarning("[Sync] Non-master client tried to broadcast seeds.");
        }
    }

    [PunRPC]
    private void RPC_SyncGameplaySeeds(int[] npcSeeds, int murdererSeed, int mapNumber)
    {
        isLoadingResultScene = false;
        SyncedNpcSeeds = npcSeeds;
        SyncedMurdererSeed = murdererSeed;
        SyncedMapNumber = mapNumber;
        GameSessionData.MurdererSeed = murdererSeed;
        GameSessionData.SuspectCodename = murdererSeed >= 0 ? $"Seed {murdererSeed}" : "Unknown";

        SeededNpcSpawnManager.SetPendingSeeds(npcSeeds, murdererSeed);

        if (mapNumber >= 1)
        {
            GameSessionManager.Instance?.SetTerrainByNumber(mapNumber);
        }

        Debug.Log($"[Sync] Received gameplay seeds. npcCount={npcSeeds?.Length ?? 0}, murdererSeed={murdererSeed}, map={mapNumber}");
    }

    public void BroadcastRoundAnswerData(int murdererSeed, string murdererFaceJson)
    {
        if (!PhotonNetwork.InRoom)
        {
            RPC_SyncRoundAnswerData(murdererSeed, murdererFaceJson);
            return;
        }

        if (isPlayerA_Artist || PhotonNetwork.IsMasterClient)
        {
            photonView.RPC(nameof(RPC_SyncRoundAnswerData), RpcTarget.AllBuffered, murdererSeed, murdererFaceJson ?? "");
        }
    }

    [PunRPC]
    private void RPC_SyncRoundAnswerData(int murdererSeed, string murdererFaceJson)
    {
        SyncedMurdererSeed = murdererSeed;
        SyncedMurdererFaceJson = murdererFaceJson ?? "";
        GameSessionData.MurdererSeed = murdererSeed;
        GameSessionData.SuspectCodename = murdererSeed >= 0 ? $"Seed {murdererSeed}" : "Unknown";
        GameSessionData.SuspectFaceJson = SyncedMurdererFaceJson;
        Debug.Log($"[Sync] Received murderer face json. seed={murdererSeed}, size={SyncedMurdererFaceJson.Length}");
    }

    public void SendFullFaceSync(string faceJson)
    {
        if (!isPlayerA_Artist)
        {
            return;
        }

        LatestArtistFaceJson = faceJson ?? "";
        GameSessionData.ArtistFaceJson = LatestArtistFaceJson;

        if (PhotonNetwork.InRoom)
        {
            photonView.RPC(nameof(RPC_SyncFullFace), RpcTarget.Others, LatestArtistFaceJson);
        }
    }

    [PunRPC]
    private void RPC_SyncFullFace(string faceJson)
    {
        LatestArtistFaceJson = faceJson ?? "";
        GameSessionData.ArtistFaceJson = LatestArtistFaceJson;

        if (activeCustomizer != null && !isPlayerA_Artist)
        {
            activeCustomizer.LoadFromJson(LatestArtistFaceJson);
            Debug.Log("[Sync] Applied artist face json on B client.");
        }
        else if (activeCustomizer == null)
        {
            Debug.LogWarning("[Sync] Received artist face json before local customizer registered.");
        }
    }

    public void RequestSceneBStateFromHost()
    {
        if (isPlayerA_Artist && PhotonNetwork.InRoom)
        {
            photonView.RPC(nameof(RPC_RequestSceneBState), RpcTarget.Others);
        }
    }

    [PunRPC]
    private void RPC_RequestSceneBState()
    {
        if (!isPlayerA_Artist)
        {
            SceneBStateJsonSaver.Instance?.SaveNow();
        }
    }

    public void SendSceneBStateToArtist(string jsonState)
    {
        if (!isPlayerA_Artist && PhotonNetwork.InRoom)
        {
            photonView.RPC(nameof(RPC_SyncSceneBState), RpcTarget.Others, jsonState ?? "");
        }
    }

    [PunRPC]
    private void RPC_SyncSceneBState(string jsonState)
    {
        if (!isPlayerA_Artist)
        {
            return;
        }

        BinASceneJsonDriver driver = FindObjectOfType<BinASceneJsonDriver>();
        if (driver != null)
        {
            driver.ApplyStateFromJsonString(jsonState);
        }
        else
        {
            Debug.LogWarning("[Sync] Received SceneB state, but no BinASceneJsonDriver was found on A.");
        }
    }

    public void SubmitFinalSettlementFromSceneB(bool success, float elapsedSeconds, string sceneBStateJson)
    {
        int murdererSeed = SyncedMurdererSeed;
        string suspectFaceJson = !string.IsNullOrEmpty(SyncedMurdererFaceJson)
            ? SyncedMurdererFaceJson
            : GameSessionData.SuspectFaceJson;
        string artistFaceJson = GetLatestArtistFaceJson();

        if (PhotonNetwork.InRoom)
        {
            photonView.RPC(
                nameof(RPC_SyncFinalSettlement),
                RpcTarget.All,
                success,
                murdererSeed,
                suspectFaceJson ?? "",
                artistFaceJson ?? "",
                elapsedSeconds,
                sceneBStateJson ?? "");
        }
        else
        {
            RPC_SyncFinalSettlement(success, murdererSeed, suspectFaceJson, artistFaceJson, elapsedSeconds, sceneBStateJson);
        }
    }

    [PunRPC]
    private void RPC_SyncFinalSettlement(
        bool success,
        int murdererSeed,
        string suspectFaceJson,
        string artistFaceJson,
        float elapsedSeconds,
        string sceneBStateJson)
    {
        GameSessionData.SetSettlementData(
            success,
            murdererSeed,
            suspectFaceJson,
            artistFaceJson,
            elapsedSeconds,
            sceneBStateJson);

        LoadResultSceneOnce();
    }

    private string GetLatestArtistFaceJson()
    {
        if (activeCustomizer != null)
        {
            LatestArtistFaceJson = activeCustomizer.SaveToJson();
        }

        if (!string.IsNullOrEmpty(LatestArtistFaceJson))
        {
            return LatestArtistFaceJson;
        }

        return GameSessionData.ArtistFaceJson;
    }

    private void LoadResultSceneOnce()
    {
        if (isLoadingResultScene)
        {
            return;
        }

        isLoadingResultScene = true;

        if (ScreenTransitionManager.Instance != null)
        {
            ScreenTransitionManager.Instance.TransitionToScene("ResultScene");
        }
        else
        {
            SceneManager.LoadScene("ResultScene");
        }
    }

    private void OnDestroy()
    {
        if (activeCustomizer != null)
        {
            activeCustomizer.OnFaceChanged -= OnLocalFaceChanged;
        }
    }
}