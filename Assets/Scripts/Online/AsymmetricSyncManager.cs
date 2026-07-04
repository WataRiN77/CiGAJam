using UnityEngine;
using Scene = UnityEngine.SceneManagement.Scene;
using SceneManager = UnityEngine.SceneManagement.SceneManager;
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
        // 🌟 跨场景不销毁单例：确保在切换 A 捏脸和 B 场景时，网络通道不断开
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

    /// <summary>
    /// 提供给 A 场景的大脸，和 B 场景左下角的小脸，在 Start 时自动注册自己
    /// </summary>
    public void RegisterLocalCustomizer(CharacterCustomizer2D customizer)
    {
        activeCustomizer = customizer;
        Debug.Log($"[Sync] 脸部渲染器注册成功！当前身份是否为A端: {isPlayerA_Artist}");
    }

    // ==========================================
    // 1. 种子分发与人群生成逻辑 (开局同步)
    // ==========================================

    /// <summary>
    /// 🌟 由房主(A)在跳转关卡前调用：生成路人种子和嫌疑人种子，并广播给B
    /// </summary>
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
        Debug.Log($"[Sync网络同步] 收到游戏种子。路人数: {npcSeeds.Length}，嫌疑人种子: {murdererSeed}");

        // 🌟 核心串联：调用你队友写好的 SeededNpcSpawnManager，把种子写入静态等待区！
        // 这样 B 场景加载后，路人就会用这批种子完美、一模一样地刷出来！
        SeededNpcSpawnManager.SetPendingSeeds(npcSeeds, murdererSeed);
    }

    // ==========================================
    // 2. A 端 ➡️ B 端：大屏捏脸 ➡️ 小屏头像同步
    // ==========================================

    /// <summary>
    /// A端在修改五官贴图（Part）时调用此函数发送
    /// </summary>
    public void SendPartChange(string paramId, int index)
    {
        if (isPlayerA_Artist)
        {
            photonView.RPC("RPC_SyncPartChange", RpcTarget.Others, paramId, index);
        }
    }

    [PunRPC]
    private void RPC_SyncPartChange(string paramId, int index)
    {
        // B 端收到 A 端传来的五官修改指令后，直接给左下角的“小脸”换件
        if (!isPlayerA_Artist && activeCustomizer != null)
        {
            // 传入 false 代表这是网络接收的指令，本地默默应用即可，防止死循环发送
            activeCustomizer.SetPart(paramId, index);
            Debug.Log($"[Sync网络同步] 成功同步 A 端画像五官: {paramId} -> {index}");
        }
    }

    // ==========================================
    // 3. 未来预留（B 端 ➡️ A 端：放大镜和排除状态回传）
    // ==========================================

    // 我们留到下一阶段慢慢写具体实现：
    // public void SendMagnifierPosition(Vector2 pos) { ... }
    // public void SendNPCMarkStatus(int npcId, int state) { ... }
}