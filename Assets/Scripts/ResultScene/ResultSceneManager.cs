using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class ResultSceneManager : MonoBehaviour
{
    [Header("打字机管理器引用")]
    [SerializeField] private TypewriterManager typewriterManager; // 场景中的 TypewriterManager

    [Header("结算人脸")]
    [SerializeField] private CharacterCustomizer2D suspectFaceCustomizer;
    [SerializeField] private CharacterCustomizer2D artistFaceCustomizer;
    [SerializeField] private bool autoFindFaceCustomizers = true;

    [Header("底部交互按钮")]
    [SerializeField] private GameObject btnContinue;             // “继续游戏”按钮（抓捕成功时显示）
    [SerializeField] private GameObject btnReturnToMenu;         // “返回主菜单”按钮（抓捕失败时显示）
    [SerializeField] private CanvasGroup buttonsCanvasGroup;     // 按钮组的 CanvasGroup（用于柔和渐显）

    [Header("印章动效配置")]
    [SerializeField] private Image stampImage;                   // 印章的 Image 组件
    [SerializeField] private Sprite stampSpriteSuccess;          // 成功时的“APPROVED”印章贴图
    [SerializeField] private Sprite stampSpriteFailed;           // 失败时的“REJECTED”印章贴图
    [SerializeField] private float stampStartScale = 3.5f;       // 盖章刚出现时的缩放大小
    [SerializeField] private float stampSlamDuration = 0.2f;     // 印章狠狠砸下的时间

    [Header("🌟 Wwise BGM 状态配置 (新增)")]
    [SerializeField] private AK.Wwise.State bgmStateSuccess;     // 🌟 胜利状态 (对应 Wwise 里的 Finish_Success)
    [SerializeField] private AK.Wwise.State bgmStateFailed;      // 🌟 失败状态 (对应 Wwise 里的 Finish_Failed)

    private void Start()
    {
        // 1. 初始隐藏底部按钮和印章
        btnContinue.SetActive(false);
        btnReturnToMenu.SetActive(false);
        if (buttonsCanvasGroup != null)
        {
            buttonsCanvasGroup.alpha = 0f;
        }

        if (stampImage != null)
        {
            stampImage.gameObject.SetActive(false);
        }

        // 🌟 2. 核心联动：调用 AkState 的内置方法 SetValue() 切换至对应 Wwise 状态！
        if (GameSessionData.IsCaptureSuccess)
        {
            if (bgmStateSuccess != null && bgmStateSuccess.IsValid())
            {
                bgmStateSuccess.SetValue(); // 切换至胜利 BGM 状态
                Debug.Log("[Wwise-BGM] 已调用 AkState 切换至: Finish_Success");
            }
        }
        else
        {
            if (bgmStateFailed != null && bgmStateFailed.IsValid())
            {
                bgmStateFailed.SetValue();  // 切换至失败 BGM 状态
                Debug.Log("[Wwise-BGM] 已调用 AkState 切换至: Finish_Failed");
            }
        }

        // 3. 提取数据并进行结算和打字机装填
        ConfigureSettlementData();

        // 4. 延时 2 秒，启动打字机和盖章表现序列
        StartCoroutine(StartSettlementPresentation());
    }

    /// <summary>
    /// 读取跨场景成绩，计算相似度，并直接注入你的 TypewriterManager 序列中！
    /// </summary>
    private void ConfigureSettlementData()
    {
        string suspectFaceJson = GameSessionData.SuspectFaceJson;
        string artistFaceJson = GameSessionData.ArtistFaceJson;
        ApplySettlementFaces(suspectFaceJson, artistFaceJson);

        if (typewriterManager == null) return;

        // A. 抓捕结果
        string successText = GameSessionData.IsCaptureSuccess ? "抓捕成功" : "抓捕失败";

        // B. 相似度计算
        int similarityPercent = CalculateFaceSimilarity(suspectFaceJson, artistFaceJson);
        string similarityText = $"相似度：{similarityPercent}%";

        // C. 嫌疑人代号
        string suspectText = $"嫌疑人：{GameSessionData.SuspectCodename}";

        // D. 格式化抓捕时间
        string timeText = $"抓捕时间 {FormatTime(GameSessionData.CaptureDurationValue)}";

        // 注入打字机序列
        if (typewriterManager.defaultSequence.Count >= 4)
        {
            typewriterManager.defaultSequence[0].content = successText;
            typewriterManager.defaultSequence[1].content = similarityText;
            typewriterManager.defaultSequence[2].content = suspectText;
            typewriterManager.defaultSequence[3].content = timeText;
        }
    }

    private void ApplySettlementFaces(string suspectFaceJson, string artistFaceJson)
    {
        ResolveFaceCustomizers();

        if (suspectFaceCustomizer != null && !string.IsNullOrEmpty(suspectFaceJson))
        {
            suspectFaceCustomizer.LoadFromJson(suspectFaceJson);
        }
        else
        {
            Debug.LogWarning("ResultSceneManager: suspect face json or face customizer is missing.", this);
        }

        if (artistFaceCustomizer != null && !string.IsNullOrEmpty(artistFaceJson))
        {
            artistFaceCustomizer.LoadFromJson(artistFaceJson);
        }
        else
        {
            Debug.LogWarning("ResultSceneManager: artist face json or face customizer is missing.", this);
        }
    }

    private void ResolveFaceCustomizers()
    {
        if (!autoFindFaceCustomizers || (suspectFaceCustomizer != null && artistFaceCustomizer != null))
        {
            return;
        }

        CharacterCustomizer2D[] customizers = FindObjectsOfType<CharacterCustomizer2D>(true);
        if (customizers == null || customizers.Length == 0)
        {
            return;
        }

        if (suspectFaceCustomizer == null)
        {
            suspectFaceCustomizer = FindFaceCustomizerByName(customizers, "suspect", "murderer", "target", "real", "correct", "嫌疑", "犯", "正确");
        }

        if (artistFaceCustomizer == null)
        {
            artistFaceCustomizer = FindFaceCustomizerByName(customizers, "artist", "player", "draw", "sketch", "portrait", "画像", "玩家");
        }

        if (suspectFaceCustomizer == null && customizers.Length > 0)
        {
            suspectFaceCustomizer = customizers[0];
        }

        if (artistFaceCustomizer == null)
        {
            for (int i = 0; i < customizers.Length; i++)
            {
                if (customizers[i] != null && customizers[i] != suspectFaceCustomizer)
                {
                    artistFaceCustomizer = customizers[i];
                    break;
                }
            }
        }
    }

    private CharacterCustomizer2D FindFaceCustomizerByName(CharacterCustomizer2D[] customizers, params string[] tokens)
    {
        for (int i = 0; i < customizers.Length; i++)
        {
            CharacterCustomizer2D customizer = customizers[i];
            if (customizer == null)
            {
                continue;
            }

            string path = GetHierarchyPath(customizer.transform).ToLowerInvariant();
            for (int j = 0; j < tokens.Length; j++)
            {
                if (!string.IsNullOrEmpty(tokens[j]) && path.Contains(tokens[j].ToLowerInvariant()))
                {
                    return customizer;
                }
            }
        }

        return null;
    }

    private string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
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

        // 3. 在打字机打字中途盖下大印！
        yield return new WaitForSeconds(2.5f);
        yield return StartCoroutine(PlayStampEffectCoroutine());

        // 4. 等待打字机完全播放完毕后，柔和显现对应按钮
        yield return new WaitForSeconds(1.5f);

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

    // 盖章动画协程
    private IEnumerator PlayStampEffectCoroutine()
    {
        if (stampImage == null) yield break;

        stampImage.sprite = GameSessionData.IsCaptureSuccess ? stampSpriteSuccess : stampSpriteFailed;
        stampImage.gameObject.SetActive(true);
        stampImage.transform.localScale = Vector3.one * stampStartScale;

        Color startColor = stampImage.color;
        stampImage.color = new Color(startColor.r, startColor.g, startColor.b, 0f);

        float elapsed = 0f;
        while (elapsed < stampSlamDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / stampSlamDuration;

            float tScale = Mathf.Lerp(stampStartScale, 1.0f, t * t);
            float tAlpha = Mathf.Min(1f, t * 2f);

            stampImage.transform.localScale = Vector3.one * tScale;
            stampImage.color = new Color(startColor.r, startColor.g, startColor.b, tAlpha);
            yield return null;
        }

        stampImage.transform.localScale = Vector3.one;
        stampImage.color = new Color(startColor.r, startColor.g, startColor.b, 1f);

        // 触发 Wwise 的盖章音效！
        AkUnitySoundEngine.PostEvent("Play_SFX_Stamp", gameObject);
        Debug.Log("[Wwise-Stamp] 盖章成功！触发 Play_SFX_Stamp 音效");

        // 物理微小回弹效果
        float bounceElapsed = 0f;
        float bounceDuration = 0.15f;
        while (bounceElapsed < bounceDuration)
        {
            bounceElapsed += Time.deltaTime;
            float t = bounceElapsed / bounceDuration;
            float bounceScale = 1.0f + Mathf.Sin(t * Mathf.PI) * 0.08f;
            stampImage.transform.localScale = Vector3.one * bounceScale;
            yield return null;
        }

        stampImage.transform.localScale = Vector3.one;
    }

    // 相似度算法
    private int CalculateFaceSimilarity(string suspectJson, string artistJson)
    {
        if (string.IsNullOrEmpty(suspectJson) || string.IsNullOrEmpty(artistJson))
        {
            return 0;
        }

        FaceSaveData suspect = JsonUtility.FromJson<FaceSaveData>(suspectJson);
        FaceSaveData artist = JsonUtility.FromJson<FaceSaveData>(artistJson);

        if (suspect == null || artist == null || suspect.organs == null || artist.organs == null || suspect.organs.Count == 0)
        {
            return 0;
        }

        float totalScore = 0f;
        int matchedOrgansCount = 0;

        foreach (var sOrgan in suspect.organs)
        {
            if (string.IsNullOrEmpty(sOrgan.partId)) continue;

            var aOrgan = artist.organs.Find(o => o.objectPath == sOrgan.objectPath && o.partId == sOrgan.partId);
            if (aOrgan == null) continue;

            matchedOrgansCount++;
            float organScore = 0f;

            if (sOrgan.spriteIndex == aOrgan.spriteIndex)
            {
                organScore += 40f;
            }

            float posDist = Vector3.Distance(sOrgan.localPosition, aOrgan.localPosition);
            float posFactor = Mathf.Clamp01(1f - posDist / 0.15f);
            organScore += 30f * posFactor;

            float scaleDiff = Vector3.Distance(sOrgan.localScale, aOrgan.localScale);
            float scaleFactor = Mathf.Clamp01(1f - scaleDiff / 0.6f);
            organScore += 20f * scaleFactor;

            float angleDiff = Mathf.Abs(sOrgan.localRotation.eulerAngles.z - aOrgan.localRotation.eulerAngles.z);
            angleDiff = Mathf.Min(angleDiff, 360f - angleDiff);
            float rotFactor = Mathf.Clamp01(1f - angleDiff / 45f);
            organScore += 10f * rotFactor;

            totalScore += organScore;
        }

        float finalScore = matchedOrgansCount > 0 ? totalScore / matchedOrgansCount : 0f;
        return Mathf.Clamp((int)finalScore, 0, 100);
    }

    private string FormatTime(float seconds)
    {
        int min = Mathf.FloorToInt(seconds / 60f);
        int sec = Mathf.FloorToInt(seconds % 60f);
        return string.Format("{0:00} : {1:00}", min, sec);
    }

    public void OnClickContinueNextRound()
    {
        if (ScreenTransitionManager.Instance != null)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                int murdererSeed = Random.Range(100000, 999999);
                int mapNumber = Random.Range(1, 5);
                int npcCount = 5;

                int[] npcSeeds = new int[npcCount];
                for (int i = 0; i < npcCount; i++)
                {
                    npcSeeds[i] = Random.Range(100000, 999999);
                }
                npcSeeds[0] = murdererSeed;

                if (AsymmetricSyncManager.Instance != null)
                {
                    AsymmetricSyncManager.Instance.BroadcastSeeds(npcSeeds, murdererSeed, mapNumber);
                }
            }

            if (AsymmetricSyncManager.Instance != null)
            {
                if (AsymmetricSyncManager.Instance.isPlayerA_Artist)
                {
                    ScreenTransitionManager.Instance.TransitionToScene("A_捏脸");
                }
                else
                {
                    ScreenTransitionManager.Instance.TransitionToScene("SceneB");
                }
            }
            else
            {
                ScreenTransitionManager.Instance.TransitionToScene("A_捏脸");
            }
        }
    }

    public void OnClickReturnToMenu()
    {
        if (ScreenTransitionManager.Instance != null)
        {
            ScreenTransitionManager.Instance.TransitionToScene("Menu");
        }
    }
}
