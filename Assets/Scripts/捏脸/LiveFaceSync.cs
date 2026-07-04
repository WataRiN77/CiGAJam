using System.Collections;
using UnityEngine;

public class LiveFaceSync : MonoBehaviour
{
    [SerializeField] private CharacterCustomizer2D customizer;
    [SerializeField] private float saveInterval = 0.5f; // 保存间隔

    private bool dirty = false;
    private Coroutine autoSaveCoroutine;

    private void Start()
    {
        if (customizer == null)
            customizer = GetComponent<CharacterCustomizer2D>();

        // 订阅变更事件，设置脏标记
        if (customizer != null)
            customizer.OnFaceChanged += MarkDirty;

        // 启动定时保存协程
        autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
    }

    private void OnDestroy()
    {
        if (customizer != null)
            customizer.OnFaceChanged -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;

    private IEnumerator AutoSaveCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(saveInterval);
            if (dirty)
            {
                SaveCurrentFace();
                dirty = false;
            }
        }
    }

    private void SaveCurrentFace()
    {
        if (customizer == null) return;
        string json = customizer.SaveToJson();
        string dir = System.IO.Path.Combine(Application.persistentDataPath, "LivePlayerFace");
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);
        string filePath = System.IO.Path.Combine(dir, "current_face.json");
        System.IO.File.WriteAllText(filePath, json);
    }

    // 可选的立即保存（如提交时）
    public void ForceSave()
    {
        SaveCurrentFace();
        dirty = false;
    }
}