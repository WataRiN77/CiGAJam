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

    [Header("按任意键开始界面")]
    [SerializeField] private TMP_Text pressAnyKeyText;       // “按任意键开始游戏”文本
    [SerializeField] private Animator bgAnimator;            // bg 背景上的 Animator 组件
    [SerializeField] private Image titleImage;               // 标题 Image
    [SerializeField] private CanvasGroup mainButtonsGroup;   // 包含那4个主菜单按钮的 CanvasGroup (用于统一控制渐变和禁用)
    [SerializeField] private float breatheSpeed = 2f;        // 呼吸灯变化速度

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
    private bool hasGameStarted = false; // 是否已经按了任意键启动了游戏
    private Coroutine breatheCoroutine;

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

        // 🌟 初始化：隐藏 4 个主按钮并禁用交互
        if (mainButtonsGroup != null)
        {
            mainButtonsGroup.alpha = 0f;
            mainButtonsGroup.interactable = false;
            mainButtonsGroup.blocksRaycasts = false;
        }

        // 🌟 初始化：显示“按任意键开始”文本并启动呼吸灯
        if (pressAnyKeyText != null)
        {
            pressAnyKeyText.gameObject.SetActive(true);
            breatheCoroutine = StartCoroutine(BreatheTextCoroutine());
        }

        // 🌟 初始化：禁用背景的 Animator（等待按任意键时再开启播放）
        if (bgAnimator != null)
        {
            bgAnimator.enabled = false;
        }
    }

    private void Update()
    {
        // 🌟 监听任意键按下
        if (!hasGameStarted && Input.anyKeyDown)
        {
            StartIntroSequence();
        }
    }

    /// <summary>
    /// 开始主菜单入场演出
    /// </summary>
    private void StartIntroSequence()
    {
        hasGameStarted = true;

        // 1. 停止呼吸灯并彻底禁用“按任意键开始”文本
        if (breatheCoroutine != null)
        {
            StopCoroutine(breatheCoroutine);
        }
        if (pressAnyKeyText != null)
        {
            pressAnyKeyText.gameObject.SetActive(false);
        }

        // 2. 激活并播放背景 bg 上的过渡动画
        if (bgAnimator != null)
        {
            bgAnimator.enabled = true; // 开启 Animator
            // 动画播放到最后一帧时，通过我们在动画中添加的 Event 触发公有方法 "ActivateMenuButtons" 即可
        }

        // 3. 让标题 Image 在 0.5 秒内渐变到完全透明
        if (titleImage != null)
        {
            StartCoroutine(FadeTitleImage(0f, 0.5f));
        }
    }

    // ==========================================
    // 🌟 新增：供背景动画最后一帧的 Event 调用的公开方法
    // ==========================================
    /// <summary>
    /// 激活 4 个主菜单按钮，并在 0.5 秒内从透明渐显变成不透明
    /// </summary>
    public void ActivateMenuButtons()
    {
        if (mainButtonsGroup != null)
        {
            StartCoroutine(FadeButtonsGroup(1f, 0.5f));
        }
    }

    // 标题渐变控制协程
    private IEnumerator FadeTitleImage(float targetAlpha, float duration)
    {
        Color startColor = titleImage.color;
        float startAlpha = startColor.a;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float curAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            titleImage.color = new Color(startColor.r, startColor.g, startColor.b, curAlpha);
            yield return null;
        }

        titleImage.color = new Color(startColor.r, startColor.g, startColor.b, targetAlpha);

        if (targetAlpha <= 0f)
        {
            titleImage.gameObject.SetActive(false); // 彻底隐藏防止遮挡射线
        }
    }

    // 4个主按钮 CanvasGroup 渐变控制协程
    private IEnumerator FadeButtonsGroup(float targetAlpha, float duration)
    {
        float startAlpha = mainButtonsGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            mainButtonsGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        mainButtonsGroup.alpha = targetAlpha;

        // 渐显到 100% 后，开启这 4 个按钮的交互
        if (targetAlpha >= 1f)
        {
            mainButtonsGroup.interactable = true;
            mainButtonsGroup.blocksRaycasts = true;
        }
    }

    // “按任意键”呼吸灯控制协程
    private IEnumerator BreatheTextCoroutine()
    {
        while (true)
        {
            // 通过正弦 Sin 函数进行平滑、往复的透明度渐变
            float alpha = (Mathf.Sin(Time.time * breatheSpeed) + 1f) / 2f;
            pressAnyKeyText.color = new Color(pressAnyKeyText.color.r, pressAnyKeyText.color.g, pressAnyKeyText.color.b, alpha);
            yield return null;
        }
    }

    // ==========================================
    // 1. 主菜单四大按钮入口（保持原有逻辑不变）
    // ==========================================

    public void OnClickTeach()
    {
        Debug.Log("[MainMenu] 点击进入教程（暂空，待后续跳转单机教程场景）");
        if (ScreenTransitionManager.Instance != null)
        {
            ScreenTransitionManager.Instance.TransitionToScene("TutorialScene");
        }
    }

    public void OnClickStart()
    {
        // 点击开始游戏后，4个主按钮自动淡出并关闭交互，把舞台留给联机滑入页面
        mainButtonsGroup.alpha = 0f;
        mainButtonsGroup.interactable = false;
        mainButtonsGroup.blocksRaycasts = false;

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
        if (ScreenTransitionManager.Instance != null)
        {
            ScreenTransitionManager.Instance.TransitionToQuit();
        }
    }

    // ==========================================
    // 2. 动效协程（遮罩渐变、按钮滑入）（保持原有逻辑不变）
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
    // 3. 联机匹配逻辑（Photon 串联）（保持原有逻辑不变）
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