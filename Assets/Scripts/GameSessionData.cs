using UnityEngine;

public static class GameSessionData
{
    public static bool IsCaptureSuccess = true;
    public static string SuspectCodename = "Code X";
    public static int MurdererSeed = -1;
    public static float CaptureDurationValue = 130f;
    public static string SuspectFaceJson = "";
    public static string ArtistFaceJson = "";
    public static string SceneBStateJson = "";

    public static void SetSettlementData(
        bool isCaptureSuccess,
        int murdererSeed,
        string suspectFaceJson,
        string artistFaceJson,
        float captureDurationValue,
        string sceneBStateJson)
    {
        IsCaptureSuccess = isCaptureSuccess;
        MurdererSeed = murdererSeed;
        SuspectCodename = murdererSeed >= 0 ? $"Seed {murdererSeed}" : "Unknown";
        SuspectFaceJson = suspectFaceJson ?? "";
        ArtistFaceJson = artistFaceJson ?? "";
        CaptureDurationValue = captureDurationValue;
        SceneBStateJson = sceneBStateJson ?? "";
    }
}
