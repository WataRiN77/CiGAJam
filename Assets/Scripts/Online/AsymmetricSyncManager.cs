using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

public class AsymmetricSyncManager : MonoBehaviourPunCallbacks
{
    public static AsymmetricSyncManager Instance { get; private set; }

    [Header("角色身份状态")]
    public bool isPlayerA_Artist = false; // 联机倒计时结束后，房主(A)为true，客机(B)为false

    [Header("动态绑定的本地脸部渲染器")]
    private CharacterCustomizer2D activeCustomizer; // 动态指向当前活动场景中的捏脸系统

    private void Awake()
    {
        Debug.Log($"[Sync-Awake] AsymmetricSyncManager 在物体 '{gameObject.name}' 上启动。");

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景不销毁
            Debug.Log("[Sync-Awake] 单例初始化成功，已设置为 DontDestroyOnLoad。");
        }
        else
        {
            Debug.Log($"[Sync-Awake] 发现重复的管理器，正在销毁物体: {gameObject.name}");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 提供给 A 场景的大脸，和 B 场景左下角的小脸自注册
    /// </summary>
    public void RegisterLocalCustomizer(CharacterCustomizer2D customizer)
    {
        if (customizer == null)
        {
            Debug.LogWarning("[Sync] 尝试注册一个空的脸部渲染器！已拒绝。");
            return;
        }

        // 卸载旧事件，防止换场景时内存泄露
        if (activeCustomizer != null)
        {
            activeCustomizer.OnFaceChanged -= OnLocalFaceChanged;
        }

        activeCustomizer = customizer;

        // 🌟 注册成功：绑定本地修改监听事件
        activeCustomizer.OnFaceChanged += OnLocalFaceChanged;

        Debug.Log($"[Sync-注册] 成功注册了本地脸部渲染器！物体名: '{customizer.gameObject.name}', 身份是否为画家 A: {isPlayerA_Artist}");
    }

    /// <summary>
    /// 监听本地捏脸修改事件（仅在 A 端生效）
    /// </summary>
    private void OnLocalFaceChanged()
    {
        if (isPlayerA_Artist && activeCustomizer != null)
        {
            string faceJson = activeCustomizer.SaveToJson();
            Debug.Log($"[Sync-事件] 检测到本地人脸修改！正在发送全脸同步数据 (JSON 长度: {faceJson.Length})");
            SendFullFaceSync(faceJson);
        }
    }

    // ==========================================
    // 1. 种子分发与人群生成逻辑 (开局同步)
    // ==========================================
    public void BroadcastSeeds(int[] npcSeeds, int murdererSeed)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("RPC_SyncGameplaySeeds", RpcTarget.AllBuffered, npcSeeds, murdererSeed);
        }
    }

    [PunRPC]
    private void RPC_SyncGameplaySeeds(int[] npcSeeds, int murdererSeed)
    {
        Debug.Log($"[Sync] 收到游戏种子。路人数: {npcSeeds.Length}，嫌疑人: {murdererSeed}");
        SeededNpcSpawnManager.SetPendingSeeds(npcSeeds, murdererSeed);
    }

    // ==========================================
    // 2. 【核心同步】：A 端 ➡️ B 端：全脸数据同步
    // ==========================================

    public void SendFullFaceSync(string faceJson)
    {
        if (isPlayerA_Artist)
        {
            Debug.Log($"[Sync-网络] 正在广播发送全脸 JSON 数据...");
            photonView.RPC("RPC_SyncFullFace", RpcTarget.Others, faceJson);
        }
    }

    [PunRPC]
    private void RPC_SyncFullFace(string faceJson)
    {
        Debug.Log($"[Sync-网络] 接收到对方发来的全脸 JSON 数据！大小: {faceJson.Length} 字节。");

        if (activeCustomizer != null)
        {
            if (!isPlayerA_Artist)
            {
                activeCustomizer.LoadFromJson(faceJson);
                Debug.Log("[Sync-成功] 对方发来的全脸数据已在本地完美解析渲染！");
            }
            else
            {
                Debug.LogWarning("[Sync-警告] 我是画家 A，忽略对方发来的脸部同步请求。");
            }
        }
        else
        {
            Debug.LogError("[Sync-错误] 收到同步数据，但本地 activeCustomizer 为空（注册未成功）！无法渲染。");
        }
    }

    // ==========================================
    // 3. 【核心同步】：B 端 ➔ A 端：局势数据同步
    // ==========================================

    /// <summary>
    /// A端在场景加载完成时调用，主动向 B 端请求一次完整的初始局势
    /// </summary>
    public void RequestSceneBStateFromHost()
    {
        if (isPlayerA_Artist && PhotonNetwork.InRoom)
        {
            Debug.Log("[Sync-网络] A端已就绪，正在请求 B 端的初始局势数据...");
            photonView.RPC("RPC_RequestSceneBState", RpcTarget.Others);
        }
    }

    [PunRPC]
    private void RPC_RequestSceneBState()
    {
        if (!isPlayerA_Artist)
        {
            Debug.Log("[Sync-网络] 收到 A 端的数据请求，正在即时序列化并发送...");
            // 调用 B 端的 Saver 重新打包并发送
            if (SceneBStateJsonSaver.Instance != null)
            {
                SceneBStateJsonSaver.Instance.SaveNow();
            }
        }
    }

    /// <summary>
    /// B端调用：将最新的 JSON 数据通过网络广播给 A端
    /// </summary>
    public void SendSceneBStateToArtist(string jsonState)
    {
        if (!isPlayerA_Artist && PhotonNetwork.InRoom)
        {
            photonView.RPC("RPC_SyncSceneBState", RpcTarget.Others, jsonState);
        }
    }

    [PunRPC]
    private void RPC_SyncSceneBState(string jsonState)
    {
        // 只有画像师 A 需要接收并更新画面
        if (isPlayerA_Artist)
        {
            BinASceneJsonDriver driver = FindObjectOfType<BinASceneJsonDriver>();
            if (driver != null)
            {
                driver.ApplyStateFromJsonString(jsonState);
            }
            else
            {
                Debug.LogWarning("[Sync-警告] 收到 B 端数据，但在 A 端未找到 BinASceneJsonDriver 组件。");
            }
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