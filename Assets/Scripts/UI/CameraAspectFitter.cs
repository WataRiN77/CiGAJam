using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraAspectFitter : MonoBehaviour
{
    [Header("目标比例")]
    public float targetAspectWidth = 16f;
    public float targetAspectHeight = 9f;

    [Header("同步摄像机列表")]
    [Tooltip("将需要同步黑边裁剪的其他摄像机（如 UI、边界或背景摄像机）拖入此处，防止黑边区域画面穿帮")]
    public List<Camera> syncCameras = new List<Camera>();

    private Camera mainCamera;

    private int lastScreenWidth = 0;
    private int lastScreenHeight = 0;

    void Awake()
    {
        mainCamera = GetComponent<Camera>();
        ApplyAspectRatio();
    }

    void Start()
    {
        ApplyAspectRatio();
    }

    public void ApplyAspectRatio()
    {
        if (mainCamera == null) return;

        float targetAspect = targetAspectWidth / targetAspectHeight;
        float windowAspect = (float)Screen.width / (float)Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        Rect targetRect;

        // 如果屏幕比例比 16:9 更窄 (比如 4:3)：上下留黑边 (Letterbox)
        if (scaleHeight < 1.0f)
        {
            targetRect = new Rect(0, (1.0f - scaleHeight) / 2.0f, 1.0f, scaleHeight);
        }
        // 如果屏幕比例比 16:9 更宽 (比如 21:9)：左右留黑边 (Pillarbox)
        else
        {
            float scaleWidth = 1.0f / scaleHeight;
            targetRect = new Rect((1.0f - scaleWidth) / 2.0f, 0, scaleWidth, 1.0f);
        }

        // 应用裁剪到主摄像机
        mainCamera.rect = targetRect;

        // 遍历并应用到所有关联的同步摄像机，防止画面溢出到黑边里
        foreach (Camera cam in syncCameras)
        {
            if (cam != null)
            {
                cam.rect = targetRect;
            }
        }
    }
    void Update()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            ApplyAspectRatio();
        }
    }
}