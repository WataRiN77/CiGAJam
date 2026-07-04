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
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景不销毁
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 提供给 A 场景的大脸，和 B 场景左下角的小脸自注册
    /// </summary>
    public void RegisterLocalCustomizer(CharacterCustomizer2D customizer)
    {
        // 🌟 卸载旧事件，防止换场景时内存泄露
        if (activeCustomizer != null)
        {
            activeCustomizer.OnFaceChanged -= OnLocalFaceChanged;
        }

        activeCustomizer = customizer;
        Debug.Log($"[Sync] 脸部渲染器注册成功！当前身份是否为A端: {isPlayerA_Artist}");

        // 🌟 核心：如果是 A 端（画像师），自动监听本地脸部的任何修改（贴图/位移/大小）
        if (isPlayerA_Artist && activeCustomizer != null)
        {
            activeCustomizer.OnFaceChanged += OnLocalFaceChanged;
        }
    }

    /// <summary>
    /// 🌟 核心：当 A 端本地捏脸发生任何改动时，自动打包数据发送
    /// </summary>
    private void OnLocalFaceChanged()
    {
        if (activeCustomizer != null && isPlayerA_Artist)
        {
            string faceJson = activeCustomizer.SaveToJson();
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
    // 🌟 2. 【核心同步】：A 端 ➡️ B 端：全脸数据同步
    // ==========================================
    
    /// <summary>
    /// 发送整张脸的 JSON 字符串
    /// </summary>
    public void SendFullFaceSync(string faceJson)
    {
        if (isPlayerA_Artist)
        {
            // 使用 Others 保证数据只发给 B 端
            photonView.RPC("RPC_SyncFullFace", RpcTarget.Others, faceJson);
        }
    }

    [PunRPC]
    private void RPC_SyncFullFace(string faceJson)
    {
        // B 端收到后，直接 Load 还原
        if (!isPlayerA_Artist && activeCustomizer != null)
        {
            activeCustomizer.LoadFromJson(faceJson);
            Debug.Log("[Sync网络同步] 成功同步 A 端的脸部（包含位置、大小、贴图）！");
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