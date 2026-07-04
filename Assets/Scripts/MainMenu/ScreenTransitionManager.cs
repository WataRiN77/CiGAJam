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

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景不销毁

            // 初始化黑屏状态
            if (transitionCanvasGroup != null)
            {
                transitionCanvasGroup.alpha = 0f;
                transitionCanvasGroup.blocksRaycasts = false;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 🌟 重点需求：如果勾选了 playFanOnStart 为 true，启动时自动播放“反”动画并延时隐藏
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

    private IEnumerator QuitCoroutine()
    {
        yield return StartCoroutine(Fade(1f, defaultDuration));
        Application.Quit();
    }

    private IEnumerator Fade(float targetAlpha, float duration)
    {
        if (transitionCanvasGroup == null) yield break;
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

        if (targetAlpha <= 0f)
        {
            transitionCanvasGroup.blocksRaycasts = false;
        }
    }

    // ==========================================
    // 2. 新增：特殊结算转场方法 (正/反动画控制)
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

        StopAllCoroutines(); // 停止可能正在运行的延迟禁用协程，防止冲突
        settlementTransitionObject.SetActive(true);
        settlementAnimator.Play("zheng");
        AkUnitySoundEngine.PostEvent("Play_Transition_TV_In", gameObject);
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