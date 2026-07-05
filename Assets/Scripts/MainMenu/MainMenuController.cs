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

    [Header("设置页面动画配置")]
    [SerializeField] private RectTransform rectSettingsPanel; // 设置面板主体的 RectTransform (Canvas 的子物体)
    [SerializeField] private Image settingsMask;             // 设置面板背后的黑色遮罩 Image
    [SerializeField] private float settingsSlideDuration = 0.4f; // 滑动时间
    [SerializeField] private float settingsOffScreenY = 1200f; // 初始在屏幕下方多远 (比如 1200 像素)

    [Header("菜单内教程弹窗")]
    [SerializeField] private bool autoBuildTutorialUI = true;
    [SerializeField] private CanvasGroup tutorialGroup;
    [SerializeField] private RectTransform tutorialPanel;
    [SerializeField] private Image tutorialImage;
    [SerializeField] private Button tutorialPrevButton;
    [SerializeField] private Button tutorialNextButton;
    [SerializeField] private Button tutorialExitButton;
    [SerializeField] private Sprite[] tutorialSprites;
    [SerializeField] private string tutorialResourcesPath = "Tutorial";
    [SerializeField] private float tutorialSlideDuration = 0.35f;
    [SerializeField] private Vector2 tutorialPrevButtonPosition = new Vector2(92f, 0f);
    [SerializeField] private Vector2 tutorialNextButtonPosition = new Vector2(-92f, 0f);
    [SerializeField] private Vector2 tutorialNavButtonSize = new Vector2(96f, 150f);
    [SerializeField] private Color tutorialNavButtonColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color tutorialNavTextColor = Color.white;

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
    [SerializeField] private TMP_Text txtHostStatus;         // A端：实时等待状态文本（如：“正在等待探员...”）
    [SerializeField] private GameObject panelLoadingClient;  // B端：加入中的加载面板
    [SerializeField] private TMP_Text txtClientStatus;       // B端：实时加载状态文本（如：“正与画像师取得联络中...”）

    [Header("按钮滑动配置")]
    [SerializeField] private float slideDuration = 0.5f;     // 滑动时间
    [SerializeField] private float maskFadeDuration = 0.4f;  // 遮罩渐变时间
    [SerializeField] private float offScreenOffset = 1200f;  // 屏幕外偏移量

    private Vector2 activePosA; // 按钮 A 最终停留位置
    private Vector2 activePosB; // 按钮 B 最终停留位置
    private Vector2 activePosSettings; // 记录设置面板正常的位置
    private bool hasGameStarted = false; // 是否已经按了任意键启动了游戏
    private Coroutine breatheCoroutine;
    private Coroutine settingsSlideCoroutine; // 控制设置面板滑动的协程
    private Coroutine tutorialCoroutine;
    private int tutorialIndex;
    
    // 🌟 终极防空手段：本地缓存生成的房间码
    private string localRoomCodeCache = ""; 

    public static MainMenuController Instance { get; private set; } // 新增单例，方便跨物体调用

    private void Awake()
    {
        Instance = this; // 初始化单例

        // 🌟 核心：强行开启后台运行！
        // 这样即使你点击了另一个窗口，当前窗口也绝对不会暂停，彻底杜绝掉线和超时！
        Application.runInBackground = true;

        // 最高优先级在 Awake 中启动服务器连接
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("[Photon] 正在以最高优先级在 Awake 中启动服务器连接...");
        }
    }

    private void Start()
    {

        // 确保 Photon 自动同步场景关闭（因为两边去不同的场景，不使用同步加载）
        PhotonNetwork.AutomaticallySyncScene = false;

        // 记录按钮 A 和 B 在场景中预设的正常位置
        activePosA = rectButtonA.anchoredPosition;
        activePosB = rectButtonB.anchoredPosition;

        // 记录设置面板在屏幕中心的初始位置
        if (rectSettingsPanel != null)
        {
            activePosSettings = rectSettingsPanel.anchoredPosition;
        }

        // 初始化 UI 状态
        lobbyCanvas.SetActive(false);
        settingsCanvas.SetActive(false);
        panelWaitingHost.SetActive(false);
        panelLoadingClient.SetActive(false);

        // 初始化：隐藏 4 个主按钮并禁用交互
        if (mainButtonsGroup != null)
        {
            mainButtonsGroup.alpha = 0f;
            mainButtonsGroup.interactable = false;
            mainButtonsGroup.blocksRaycasts = false;
        }

        // 初始化：显示“按任意键开始”文本并启动呼吸灯
        if (pressAnyKeyText != null)
        {
            pressAnyKeyText.gameObject.SetActive(true);
            breatheCoroutine = StartCoroutine(BreatheTextCoroutine());
        }

        // 初始化：禁用背景的 Animator（等待按任意键时再开启播放）
        if (bgAnimator != null)
        {
            bgAnimator.enabled = false;
        }

        if (mainButtonsGroup != null)
        {
            mainButtonsGroup.alpha = 0f;
            mainButtonsGroup.gameObject.SetActive(false); // 彻底禁用物体
        }

        InitializeTutorialOverlay();
    }

    // 监听连接到 Photon 服务器的回调
    public override void OnConnectedToMaster()
    {
        Debug.Log("[Photon] 成功连接至中国/亚洲 Master 服务器！现在可以正常联机了。");
    }

    private void Update()
    {
        // 监听任意键按下
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
    // 供背景动画最后一帧的 Event 调用的公开方法
    // ==========================================
    public void ActivateMenuButtons()
    {
        if (mainButtonsGroup != null)
        {
            mainButtonsGroup.gameObject.SetActive(true);  // 1. 先激活物体
            mainButtonsGroup.alpha = 0f;                  // 2. 确保初始是完全透明的
            mainButtonsGroup.interactable = false;
            mainButtonsGroup.blocksRaycasts = false;

            StartCoroutine(FadeButtonsGroup(1f, 0.5f));   // 3. 再启动 0.5s 渐显协程
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
    // 1. 主菜单四大按钮入口
    // ==========================================

    public void OnClickTeach()
    {
        OpenTutorial();
    }

    public void OnClickTutorialPrev()
    {
        ShowTutorialPage(tutorialIndex - 1, -1);
    }

    public void OnClickTutorialNext()
    {
        if (IsLastTutorialPage())
        {
            CloseTutorial();
            return;
        }

        ShowTutorialPage(tutorialIndex + 1, 1);
    }

    public void OnClickTutorialExit()
    {
        CloseTutorial();
    }

    private void InitializeTutorialOverlay()
    {
        LoadTutorialSpritesIfNeeded(false);

        if (autoBuildTutorialUI && tutorialPanel == null)
        {
            BuildTutorialOverlay();
        }

        if (tutorialPrevButton != null)
        {
            tutorialPrevButton.onClick.RemoveListener(OnClickTutorialPrev);
            tutorialPrevButton.onClick.AddListener(OnClickTutorialPrev);
        }

        if (tutorialNextButton != null)
        {
            tutorialNextButton.onClick.RemoveListener(OnClickTutorialNext);
            tutorialNextButton.onClick.AddListener(OnClickTutorialNext);
        }

        if (tutorialExitButton != null)
        {
            tutorialExitButton.onClick.RemoveListener(OnClickTutorialExit);
            tutorialExitButton.onClick.AddListener(OnClickTutorialExit);
        }

        SetTutorialVisible(false, false);
    }

    private void LoadTutorialSpritesIfNeeded(bool forceReload)
    {
        if (!forceReload && tutorialSprites != null && tutorialSprites.Length > 0)
        {
            return;
        }

        Sprite[] sprites = Resources.LoadAll<Sprite>(tutorialResourcesPath);
        if (sprites != null && sprites.Length > 0)
        {
            System.Array.Sort(sprites, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            tutorialSprites = sprites;
            LogLoadedTutorialSprites();
            return;
        }

        Texture2D[] textures = Resources.LoadAll<Texture2D>(tutorialResourcesPath);
        if (textures == null || textures.Length == 0)
        {
            return;
        }

        System.Array.Sort(textures, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        tutorialSprites = new Sprite[textures.Length];

        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D texture = textures[i];
            tutorialSprites[i] = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            tutorialSprites[i].name = texture.name;
        }

        LogLoadedTutorialSprites();
    }

    private void BuildTutorialOverlay()
    {
        Transform parent = mainCanvas != null ? mainCanvas.transform : transform;

        GameObject root = new GameObject("TutorialOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.transform.SetParent(parent, false);
        root.transform.SetAsLastSibling();

        tutorialPanel = root.GetComponent<RectTransform>();
        StretchToParent(tutorialPanel);

        tutorialGroup = root.GetComponent<CanvasGroup>();
        Image blocker = root.GetComponent<Image>();
        blocker.color = Color.black;
        blocker.raycastTarget = true;

        GameObject imageObject = new GameObject("TutorialImage", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(root.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        StretchToParent(imageRect);
        tutorialImage = imageObject.GetComponent<Image>();
        tutorialImage.color = Color.white;
        tutorialImage.preserveAspect = false;
        tutorialImage.raycastTarget = false;

        tutorialPrevButton = CreateTutorialButton("TutorialPrevButton", "<", root.transform, tutorialPrevButtonPosition, tutorialNavButtonSize, TextAlignmentOptions.Center);
        tutorialNextButton = CreateTutorialButton("TutorialNextButton", ">", root.transform, tutorialNextButtonPosition, tutorialNavButtonSize, TextAlignmentOptions.Center);

        AnchorButton(tutorialPrevButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        AnchorButton(tutorialNextButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
    }

    private void OpenTutorial()
    {
        InitializeTutorialOverlay();
        LoadTutorialSpritesIfNeeded(true);

        if (tutorialPanel == null || tutorialImage == null)
        {
            Debug.LogWarning("[MainMenu] Tutorial UI is not configured.");
            return;
        }

        if (tutorialSprites == null || tutorialSprites.Length == 0)
        {
            Debug.LogWarning("[MainMenu] Tutorial images are missing. Assign tutorialSprites or put images under Assets/Resources/Tutorial.");
            return;
        }

        if (tutorialCoroutine != null)
        {
            StopCoroutine(tutorialCoroutine);
        }

        tutorialIndex = 0;
        ApplyTutorialPage();

        if (mainButtonsGroup != null)
        {
            mainButtonsGroup.interactable = false;
            mainButtonsGroup.blocksRaycasts = false;
        }

        tutorialCoroutine = StartCoroutine(ShowTutorialRoutine());
    }

    private void LogLoadedTutorialSprites()
    {
        if (tutorialSprites == null || tutorialSprites.Length == 0)
        {
            Debug.LogWarning($"[MainMenu] No tutorial images loaded from Resources/{tutorialResourcesPath}.");
            return;
        }

        string names = string.Join(", ", System.Array.ConvertAll(tutorialSprites, sprite => sprite != null ? sprite.name : "<null>"));
        Debug.Log($"[MainMenu] Loaded tutorial images from Resources/{tutorialResourcesPath}: {names}");
    }

    private void CloseTutorial()
    {
        if (tutorialPanel == null)
        {
            return;
        }

        if (tutorialCoroutine != null)
        {
            StopCoroutine(tutorialCoroutine);
        }

        tutorialCoroutine = StartCoroutine(HideTutorialRoutine());
    }

    private void ShowTutorialPage(int newIndex, int direction)
    {
        if (tutorialSprites == null || tutorialSprites.Length == 0)
        {
            return;
        }

        if (newIndex < 0 || newIndex >= tutorialSprites.Length || newIndex == tutorialIndex)
        {
            return;
        }

        if (tutorialCoroutine != null)
        {
            StopCoroutine(tutorialCoroutine);
        }

        tutorialCoroutine = StartCoroutine(SwitchTutorialPageRoutine(newIndex, direction));
    }

    private IEnumerator ShowTutorialRoutine()
    {
        yield return FadeScreenForTutorial(1f);

        SetTutorialVisible(true, true);
        tutorialPanel.anchoredPosition = Vector2.zero;
        tutorialGroup.alpha = 1f;

        yield return FadeScreenForTutorial(0f);
        tutorialCoroutine = null;
    }

    private IEnumerator HideTutorialRoutine()
    {
        yield return FadeScreenForTutorial(1f);

        SetTutorialVisible(false, false);

        if (mainButtonsGroup != null)
        {
            mainButtonsGroup.gameObject.SetActive(true);
            mainButtonsGroup.alpha = 1f;
            mainButtonsGroup.interactable = true;
            mainButtonsGroup.blocksRaycasts = true;
        }

        yield return FadeScreenForTutorial(0f);
        tutorialCoroutine = null;
    }

    private IEnumerator SwitchTutorialPageRoutine(int newIndex, int direction)
    {
        yield return FadeScreenForTutorial(1f);

        tutorialIndex = newIndex;
        ApplyTutorialPage();
        tutorialPanel.anchoredPosition = Vector2.zero;
        tutorialImage.rectTransform.anchoredPosition = Vector2.zero;
        tutorialImage.color = Color.white;

        yield return FadeScreenForTutorial(0f);
        tutorialCoroutine = null;
    }

    private IEnumerator FadeScreenForTutorial(float targetAlpha)
    {
        ScreenTransitionManager transition = ScreenTransitionManager.Instance;
        if (transition != null)
        {
            yield return transition.FadeScreen(targetAlpha, tutorialSlideDuration);
            yield break;
        }

        yield return FadeTutorialGroupFallback(targetAlpha);
    }

    private IEnumerator FadeTutorialGroupFallback(float targetAlpha)
    {
        if (tutorialGroup == null)
        {
            yield break;
        }

        float startAlpha = tutorialGroup.alpha;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, tutorialSlideDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            tutorialGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        tutorialGroup.alpha = targetAlpha;
    }

    private void ApplyTutorialPage()
    {
        if (tutorialImage != null && tutorialSprites != null && tutorialIndex >= 0 && tutorialIndex < tutorialSprites.Length)
        {
            tutorialImage.sprite = tutorialSprites[tutorialIndex];
        }

        int lastIndex = tutorialSprites != null ? tutorialSprites.Length - 1 : 0;

        if (tutorialPrevButton != null)
        {
            tutorialPrevButton.gameObject.SetActive(tutorialIndex > 0);
        }

        if (tutorialNextButton != null)
        {
            tutorialNextButton.gameObject.SetActive(true);
        }

        if (tutorialExitButton != null)
        {
            tutorialExitButton.gameObject.SetActive(false);
        }
    }

    private bool IsLastTutorialPage()
    {
        return tutorialSprites != null && tutorialSprites.Length > 0 && tutorialIndex >= tutorialSprites.Length - 1;
    }

    private void SetTutorialVisible(bool visible, bool interactive)
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.gameObject.SetActive(visible);
            tutorialPanel.anchoredPosition = Vector2.zero;
        }

        if (tutorialGroup != null)
        {
            tutorialGroup.alpha = visible ? 1f : 0f;
            tutorialGroup.interactable = interactive || visible;
            tutorialGroup.blocksRaycasts = interactive || visible;
        }
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
    }

    private Button CreateTutorialButton(string objectName, string label, Transform parent, Vector2 anchoredPosition, Vector2 size, TextAlignmentOptions alignment)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = buttonObject.GetComponent<Image>();
        image.color = tutorialNavButtonColor;

        Button button = buttonObject.GetComponent<Button>();

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        StretchToParent(textRect);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = label.Length > 1 ? 30f : 58f;
        text.alignment = alignment;
        text.color = tutorialNavTextColor;
        text.raycastTarget = false;

        return button;
    }

    private static void AnchorButton(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        Vector2 position = rect.anchoredPosition;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
    }

    public void OnClickStart()
    {
        // 点击开始游戏后，4个主按钮自动淡出并关闭交互，把舞台留给联机滑入页面
        mainButtonsGroup.alpha = 0f;
        mainButtonsGroup.interactable = false;
        mainButtonsGroup.blocksRaycasts = false;
        mainButtonsGroup.gameObject.SetActive(false); // 隐退时也顺手彻底关闭物体

        lobbyCanvas.SetActive(true);
        StartCoroutine(TransitionToLobby());
    }

    // 打开设置
    public void OnClickSettings()
    {
        settingsCanvas.SetActive(true);
        if (settingsSlideCoroutine != null) StopCoroutine(settingsSlideCoroutine);
        settingsSlideCoroutine = StartCoroutine(SlideSettingsPanel(true));
    }

    // 关闭设置
    public void OnClickCloseSettings()
    {
        if (settingsSlideCoroutine != null) StopCoroutine(settingsSlideCoroutine);
        settingsSlideCoroutine = StartCoroutine(SlideSettingsPanel(false));
    }

    // 设置面板滑动与黑色遮罩渐变行动
    private IEnumerator SlideSettingsPanel(bool isOpening)
    {
        Vector2 startPos = isOpening ? new Vector2(activePosSettings.x, activePosSettings.y - settingsOffScreenY) : activePosSettings;
        Vector2 endPos = isOpening ? activePosSettings : new Vector2(activePosSettings.x, activePosSettings.y - settingsOffScreenY);

        float maxAlpha = 215f / 255f;
        float startAlpha = isOpening ? 0f : maxAlpha;
        float endAlpha = isOpening ? maxAlpha : 0f;

        rectSettingsPanel.anchoredPosition = startPos;

        if (settingsMask != null)
        {
            settingsMask.color = new Color(settingsMask.color.r, settingsMask.color.g, settingsMask.color.b, startAlpha);
        }

        float elapsed = 0f;
        while (elapsed < settingsSlideDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settingsSlideDuration;

            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            rectSettingsPanel.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);

            if (settingsMask != null)
            {
                float curAlpha = Mathf.Lerp(startAlpha, endAlpha, smoothT);
                settingsMask.color = new Color(settingsMask.color.r, settingsMask.color.g, settingsMask.color.b, curAlpha);
            }

            yield return null;
        }

        rectSettingsPanel.anchoredPosition = endPos;
        if (settingsMask != null)
        {
            settingsMask.color = new Color(settingsMask.color.r, settingsMask.color.g, settingsMask.color.b, endAlpha);
        }

        if (!isOpening)
        {
            settingsCanvas.SetActive(false);
        }
    }

    public void OnClickQuit()
    {
        Debug.Log("[MainMenu] 退出游戏");
        if (ScreenTransitionManager.Instance != null)
        {
            ScreenTransitionManager.Instance.TransitionToQuit();
        }
        else
        {
            Application.Quit(); // 兜底退出
        }
    }

    // ==========================================
    // 2. 联机选择界面滑入动效协程
    // ==========================================
    private IEnumerator TransitionToLobby()
    {
        bgMask.color = new Color(0, 0, 0, 0);
        rectButtonA.anchoredPosition = new Vector2(activePosA.x - offScreenOffset, activePosA.y);
        rectButtonB.anchoredPosition = new Vector2(activePosB.x + offScreenOffset, activePosB.y);

        float elapsed = 0f;
        while (elapsed < Mathf.Max(slideDuration, maskFadeDuration))
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;
            float tMask = elapsed / maskFadeDuration;

            bgMask.color = new Color(0, 0, 0, Mathf.Min(0.6f, tMask * 0.6f));

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

    // 点击左侧 A 按钮：创建房间并作为外勤画像师
    public void OnClickCreateRoomA()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            panelWaitingHost.SetActive(true);
            if (txtHostStatus != null)
            {
                txtHostStatus.text = "网络连接中，请稍候...";
            }
            Debug.LogWarning("[Photon] 还在连接服务器中，请稍等 1-2 秒再试。");
            return;
        }

        panelWaitingHost.SetActive(true);

        // 初始化 A端 的等待状态文本
        if (txtHostStatus != null)
        {
            txtHostStatus.text = "正在等待探员...";
        }

        string roomCode = Random.Range(100000, 999999).ToString();
        localRoomCodeCache = roomCode;

        if (txtWaitingRoomCode != null)
        {
            txtWaitingRoomCode.text = localRoomCodeCache;
            Debug.Log($"[本地UI] 成功本地填入房间码: {localRoomCodeCache}");
        }

        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 2 };
        PhotonNetwork.CreateRoom(roomCode, roomOptions);
    }

    // 点击右侧 B 按钮：加入房间
    public void OnClickJoinRoomB()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("[Photon] 还在连接服务器中，无法加入。");
            return;
        }

        string roomCode = inputFieldCode.text.Trim();
        if (string.IsNullOrEmpty(roomCode)) return;

        panelLoadingClient.SetActive(true);

        // 初始化 B端 的联络中状态文本
        if (txtClientStatus != null)
        {
            txtClientStatus.text = "正与画像师取得联络中...";
        }

        PhotonNetwork.JoinRoom(roomCode);
    }

    public override void OnCreatedRoom()
    {
        // 🌟 因为在 OnClickCreateRoomA 里已经秒显了，这里只需要打印一条日志确认即可
        Debug.Log($"[Photon] 服务器成功创建房间! 本地房间码: {localRoomCodeCache}");
    }

    // 成功进入房间（双端都会触发此回调）
    public override void OnJoinedRoom()
    {
        // 🌟 保险一：核心防空保护。如果因为网络同步慢，CurrentRoom 还没就绪，直接拦截等待，防止崩溃！
        if (PhotonNetwork.CurrentRoom == null)
        {
            Debug.LogWarning("[Photon] 已经加入房间，但服务器房间数据尚未同步完毕，等待中...");
            return;
        }

        Debug.Log($"[Photon] 已进入房间。当前人数: {PhotonNetwork.CurrentRoom.PlayerCount}");

        // 如果房间人满了（2人），触发倒计时
        if (PhotonNetwork.CurrentRoom.PlayerCount == 2)
        {
            // 🌟 保险二：不依赖父类的默认缓存，直接通过 GetComponent 物理抓取 PhotonView
            PhotonView pv = GetComponent<PhotonView>();

            if (pv != null)
            {
                pv.RPC("RPC_StartMatchCountdown", RpcTarget.All);
                Debug.Log("[Photon] 人数已满，已成功通过 RPC 发送倒计时启动指令。");
            }
            else
            {
                Debug.LogError("[Photon] 严重错误：在 主视觉Canvas 上未找到 PhotonView 组件，无法发送同步指令！");
            }
        }
    }

    [PunRPC]
    private void RPC_StartMatchCountdown()
    {
        // 保持各自现有的面板激活，仅在原有面板内部替换文本
        if (PhotonNetwork.IsMasterClient)
        {
            panelWaitingHost.SetActive(true);
            if (txtHostStatus != null)
            {
                txtHostStatus.text = "探员已就位，即将开始任务";
            }
        }
        else
        {
            panelLoadingClient.SetActive(true);
            if (txtClientStatus != null)
            {
                txtClientStatus.text = "已与画像师连通，即将开始任务";
            }
        }

        if (AsymmetricSyncManager.Instance != null)
        {
            AsymmetricSyncManager.Instance.isPlayerA_Artist = PhotonNetwork.IsMasterClient;
        }

        StartCoroutine(CountdownCoroutine());
    }

    private IEnumerator CountdownCoroutine()
    {
        yield return new WaitForSeconds(1.5f);

        // 🌟 跳转前，由房主 A 生成一整套种子，并通过 AsymmetricSyncManager 广播给 B 的 SeededNpcSpawnManager
        if (PhotonNetwork.IsMasterClient)
        {
            int murdererSeed = Random.Range(100000, 999999); // 随机生成嫌疑人种子
            int npcCount = 15; // 准备生成的 NPC 数量
            int mapNumber = Random.Range(1, 5); // Scene index: 1-4
            int[] npcSeeds = new int[npcCount];
            for (int i = 0; i < npcCount; i++)
            {
                npcSeeds[i] = Random.Range(100000, 999999); // 随机生成干扰路人种子
            }
            npcSeeds[0] = murdererSeed; // 确保其中一个是正确答案

            if (AsymmetricSyncManager.Instance != null)
            {
                AsymmetricSyncManager.Instance.BroadcastSeeds(npcSeeds, murdererSeed, mapNumber);
            }
        }

        yield return new WaitForSeconds(0.1f); // 稍等 0.1s 确保数据包发送出去了

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
