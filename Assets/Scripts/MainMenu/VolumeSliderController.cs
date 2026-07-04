using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Slider))]
public class VolumeSliderController : MonoBehaviour, IPointerUpHandler
{
    public enum VolumeType { Music, SFX }
    [Header("音量配置")]
    [SerializeField] private VolumeType type;

    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        // 规范 Slider 范围为 0 到 100
        slider.minValue = 0f;
        slider.maxValue = 100f;
    }

    // 当玩家拖拽完并松开鼠标的那一瞬间触发
    public void OnPointerUp(PointerEventData eventData)
    {
        float volumeValue = slider.value;

        if (type == VolumeType.Music)
        {
            AkUnitySoundEngine.SetRTPCValue("Volume_Music", volumeValue);
            Debug.Log($"[Wwise] 音乐音量已调整为: {volumeValue}");
        }
        else if (type == VolumeType.SFX)
        {
            AkUnitySoundEngine.SetRTPCValue("Volume_SFX", volumeValue);
            Debug.Log($"[Wwise] 音效音量已调整为: {volumeValue}");
        }
    }
}