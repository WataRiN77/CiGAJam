using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TypewriterManager : MonoBehaviour
{
    public static TypewriterManager Instance { get; private set; }

    [System.Serializable]
    public class TypewriterEntry
    {
        public TMP_Text textComponent;          // 目标 TextMeshPro 文本组件
        public float delayTime;                 // 延迟播放的时间（秒，自方法调用起算，0 代表立刻开始）
        [TextArea(2, 5)] public string content; // 实际要打印的内容（若为空，则自动使用文本组件中原有的文字）
    }

    [Header("打字速度")]
    [SerializeField] private float delayBetweenChars = 0.05f; // 每个字符出现的间隔时间

    [Header("Wwise 循环音效配置 (对接 Wwise v2024.1+)")]
    [SerializeField] private string playLoopEvent = "Play_SFX_Typing"; // 开始播放循环打字声
    [SerializeField] private string stopLoopEvent = "Stop_SFX_Typing"; // 停止播放循环打字声

    [Header("默认播放序列（可选，可在面板里配好用于测试）")]
    [SerializeField] public List<TypewriterEntry> defaultSequence;

    private List<Coroutine> activeCoroutines = new List<Coroutine>();
    private int activeTypingCount = 0; // 当前正在打字的文本框数量计数器
    private uint loopPlayingId = 0;    // 记录正在播放的 Wwise 循环音效 ID

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // 测试用：如果面板里勾选了默认序列，可以按快捷键（比如空格）触发测试
    }

    /// <summary>
    /// 【核心公开方法】：传入一个打字序列列表，开始播放异步错落打字（音频自动合并为单轨）
    /// </summary>
    public void PlaySequence(List<TypewriterEntry> sequence)
    {
        // 1. 播放新序列前，先安全停止所有正在进行的旧打字和音效
        StopAllSequences();

        if (sequence == null || sequence.Count == 0) return;

        // 2. 依次开启每个文本框的延迟打字协程
        foreach (var entry in sequence)
        {
            if (entry.textComponent == null) continue;

            Coroutine co = StartCoroutine(TypewriteWithDelayCoroutine(entry));
            activeCoroutines.Add(co);
        }
    }

    /// <summary>
    /// 提供给外部或按钮的测试方法：直接播放面板里配好的默认序列
    /// </summary>
    public void PlayDefaultSequence()
    {
        PlaySequence(defaultSequence);
    }

    private IEnumerator TypewriteWithDelayCoroutine(TypewriterEntry entry)
    {
        // 在延迟期间，先将文本框内容清空，防止剧透
        entry.textComponent.text = "";
        entry.textComponent.maxVisibleCharacters = 0;

        // 1. 等待各自设定的延迟时间
        yield return new WaitForSeconds(entry.delayTime);

        // 2. 延迟结束，准备开始打字
        string textToPrint = string.IsNullOrEmpty(entry.content) ? entry.textComponent.text : entry.content;
        entry.textComponent.text = textToPrint;
        entry.textComponent.ForceMeshUpdate();

        int totalVisibleCharacters = textToPrint.Length;
        int counter = 0;

        // 🌟【音频智能启动逻辑】🌟
        // 只有当“活跃打字文本框”的数量从 0 变成 1 时，才发送 Play 音效（防止多重音轨重叠噪音）
        activeTypingCount++;
        if (activeTypingCount == 1)
        {
            StartTypingSound();
        }

        // 3. 执行打字机动画
        while (counter <= totalVisibleCharacters)
        {
            entry.textComponent.maxVisibleCharacters = counter;
            counter++;
            yield return new WaitForSeconds(delayBetweenChars);
        }

        // 🌟【音频智能关闭逻辑】🌟
        // 每一个文本框打完字，计数减 1。只有当“最后一个”也打完、计数彻底归 0 时，才发送 Stop 音效
        activeTypingCount--;
        if (activeTypingCount == 0)
        {
            StopTypingSound();
        }
    }

    // ==========================================
    // 音频底层控制与清理
    // ==========================================

    private void StartTypingSound()
    {
        if (!string.IsNullOrEmpty(playLoopEvent))
        {
            loopPlayingId = AkUnitySoundEngine.PostEvent(playLoopEvent, gameObject);
            Debug.Log("[TypewriterManager] 开启打字音效...");
        }
    }

    private void StopTypingSound()
    {
        if (!string.IsNullOrEmpty(stopLoopEvent))
        {
            AkUnitySoundEngine.PostEvent(stopLoopEvent, gameObject);
        }

        if (loopPlayingId != 0)
        {
            AkUnitySoundEngine.StopPlayingID(loopPlayingId);
            loopPlayingId = 0;
        }
        Debug.Log("[TypewriterManager] 关闭打字音效。");
    }

    /// <summary>
    /// 强行停止所有正在运行的打字序列并重置音频
    /// </summary>
    public void StopAllSequences()
    {
        foreach (var co in activeCoroutines)
        {
            if (co != null) StopCoroutine(co);
        }
        activeCoroutines.Clear();

        activeTypingCount = 0;
        StopTypingSound();
    }

    private void OnDisable()
    {
        StopAllSequences();
    }
}