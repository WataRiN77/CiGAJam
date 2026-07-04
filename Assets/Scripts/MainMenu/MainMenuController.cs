using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

public class MainMenuController : MonoBehaviourPunCallbacks
{
    [Header("Canvas 引用")]
    [SerializeField] private GameObject mainCanvas;          // 初始主界面 Canvas
    [SerializeField] private GameObject lobbyCanvas;         // 联机选择页面 Canvas
    [SerializeField] private GameObject settingsCanvas;      // 设置页面 Canvas

    [Header("联机选择页面 UI")]
    [SerializeField] private Image bgMask;                   // 背景黑色遮罩 Image
    [SerializeField] private RectTransform rectButtonA;      // A按钮的 RectTransform
    [SerializeField] private RectTransform rectButtonB;      // B按钮的 RectTransform
    [SerializeField] private TMP_InputField inputFieldCode;  // 房间码输入框

    [Header("弹窗与状态 UI")]
    [SerializeField] private GameObject panelWaitingHost;    // A端：等待外勤加入的面板
    [SerializeField] private TMP_Text txtWaitingRoomCode;    // A端：展示房间号的文本
    [SerializeField] private GameObject panelLoadingClient;  // B端：加入中的加载面板
    [SerializeField] private GameObject panelMatchSuccess;   // 双端：匹配成功面板
    [SerializeField] private TMP_Text txtCountdown;          // 双端：倒计时 3 秒文本

    [Header("按钮滑动配置")]
    [SerializeField] private float slideDuration = 0.5f;     // 滑动时间
    [SerializeField] private float maskFadeDuration = 0.4f;  // 遮罩渐变时间
    [SerializeField] private float offScreenOffset = 1200f;  // 屏幕外偏移量

    private Vector2 activePosA; // 按钮 A 最终停留位置
    private Vector2 activePosB; // 按钮 B 最终停留位置

    private void Start()
    {
        // 确保 Photon 自动同步场景关闭（因为两边去不同的场景，不使用同步加载）
        PhotonNetwork.AutomaticallySyncScene = false;

        // 记录按钮 A 和 B 在场景中预设的正常位置
        activePosA = rectButtonA.anchoredPosition;
        activePosB = rectButtonB.anchoredPosition;

        // 初始化 UI 状态
        lobbyCanvas.SetActive(false);
        settingsCanvas.SetActive(false);
        panelWaitingHost.SetActive(false);
        panelLoadingClient.SetActive(false);
        panelMatchSuccess.SetActive(false);
    }

    // ==========================================
    // 1. 主菜单四大按钮入口
    // ==========================================

    public void OnClickTeach()
    {
        Debug.Log("[MainMenu] 点击进入教程（暂空，待后续跳转单机教程场景）");
        // TODO: SceneManager.LoadScene("TutorialScene");
    }

    public void OnClickStart()
    {
        mainCanvas.SetActive(false);
        lobbyCanvas.SetActive(true);
        StartCoroutine(TransitionToLobby());
    }

    public void OnClickSettings()
    {
        settingsCanvas.SetActive(true);
    }

    public void OnClickQuit()
    {
        Debug.Log("[MainMenu] 退出游戏");
        Application.Quit();
    }

    // ==========================================
    // 2. 动效协程（遮罩渐变、按钮滑入）
    // ==========================================
    private IEnumerator TransitionToLobby()
    {
        // 初始状态：按钮在屏幕外，遮罩完全透明
        bgMask.color = new Color(0, 0, 0, 0);
        rectButtonA.anchoredPosition = new Vector2(activePosA.x - offScreenOffset, activePosA.y);
        rectButtonB.anchoredPosition = new Vector2(activePosB.x + offScreenOffset, activePosB.y);

        float elapsed = 0f;
        while (elapsed < Mathf.Max(slideDuration, maskFadeDuration))
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;
            float tMask = elapsed / maskFadeDuration;

            // 1. 遮罩渐变 (透明 -> 0.6f 不透明)
            bgMask.color = new Color(0, 0, 0, Mathf.Min(0.6f, tMask * 0.6f));

            // 2. 按钮平滑滑入 (使用 SmoothStep 让滑动有缓冲感)
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            rectButtonA.anchoredPosition = Vector2.Lerp(new Vector2(activePosA.x - offScreenOffset, activePosA.y), activePosA, smoothT);
            rectButtonB.anchoredPosition = Vector2.Lerp(new Vector2(activePosB.x + offScreenOffset, activePosB.y), activePosB, smoothT);

            yield return null;
        }

        rectButtonA.anchoredPosition = activePosA;
        rectButtonB.anchoredPosition = activePosB;
    }

    // ==========================================
    // 3. 联机匹配逻辑（Photon 串联）
    // ==========================================

    // 点击左侧 A 按钮：创建房间并作为画家A
    public void OnClickCreateRoomA()
    {
        panelWaitingHost.SetActive(true);
        txtWaitingRoomCode.text = "正在申请房间...";

        // 随机生成 6 位房间码
        string roomCode = Random.Range(100000, 999999).ToString();
        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 2 };

        PhotonNetwork.CreateRoom(roomCode, roomOptions);
    }

    public override void OnCreatedRoom()
    {
        // 房主显示房间码
        txtWaitingRoomCode.text = PhotonNetwork.CurrentRoom.Name;
        Debug.Log($"[Photon] 成功创建房间: {PhotonNetwork.CurrentRoom.Name}");
    }

    // 点击右侧 B 按钮：加入房间
    public void OnClickJoinRoomB()
    {
        string roomCode = inputFieldCode.text.Trim();
        if (string.IsNullOrEmpty(roomCode)) return;

        panelLoadingClient.SetActive(true);
        PhotonNetwork.JoinRoom(roomCode);
    }

    // 成功进入房间（双端都会触发此回调）
    public override void OnJoinedRoom()
    {
        Debug.Log($"[Photon] 已进入房间。当前人数: {PhotonNetwork.CurrentRoom.PlayerCount}");

        // 如果房间人满了（2人），触发倒计时
        if (PhotonNetwork.CurrentRoom.PlayerCount == 2)
        {
            // 通过网络同步开启倒计时
            photonView.RPC("RPC_StartMatchCountdown", RpcTarget.All);
        }
    }

    [PunRPC]
    private void RPC_StartMatchCountdown()
    {
        // 关闭等待和加载弹窗，开启成功倒计时面板
        panelWaitingHost.SetActive(false);
        panelLoadingClient.SetActive(false);
        panelMatchSuccess.SetActive(true);

        // 确定身份：如果是房主就是 A 端，否则是 B 端
        //if (AsymmetricSyncManager.Instance != null)
        {
            //AsymmetricSyncManager.Instance.isPlayerA_Artist = PhotonNetwork.IsMasterClient;
        }

        StartCoroutine(CountdownCoroutine());
    }

    private IEnumerator CountdownCoroutine()
    {
        int timer = 3;
        while (timer > 0)
        {
            txtCountdown.text = timer.ToString();
            yield return new WaitForSeconds(1f);
            timer--;
        }

        txtCountdown.text = "GO!";
        yield return new WaitForSeconds(0.5f);

        // 卸载大厅，分别跳转不同的场景
        if (PhotonNetwork.IsMasterClient)
        {
            SceneManager.LoadScene("A_捏脸"); // A 玩家加载捏脸
        }
        else
        {
            SceneManager.LoadScene("SceneB");  // B 玩家加载找人场景
        }
    }
}