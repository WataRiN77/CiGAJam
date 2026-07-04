using System.Collections;
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
    [SerializeField] private bool waitForNetworkSeedsOnStart = true;
    [SerializeField] private float networkSeedWaitTimeout = 5f;
    [SerializeField] private bool clearPendingSeedsAfterSpawn;

    [Header("SceneB Json Seeds")]
    [SerializeField] private bool useSceneBJsonSeedsOnStart;
    [SerializeField] private bool waitForSceneBJsonSeeds = true;
    [SerializeField] private float sceneBJsonSeedWaitTimeout = 5f;
    [SerializeField] private bool fallbackToConfiguredSeedsWhenJsonMissing;

    [Header("Generated Seeds")]
    [SerializeField] private bool generateSeedsWhenNoneConfigured = true;
    [SerializeField] private int generatedNpcCount = 5;
    [SerializeField] private int generatedSeedMin = 1000;
    [SerializeField] private int generatedSeedMax = 999999;

    [Header("Spawn")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool clearPreviouslySpawned = true;
    [SerializeField] private Vector3 spawnCenter;
    [SerializeField] private Vector3 spawnRange = new Vector3(10f, 0f, 10f);
    [SerializeField] private float minDistanceBetweenNpcs = 1.2f;
    [SerializeField] private Vector3 spawnRotationEuler;

    [Header("Position Seed Preview")]
    [SerializeField] private int previewPositionSeed;
    [SerializeField] private Vector3 previewInitialPoint;

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
        Debug.Log($"[Spawn-Debug] SeededNpcSpawnManager 启动。spawnOnStart: {spawnOnStart}, useSceneBJsonSeedsOnStart: {useSceneBJsonSeedsOnStart}");
        Debug.Log($"[Spawn-Debug] 静态缓存内 PendingNpcSeeds 状态: {(PendingNpcSeeds != null ? $"存在 ({PendingNpcSeeds.Length} 个种子)" : "为 null")}");

        if (spawnOnStart)
        {
            if (HasNetworkSeeds())
            {
                SpawnFromConfiguredKeys();
            }
            else if (waitForNetworkSeedsOnStart && preferPendingKeys)
            {
                StartCoroutine(SpawnFromNetworkSeedsWhenReady());
            }
            else if (useSceneBJsonSeedsOnStart)
            {
                Debug.Log("[Spawn-Debug] 流程分支：进入 [等待本地JSON文件] 模式。启动协程...");
                StartCoroutine(SpawnFromSceneBJsonWhenReady());
            }
            else
            {
                Debug.Log("[Spawn-Debug] 流程分支：进入 [直接使用内存种子生成] 模式。开始解析...");
                SpawnFromConfiguredKeys();
            }
        }
    }

    private IEnumerator SpawnFromSceneBJsonWhenReady()
    {
        float elapsed = 0f;

        while (waitForSceneBJsonSeeds && elapsed < sceneBJsonSeedWaitTimeout)
        {
            if (TrySpawnFromSceneBJson())
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (TrySpawnFromSceneBJson())
        {
            yield break;
        }

        if (fallbackToConfiguredSeedsWhenJsonMissing)
        {
            SpawnFromConfiguredKeys();
        }
        else
        {
            Debug.LogWarning($"{nameof(SeededNpcSpawnManager)} is waiting for SceneB json seeds, but no valid json was found.", this);
        }
    }

    private IEnumerator SpawnFromNetworkSeedsWhenReady()
    {
        float elapsed = 0f;

        while (elapsed < networkSeedWaitTimeout)
        {
            if (HasNetworkSeeds())
            {
                SpawnFromConfiguredKeys();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (useSceneBJsonSeedsOnStart)
        {
            StartCoroutine(SpawnFromSceneBJsonWhenReady());
            yield break;
        }

        SpawnFromConfiguredKeys();
    }

    private static bool HasPendingSeeds()
    {
        return PendingNpcSeeds != null && PendingNpcSeeds.Length > 0;
    }

    private static bool HasSyncedSeeds()
    {
        return AsymmetricSyncManager.Instance != null &&
               AsymmetricSyncManager.Instance.SyncedNpcSeeds != null &&
               AsymmetricSyncManager.Instance.SyncedNpcSeeds.Length > 0;
    }

    private static bool HasNetworkSeeds()
    {
        return HasPendingSeeds() || HasSyncedSeeds();
    }

    private bool TrySpawnFromSceneBJson()
    {
        if (SceneBStateJsonSaver.Instance == null ||
            !SceneBStateJsonSaver.Instance.TryGetInitialJsonForSpawn(out SceneBStateSaveData state))
        {
            return false;
        }

        int? murdererSeed = state.murdererSeed >= 0 ? state.murdererSeed : (int?)null;
        SpawnFromSeeds(state.npcSeeds, murdererSeed);
        return true;
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
        Debug.Log($"[Spawn-Debug] 开始执行核心生成逻辑。接收到的种子数: {npcSeeds?.Length ?? 0}, 嫌疑人种子: {(murdererSeed.HasValue ? murdererSeed.Value.ToString() : "无")}");

        if (npcPrefab == null)
        {
            Debug.LogError("[Spawn-Debug] 错误：NPC Prefab 为空，生成终止！", this);
            return;
        }

        if (clearPreviouslySpawned)
        {
            ClearSpawnedNpcs();
        }

        if ((npcSeeds == null || npcSeeds.Length == 0) && !murdererSeed.HasValue)
        {
            Debug.LogWarning("[Spawn-Debug] 警告：没有收到任何有效种子，停止实例化。", this);
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

        Debug.Log($"[Spawn-Debug] 角色生成阶段完毕。成功实例化了 {spawnedNpcs.Count} 个 NPC。开始刷新并保存状态...");

        SceneBStateJsonSaver.Instance?.RefreshNpcListFromScene();
        SceneBStateJsonSaver.Instance?.SaveNow();
    }

    public int[] GetConfiguredNpcSeedsForJson()
    {
        int[] seeds = GetActiveSeeds();

        if (seeds != null && seeds.Length > 0)
        {
            return RemoveDuplicateSeeds(seeds);
        }

        if (!generateSeedsWhenNoneConfigured)
        {
            return new int[0];
        }

        return GenerateNpcSeeds();
    }

    public int? GetConfiguredMurdererSeedForJson()
    {
        int? configuredMurdererSeed = GetActiveMurdererSeed();

        if (configuredMurdererSeed.HasValue)
        {
            return configuredMurdererSeed;
        }

        int[] seeds = GetConfiguredNpcSeedsForJson();
        return seeds.Length > 0 ? seeds[0] : (int?)null;
    }

    public int[] BuildFinalSpawnSeedList(int[] npcSeeds, int? murdererSeed)
    {
        List<int> seeds = new List<int>();

        if (npcSeeds != null)
        {
            for (int i = 0; i < npcSeeds.Length; i++)
            {
                AddUniqueSeed(seeds, npcSeeds[i]);
            }
        }

        if (spawnMissingMurdererSeed && murdererSeed.HasValue)
        {
            AddUniqueSeed(seeds, murdererSeed.Value);
        }

        return seeds.ToArray();
    }

    public Vector3[] GetInitialPointsForSeeds(int[] npcSeeds, int? murdererSeed)
    {
        int[] finalSeeds = BuildFinalSpawnSeedList(npcSeeds, murdererSeed);
        List<Vector3> usedPositions = new List<Vector3>();
        Vector3[] positions = new Vector3[finalSeeds.Length];

        for (int i = 0; i < finalSeeds.Length; i++)
        {
            positions[i] = GetSpawnPosition(finalSeeds[i], i, usedPositions);
        }

        return positions;
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
        int positionSeed = baseSeed;
        Vector3 position = GetSpawnPosition(positionSeed, index, usedPositions);
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

        int faceSeed = baseSeed;
        ApplyFaceSeed(npc, faceSeed);
        ApplyClothingSeed(npc, baseSeed);
        RandomWanderFloat wander = ApplyMovementSeed(npc, baseSeed);
        identity.Initialize(seed, baseSeed, faceSeed, positionSeed, position, isMurderer);

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

    public Vector3 GetInitialPointFromSeed(int seed)
    {
        List<Vector3> usedPositions = new List<Vector3>();
        return GetSpawnPosition(seed, 0, usedPositions);
    }

    [ContextMenu("Preview Initial Point From Seed")]
    public void PreviewInitialPointFromSeed()
    {
        previewInitialPoint = GetInitialPointFromSeed(previewPositionSeed);
        Debug.Log($"Position Seed {previewPositionSeed} -> Initial Point {previewInitialPoint}", this);
    }

    private int[] GenerateNpcSeeds()
    {
        int count = Mathf.Max(1, generatedNpcCount);
        int min = Mathf.Min(generatedSeedMin, generatedSeedMax);
        int max = Mathf.Max(generatedSeedMin, generatedSeedMax);
        System.Random random = new System.Random();
        List<int> seeds = new List<int>();

        while (seeds.Count < count)
        {
            int seed = random.Next(min, max + 1);
            AddUniqueSeed(seeds, seed);
        }

        return seeds.ToArray();
    }

    private static int[] RemoveDuplicateSeeds(int[] source)
    {
        List<int> result = new List<int>();

        for (int i = 0; i < source.Length; i++)
        {
            AddUniqueSeed(result, source[i]);
        }

        return result.ToArray();
    }

    private static void AddUniqueSeed(List<int> seeds, int seed)
    {
        if (!seeds.Contains(seed))
        {
            seeds.Add(seed);
        }
    }

    private void ApplyFaceSeed(GameObject npc, int seed)
    {
        FaceGenerator faceGenerator = npc.GetComponentInChildren<FaceGenerator>(true);

        if (faceGenerator != null)
        {
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

    private void ApplyClothingSeed(GameObject npc, int seed)
    {
        SeededClothingSelector clothingSelector = npc.GetComponentInChildren<SeededClothingSelector>(true);

        if (clothingSelector == null)
        {
            clothingSelector = npc.AddComponent<SeededClothingSelector>();
        }

        clothingSelector.ApplySeed(seed);
    }

    private Vector3 GetSpawnPosition(int baseSeed, int index, List<Vector3> usedPositions)
    {
        System.Random random = new System.Random(baseSeed);
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

        if (preferPendingKeys &&
            AsymmetricSyncManager.Instance != null &&
            AsymmetricSyncManager.Instance.SyncedNpcSeeds != null &&
            AsymmetricSyncManager.Instance.SyncedNpcSeeds.Length > 0)
        {
            return AsymmetricSyncManager.Instance.SyncedNpcSeeds;
        }

        return useManualKeys ? manualNpcSeeds : PendingNpcSeeds;
    }

    private int? GetActiveMurdererSeed()
    {
        if (preferPendingKeys && PendingNpcSeeds != null && PendingNpcSeeds.Length > 0)
        {
            return PendingMurdererSeed;
        }

        if (preferPendingKeys &&
            AsymmetricSyncManager.Instance != null &&
            AsymmetricSyncManager.Instance.SyncedNpcSeeds != null &&
            AsymmetricSyncManager.Instance.SyncedNpcSeeds.Length > 0 &&
            AsymmetricSyncManager.Instance.SyncedMurdererSeed >= 0)
        {
            return AsymmetricSyncManager.Instance.SyncedMurdererSeed;
        }

        return useManualKeys && useManualMurdererSeed ? manualMurdererSeed : PendingMurdererSeed;
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
        generatedNpcCount = Mathf.Max(1, generatedNpcCount);
        networkSeedWaitTimeout = Mathf.Max(0f, networkSeedWaitTimeout);
        sceneBJsonSeedWaitTimeout = Mathf.Max(0f, sceneBJsonSeedWaitTimeout);
    }
}
