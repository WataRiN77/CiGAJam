using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

// 继承 MonoBehaviourPunCallbacks 可以方便地监听 Photon 的各种网络事件
public class PhotonLobby : MonoBehaviourPunCallbacks
{
    [Header("UI 绑定")]
    [SerializeField] private Button btnCreate;
    [SerializeField] private Button btnJoin;
    [SerializeField] private TMP_Text txtStatus; // 用于显示当前连接状态/房间码
    [SerializeField] private TMP_InputField inputJoinCode;

    private void Start()
    {
        btnCreate.interactable = false;
        btnJoin.interactable = false;
        txtStatus.text = "正在连接到中国服务器...";

        // 1. 初始化并连接到 Photon 状态服务器
        PhotonNetwork.ConnectUsingSettings();

        // 2. 绑定按钮事件
        btnCreate.onClick.AddListener(CreateRoom);
        btnJoin.onClick.AddListener(JoinRoom);
    }

    // 成功连接到 Photon 状态服务器的回调
    public override void OnConnectedToMaster()
    {
        Debug.Log("[Photon] 成功连接至 Master 服务器！");
        txtStatus.text = "已连接到服务器，可以创建或加入房间。";
        btnCreate.interactable = true;
        btnJoin.interactable = true;
    }

    // 创建房间
    private void CreateRoom()
    {
        // 随机生成一个 6 位数的房间码
        string roomCode = Random.Range(100000, 999999).ToString();

        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = 2; // 限制 2 人

        txtStatus.text = "正在创建房间...";
        btnCreate.interactable = false;

        // 调用 Photon 创建房间
        PhotonNetwork.CreateRoom(roomCode, roomOptions);
    }

    // 创建房间成功的回调
    public override void OnCreatedRoom()
    {
        string currentRoomCode = PhotonNetwork.CurrentRoom.Name;
        txtStatus.text = $"房间创建成功！\n房间码: {currentRoomCode}";
        Debug.Log($"[Photon] 房间创建成功！房间名/房间码: {currentRoomCode}");
    }

    // 加入房间
    private void JoinRoom()
    {
        string roomCode = inputJoinCode.text.Trim();
        if (string.IsNullOrEmpty(roomCode))
        {
            txtStatus.text = "请输入房间码！";
            return;
        }

        txtStatus.text = "正在加入房间...";
        btnJoin.interactable = false;

        // 调用 Photon 加入房间
        PhotonNetwork.JoinRoom(roomCode);
    }

    // 成功加入房间的回调
    public override void OnJoinedRoom()
    {
        txtStatus.text = $"已成功进入房间: {PhotonNetwork.CurrentRoom.Name}！";
        Debug.Log($"[Photon] 成功加入房间！当前房间玩家数: {PhotonNetwork.CurrentRoom.PlayerCount}");

        // 此时两个玩家已经在同一个房间了！
        // 之后可以在这里写跳转对战场景的逻辑
    }

    // 加入房间失败的回调
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        txtStatus.text = $"加入失败: {message}";
        btnJoin.interactable = true;
        Debug.LogError($"[Photon] 加入房间失败: {message}");
    }
}