using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
public class UISoundManager : MonoBehaviour
{
    [Header("Wwise 事件")]
    public AK.Wwise.Event hoverEnterEvent;   // 鼠标进入子按钮时播放
    public AK.Wwise.Event hoverExitEvent;    // 鼠标离开子按钮时播放
    public AK.Wwise.Event clickEvent;        // 点击子按钮时播放

    [Header("设置")]
    public bool enableHoverSound = true;
    public bool enableClickSound = true;

    private HashSet<Button> boundButtons = new HashSet<Button>();

    void Start(){}

    private void AddHoverEvents(Button btn)
    {
        // 获取或创建 EventTrigger
        EventTrigger trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = btn.gameObject.AddComponent<EventTrigger>();

        // 创建 PointerEnter 条目
        var enterEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        enterEntry.callback.AddListener((data) => OnPointerEnter());

        // 创建 PointerExit 条目
        var exitEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        exitEntry.callback.AddListener((data) => OnPointerExit());

        trigger.triggers.Add(enterEntry);
        trigger.triggers.Add(exitEntry);
    }

    public void PlayClickSound()
    {
        if (clickEvent != null && enableClickSound)
            clickEvent.Post(gameObject);
    }

    public void PlayHoverEnter()
    {
        if (hoverEnterEvent != null && enableHoverSound)
            hoverEnterEvent.Post(gameObject);
    }

    public void PlayHoverExit()
    {
        if (hoverExitEvent != null && enableHoverSound)
            hoverExitEvent.Post(gameObject);
    }
    private void OnPointerEnter()
    {
        PlayHoverEnter();
    }

    private void OnPointerExit()
    {
        PlayHoverExit();
    }
}