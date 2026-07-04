using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using AK.Wwise; // 引入Wwise命名空间

public class ScreenTransitionManager : MonoBehaviour
{
    public static ScreenTransitionManager Instance { get; private set; }

    [Header("通用黑屏转场 UI")]
    [SerializeField] private CanvasGroup transitionCanvasGroup; // 包含黑屏 Image 的 CanvasGroup
    [SerializeField] private float defaultDuration = 0.5f;     // 默认转场时间（秒）

    [Header("特殊结算转场 (电视噪音/雪花)")]
    [SerializeField] private GameObject settlementTransitionObject; // “结算转场”游戏物体
    [SerializeField] private Animator settlementAnimator;           // “结算转场”物体的 Animator 组件
    [SerializeField] private bool playFanOnStart = false;            // 是否在启动时播放“反”动画

    private Coroutine disableSettlementCoroutine; // 🌟 新增：专门用来存储“延时关闭雪花屏”的协程引用

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景不销毁

            // 初始化黑屏状态为禁用
            if (transitionCanvasGroup != null)
            {
                transitionCanvasGroup.alpha = 0f;
                transitionCanvasGroup.blocksRaycasts = false;
                transitionCanvasGroup.gameObject.SetActive(false); // 初始默认关闭
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 如果勾选了 playFanOnStart 为 true，启动时自动播放“反”动画并延时隐藏
        if (playFanOnStart)
        {
            PlaySettlementFan();
        }
    }

    // ==========================================
    // 1. 通用黑屏转场方法（保留）
    // ==========================================
    public void TransitionToScene(string sceneName)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    public IEnumerator FadeScreen(float targetAlpha)
    {
        yield return FadeScreen(targetAlpha, defaultDuration);
    }

    public IEnumerator FadeScreen(float targetAlpha, float duration)
    {
        yield return StartCoroutine(Fade(targetAlpha, duration));
    }

    /// <summary>
    /// 🌟 修改后：点击退出游戏时，调用特殊结算雪花转场
    /// </summary>
    public void TransitionToQuit()
    {
        StartCoroutine(QuitCoroutine());
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        yield return StartCoroutine(Fade(1f, defaultDuration));

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        yield return StartCoroutine(Fade(0f, defaultDuration));
    }

    /// <summary>
    /// 🌟 修改后：播放电视雪花（zheng）动画，并在 1 秒后关闭游戏
    /// </summary>
    private IEnumerator QuitCoroutine()
    {
        // 1. 播放结算雪花屏（zheng）动画与 Wwise 开机声
        PlaySettlementZheng();

        // 2. 停顿 1 秒，让玩家完整感受雪花屏和音效
        yield return new WaitForSeconds(1.5f);

        // 3. 彻底退出游戏
        Debug.Log("[Transition] 转场完成，退出游戏");
#if UNITY_EDITOR
        // 🌟 如果是在 Unity 编辑器里测试，点击退出会自动停止 Play 模式！
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 🌟 如果是打包出来的游戏（exe），执行真正的关闭游戏程序
        Application.Quit(); 
#endif
    }

    /// <summary>
    /// 🌟 修复：在渐变黑屏前，先一步激活 transitionCanvasGroup 物体
    /// </summary>
    private IEnumerator Fade(float targetAlpha, float duration)
    {
        if (transitionCanvasGroup == null) yield break;

        // 🌟 核心修复：如果要渐变黑屏（目标透明度 > 0），先激活该物体，防止协程无法启动
        if (targetAlpha > 0f)
        {
            transitionCanvasGroup.gameObject.SetActive(true);
        }

        transitionCanvasGroup.blocksRaycasts = true;

        float startAlpha = transitionCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transitionCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        transitionCanvasGroup.alpha = targetAlpha;

        // 🌟 核心优化：如果已经淡出到完全透明，直接禁用物体，节省 GPU 渲染开销
        if (targetAlpha <= 0f)
        {
            transitionCanvasGroup.blocksRaycasts = false;
            transitionCanvasGroup.gameObject.SetActive(false);
        }
    }

    // ==========================================
    // 2. 特殊结算转场方法 (正/反动画控制)
    // ==========================================

    /// <summary>
    /// 方法一：激活结算转场物体并播放“zheng”动画
    /// </summary>
    public void PlaySettlementZheng()
    {
        if (settlementTransitionObject == null || settlementAnimator == null)
        {
            Debug.LogWarning("[Transition] 结算转场引用为空，无法播放 zheng 动画！");
            return;
        }

        // 🌟 修复：只停止“延迟关闭物体”的协程，绝对不使用 StopAllCoroutines 误伤退出协程！
        if (disableSettlementCoroutine != null)
        {
            StopCoroutine(disableSettlementCoroutine);
            disableSettlementCoroutine = null;
        }
        settlementTransitionObject.SetActive(true);
        settlementAnimator.Play("zheng");
        AkUnitySoundEngine.PostEvent("Play_Transition_TV_Out", gameObject);
        Debug.Log("[Transition] 播放结算转场：zheng (正) 动画");
    }

    /// <summary>
    /// 方法二：播放“fan”动画，并在 1.5 秒后禁用该物体
    /// </summary>
    public void PlaySettlementFan()
    {
        if (settlementTransitionObject == null || settlementAnimator == null)
        {
            Debug.LogWarning("[Transition] 结算转场引用为空，无法播放 fan 动画！");
            return;
        }

        settlementTransitionObject.SetActive(true); // 确保物体是激活的
        settlementAnimator.Play("fan");
        AkUnitySoundEngine.PostEvent("Play_Transition_TV_In", gameObject);
        Debug.Log("[Transition] 播放结算转场：fan (反) 动画");

        // 开启协程，在 1.5 秒后自动关闭转场物体
        StartCoroutine(DisableSettlementObjectAfterDelay(1.5f));
    }

    private IEnumerator DisableSettlementObjectAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        settlementTransitionObject.SetActive(false);
        Debug.Log("[Transition] 结算转场播放完毕，物体已禁用。");
    }
}
