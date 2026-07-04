// FaceDataManager.cs
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class FaceDataManager : MonoBehaviour
{
    public static FaceDataManager Instance { get; private set; }

    [Header("引用")]
    [SerializeField] private CharacterCustomizer2D customizer;

    private string saveDirectory;
    private string indexPath;
    private FaceSaveIndex index;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        saveDirectory = Application.persistentDataPath + "/FaceSaves/";
        indexPath = saveDirectory + "index.json";

        // 确保目录存在
        if (!Directory.Exists(saveDirectory))
            Directory.CreateDirectory(saveDirectory);

        LoadIndex();
    }

    // 加载索引
    private void LoadIndex()
    {
        if (File.Exists(indexPath))
        {
            string json = File.ReadAllText(indexPath);
            index = JsonUtility.FromJson<FaceSaveIndex>(json);
        }
        if (index == null) index = new FaceSaveIndex();
    }

    // 保存索引
    private void SaveIndex()
    {
        string json = JsonUtility.ToJson(index, true);
        File.WriteAllText(indexPath, json);
    }

    // 保存当前捏脸为新存档
    public void SaveCurrentAsNew(string saveName)
    {
        if (customizer == null)
        {
            Debug.LogError("FaceDataManager: customizer 未设置");
            return;
        }

        // 生成唯一文件名
        string fileName = "face_" + System.DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Random.Range(1000, 9999) + ".json";
        string filePath = saveDirectory + fileName;

        // 获取当前捏脸数据并写入文件
        string faceJson = customizer.SaveToJson();
        File.WriteAllText(filePath, faceJson);

        // 添加到索引
        index.entries.Add(new FaceSaveEntry
        {
            saveName = saveName,
            fileName = fileName,
            createdAt = System.DateTime.Now.ToString("g")
        });
        SaveIndex();
        Debug.Log($"存档已保存: {saveName}");
    }

    // 覆盖指定存档（通过文件名）
    public void OverwriteSave(string fileName)
    {
        if (customizer == null) return;
        string filePath = saveDirectory + fileName;
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"存档文件不存在: {fileName}");
            return;
        }

        string faceJson = customizer.SaveToJson();
        File.WriteAllText(filePath, faceJson);
        Debug.Log($"存档已覆盖: {fileName}");
    }

    // 加载指定存档
    public void LoadSave(string fileName)
    {
        string filePath = saveDirectory + fileName;
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"存档文件不存在: {fileName}");
            return;
        }

        string json = File.ReadAllText(filePath);
        customizer.LoadFromJson(json);
        Debug.Log($"已加载存档: {fileName}");
    }

    // 删除存档（同时删除文件与索引）
    public void DeleteSave(string fileName)
    {
        string filePath = saveDirectory + fileName;
        if (File.Exists(filePath))
            File.Delete(filePath);

        index.entries.RemoveAll(e => e.fileName == fileName);
        SaveIndex();
        Debug.Log($"已删除存档: {fileName}");
    }

    // 获取所有存档列表（用于 UI 显示）
    public List<FaceSaveEntry> GetAllSaves()
    {
        return index.entries;
    }

    public void ClearAllSaves()
    {
        // 删除目录下所有文件
        foreach (string file in Directory.GetFiles(saveDirectory))
        {
            File.Delete(file);
        }

        // 清空索引
        index.entries.Clear();
        SaveIndex();

        Debug.Log("所有存档已清除");
    }
}