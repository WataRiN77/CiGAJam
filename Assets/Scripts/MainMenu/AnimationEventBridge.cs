using UnityEngine;

public class AnimationEventBridge : MonoBehaviour
{
    /// <summary>
    /// 提供给背景动画最后一帧的 Event 调用
    /// </summary>
    public void OnIntroAnimationFinished()
    {
        // 跨物体呼叫主菜单控制器，激活并渐显按钮
        if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.ActivateMenuButtons();
        }
    }
}