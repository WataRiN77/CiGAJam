using System;
using UnityEngine;
using UnityEngine.UI;

public class RandomFaceGenerator2D : MonoBehaviour
{
    [Serializable]
    public class FacePartSlot
    {
        public string displayName;
        public string targetPath;
        public Transform target;
        public Sprite[] sprites;
        public string spriteSourceSlot;
        public bool mirrorSpriteX;
        public bool overrideSpriteSorting = true;
        public int sortingOrder;

        [Header("Anchor")]
        public bool useCurrentLocalTransformAsAnchor = true;
        public bool randomizeTransform = true;
        public Vector3 anchorLocalPosition;
        public Vector3 anchorLocalEulerAngles;
        public Vector3 anchorLocalScale = Vector3.one;

        [Header("Random Offset")]
        public Vector2 localOffsetRange = new Vector2(0.03f, 0.03f);
        public Vector2 randomRotationZRange = new Vector2(-4f, 4f);

        [Header("Gizmos")]
        public Color gizmoColor = Color.yellow;
    }

    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool useFixedSeed;
    [SerializeField] private int fixedSeed = 1001;
    [SerializeField] private FacePartSlot[] slots;

    [Header("Scene Gizmos")]
    [SerializeField] private bool alwaysShowGizmos = true;
    [SerializeField] private bool showLabels = true;
    [SerializeField] private float anchorSphereRadius = 0.025f;

    private System.Random random;
    private bool hasGeneratedExternally;

    private void Awake()
    {
        ResolveAllTargets();
        CaptureCurrentAnchorsIfNeeded();
    }

    private void Start()
    {
        if (generateOnStart && !hasGeneratedExternally)
        {
            GenerateRandomFace();
        }
    }

    [ContextMenu("Generate Random Face")]
    public void GenerateRandomFace()
    {
        GenerateRandomFace(useFixedSeed ? fixedSeed : UnityEngine.Random.Range(0, int.MaxValue));
    }

    public void GenerateRandomFace(int seed)
    {
        hasGeneratedExternally = Application.isPlaying;
        random = new System.Random(seed);

        ResolveAllTargets();
        CaptureCurrentAnchorsIfNeeded();

        if (slots == null)
        {
            return;
        }

        foreach (FacePartSlot slot in slots)
        {
            ApplySlot(slot);
        }
    }

    [ContextMenu("Capture Current Transforms As Anchors")]
    public void CaptureCurrentTransformsAsAnchors()
    {
        ResolveAllTargets();

        foreach (FacePartSlot slot in slots)
        {
            if (slot == null || slot.target == null)
            {
                continue;
            }

            slot.anchorLocalPosition = slot.target.localPosition;
            slot.anchorLocalEulerAngles = slot.target.localEulerAngles;
            slot.anchorLocalScale = slot.target.localScale;
            slot.useCurrentLocalTransformAsAnchor = false;
        }
    }

    [ContextMenu("Setup Default Character Root Slots")]
    public void SetupDefaultCharacterRootSlots()
    {
        slots = new[]
        {
            CreateFixedSlot("Base Face", "Base_Face", new Color(0.9f, 0.9f, 0.9f), 0),
            CreateSlot("Hair", "Hair/Hair_Style", new Color(1f, 0.75f, 0.1f), 20),
            CreateSlot("Eyebrow L", "Eyebrow/Eyebrow_L", new Color(1f, 0.6f, 0.1f), 40),
            CreateMirroredSlot("Eyebrow R", "Eyebrow/Eyebrow_R", "Eyebrow L", new Color(1f, 0.6f, 0.1f), 40),
            CreateSlot("Eye L", "Eye/Eye_L", new Color(0.2f, 0.8f, 1f), 30),
            CreateMirroredSlot("Eye R", "Eye/Eye_R", "Eye L", new Color(0.2f, 0.8f, 1f), 30),
            CreateSlot("Nose", "Nose/Nose_Style", new Color(0.6f, 1f, 0.4f), 50),
            CreateSlot("Mouth", "Mouth/Mouth_Style", new Color(1f, 0.3f, 0.8f), 60)
        };

        ResolveAllTargets();
    }

    private FacePartSlot CreateSlot(string displayName, string targetPath, Color gizmoColor, int sortingOrder)
    {
        return new FacePartSlot
        {
            displayName = displayName,
            targetPath = targetPath,
            sortingOrder = sortingOrder,
            localOffsetRange = new Vector2(0.03f, 0.03f),
            randomRotationZRange = new Vector2(-4f, 4f),
            useCurrentLocalTransformAsAnchor = true,
            randomizeTransform = true,
            gizmoColor = gizmoColor
        };
    }

    private FacePartSlot CreateFixedSlot(string displayName, string targetPath, Color gizmoColor, int sortingOrder)
    {
        FacePartSlot slot = CreateSlot(displayName, targetPath, gizmoColor, sortingOrder);
        slot.randomizeTransform = false;
        slot.localOffsetRange = Vector2.zero;
        slot.randomRotationZRange = Vector2.zero;
        return slot;
    }

    private FacePartSlot CreateMirroredSlot(string displayName, string targetPath, string spriteSourceSlot, Color gizmoColor, int sortingOrder)
    {
        FacePartSlot slot = CreateSlot(displayName, targetPath, gizmoColor, sortingOrder);
        slot.spriteSourceSlot = spriteSourceSlot;
        slot.mirrorSpriteX = true;
        return slot;
    }

    private void ResolveAllTargets()
    {
        if (slots == null)
        {
            return;
        }

        foreach (FacePartSlot slot in slots)
        {
            if (slot == null || slot.target != null || string.IsNullOrWhiteSpace(slot.targetPath))
            {
                continue;
            }

            slot.target = transform.Find(slot.targetPath.Trim());
        }
    }

    private void CaptureCurrentAnchorsIfNeeded()
    {
        if (slots == null)
        {
            return;
        }

        foreach (FacePartSlot slot in slots)
        {
            if (slot == null || slot.target == null || !slot.useCurrentLocalTransformAsAnchor)
            {
                continue;
            }

            slot.anchorLocalPosition = slot.target.localPosition;
            slot.anchorLocalEulerAngles = slot.target.localEulerAngles;
            slot.anchorLocalScale = slot.target.localScale;
        }
    }

    private void ApplySlot(FacePartSlot slot)
    {
        if (slot == null || slot.target == null)
        {
            return;
        }

        ApplyRandomSprite(slot);
        ApplySpriteSorting(slot);
        slot.target.localScale = slot.anchorLocalScale;
        ApplyMirror(slot);

        if (!slot.randomizeTransform)
        {
            slot.target.localPosition = slot.anchorLocalPosition;
            slot.target.localEulerAngles = slot.anchorLocalEulerAngles;
            return;
        }

        Vector3 localPosition = slot.anchorLocalPosition;
        localPosition.x += RandomRange(-slot.localOffsetRange.x, slot.localOffsetRange.x);
        localPosition.y += RandomRange(-slot.localOffsetRange.y, slot.localOffsetRange.y);
        slot.target.localPosition = localPosition;

        Vector3 localEulerAngles = slot.anchorLocalEulerAngles;
        localEulerAngles.z += RandomRange(slot.randomRotationZRange.x, slot.randomRotationZRange.y);
        slot.target.localEulerAngles = localEulerAngles;
    }

    private void ApplyRandomSprite(FacePartSlot slot)
    {
        Sprite[] sprites = GetSpritesForSlot(slot);

        if (sprites == null || sprites.Length == 0)
        {
            return;
        }

        Sprite sprite = sprites[random.Next(0, sprites.Length)];

        SpriteRenderer spriteRenderer = slot.target.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
            return;
        }

        Image image = slot.target.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
        }
    }

    private Sprite[] GetSpritesForSlot(FacePartSlot slot)
    {
        if (slot == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(slot.spriteSourceSlot))
        {
            FacePartSlot source = FindSlot(slot.spriteSourceSlot);

            if (source != null && source.sprites != null && source.sprites.Length > 0)
            {
                return source.sprites;
            }
        }

        return slot.sprites;
    }

    private FacePartSlot FindSlot(string displayName)
    {
        if (slots == null)
        {
            return null;
        }

        foreach (FacePartSlot slot in slots)
        {
            if (slot != null && slot.displayName == displayName)
            {
                return slot;
            }
        }

        return null;
    }

    private void ApplyMirror(FacePartSlot slot)
    {
        SpriteRenderer spriteRenderer = slot.target.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = slot.mirrorSpriteX;
            return;
        }

        Image image = slot.target.GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        Vector3 scale = slot.anchorLocalScale;
        scale.x = Mathf.Abs(scale.x) * (slot.mirrorSpriteX ? -1f : 1f);
        slot.target.localScale = scale;
    }

    private void ApplySpriteSorting(FacePartSlot slot)
    {
        if (!slot.overrideSpriteSorting)
        {
            return;
        }

        SpriteRenderer spriteRenderer = slot.target.GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = GetEffectiveSortingOrder(slot);
        }
    }

    private int GetEffectiveSortingOrder(FacePartSlot slot)
    {
        if (slot.sortingOrder != 0 || slot.displayName == "Base Face")
        {
            return slot.sortingOrder;
        }

        switch (slot.displayName)
        {
            case "Hair":
                return 20;
            case "Eye L":
            case "Eye R":
                return 30;
            case "Eyebrow L":
            case "Eyebrow R":
                return 40;
            case "Nose":
                return 50;
            case "Mouth":
                return 60;
            default:
                return slot.sortingOrder;
        }
    }

    private float RandomRange(float min, float max)
    {
        if (min > max)
        {
            float temp = min;
            min = max;
            max = temp;
        }

        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private void OnDrawGizmos()
    {
        if (!alwaysShowGizmos)
        {
            return;
        }

        DrawSlotGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        DrawSlotGizmos();
    }

    private void DrawSlotGizmos()
    {
        if (slots == null)
        {
            return;
        }

        ResolveAllTargets();

        foreach (FacePartSlot slot in slots)
        {
            DrawSlotGizmo(slot);
        }
    }

    private void DrawSlotGizmo(FacePartSlot slot)
    {
        if (slot == null || slot.target == null)
        {
            return;
        }

        Vector3 anchor = slot.target.parent != null
            ? slot.target.parent.TransformPoint(slot.anchorLocalPosition)
            : slot.anchorLocalPosition;

        Vector3 right = slot.target.parent != null ? slot.target.parent.right : Vector3.right;
        Vector3 up = slot.target.parent != null ? slot.target.parent.up : Vector3.up;

        Vector3 halfRight = right * Mathf.Abs(slot.localOffsetRange.x);
        Vector3 halfUp = up * Mathf.Abs(slot.localOffsetRange.y);

        Color color = slot.gizmoColor;
        color.a = 0.9f;
        Gizmos.color = color;
        Gizmos.DrawSphere(anchor, anchorSphereRadius);

        color.a = 0.35f;
        Gizmos.color = color;

        Vector3 topLeft = anchor - halfRight + halfUp;
        Vector3 topRight = anchor + halfRight + halfUp;
        Vector3 bottomRight = anchor + halfRight - halfUp;
        Vector3 bottomLeft = anchor - halfRight - halfUp;

        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);

#if UNITY_EDITOR
        if (showLabels)
        {
            UnityEditor.Handles.color = slot.gizmoColor;
            UnityEditor.Handles.Label(anchor + halfUp + Vector3.up * anchorSphereRadius, slot.displayName);
        }
#endif
    }

    private void OnValidate()
    {
        anchorSphereRadius = Mathf.Max(0.001f, anchorSphereRadius);

        if (slots == null)
        {
            return;
        }

        foreach (FacePartSlot slot in slots)
        {
            if (slot == null)
            {
                continue;
            }

            slot.localOffsetRange.x = Mathf.Max(0f, slot.localOffsetRange.x);
            slot.localOffsetRange.y = Mathf.Max(0f, slot.localOffsetRange.y);
        }
    }
}
