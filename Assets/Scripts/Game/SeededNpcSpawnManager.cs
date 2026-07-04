using System;
using System.Collections.Generic;
using UnityEngine;

public class SeededNpcSpawnManager : MonoBehaviour
{
    public static int[] PendingNpcSeeds { get; private set; }
    public static int? PendingMurdererSeed { get; private set; }

    [Header("Prefab")]
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private Transform spawnParent;

    [Header("Manual Seeds")]
    [SerializeField] private bool useManualKeys = true;
    [SerializeField] private int[] manualNpcSeeds;
    [SerializeField] private int manualMurdererSeed;
    [SerializeField] private bool useManualMurdererSeed = true;
    [SerializeField] private bool spawnMissingMurdererSeed = true;

    [Header("Cross Scene Seeds")]
    [SerializeField] private bool preferPendingKeys = true;
    [SerializeField] private bool clearPendingSeedsAfterSpawn;

    [Header("Spawn")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool clearPreviouslySpawned = true;
    [SerializeField] private Vector3 spawnCenter;
    [SerializeField] private Vector3 spawnRange = new Vector3(10f, 0f, 10f);
    [SerializeField] private float minDistanceBetweenNpcs = 1.2f;
    [SerializeField] private Vector3 spawnRotationEuler;

    [Header("Seed Salts")]
    [SerializeField] private bool useRawKeySeedForFace = true;
    [SerializeField] private int faceSeedSalt;
    [SerializeField] private int movementSeedSalt = 2207;
    [SerializeField] private int positionSeedSalt = 3301;

    [Header("Tags")]
    [SerializeField] private string murdererTag = "Murderer";

    [Header("Scene Gizmos")]
    [SerializeField] private bool alwaysShowSpawnRange = true;
    [SerializeField] private Color rangeColor = new Color(1f, 0.85f, 0.1f, 0.25f);

    private readonly List<GameObject> spawnedNpcs = new List<GameObject>();

    public IReadOnlyList<GameObject> SpawnedNpcs => spawnedNpcs;

    public static void SetPendingSeeds(IEnumerable<int> npcSeeds, int murdererSeed)
    {
        PendingNpcSeeds = npcSeeds == null ? null : new List<int>(npcSeeds).ToArray();
        PendingMurdererSeed = murdererSeed;
    }

    public static void SetPendingSeeds(IEnumerable<int> npcSeeds, int? murdererSeed)
    {
        PendingNpcSeeds = npcSeeds == null ? null : new List<int>(npcSeeds).ToArray();
        PendingMurdererSeed = murdererSeed;
    }

    public static void ClearPendingSeeds()
    {
        PendingNpcSeeds = null;
        PendingMurdererSeed = null;
    }

    public static void ClearPendingKeys()
    {
        ClearPendingSeeds();
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
        int[] seeds = GetActiveSeeds();
        int? murdererSeed = GetActiveMurdererSeed();
        SpawnFromSeeds(seeds, murdererSeed);

        if (clearPendingSeedsAfterSpawn)
        {
            ClearPendingSeeds();
        }
    }

    public void SpawnFromSeeds(int[] npcSeeds, int? murdererSeed)
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

        if ((npcSeeds == null || npcSeeds.Length == 0) && !murdererSeed.HasValue)
        {
            Debug.LogWarning($"{nameof(SeededNpcSpawnManager)} has no NPC seeds to spawn.", this);
            return;
        }

        List<Vector3> usedPositions = new List<Vector3>();
        bool foundMurdererSeed = !murdererSeed.HasValue;
        int spawnedCount = 0;

        if (npcSeeds != null)
        {
            for (int i = 0; i < npcSeeds.Length; i++)
            {
                int seed = npcSeeds[i];
                bool isMurderer = murdererSeed.HasValue && seed == murdererSeed.Value;
                foundMurdererSeed |= isMurderer;
                SpawnSingleNpc(seed, spawnedCount, isMurderer, usedPositions);
                spawnedCount++;
            }
        }

        if (!foundMurdererSeed && spawnMissingMurdererSeed && murdererSeed.HasValue)
        {
            SpawnSingleNpc(murdererSeed.Value, spawnedCount, true, usedPositions);
            foundMurdererSeed = true;
        }
        else if (!foundMurdererSeed && murdererSeed.HasValue)
        {
            Debug.LogWarning(
                $"{nameof(SeededNpcSpawnManager)} did not find Murderer Seed '{murdererSeed.Value}' in NPC seeds.",
                this
            );
        }
    }

    public void ClearSpawnedNpcs()
    {
        for (int i = spawnedNpcs.Count - 1; i >= 0; i--)
        {
            GameObject spawnedNpc = spawnedNpcs[i];

            if (spawnedNpc == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(spawnedNpc);
            }
            else
            {
                DestroyImmediate(spawnedNpc);
            }
        }

        spawnedNpcs.Clear();
    }

    private void SpawnSingleNpc(int seed, int index, bool isMurderer, List<Vector3> usedPositions)
    {
        int baseSeed = seed;
        Vector3 position = GetSpawnPosition(baseSeed, index, usedPositions);
        Quaternion rotation = Quaternion.Euler(spawnRotationEuler);
        Transform parent = spawnParent != null ? spawnParent : transform;

        GameObject npc = Instantiate(npcPrefab, position, rotation, parent);
        npc.name = $"{npcPrefab.name}_{index}_{seed}";
        spawnedNpcs.Add(npc);
        SeededNpcIdentity identity = npc.GetComponent<SeededNpcIdentity>();

        if (identity == null)
        {
            identity = npc.AddComponent<SeededNpcIdentity>();
        }

        int faceSeed = useRawKeySeedForFace ? baseSeed : CombineSeed(baseSeed, faceSeedSalt);
        ApplyFaceSeed(npc, faceSeed);
        RandomWanderFloat wander = ApplyMovementSeed(npc, CombineSeed(baseSeed, movementSeedSalt));
        identity.Initialize(seed, baseSeed, faceSeed, isMurderer);

        if (isMurderer)
        {
            TrySetTag(npc, murdererTag);
            if (wander != null && wander.gameObject != npc)
            {
                TrySetTag(wander.gameObject, murdererTag);
            }

            Debug.Log($"Spawned Murderer NPC from seed '{seed}'.", wander != null ? wander.gameObject : npc);
        }
    }

    private void ApplyFaceSeed(GameObject npc, int seed)
    {
        FaceGenerator faceGenerator = npc.GetComponentInChildren<FaceGenerator>(true);

        if (faceGenerator != null)
        {
            Debug.Log("1234321");
            faceGenerator.GenerateAndApplyFace(seed);
            return;
        }

        RandomFaceGenerator2D fallbackGenerator = npc.GetComponentInChildren<RandomFaceGenerator2D>(true);

        if (fallbackGenerator != null)
        {
            fallbackGenerator.GenerateRandomFace(seed);
        }
    }

    private RandomWanderFloat ApplyMovementSeed(GameObject npc, int seed)
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

        return wander;
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

    private int[] GetActiveSeeds()
    {
        if (preferPendingKeys && PendingNpcSeeds != null && PendingNpcSeeds.Length > 0)
        {
            return PendingNpcSeeds;
        }

        return useManualKeys ? manualNpcSeeds : PendingNpcSeeds;
    }

    private int? GetActiveMurdererSeed()
    {
        if (preferPendingKeys && PendingNpcSeeds != null && PendingNpcSeeds.Length > 0)
        {
            return PendingMurdererSeed;
        }

        return useManualKeys && useManualMurdererSeed ? manualMurdererSeed : PendingMurdererSeed;
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
