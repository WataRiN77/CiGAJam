using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class FaceGameController : MonoBehaviour
{
    [Header("游戏设置")]
    [SerializeField] private bool useTimer = false;          // 是否启用倒计时
    [SerializeField] private float gameDuration = 120f;      // 总游戏时间（秒）

    [Header("状态事件（可选，方便UI绑定）")]
    public UnityEvent OnGameStarted;
    public UnityEvent<float> OnScoreSubmitted;   // 传递分数
    public UnityEvent OnGameEnded;

    // 当前状态
    public enum GameState { Idle, Playing, Ended }
    public GameState CurrentState { get; private set; } = GameState.Idle;

    private FaceCustomizationGameManager gameManager;
    private Coroutine timerCoroutine;
    private float timeRemaining;

    private void Start()
    {
        gameManager = FaceCustomizationGameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("FaceCustomizationGameManager 未找到，请确保场景中有该组件");
            return;
        }

        StartGame();
    }

    /// <summary>
    /// 开始新一局游戏
    /// </summary>
    public void StartGame()
    {
        if (CurrentState == GameState.Playing)
        {
            Debug.LogWarning("游戏已经在进行中");
            return;
        }

        // 生成答案、初始证词、干扰项
        gameManager.GenerateNewRound();

        CurrentState = GameState.Playing;
        OnGameStarted?.Invoke();

        // 启动计时器（如果启用）
        if (useTimer)
        {
            timeRemaining = gameDuration;
            if (timerCoroutine != null) StopCoroutine(timerCoroutine);
            timerCoroutine = StartCoroutine(TimerCoroutine());
        }
    }

    /// <summary>
    /// 玩家提交捏脸，计算分数并结束游戏
    /// </summary>
    public void SubmitFace()
    {
        if (CurrentState != GameState.Playing)
        {
            Debug.LogWarning("当前不在游戏进行中，无法提交");
            return;
        }

        float score = gameManager.SubmitCurrentFace();
        CurrentState = GameState.Ended;

        // 停止计时
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        OnScoreSubmitted?.Invoke(score);
        OnGameEnded?.Invoke();
    }

    /// <summary>
    /// 强制结束当前游戏（超时或手动放弃）
    /// </summary>
    public void EndGame()
    {
        if (CurrentState != GameState.Playing) return;

        // 可以选择自动提交，或视为放弃（得分为0）
        float score = 0f;
        // 若希望自动提交可改为：score = gameManager.SubmitCurrentFace();
        CurrentState = GameState.Ended;

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        OnScoreSubmitted?.Invoke(score);
        OnGameEnded?.Invoke();
    }

    /// <summary>
    /// 重新开始游戏（回到 Idle 后可直接 StartGame）
    /// </summary>
    public void RestartGame()
    {
        EndGame();  // 确保清理
        CurrentState = GameState.Idle;
        StartGame();
    }

    private IEnumerator TimerCoroutine()
    {
        while (timeRemaining > 0 && CurrentState == GameState.Playing)
        {
            timeRemaining -= Time.deltaTime;
            // 可在此添加每秒事件，例如 OnTimerUpdated?.Invoke(timeRemaining);
            yield return null;
        }

        if (CurrentState == GameState.Playing)
        {
            Debug.Log("时间到，自动提交");
            SubmitFace();
        }
    }

    /// <summary>
    /// 获取剩余时间（供 UI 显示）
    /// </summary>
    public float GetTimeRemaining() => timeRemaining;
}