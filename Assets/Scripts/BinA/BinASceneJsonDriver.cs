using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
public class BinASceneJsonDriver : MonoBehaviour
{
    [Header("Json Source")]
    [SerializeField] private string jsonPath = @"C:\CiGAJam\SceneBState.json";
    [SerializeField] private bool reloadWhenJsonChanges = true;
    [SerializeField] private float reloadInterval = 0.2f;
    [SerializeField] private bool logJsonPath = true;
    [SerializeField] private bool removeCopiedSceneBStateSavers = true;

    [Header("Seed Source")]
    [SerializeField] private FaceCustomizationGameManager faceGameManager;
    [SerializeField] private SeededNpcSpawnManager npcSpawnManager;
    [SerializeField] private bool useJsonSeedsAsAuthoritative = true;
    [SerializeField] private bool generateRoundIfNoAnswer = false;
    [SerializeField] private bool appendJsonNpcSeedsIfMissing = true;
    [SerializeField] private int[] fallbackNpcSeeds;
    [SerializeField] private int fallbackMurdererSeed = -1;

    [Header("Terrain")]
    [SerializeField] private GameObject[] terrains;

    [Header("State Apply")]
    [SerializeField] private bool snapNpcsToJsonInitialPositions = true;
    [SerializeField] private string arrowChildName = "arrows";
    [SerializeField] private string fallbackArrowChildName = "Arrow";
    [SerializeField] private string shotPointChildName = "shotpoint";
    [SerializeField] private string bloodFxChildName = "blood";
    [SerializeField] private Vector3 mirroredDeathForceDirection = Vector3.back;

    [Header("Output Camera")]
    [SerializeField] private Camera outputCamera;
    [SerializeField] private RenderTexture outputRenderTexture;
    [SerializeField] private RawImage outputRawImage;
    [SerializeField] private bool bindOutputTextureOnStart = true;
    [SerializeField] private bool forceOutputCameraEnabled = true;
    [SerializeField] private Vector3 fixedCameraPosition;
    [SerializeField] private Vector3 fixedCameraEulerAngles;
    [SerializeField] private bool useCurrentCameraTransformAsFixedOnStart = true;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 6f, -6f);
    [SerializeField] private bool lookAtTrackedNpc = true;
    [SerializeField] private float followSmoothTime = 0.12f;
    [SerializeField] private bool animateOrthographicSize = true;
    [SerializeField] private float trackedOrthographicSize = 2f;
    [SerializeField] private float orthographicSizeTransitionTime = 0.3f;

    [Header("Shot Feedback")]
    [SerializeField] private bool playShotFeedback = true;
    [SerializeField] private float shotFeedbackDuration = 1f;
    [SerializeField] private float shotSlowMotionScale = 0.2f;
    [SerializeField] private float shotCameraShakeStrength = 0.35f;
    [SerializeField] private float shotCameraShakeFrequency = 35f;
    [SerializeField] private float rawImageShakeStrength = 14f;
    [SerializeField] private float rawImageShakeFrequency = 45f;

    [Header("Events")]
    [SerializeField] private UnityEvent onGameStillRunning;
    [SerializeField] private UnityEvent onGameSucceeded;
    [SerializeField] private UnityEvent onGameFailed;

    private readonly Dictionary<int, SeededNpcIdentity> identitiesBySeed = new Dictionary<int, SeededNpcIdentity>();
    private readonly HashSet<int> shotAppliedSeeds = new HashSet<int>();
    private SceneBStateSaveData currentState;
    private DateTime lastJsonWriteTimeUtc;
    private float reloadTimer;
    private Transform trackedTarget;
    private Vector3 cameraVelocity;
    private float fixedOrthographicSize = 5f;
    private Vector3 outputCameraShakeOffset;
    private RectTransform outputRawImageRect;
    private Vector2 outputRawImageOriginalAnchoredPosition;
    private Coroutine shotFeedbackRoutine;
    private float shotFeedbackOriginalTimeScale = 1f;
    private float shotFeedbackOriginalFixedDeltaTime = 0.02f;
    private bool shotFeedbackTimeScaleActive;

    public string JsonPath => jsonPath;
    public SceneBStateSaveData CurrentState => currentState;

    private void Awake()
    {
        if (removeCopiedSceneBStateSavers)
        {
            RemoveCopiedSceneBStateSavers();
        }
    }

    private void Start()
    {
        if (bindOutputTextureOnStart)
        {
            EnsureOutputTextureBinding();
        }

        CacheOutputRawImageRect();

        if (useCurrentCameraTransformAsFixedOnStart && outputCamera != null)
        {
            fixedCameraPosition = outputCamera.transform.position;
            fixedCameraEulerAngles = outputCamera.transform.eulerAngles;
            fixedOrthographicSize = outputCamera.orthographicSize;
        }

        if (logJsonPath)
        {
            Debug.Log($"BinA reads SceneB json from: {jsonPath}", this);
        }

        LoadAndApplyJson(true);
    }

    private void Update()
    {
        if (reloadWhenJsonChanges)
        {
            reloadTimer -= Time.deltaTime;

            if (reloadTimer <= 0f)
            {
                reloadTimer = Mathf.Max(0.02f, reloadInterval);
                ReloadIfJsonChanged();
            }
        }

        UpdateOutputCamera();
    }

    private void OnDisable()
    {
        if (shotFeedbackRoutine != null)
        {
            StopCoroutine(shotFeedbackRoutine);
            shotFeedbackRoutine = null;
        }

        RestoreShotFeedbackState();
    }

    [ContextMenu("Load And Apply Json")]
    public void LoadAndApplyJsonFromContextMenu()
    {
        LoadAndApplyJson(true);
    }

    private void LoadAndApplyJson(bool forceSpawn)
    {
        SceneBStateSaveData loadedState = ReadJsonState();

        if (loadedState == null)
        {
            return;
        }

        currentState = loadedState;
        ApplyTerrain(currentState.mapNumber);

        if (forceSpawn || identitiesBySeed.Count == 0)
        {
            SpawnNpcsFromFaceGameManager(currentState);
        }

        RebuildIdentityLookup();
        ApplyNpcStates(currentState);
        ApplyGameState(currentState);
    }

    private SceneBStateSaveData ReadJsonState()
    {
        if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
        {
            Debug.LogWarning($"BinA json file not found: {jsonPath}", this);
            return null;
        }

        try
        {
            lastJsonWriteTimeUtc = File.GetLastWriteTimeUtc(jsonPath);
            string json = File.ReadAllText(jsonPath);
            return JsonUtility.FromJson<SceneBStateSaveData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogError($"BinA failed to read json '{jsonPath}'. {exception.Message}", this);
            return null;
        }
    }

    private void RemoveCopiedSceneBStateSavers()
    {
        SceneBStateJsonSaver[] savers = FindObjectsOfType<SceneBStateJsonSaver>(true);

        for (int i = 0; i < savers.Length; i++)
        {
            SceneBStateJsonSaver saver = savers[i];

            if (saver == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(saver);
            }
            else
            {
                DestroyImmediate(saver);
            }
        }
    }

    private void ReloadIfJsonChanged()
    {
        if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
        {
            return;
        }

        DateTime writeTime = File.GetLastWriteTimeUtc(jsonPath);

        if (writeTime == lastJsonWriteTimeUtc)
        {
            return;
        }

        LoadAndApplyJson(false);
    }

    private void ApplyTerrain(int mapNumber)
    {
        if (terrains == null || terrains.Length == 0)
        {
            return;
        }

        int selectedIndex = Mathf.Clamp(mapNumber - 1, 0, terrains.Length - 1);

        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i] != null)
            {
                terrains[i].SetActive(i == selectedIndex);
            }
        }
    }

    private void SpawnNpcsFromFaceGameManager(SceneBStateSaveData state)
    {
        if (npcSpawnManager == null)
        {
            npcSpawnManager = FindObjectOfType<SeededNpcSpawnManager>();
        }

        if (npcSpawnManager == null)
        {
            Debug.LogWarning("BinA needs a SeededNpcSpawnManager reference.", this);
            return;
        }

        int murdererSeed = GetMurdererSeed(state);
        int[] npcSeeds = GetNpcSeeds(state, murdererSeed);

        if (npcSeeds == null || npcSeeds.Length == 0)
        {
            Debug.LogWarning("BinA has no NPC seeds to spawn.", this);
            return;
        }

        npcSpawnManager.SpawnFromSeeds(npcSeeds, murdererSeed >= 0 ? murdererSeed : (int?)null);
        RebuildIdentityLookup();

        if (snapNpcsToJsonInitialPositions)
        {
            SnapSpawnedNpcsToJsonPositions(state);
        }
    }

    private int[] GetNpcSeeds(SceneBStateSaveData state, int murdererSeed)
    {
        List<int> seeds = new List<int>();

        if (useJsonSeedsAsAuthoritative && state != null)
        {
            if (state.npcSeeds != null && state.npcSeeds.Length > 0)
            {
                for (int i = 0; i < state.npcSeeds.Length; i++)
                {
                    AddUniqueSeed(seeds, state.npcSeeds[i]);
                }

                return seeds.ToArray();
            }

            if (state.npcs != null && state.npcs.Length > 0)
            {
                for (int i = 0; i < state.npcs.Length; i++)
                {
                    AddUniqueSeed(seeds, state.npcs[i].seed);
                }

                return seeds.ToArray();
            }
        }

        if (faceGameManager == null)
        {
            faceGameManager = FaceCustomizationGameManager.Instance != null
                ? FaceCustomizationGameManager.Instance
                : FindObjectOfType<FaceCustomizationGameManager>();
        }

        if (faceGameManager != null)
        {
            if (generateRoundIfNoAnswer && faceGameManager.GetCurrentSeed() < 0)
            {
                faceGameManager.GenerateNewRound();
            }

            List<int> faceSeeds = faceGameManager.GetAllSeeds();

            if (faceSeeds != null)
            {
                for (int i = 0; i < faceSeeds.Count; i++)
                {
                    AddUniqueSeed(seeds, faceSeeds[i]);
                }
            }
        }

        if (fallbackNpcSeeds != null)
        {
            for (int i = 0; i < fallbackNpcSeeds.Length; i++)
            {
                AddUniqueSeed(seeds, fallbackNpcSeeds[i]);
            }
        }

        if (appendJsonNpcSeedsIfMissing && state != null && state.npcs != null)
        {
            for (int i = 0; i < state.npcs.Length; i++)
            {
                AddUniqueSeed(seeds, state.npcs[i].seed);
            }
        }

        if (murdererSeed >= 0)
        {
            AddUniqueSeed(seeds, murdererSeed);
        }

        return seeds.ToArray();
    }

    private int GetMurdererSeed(SceneBStateSaveData state)
    {
        if (useJsonSeedsAsAuthoritative && state != null)
        {
            if (state.murdererSeed >= 0)
            {
                return state.murdererSeed;
            }

            if (state.npcs != null)
            {
                for (int i = 0; i < state.npcs.Length; i++)
                {
                    if (state.npcs[i].isMurderer)
                    {
                        return state.npcs[i].seed;
                    }
                }
            }
        }

        if (faceGameManager == null)
        {
            faceGameManager = FaceCustomizationGameManager.Instance != null
                ? FaceCustomizationGameManager.Instance
                : FindObjectOfType<FaceCustomizationGameManager>();
        }

        if (faceGameManager != null)
        {
            int seed = faceGameManager.GetCurrentSeed();

            if (seed >= 0)
            {
                return seed;
            }
        }

        return fallbackMurdererSeed;
    }

    private static void AddUniqueSeed(List<int> seeds, int seed)
    {
        if (!seeds.Contains(seed))
        {
            seeds.Add(seed);
        }
    }

    private void RebuildIdentityLookup()
    {
        identitiesBySeed.Clear();
        SeededNpcIdentity[] identities = FindObjectsOfType<SeededNpcIdentity>(true);

        for (int i = 0; i < identities.Length; i++)
        {
            SeededNpcIdentity identity = identities[i];

            if (identity != null && !identitiesBySeed.ContainsKey(identity.NpcSeed))
            {
                identitiesBySeed.Add(identity.NpcSeed, identity);
            }
        }
    }

    private void SnapSpawnedNpcsToJsonPositions(SceneBStateSaveData state)
    {
        if (state == null || state.npcs == null)
        {
            return;
        }

        for (int i = 0; i < state.npcs.Length; i++)
        {
            NpcRuntimeState npcState = state.npcs[i];

            if (identitiesBySeed.TryGetValue(npcState.seed, out SeededNpcIdentity identity) && identity != null)
            {
                identity.transform.position = ToVector3(npcState.initialPosition);
            }
        }
    }

    private void ApplyNpcStates(SceneBStateSaveData state)
    {
        trackedTarget = null;

        if (state == null || state.npcs == null)
        {
            return;
        }

        for (int i = 0; i < state.npcs.Length; i++)
        {
            NpcRuntimeState npcState = state.npcs[i];

            if (!identitiesBySeed.TryGetValue(npcState.seed, out SeededNpcIdentity identity) || identity == null)
            {
                continue;
            }

            ApplyMarkedState(identity.transform, npcState.isMarked);
            ApplyShotState(identity.transform, npcState);

            if (npcState.isTracked && !npcState.isShot)
            {
                trackedTarget = identity.transform;
            }
        }
    }

    private void ApplyMarkedState(Transform npcRoot, bool isMarked)
    {
        Transform arrow = FindChildRecursive(npcRoot, arrowChildName);

        if (arrow == null && !string.IsNullOrWhiteSpace(fallbackArrowChildName))
        {
            arrow = FindChildRecursive(npcRoot, fallbackArrowChildName);
        }

        if (arrow != null)
        {
            arrow.gameObject.SetActive(isMarked);
        }
    }

    private void ApplyShotState(Transform npcRoot, NpcRuntimeState npcState)
    {
        if (!npcState.isShot)
        {
            return;
        }

        ActivateShotPointChildren(npcRoot);

        if (shotAppliedSeeds.Contains(npcState.seed))
        {
            return;
        }

        shotAppliedSeeds.Add(npcState.seed);

        RandomWanderFloat wander = npcRoot.GetComponent<RandomWanderFloat>();

        if (wander == null)
        {
            wander = npcRoot.GetComponentInChildren<RandomWanderFloat>(true);
        }

        if (wander != null && wander.IsAlive)
        {
            wander.Die(mirroredDeathForceDirection);
        }

        PlayBloodFx(npcRoot);

        if (playShotFeedback)
        {
            BeginShotFeedback();
        }
    }

    private void ActivateShotPointChildren(Transform npcRoot)
    {
        Transform shotPoint = FindChildRecursive(npcRoot, shotPointChildName);

        if (shotPoint == null)
        {
            return;
        }

        foreach (Transform child in shotPoint)
        {
            child.gameObject.SetActive(true);
        }
    }

    private void PlayBloodFx(Transform npcRoot)
    {
        Transform bloodFx = FindChildRecursive(npcRoot, bloodFxChildName);

        if (bloodFx == null)
        {
            return;
        }

        bloodFx.gameObject.SetActive(true);

        ParticleSystem[] particleSystems = bloodFx.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null)
            {
                continue;
            }

            particleSystems[i].Clear(true);
            particleSystems[i].Play(true);
        }

        PlayVisualEffectIfPresent(bloodFx);
    }

    private void PlayVisualEffectIfPresent(Transform fxRoot)
    {
        Component[] components = fxRoot.GetComponentsInChildren<Component>(true);

        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];

            if (component == null || component.GetType().FullName != "UnityEngine.VFX.VisualEffect")
            {
                continue;
            }

            component.gameObject.SetActive(true);
            component.GetType().GetMethod("Reinit")?.Invoke(component, null);
            component.GetType().GetMethod("Play", Type.EmptyTypes)?.Invoke(component, null);
        }
    }

    private void ApplyGameState(SceneBStateSaveData state)
    {
        if (state == null)
        {
            return;
        }

        if (!state.gameEnded)
        {
            onGameStillRunning?.Invoke();
            return;
        }

        if (state.gameSucceeded)
        {
            onGameSucceeded?.Invoke();
        }
        else
        {
            onGameFailed?.Invoke();
        }
    }

    private void UpdateOutputCamera()
    {
        if (outputCamera == null)
        {
            return;
        }

        if (forceOutputCameraEnabled && !outputCamera.enabled)
        {
            outputCamera.enabled = true;
        }

        Vector3 targetPosition = trackedTarget != null
            ? trackedTarget.position + followOffset
            : fixedCameraPosition;

        if (followSmoothTime <= 0f)
        {
            outputCamera.transform.position = targetPosition + outputCameraShakeOffset;
        }
        else
        {
            outputCamera.transform.position = Vector3.SmoothDamp(
                outputCamera.transform.position,
                targetPosition + outputCameraShakeOffset,
                ref cameraVelocity,
                followSmoothTime
            );
        }

        if (trackedTarget != null && lookAtTrackedNpc)
        {
            Vector3 direction = trackedTarget.position - outputCamera.transform.position;

            if (direction.sqrMagnitude > 0.0001f)
            {
                outputCamera.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
        else
        {
            outputCamera.transform.rotation = Quaternion.Euler(fixedCameraEulerAngles);
        }

        UpdateOutputCameraSize();
    }

    private void UpdateOutputCameraSize()
    {
        if (!animateOrthographicSize || outputCamera == null || !outputCamera.orthographic)
        {
            return;
        }

        float targetSize = trackedTarget != null ? trackedOrthographicSize : fixedOrthographicSize;

        if (orthographicSizeTransitionTime <= 0f)
        {
            outputCamera.orthographicSize = targetSize;
            return;
        }

        float sizeDistance = Mathf.Max(0.01f, Mathf.Abs(trackedOrthographicSize - fixedOrthographicSize));
        float maxDelta = sizeDistance / orthographicSizeTransitionTime * Time.deltaTime;
        outputCamera.orthographicSize = Mathf.MoveTowards(outputCamera.orthographicSize, targetSize, maxDelta);
    }

    private void EnsureOutputTextureBinding()
    {
        if (outputCamera == null)
        {
            outputCamera = FindOutputCamera();
        }

        if (outputRenderTexture == null && outputCamera != null)
        {
            outputRenderTexture = outputCamera.targetTexture;
        }

        if (outputRawImage == null)
        {
            outputRawImage = FindOutputRawImage();
        }

        if (outputRenderTexture == null && outputRawImage != null)
        {
            outputRenderTexture = outputRawImage.texture as RenderTexture;
        }

        if (outputCamera != null)
        {
            if (forceOutputCameraEnabled)
            {
                outputCamera.enabled = true;
            }

            if (outputRenderTexture != null)
            {
                outputCamera.targetTexture = outputRenderTexture;
            }
            else
            {
                Debug.LogWarning("BinA Output Camera has no RenderTexture target. Assign Output Render Texture or set the camera Target Texture.", this);
            }
        }
        else
        {
            Debug.LogWarning("BinA did not find an Output Camera. Assign it on BinASceneJsonDriver.", this);
        }

        if (outputRawImage != null)
        {
            if (outputRenderTexture != null)
            {
                outputRawImage.texture = outputRenderTexture;
                outputRawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                CacheOutputRawImageRect();
            }
            else
            {
                Debug.LogWarning("BinA Output RawImage has no RenderTexture to display.", this);
            }
        }
    }

    private void BeginShotFeedback()
    {
        if (shotFeedbackRoutine != null)
        {
            StopCoroutine(shotFeedbackRoutine);
            shotFeedbackRoutine = null;
            RestoreShotFeedbackState();
        }

        shotFeedbackRoutine = StartCoroutine(ShotFeedbackRoutine());
    }

    private IEnumerator ShotFeedbackRoutine()
    {
        shotFeedbackOriginalTimeScale = Time.timeScale;
        shotFeedbackOriginalFixedDeltaTime = Time.fixedDeltaTime;
        shotFeedbackTimeScaleActive = true;
        float duration = Mathf.Max(0.01f, shotFeedbackDuration);
        float elapsed = 0f;

        Time.timeScale = Mathf.Clamp(shotSlowMotionScale, 0.01f, 1f);
        Time.fixedDeltaTime = shotFeedbackOriginalFixedDeltaTime * Time.timeScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float intensity = 1f - normalizedTime;
            float cameraWave = Mathf.Sin(elapsed * shotCameraShakeFrequency);
            Vector2 cameraNoise = UnityEngine.Random.insideUnitCircle * shotCameraShakeStrength * intensity;
            Vector3 cameraRight = outputCamera != null ? outputCamera.transform.right : Vector3.right;
            Vector3 cameraUp = outputCamera != null ? outputCamera.transform.up : Vector3.up;

            outputCameraShakeOffset =
                cameraRight * cameraNoise.x +
                cameraUp * (cameraNoise.y + cameraWave * shotCameraShakeStrength * 0.35f * intensity);

            ApplyRawImageShake(elapsed, intensity);
            yield return null;
        }

        shotFeedbackRoutine = null;
        RestoreShotFeedbackState();
    }

    private void ApplyRawImageShake(float elapsed, float intensity)
    {
        if (outputRawImageRect == null)
        {
            CacheOutputRawImageRect();
        }

        if (outputRawImageRect == null)
        {
            return;
        }

        float wave = Mathf.Sin(elapsed * rawImageShakeFrequency);
        Vector2 noise = UnityEngine.Random.insideUnitCircle * rawImageShakeStrength * intensity;
        outputRawImageRect.anchoredPosition =
            outputRawImageOriginalAnchoredPosition +
            noise +
            Vector2.up * wave * rawImageShakeStrength * 0.25f * intensity;
    }

    private void RestoreShotFeedbackState()
    {
        outputCameraShakeOffset = Vector3.zero;

        if (shotFeedbackTimeScaleActive)
        {
            Time.timeScale = shotFeedbackOriginalTimeScale;
            Time.fixedDeltaTime = shotFeedbackOriginalFixedDeltaTime;
            shotFeedbackTimeScaleActive = false;
        }

        if (outputRawImageRect != null)
        {
            outputRawImageRect.anchoredPosition = outputRawImageOriginalAnchoredPosition;
        }
    }

    private void CacheOutputRawImageRect()
    {
        if (outputRawImage == null)
        {
            return;
        }

        outputRawImageRect = outputRawImage.rectTransform;

        if (outputRawImageRect != null)
        {
            outputRawImageOriginalAnchoredPosition = outputRawImageRect.anchoredPosition;
        }
    }

    private static Camera FindOutputCamera()
    {
        Camera[] cameras = FindObjectsOfType<Camera>(true);

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].name.IndexOf("output", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return cameras[i];
            }
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].targetTexture != null)
            {
                return cameras[i];
            }
        }

        return null;
    }

    private static RawImage FindOutputRawImage()
    {
        RawImage[] rawImages = FindObjectsOfType<RawImage>(true);

        for (int i = 0; i < rawImages.Length; i++)
        {
            RawImage rawImage = rawImages[i];

            if (rawImage != null && rawImage.name.IndexOf("roll", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return rawImage;
            }
        }

        for (int i = 0; i < rawImages.Length; i++)
        {
            RawImage rawImage = rawImages[i];

            if (rawImage != null && rawImage.name.IndexOf("output", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return rawImage;
            }
        }

        for (int i = 0; i < rawImages.Length; i++)
        {
            RawImage rawImage = rawImages[i];

            if (rawImage != null && string.Equals(rawImage.name, "B", StringComparison.OrdinalIgnoreCase))
            {
                return rawImage;
            }
        }

        return null;
    }

    private static Vector3 ToVector3(SerializableVector3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform result = FindChildRecursive(child, childName);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        reloadInterval = Mathf.Max(0.02f, reloadInterval);
        followSmoothTime = Mathf.Max(0f, followSmoothTime);
        trackedOrthographicSize = Mathf.Max(0.01f, trackedOrthographicSize);
        orthographicSizeTransitionTime = Mathf.Max(0f, orthographicSizeTransitionTime);
        shotFeedbackDuration = Mathf.Max(0.01f, shotFeedbackDuration);
        shotSlowMotionScale = Mathf.Clamp(shotSlowMotionScale, 0.01f, 1f);
        shotCameraShakeStrength = Mathf.Max(0f, shotCameraShakeStrength);
        shotCameraShakeFrequency = Mathf.Max(0f, shotCameraShakeFrequency);
        rawImageShakeStrength = Mathf.Max(0f, rawImageShakeStrength);
        rawImageShakeFrequency = Mathf.Max(0f, rawImageShakeFrequency);

        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            jsonPath = @"C:\CiGAJam\SceneBState.json";
        }
    }
}
