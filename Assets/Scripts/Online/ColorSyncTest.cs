using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class ColorSyncTest : MonoBehaviourPun
{
    [Header("UI 绑定")]
    [SerializeField] private TMP_InputField inputField; // 输入框
    [SerializeField] private Button btnSend;            // 发送按钮
    [SerializeField] private Image imgColorDisplay;     // 显示颜色的色块

    private void Start()
    {
        // 绑定本地按钮点击事件
        btnSend.onClick.AddListener(OnSendClicked);
    }

    private void OnSendClicked()
    {
        // 1. 获取输入框的内容并转为整数
        if (int.TryParse(inputField.text, out int number))
        {
            // 限制输入必须在 1-5 之间
            if (number >= 1 && number <= 5)
            {
                // 2. 发送 RPC！
                // RpcTarget.All 表示“包括我自己在内，房间里的所有人都会执行这个同步函数”
                photonView.RPC("SyncColorOnAllClients", RpcTarget.All, number);
            }
            else
            {
                Debug.LogWarning("请输入 1 到 5 之间的数字！");
            }
        }
        else
        {
            Debug.LogWarning("请输入有效的数字！");
        }
    }

    /// <summary>
    /// 被网络同步执行的函数。必须加上 [PunRPC] 标签！
    /// </summary>
    [PunRPC]
    private void SyncColorOnAllClients(int colorIndex)
    {
        Debug.Log($"[网络同步] 收到指令，将颜色修改为对应 ID: {colorIndex}");

        // 根据 1-5 的数字修改颜色
        switch (colorIndex)
        {
            case 1:
                imgColorDisplay.color = Color.red; // 红
                break;
            case 2:
                imgColorDisplay.color = new Color(1f, 0.6f, 0f); // 橙 (RGB配比)
                break;
            case 3:
                imgColorDisplay.color = Color.yellow; // 黄
                break;
            case 4:
                imgColorDisplay.color = Color.green; // 绿
                break;
            case 5:
                imgColorDisplay.color = Color.blue; //蓝
                break;
        }
    }
}