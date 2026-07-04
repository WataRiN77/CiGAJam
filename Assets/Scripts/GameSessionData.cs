using UnityEngine;

/// <summary>
/// 静态数据中转站：用于跨场景存储上一局对战的最终成绩
/// </summary>
public static class GameSessionData
{
    // 1. 抓捕是否成功
    public static bool IsCaptureSuccess = true;

    // 2. 嫌疑人的代号
    public static string SuspectCodename = "代号X";

    // 3. 抓捕消耗的总时间（秒）
    public static float CaptureDurationValue = 130f; // 例如 130 秒对应 02:10

    // 4. 【核心人脸数据】：用于计算相似度
    public static string SuspectFaceJson = ""; // 嫌犯真实的脸部数据 (由 AnswerGenerator 产生)
    public static string ArtistFaceJson = "";  // 玩家A拼出来的脸部数据 (由 CharacterCustomizer2D 产生)
}