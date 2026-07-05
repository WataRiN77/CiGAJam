using UnityEngine;

public static class GameSessionData
{
    public const int BaseNpcCount = 5;
    public const int NpcCountIncreasePerSuccess = 2;

    public static bool IsCaptureSuccess = true;
    public static string SuspectCodename = "Code X";
    public static int MurdererSeed = -1;
    public static float CaptureDurationValue = 130f;
    public static string SuspectFaceJson = "";
    public static string ArtistFaceJson = "";
    public static string SceneBStateJson = "";
    public static int CurrentNpcCount = BaseNpcCount;

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

    public static void ResetRoundProgress()
    {
        CurrentNpcCount = BaseNpcCount;
        ClearRoundFaceData();
    }

    public static int AdvanceNpcCountForSuccessfulContinue()
    {
        CurrentNpcCount = Mathf.Max(BaseNpcCount, CurrentNpcCount) + NpcCountIncreasePerSuccess;
        return CurrentNpcCount;
    }

    public static void SetCurrentNpcCount(int npcCount)
    {
        CurrentNpcCount = Mathf.Max(BaseNpcCount, npcCount);
    }

    public static void ClearRoundFaceData()
    {
        IsCaptureSuccess = true;
        SuspectCodename = "Code X";
        MurdererSeed = -1;
        CaptureDurationValue = 0f;
        SuspectFaceJson = "";
        ArtistFaceJson = "";
        SceneBStateJson = "";
    }
}
