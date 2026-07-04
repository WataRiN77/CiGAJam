using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class ResultSceneManager : MonoBehaviour
{
    [Header("打字机管理器引用")]
    [SerializeField] private TypewriterManager typewriterManager; // 场景中的 TypewriterManager

    [Header("底部交互按钮")]
    [SerializeField] private GameObject btnContinue;             // “继续游戏”按钮（抓捕成功时显示）
    [SerializeField] private GameObject btnReturnToMenu;         // “返回主菜单”按钮（抓捕失败时显示）
    [SerializeField] private CanvasGroup buttonsCanvasGroup;     // 按钮组的 CanvasGroup（用于柔和渐显）

    private void Start()
    {
        // 1. 初始隐藏底部按钮
        btnContinue.SetActive(false);
        btnReturnToMenu.SetActive(false);
        if (buttonsCanvasGroup != null)
        {
            buttonsCanvasGroup.alpha = 0f;
        }

        // 2. 核心：提取数据并进行结算和打字机装填
        ConfigureSettlementData();

        // 3. 延时 2 秒（给电视雪花屏和转场留出时间），然后启动打字机序列
        StartCoroutine(StartSettlementPresentation());
    }

    /// <summary>
    /// 读取跨场景成绩，计算相似度，并直接注入你的 TypewriterManager 序列中！
    /// </summary>
    private void ConfigureSettlementData()
    {
        if (typewriterManager == null) return;

        // A. 抓捕结果
        string successText = GameSessionData.IsCaptureSuccess ? "抓捕成功" : "抓捕失败";

        // B. 相似度计算
        int similarityPercent = CalculateFaceSimilarity(GameSessionData.SuspectFaceJson, GameSessionData.ArtistFaceJson);
        string similarityText = $"相似度：{similarityPercent}%";

        // C. 嫌疑人代号
        string suspectText = $"嫌疑人：{GameSessionData.SuspectCodename}";

        // D. 格式化抓捕时间（将秒数如 130 转化为 02:10 格式）
        string timeText = $"抓捕时间 {FormatTime(GameSessionData.CaptureDurationValue)}";

        // 🌟 巧妙融合：直接把你本地配置好的 4 个打字机序列的 Content 填入！
        // 这样你就完全不需要在 Inspector 里重新拖拽，直接复用了你精美的界面配置
        if (typewriterManager.defaultSequence.Count >= 4)
        {
            typewriterManager.defaultSequence[0].content = successText;
            typewriterManager.defaultSequence[1].content = similarityText;
            typewriterManager.defaultSequence[2].content = suspectText;
            typewriterManager.defaultSequence[3].content = timeText;
        }
    }

    private IEnumerator StartSettlementPresentation()
    {
        // 1. 延时 2 秒启动（等待开场电视噪音退去）
        yield return new WaitForSeconds(2.0f);

        // 2. 启动打字机开始打字
        if (typewriterManager != null)
        {
            typewriterManager.PlayDefaultSequence();
        }

        // 3. 延时 4 秒后（等待打字机差不多打完），柔和淡出显现对应按钮
        yield return new WaitForSeconds(4.0f);

        // 根据抓捕成败，激活对应按钮
        if (GameSessionData.IsCaptureSuccess)
        {
            btnContinue.SetActive(true);
        }
        else
        {
            btnReturnToMenu.SetActive(true);
        }

        // 渐显按钮
        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            if (buttonsCanvasGroup != null)
            {
                buttonsCanvasGroup.alpha = elapsed / 0.5f;
            }
            yield return null;
        }
    }

    // ==========================================
    // 🌟 画像与嫌疑人相似度对比算法 (高保真游戏设计)
    // ==========================================
    private int CalculateFaceSimilarity(string suspectJson, string artistJson)
    {
        // 如果数据为空，提供一个好玩的基础保底分
        if (string.IsNullOrEmpty(suspectJson) || string.IsNullOrEmpty(artistJson))
        {
            return Random.Range(30, 45);
        }

        FaceSaveData suspect = JsonUtility.FromJson<FaceSaveData>(suspectJson);
        FaceSaveData artist = JsonUtility.FromJson<FaceSaveData>(artistJson);

        if (suspect == null || artist == null || suspect.organs.Count == 0)
        {
            return Random.Range(30, 45);
        }

        float totalScore = 0f;
        int matchedOrgansCount = 0;

        // 遍历嫌犯的每一个五官
        foreach (var sOrgan in suspect.organs)
        {
            if (string.IsNullOrEmpty(sOrgan.partId)) continue;

            // 寻找玩家 A 拼出来的相同部位
            var aOrgan = artist.organs.Find(o => o.objectPath == sOrgan.objectPath && o.partId == sOrgan.partId);
            if (aOrgan == null) continue;

            matchedOrgansCount++;
            float organScore = 0f;

            // 1. 贴图款式是否一致 (占 40%)
            if (sOrgan.spriteIndex == aOrgan.spriteIndex)
            {
                organScore += 40f;
            }

            // 2. 位置贴合度 (占 30%)
            float posDist = Vector3.Distance(sOrgan.localPosition, aOrgan.localPosition);
            float posFactor = Mathf.Clamp01(1f - posDist / 0.15f); // 0.15 范围内计算误差
            organScore += 30f * posFactor;

            // 3. 缩放贴合度 (占 20%)
            float scaleDiff = Vector3.Distance(sOrgan.localScale, aOrgan.localScale);
            float scaleFactor = Mathf.Clamp01(1f - scaleDiff / 0.6f); // 0.6 范围内计算误差
            organScore += 20f * scaleFactor;

            // 4. 旋转角度贴合度 (占 10%)
            float angleDiff = Mathf.Abs(sOrgan.localRotation.eulerAngles.z - aOrgan.localRotation.eulerAngles.z);
            angleDiff = Mathf.Min(angleDiff, 360f - angleDiff);
            float rotFactor = Mathf.Clamp01(1f - angleDiff / 45f); // 45度以内计算误差
            organScore += 10f * rotFactor;

            totalScore += organScore;
        }

        // 计算平均值
        float finalScore = matchedOrgansCount > 0 ? totalScore / matchedOrgansCount : 0f;

        // 最终返回 0% - 100% 的整数
        return Mathf.Clamp((int)finalScore, 0, 100);
    }

    // 格式化时间辅助函数 (将 130 秒转换为 "02:10")
    private string FormatTime(float seconds)
    {
        int min = Mathf.FloorToInt(seconds / 60f);
        int sec = Mathf.FloorToInt(seconds % 60f);
        return string.Format("{0:00} : {1:00}", min, sec);
    }

    // “继续游戏”按钮绑定：生成新一轮种子并“各回各家”
    public void OnClickContinueNextRound()
    {
        if (ScreenTransitionManager.Instance != null)
        {
            // 🌟 1. 核心网络同步：如果是房主（A），开启新一轮前，生成并分发全新的一组随机种子！
            if (PhotonNetwork.IsMasterClient)
            {
                int murdererSeed = Random.Range(100000, 999999); // 产生新的嫌犯

                // 💡 提示：你们策划案里提到了“难度递增”，你可以在这里根据通关轮次逐渐增加路人数
                int npcCount = 15; // 第二轮可以设计成 20 甚至 25 人

                int[] npcSeeds = new int[npcCount];
                for (int i = 0; i < npcCount; i++)
                {
                    npcSeeds[i] = Random.Range(100000, 999999); // 产生新的路人
                }
                npcSeeds[0] = murdererSeed; // 确保塞入正确答案

                // 呼叫同步管理器分发新种子
                if (AsymmetricSyncManager.Instance != null)
                {
                    AsymmetricSyncManager.Instance.BroadcastSeeds(npcSeeds, murdererSeed);
                }
            }

            // 🌟 2. 核心分流跳转：根据身份状态，让两名玩家退回各自的关卡场景
            if (AsymmetricSyncManager.Instance != null)
            {
                if (AsymmetricSyncManager.Instance.isPlayerA_Artist)
                {
                    // A 玩家（画家）进入捏脸场景开始新一轮拼图
                    ScreenTransitionManager.Instance.TransitionToScene("A_捏脸");
                }
                else
                {
                    // B 玩家（侦察员）进入对战场景开始新一轮搜寻
                    ScreenTransitionManager.Instance.TransitionToScene("SceneB");
                }
            }
            else
            {
                // 兜底单机测试方案
                ScreenTransitionManager.Instance.TransitionToScene("A_捏脸");
            }
        }
    }

    // “返回主菜单”按钮绑定：电视雪花屏退出
    public void OnClickReturnToMenu()
    {
        if (ScreenTransitionManager.Instance != null)
        {
            ScreenTransitionManager.Instance.TransitionToScene("Menu");
        }
    }
}