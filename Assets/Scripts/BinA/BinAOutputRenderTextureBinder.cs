using System;
using UnityEngine;
using UnityEngine.UI;

public class BinAOutputRenderTextureBinder : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private Camera outputCamera;
    [SerializeField] private RenderTexture outputTexture;
    [SerializeField] private RawImage targetRawImage;
    [SerializeField] private bool bindOnStart = true;
    [SerializeField] private bool forceCameraEnabled = true;
    [SerializeField] private bool renderOnceAfterBinding;

    [Header("Auto Find")]
    [SerializeField] private bool autoFindMissingReferences = true;
    [SerializeField] private string outputCameraNameContains = "output";
    [SerializeField] private string rawImageNameContains = "roll";

    private void Start()
    {
        if (bindOnStart)
        {
            Bind();
        }
    }

    [ContextMenu("Bind Output Texture")]
    public void Bind()
    {
        if (autoFindMissingReferences)
        {
            FindMissingReferences();
        }

        if (outputTexture == null && outputCamera != null)
        {
            outputTexture = outputCamera.targetTexture;
        }

        if (outputTexture == null && targetRawImage != null)
        {
            outputTexture = targetRawImage.texture as RenderTexture;
        }

        if (outputCamera != null)
        {
            if (forceCameraEnabled)
            {
                outputCamera.enabled = true;
            }

            if (outputTexture != null)
            {
                outputCamera.targetTexture = outputTexture;
            }
            else
            {
                Debug.LogWarning("Output camera has no RenderTexture. Assign Output Texture or set Camera Target Texture.", this);
            }
        }
        else
        {
            Debug.LogWarning("No output camera found. Assign Output Camera.", this);
        }

        if (targetRawImage != null)
        {
            if (outputTexture != null)
            {
                targetRawImage.texture = outputTexture;
                targetRawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
            else
            {
                Debug.LogWarning("RawImage has no RenderTexture to display.", this);
            }
        }
        else
        {
            Debug.LogWarning("No target RawImage found. Assign Target Raw Image.", this);
        }

        if (renderOnceAfterBinding && outputCamera != null && outputTexture != null)
        {
            outputCamera.Render();
        }
    }

    private void FindMissingReferences()
    {
        if (outputCamera == null)
        {
            outputCamera = FindCameraByName(outputCameraNameContains);
        }

        if (targetRawImage == null)
        {
            targetRawImage = FindRawImageByName(rawImageNameContains);
        }
    }

    private static Camera FindCameraByName(string namePart)
    {
        Camera[] cameras = FindObjectsOfType<Camera>(true);

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];

            if (camera != null && ContainsIgnoreCase(camera.name, namePart))
            {
                return camera;
            }
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];

            if (camera != null && camera.targetTexture != null)
            {
                return camera;
            }
        }

        return null;
    }

    private static RawImage FindRawImageByName(string namePart)
    {
        RawImage[] rawImages = FindObjectsOfType<RawImage>(true);

        for (int i = 0; i < rawImages.Length; i++)
        {
            RawImage rawImage = rawImages[i];

            if (rawImage != null && ContainsIgnoreCase(rawImage.name, namePart))
            {
                return rawImage;
            }
        }

        for (int i = 0; i < rawImages.Length; i++)
        {
            RawImage rawImage = rawImages[i];

            if (rawImage != null && string.Equals(rawImage.name, "B", StringComparison.OrdinalIgnoreCase))
            {
                return rawImage;
            }
        }

        return null;
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
