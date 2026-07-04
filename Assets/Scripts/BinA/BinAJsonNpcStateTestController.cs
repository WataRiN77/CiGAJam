using System;
using System.IO;
using UnityEngine;

public class BinAJsonNpcStateTestController : MonoBehaviour
{
    private enum SeedFillMode
    {
        FirstNpc,
        RandomNpc,
        Murderer,
        FirstNonMurderer
    }

    [Header("Json")]
    [SerializeField] private string jsonPath = @"C:\CiGAJam\SceneBState.json";
    [SerializeField] private bool logChanges = true;

    [Header("Selected NPC")]
    [SerializeField] private int selectedSeed = -1;
    [SerializeField] private bool fillSeedFromJsonOnStart = true;
    [SerializeField] private SeedFillMode seedFillMode = SeedFillMode.RandomNpc;

    [Header("Keyboard Test")]
    [SerializeField] private bool enableKeyboardTest = true;
    [SerializeField] private KeyCode nextNpcKey = KeyCode.Tab;
    [SerializeField] private KeyCode followKey = KeyCode.F;
    [SerializeField] private KeyCode clearFollowKey = KeyCode.C;
    [SerializeField] private KeyCode toggleMarkKey = KeyCode.M;
    [SerializeField] private KeyCode toggleShotKey = KeyCode.X;
    [SerializeField] private KeyCode reloadSelectionKey = KeyCode.R;

    public int SelectedSeed => selectedSeed;

    private void Start()
    {
        if (fillSeedFromJsonOnStart && selectedSeed < 0)
        {
            FillSelectedSeedFromJson();
        }
    }

    private void Update()
    {
        if (!enableKeyboardTest)
        {
            return;
        }

        if (Input.GetKeyDown(nextNpcKey))
        {
            SelectNextNpc();
        }

        if (Input.GetKeyDown(followKey))
        {
            FollowSelectedNpc();
        }

        if (Input.GetKeyDown(clearFollowKey))
        {
            ClearFollow();
        }

        if (Input.GetKeyDown(toggleMarkKey))
        {
            ToggleSelectedMark();
        }

        if (Input.GetKeyDown(toggleShotKey))
        {
            ToggleSelectedShot();
        }

        if (Input.GetKeyDown(reloadSelectionKey))
        {
            FillSelectedSeedFromJson();
        }
    }

    [ContextMenu("Fill Selected Seed From Json")]
    public void FillSelectedSeedFromJson()
    {
        switch (seedFillMode)
        {
            case SeedFillMode.RandomNpc:
                SelectRandomNpc();
                break;
            case SeedFillMode.Murderer:
                SelectMurdererNpc();
                break;
            case SeedFillMode.FirstNonMurderer:
                SelectFirstNonMurdererNpc();
                break;
            default:
                SelectFirstNpc();
                break;
        }
    }

    [ContextMenu("Select First NPC")]
    public void SelectFirstNpc()
    {
        SceneBStateSaveData state = ReadState();

        if (state == null || state.npcs == null || state.npcs.Length == 0)
        {
            Debug.LogWarning($"No NPCs found in json: {jsonPath}", this);
            return;
        }

        selectedSeed = state.npcs[0].seed;
        Log($"Selected first NPC seed {selectedSeed}.");
    }

    [ContextMenu("Select Random NPC")]
    public void SelectRandomNpc()
    {
        SceneBStateSaveData state = ReadState();

        if (state == null || state.npcs == null || state.npcs.Length == 0)
        {
            Debug.LogWarning($"No NPCs found in json: {jsonPath}", this);
            return;
        }

        int index = UnityEngine.Random.Range(0, state.npcs.Length);
        selectedSeed = state.npcs[index].seed;
        Log($"Selected random NPC seed {selectedSeed}.");
    }

    [ContextMenu("Select Murderer NPC")]
    public void SelectMurdererNpc()
    {
        SceneBStateSaveData state = ReadState();

        if (state == null || state.npcs == null || state.npcs.Length == 0)
        {
            Debug.LogWarning($"No NPCs found in json: {jsonPath}", this);
            return;
        }

        NpcRuntimeState murderer = FindMurdererNpc(state);

        if (murderer == null)
        {
            Debug.LogWarning($"No Murderer NPC found in json: {jsonPath}", this);
            return;
        }

        selectedSeed = murderer.seed;
        Log($"Selected Murderer NPC seed {selectedSeed}.");
    }

    [ContextMenu("Select First Non Murderer NPC")]
    public void SelectFirstNonMurdererNpc()
    {
        SceneBStateSaveData state = ReadState();

        if (state == null || state.npcs == null || state.npcs.Length == 0)
        {
            Debug.LogWarning($"No NPCs found in json: {jsonPath}", this);
            return;
        }

        for (int i = 0; i < state.npcs.Length; i++)
        {
            NpcRuntimeState npc = state.npcs[i];

            if (npc != null && !npc.isMurderer)
            {
                selectedSeed = npc.seed;
                Log($"Selected non-Murderer NPC seed {selectedSeed}.");
                return;
            }
        }

        Debug.LogWarning($"No non-Murderer NPC found in json: {jsonPath}", this);
    }

    [ContextMenu("Select Next NPC")]
    public void SelectNextNpc()
    {
        SceneBStateSaveData state = ReadState();

        if (state == null || state.npcs == null || state.npcs.Length == 0)
        {
            Debug.LogWarning($"No NPCs found in json: {jsonPath}", this);
            return;
        }

        int currentIndex = -1;

        for (int i = 0; i < state.npcs.Length; i++)
        {
            if (state.npcs[i] != null && state.npcs[i].seed == selectedSeed)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % state.npcs.Length;
        selectedSeed = state.npcs[nextIndex].seed;
        Log($"Selected NPC seed {selectedSeed}.");
    }

    [ContextMenu("Follow Selected NPC")]
    public void FollowSelectedNpc()
    {
        ModifyState(state =>
        {
            bool found = false;

            for (int i = 0; i < state.npcs.Length; i++)
            {
                NpcRuntimeState npc = state.npcs[i];

                if (npc == null)
                {
                    continue;
                }

                npc.isTracked = npc.seed == selectedSeed;
                found |= npc.seed == selectedSeed;
            }

            if (!found)
            {
                Debug.LogWarning($"Selected seed {selectedSeed} was not found in json.", this);
            }
        }, $"Follow seed {selectedSeed}");
    }

    [ContextMenu("Clear Follow")]
    public void ClearFollow()
    {
        ModifyState(state =>
        {
            for (int i = 0; i < state.npcs.Length; i++)
            {
                if (state.npcs[i] != null)
                {
                    state.npcs[i].isTracked = false;
                }
            }
        }, "Clear follow");
    }

    [ContextMenu("Toggle Selected Mark")]
    public void ToggleSelectedMark()
    {
        ModifySelectedNpc(npc => npc.isMarked = !npc.isMarked, $"Toggle mark seed {selectedSeed}");
    }

    [ContextMenu("Toggle Selected Shot")]
    public void ToggleSelectedShot()
    {
        ModifySelectedNpc(npc => npc.isShot = !npc.isShot, $"Toggle shot seed {selectedSeed}");
    }

    public void SetSelectedSeed(int seed)
    {
        selectedSeed = seed;
        Log($"Selected NPC seed {selectedSeed}.");
    }

    public void FillRandomSeedFromJson()
    {
        SelectRandomNpc();
    }

    public void FillMurdererSeedFromJson()
    {
        SelectMurdererNpc();
    }

    public void FillFirstSeedFromJson()
    {
        SelectFirstNpc();
    }

    public void FollowSeed(int seed)
    {
        selectedSeed = seed;
        FollowSelectedNpc();
    }

    public void SetSelectedMarked(bool isMarked)
    {
        ModifySelectedNpc(npc => npc.isMarked = isMarked, $"Set mark seed {selectedSeed} = {isMarked}");
    }

    public void SetSelectedShot(bool isShot)
    {
        ModifySelectedNpc(npc => npc.isShot = isShot, $"Set shot seed {selectedSeed} = {isShot}");
    }

    private void ModifySelectedNpc(Action<NpcRuntimeState> apply, string label)
    {
        ModifyState(state =>
        {
            NpcRuntimeState npc = FindNpc(state, selectedSeed);

            if (npc == null)
            {
                Debug.LogWarning($"Selected seed {selectedSeed} was not found in json.", this);
                return;
            }

            apply?.Invoke(npc);
        }, label);
    }

    private void ModifyState(Action<SceneBStateSaveData> apply, string label)
    {
        SceneBStateSaveData state = ReadState();

        if (state == null)
        {
            return;
        }

        if (state.npcs == null)
        {
            state.npcs = new NpcRuntimeState[0];
        }

        apply?.Invoke(state);
        WriteState(state);
        Log(label);
    }

    private SceneBStateSaveData ReadState()
    {
        if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
        {
            Debug.LogWarning($"Json file not found: {jsonPath}", this);
            return null;
        }

        try
        {
            string json = File.ReadAllText(jsonPath);
            return JsonUtility.FromJson<SceneBStateSaveData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to read json '{jsonPath}'. {exception.Message}", this);
            return null;
        }
    }

    private void WriteState(SceneBStateSaveData state)
    {
        try
        {
            string directory = Path.GetDirectoryName(jsonPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(state, true);
            File.WriteAllText(jsonPath, json);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to write json '{jsonPath}'. {exception.Message}", this);
        }
    }

    private static NpcRuntimeState FindNpc(SceneBStateSaveData state, int seed)
    {
        if (state == null || state.npcs == null)
        {
            return null;
        }

        for (int i = 0; i < state.npcs.Length; i++)
        {
            NpcRuntimeState npc = state.npcs[i];

            if (npc != null && npc.seed == seed)
            {
                return npc;
            }
        }

        return null;
    }

    private static NpcRuntimeState FindMurdererNpc(SceneBStateSaveData state)
    {
        if (state == null || state.npcs == null)
        {
            return null;
        }

        for (int i = 0; i < state.npcs.Length; i++)
        {
            NpcRuntimeState npc = state.npcs[i];

            if (npc != null && npc.isMurderer)
            {
                return npc;
            }
        }

        if (state.murdererSeed < 0)
        {
            return null;
        }

        return FindNpc(state, state.murdererSeed);
    }

    private void Log(string message)
    {
        if (logChanges)
        {
            Debug.Log($"BinA JSON test: {message}", this);
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            jsonPath = @"C:\CiGAJam\SceneBState.json";
        }
    }
}
