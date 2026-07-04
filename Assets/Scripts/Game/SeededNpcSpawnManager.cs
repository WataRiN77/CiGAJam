using System;
using System.Collections.Generic;
using UnityEngine;

public class SeededNpcSpawnManager : MonoBehaviour
{
    public static string[] PendingNpcKeys { get; private set; }
    public static string PendingMurdererKey { get; private set; }

    [Header("Prefab")]
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private Transform spawnParent;

    [Header("Manual Keys")]
    [SerializeField] private bool useManualKeys = true;
    [SerializeField] private string[] manualNpcKeys;
    [SerializeField] private string manualMurdererKey;

    [Header("Cross Scene Keys")]
    [SerializeField] private bool preferPendingKeys = true;
    [SerializeField] private bool clearPendingKeysAfterSpawn;

    [Header("Spawn")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool clearPreviouslySpawned = true;
    [SerializeField] private Vector3 spawnCenter;
    [SerializeField] private Vector3 spawnRange = new Vector3(10f, 0f, 10f);
    [SerializeField] private float minDistanceBetweenNpcs = 1.2f;
    [SerializeField] private Vector3 spawnRotationEuler;

    [Header("Seed Salts")]
    [SerializeField] private int faceSeedSalt = 1103;
    [SerializeField] private int movementSeedSalt = 2207;
    [SerializeField] private int positionSeedSalt = 3301;

    [Header("Tags")]
    [SerializeField] private string murdererTag = "Murderer";

    [Header("Scene Gizmos")]
    [SerializeField] private bool alwaysShowSpawnRange = true;
    [SerializeField] private Color rangeColor = new Color(1f, 0.85f, 0.1f, 0.25f);

    private readonly List<GameObject> spawnedNpcs = new List<GameObject>();

    public IReadOnlyList<GameObject> SpawnedNpcs => spawnedNpcs;

    public static void SetPendingKeys(IEnumerable<string> npcKeys, string murdererKey)
    {
        PendingNpcKeys = npcKeys == null ? null : new List<string>(npcKeys).ToArray();
        PendingMurdererKey = murdererKey;
    }

    public static void ClearPendingKeys()
    {
        PendingNpcKeys = null;
        PendingMurdererKey = null;
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnFromConfiguredKeys();
        }
    }

    [ContextMenu("Spawn From Configured Keys")]
    public void SpawnFromConfiguredKeys()
    {
        string[] keys = GetActiveKeys();
        string murdererKey = GetActiveMurdererKey();
        SpawnFromKeys(keys, murdererKey);

        if (clearPendingKeysAfterSpawn)
        {
            ClearPendingKeys();
        }
    }

    public void SpawnFromKeys(string[] npcKeys, string murdererKey)
    {
        if (npcPrefab == null)
        {
            Debug.LogWarning($"{nameof(SeededNpcSpawnManager)} needs an NPC prefab.", this);
            return;
        }

        if (clearPreviouslySpawned)
        {
            ClearSpawnedNpcs();
        }

        if (npcKeys == null || npcKeys.Length == 0)
        {
            Debug.LogWarning($"{nameof(SeededNpcSpawnManager)} has no NPC keys to spawn.", this);
            return;
        }

        List<Vector3> usedPositions = new List<Vector3>();

        for (int i = 0; i < npcKeys.Length; i++)
        {
            string key = NormalizeKey(npcKeys[i]);

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            bool isMurderer = AreKeysEqual(key, murdererKey);
            SpawnSingleNpc(key, i, isMurderer, usedPositions);
        }
    }

    public void ClearSpawnedNpcs()
    {
        for (int i = spawnedNpcs.Count - 1; i >= 0; i--)
        {
            if (spawnedNpcs[i] != null)
            {
                Destroy(spawnedNpcs[i]);
            }
        }

        spawnedNpcs.Clear();
    }

    private void SpawnSingleNpc(string key, int index, bool isMurderer, List<Vector3> usedPositions)
    {
        int baseSeed = GetStableSeed(key);
        Vector3 position = GetSpawnPosition(baseSeed, index, usedPositions);
        Quaternion rotation = Quaternion.Euler(spawnRotationEuler);
        Transform parent = spawnParent != null ? spawnParent : transform;

        GameObject npc = Instantiate(npcPrefab, position, rotation, parent);
        npc.name = $"{npcPrefab.name}_{index}_{key}";
        spawnedNpcs.Add(npc);

        ApplyFaceSeed(npc, CombineSeed(baseSeed, faceSeedSalt));
        ApplyMovementSeed(npc, CombineSeed(baseSeed, movementSeedSalt));

        if (isMurderer)
        {
            TrySetTag(npc, murdererTag);
        }
    }

    private void ApplyFaceSeed(GameObject npc, int seed)
    {
        RandomFaceGenerator2D faceGenerator = npc.GetComponentInChildren<RandomFaceGenerator2D>(true);

        if (faceGenerator != null)
        {
            faceGenerator.GenerateRandomFace(seed);
        }
    }

    private void ApplyMovementSeed(GameObject npc, int seed)
    {
        RandomWanderFloat wander = npc.GetComponent<RandomWanderFloat>();

        if (wander == null)
        {
            wander = npc.GetComponentInChildren<RandomWanderFloat>(true);
        }

        if (wander != null)
        {
            wander.SetFixedSeed(seed);
        }
    }

    private Vector3 GetSpawnPosition(int baseSeed, int index, List<Vector3> usedPositions)
    {
        int positionSeed = CombineSeed(baseSeed + index * 7919, positionSeedSalt);
        System.Random random = new System.Random(positionSeed);
        int attempts = Mathf.Max(12, usedPositions.Count * 6);
        Vector3 candidate = spawnCenter;

        for (int i = 0; i < attempts; i++)
        {
            candidate = GetRandomPointInSpawnRange(random);

            if (IsFarEnough(candidate, usedPositions))
            {
                usedPositions.Add(candidate);
                return candidate;
            }
        }

        usedPositions.Add(candidate);
        return candidate;
    }

    private Vector3 GetRandomPointInSpawnRange(System.Random random)
    {
        float halfX = Mathf.Abs(spawnRange.x) * 0.5f;
        float halfY = Mathf.Abs(spawnRange.y) * 0.5f;
        float halfZ = Mathf.Abs(spawnRange.z) * 0.5f;

        return spawnCenter + new Vector3(
            RandomRange(random, -halfX, halfX),
            RandomRange(random, -halfY, halfY),
            RandomRange(random, -halfZ, halfZ)
        );
    }

    private bool IsFarEnough(Vector3 candidate, List<Vector3> usedPositions)
    {
        if (minDistanceBetweenNpcs <= 0f)
        {
            return true;
        }

        for (int i = 0; i < usedPositions.Count; i++)
        {
            if (Vector3.Distance(candidate, usedPositions[i]) < minDistanceBetweenNpcs)
            {
                return false;
            }
        }

        return true;
    }

    private string[] GetActiveKeys()
    {
        if (preferPendingKeys && PendingNpcKeys != null && PendingNpcKeys.Length > 0)
        {
            return PendingNpcKeys;
        }

        return useManualKeys ? manualNpcKeys : PendingNpcKeys;
    }

    private string GetActiveMurdererKey()
    {
        if (preferPendingKeys && PendingNpcKeys != null && PendingNpcKeys.Length > 0)
        {
            return PendingMurdererKey;
        }

        return useManualKeys ? manualMurdererKey : PendingMurdererKey;
    }

    private static bool AreKeysEqual(string a, string b)
    {
        return string.Equals(NormalizeKey(a), NormalizeKey(b), StringComparison.Ordinal);
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }

    private static int GetStableSeed(string key)
    {
        key = NormalizeKey(key);

        if (int.TryParse(key, out int intSeed))
        {
            return intSeed;
        }

        unchecked
        {
            int hash = 23;

            for (int i = 0; i < key.Length; i++)
            {
                hash = hash * 31 + key[i];
            }

            return hash;
        }
    }

    private static int CombineSeed(int seed, int salt)
    {
        unchecked
        {
            return seed * 397 ^ salt;
        }
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private void TrySetTag(GameObject target, string tagName)
    {
        if (target == null || string.IsNullOrWhiteSpace(tagName))
        {
            return;
        }

        try
        {
            target.tag = tagName;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"Tag '{tagName}' does not exist. Please create it in Unity Tag Manager.", target);
        }
    }

    private void OnDrawGizmos()
    {
        if (!alwaysShowSpawnRange)
        {
            return;
        }

        DrawSpawnRange();
    }

    private void OnDrawGizmosSelected()
    {
        DrawSpawnRange();
    }

    private void DrawSpawnRange()
    {
        Vector3 size = new Vector3(Mathf.Abs(spawnRange.x), Mathf.Abs(spawnRange.y), Mathf.Abs(spawnRange.z));
        Color fill = rangeColor;
        fill.a = 0.12f;
        Gizmos.color = fill;
        Gizmos.DrawCube(spawnCenter, size);

        Color wire = rangeColor;
        wire.a = 0.9f;
        Gizmos.color = wire;
        Gizmos.DrawWireCube(spawnCenter, size);
    }

    private void OnValidate()
    {
        minDistanceBetweenNpcs = Mathf.Max(0f, minDistanceBetweenNpcs);
    }
}
