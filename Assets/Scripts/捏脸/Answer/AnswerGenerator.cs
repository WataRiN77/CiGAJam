// AnswerGenerator.cs
using UnityEngine;

public class AnswerGenerator : MonoBehaviour
{
    [SerializeField] private CharacterCustomizer2D customizer;   // 参考捏脸系统获取数据资产
    [SerializeField] private CharacterCustomizationData2D dataAsset; // 或直接从customizer获取

    /// <summary>
    /// 生成随机答案，返回可直接序列化的 FaceSaveData
    /// </summary>
    public FaceSaveData GenerateAnswer()
    {
        // 保存当前捏脸状态，生成答案后再恢复
        string originalJson = customizer.SaveToJson();

        // 使用 customizer 的随机生成（也可以用自定义随机逻辑）
        customizer.RandomizeAll();

        // 保存随机出的数据
        string answerJson = customizer.SaveToJson();
        FaceSaveData answer = JsonUtility.FromJson<FaceSaveData>(answerJson);

        // 恢复原状
        customizer.LoadFromJson(originalJson);

        return answer;
    }
}