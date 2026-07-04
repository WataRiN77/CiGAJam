using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }
    public static GameSessionResult LastResult { get; private set; } = new GameSessionResult();

    [Header("Session")]
    [SerializeField] private CountdownTimerUI countdownTimer;
    [SerializeField] private int maxShots = 3;
    [SerializeField] private string murdererTag = "Murderer";
    [SerializeField] private bool startTimerOnEnable = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Terrain")]
    [SerializeField] private GameObject[] terrains;
    [SerializeField] private bool randomizeTerrainOnSessionStart = true;
    [SerializeField] private bool logSelectedTerrain;

    [Header("End Scene")]
    [SerializeField] private bool loadSceneOnEnd;
    [SerializeField] private string endSceneName;

    [Header("Events")]
    [SerializeField] private UnityEvent onSuccess;
    [SerializeField] private UnityEvent onFailure;
    [SerializeField] private UnityEvent onGameEnded;

    private float sessionStartTime;
    private int shotsFired;
    private int selectedTerrainNumber;
    private bool hasEnded;
    private bool lastGameSucceeded;
    private bool isDuplicateInstance;

    public int ShotsFired => shotsFired;
    public int SelectedTerrainNumber => selectedTerrainNumber;
    public bool HasEnded => hasEnded;
    public bool LastGameSucceeded => lastGameSucceeded;
    public float ElapsedSeconds => countdownTimer != null ? countdownTimer.ElapsedSeconds : Time.time - sessionStartTime;
    public float RemainingSeconds => countdownTimer != null ? countdownTimer.RemainingSeconds : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            isDuplicateInstance = true;
            enabled = false;
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (countdownTimer == null)
        {
            countdownTimer = FindObjectOfType<CountdownTimerUI>();
        }
    }

    private void OnEnable()
    {
        if (isDuplicateInstance)
        {
            return;
        }

        if (countdownTimer != null)
        {
            countdownTimer.CountdownFinished += HandleCountdownFinished;
        }

        StartSession();
    }

    private void OnDisable()
    {
        if (countdownTimer != null)
        {
            countdownTimer.CountdownFinished -= HandleCountdownFinished;
        }
    }

    public void StartSession()
    {
        shotsFired = 0;
        hasEnded = false;
        lastGameSucceeded = false;
        sessionStartTime = Time.time;
        LastResult = new GameSessionResult();

        if (randomizeTerrainOnSessionStart)
        {
            RandomizeTerrain();
        }

        LastResult.selectedTerrainNumber = selectedTerrainNumber;

        if (countdownTimer != null)
        {
            countdownTimer.ResetTimer();

            if (startTimerOnEnable)
            {
                countdownTimer.StartCountdown();
            }
        }
    }

    public void RegisterShot(Transform target)
    {
        if (hasEnded)
        {
            return;
        }

        shotsFired++;

        bool killedMurderer = target != null && target.CompareTag(murdererTag);

        if (killedMurderer)
        {
            Debug.Log(
                $"Killed Murderer. Remaining Time: {RemainingSeconds:0.00}s, Shots Fired: {shotsFired}",
                target
            );
            EndGame(true, GameEndReason.KilledMurderer);
            return;
        }

        if (shotsFired >= maxShots)
        {
            EndGame(false, GameEndReason.OutOfShots);
        }
    }

    public void EndGame(bool success, GameEndReason reason)
    {
        if (hasEnded)
        {
            return;
        }

        hasEnded = true;
        lastGameSucceeded = success;

        if (countdownTimer != null)
        {
            countdownTimer.StopCountdown();
        }

        LastResult = new GameSessionResult
        {
            success = success,
            reason = reason,
            shotsFired = shotsFired,
            elapsedSeconds = ElapsedSeconds,
            selectedTerrainNumber = selectedTerrainNumber
        };

        if (success)
        {
            onSuccess?.Invoke();
        }
        else
        {
            onFailure?.Invoke();
        }

        onGameEnded?.Invoke();

        if (loadSceneOnEnd && !string.IsNullOrWhiteSpace(endSceneName))
        {
            SceneManager.LoadScene(endSceneName);
        }
    }

    private void HandleCountdownFinished()
    {
        EndGame(false, GameEndReason.TimeUp);
    }

    public void RandomizeTerrain()
    {
        if (terrains == null || terrains.Length == 0)
        {
            selectedTerrainNumber = 0;
            return;
        }

        int selectedIndex = UnityEngine.Random.Range(0, terrains.Length);
        SetTerrainByIndex(selectedIndex);
    }

    public void SetTerrainByNumber(int terrainNumber)
    {
        if (terrains == null || terrains.Length == 0)
        {
            selectedTerrainNumber = 0;
            return;
        }

        int selectedIndex = Mathf.Clamp(terrainNumber - 1, 0, terrains.Length - 1);
        SetTerrainByIndex(selectedIndex);
    }

    private void SetTerrainByIndex(int selectedIndex)
    {
        selectedTerrainNumber = selectedIndex + 1;

        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i] != null)
            {
                terrains[i].SetActive(i == selectedIndex);
            }
        }

        if (logSelectedTerrain)
        {
            Debug.Log($"Selected Terrain: {selectedTerrainNumber}", this);
        }
    }

    private void OnValidate()
    {
        maxShots = Mathf.Max(1, maxShots);
    }
}

[Serializable]
public class GameSessionResult
{
    public bool success;
    public GameEndReason reason;
    public int shotsFired;
    public float elapsedSeconds;
    public int selectedTerrainNumber;
}

public enum GameEndReason
{
    None,
    TimeUp,
    OutOfShots,
    KilledMurderer
}
