using UnityEngine;

[RequireComponent(typeof(PlayerController2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAfterimageEffect : MonoBehaviour
{
    private static Material runtimeFallbackMaterial;
    private static Sprite runtimeBlockSprite;

    private struct AfterimageSlot
    {
        public SpriteRenderer Renderer;
        public float RemainingTime;
        public float Duration;
        public bool Active;
    }

    [SerializeField] private bool useBlockAfterimages = true;
    [SerializeField] private int poolSize = 5;
    [SerializeField] private float spawnInterval = 0.065f;
    [SerializeField] private float lifetime = 0.14f;
    [SerializeField] private float alpha = 0.38f;
    [SerializeField] private float moveSpeedThreshold = 0.35f;
    [SerializeField] private int sortingOrderOffset = 1;
    [SerializeField] private Vector2 fallbackBlockSize = new Vector2(1f, 1f);

    private PlayerController2D player;
    private SpriteRenderer sourceRenderer;
    private Collider2D sourceCollider;
    private AfterimageSlot[] slots;
    private Transform root;
    private float timer;
    private int activeCount;

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        player = GetComponent<PlayerController2D>();
        sourceRenderer = GetComponent<SpriteRenderer>();
        sourceCollider = GetComponent<Collider2D>();
        poolSize = Mathf.Max(1, poolSize);
        EnsurePool();
    }

    private void Update()
    {
        if (player == null || sourceRenderer == null)
        {
            Initialize();
            if (player == null || sourceRenderer == null)
            {
                return;
            }
        }

        bool moving = player.Velocity.sqrMagnitude >= moveSpeedThreshold * moveSpeedThreshold;
        if (moving)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                SpawnAfterimage();
                timer = spawnInterval;
            }
        }
        else
        {
            timer = 0f;
        }

        if (activeCount > 0)
        {
            UpdateSlots();
        }
    }

    private void SpawnAfterimage()
    {
        if (slots == null || slots.Length == 0)
        {
            return;
        }

        int index = FindSlot();
        AfterimageSlot slot = slots[index];
        if (slot.Renderer == null)
        {
            return;
        }

        bool wasActive = slot.Active;
        slot.Renderer.sprite = useBlockAfterimages ? GetRuntimeBlockSprite() : sourceRenderer.sprite;
        slot.Renderer.flipX = sourceRenderer.flipX;
        slot.Renderer.flipY = sourceRenderer.flipY;
        slot.Renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        slot.Renderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
        slot.Renderer.transform.position = transform.position;
        slot.Renderer.transform.rotation = useBlockAfterimages ? Quaternion.identity : transform.rotation;
        slot.Renderer.transform.localScale = useBlockAfterimages ? GetBlockScale() : transform.localScale;
        slot.Renderer.sharedMaterial = sourceRenderer.sharedMaterial != null ? sourceRenderer.sharedMaterial : GetRuntimeFallbackMaterial();
        slot.Renderer.color = GetColor(alpha);
        slot.Renderer.enabled = true;
        slot.Active = true;
        slot.Duration = lifetime;
        slot.RemainingTime = lifetime;
        slots[index] = slot;

        if (!wasActive)
        {
            activeCount++;
        }
    }

    private void UpdateSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            AfterimageSlot slot = slots[i];
            if (!slot.Active || slot.Renderer == null)
            {
                continue;
            }

            slot.RemainingTime -= Time.deltaTime;
            if (slot.RemainingTime <= 0f)
            {
                slot.Active = false;
                slot.Renderer.enabled = false;
                slots[i] = slot;
                activeCount = Mathf.Max(0, activeCount - 1);
                continue;
            }

            float fade = Mathf.Clamp01(slot.RemainingTime / slot.Duration);
            slot.Renderer.color = GetColor(alpha * fade);
            slots[i] = slot;
        }
    }

    private void EnsurePool()
    {
        if (slots != null && slots.Length == poolSize && root != null)
        {
            return;
        }

        if (root != null)
        {
            Destroy(root.gameObject);
        }

        GameObject rootObject = new GameObject("PlayerAfterimages");
        root = rootObject.transform;
        root.SetParent(transform.parent, false);
        root.position = transform.position;

        slots = new AfterimageSlot[poolSize];
        activeCount = 0;

        for (int i = 0; i < poolSize; i++)
        {
            GameObject slotObject = new GameObject($"Afterimage_{i}");
            slotObject.transform.SetParent(root, false);
            SpriteRenderer renderer = slotObject.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            renderer.sharedMaterial = sourceRenderer != null && sourceRenderer.sharedMaterial != null
                ? sourceRenderer.sharedMaterial
                : GetRuntimeFallbackMaterial();
            slots[i] = new AfterimageSlot
            {
                Renderer = renderer,
                Duration = lifetime,
                RemainingTime = 0f,
                Active = false
            };
        }
    }

    private int FindSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].Active)
            {
                return i;
            }
        }

        int oldestIndex = 0;
        float oldestTime = slots[0].RemainingTime;
        for (int i = 1; i < slots.Length; i++)
        {
            if (slots[i].RemainingTime < oldestTime)
            {
                oldestTime = slots[i].RemainingTime;
                oldestIndex = i;
            }
        }

        return oldestIndex;
    }

    private Vector3 GetBlockScale()
    {
        if (sourceCollider == null)
        {
            return new Vector3(fallbackBlockSize.x, fallbackBlockSize.y, 1f);
        }

        Vector2 size = sourceCollider.bounds.size;
        return new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), 1f);
    }

    private Color GetColor(float targetAlpha)
    {
        return ParticleColorUtility.PlayerColor(player, Color.white, targetAlpha);
    }

    private static Sprite GetRuntimeBlockSprite()
    {
        if (runtimeBlockSprite != null)
        {
            return runtimeBlockSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "RuntimeAfterimageBlock",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        runtimeBlockSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        runtimeBlockSprite.name = "RuntimeAfterimageBlockSprite";
        return runtimeBlockSprite;
    }

    private static Material GetRuntimeFallbackMaterial()
    {
        if (runtimeFallbackMaterial != null)
        {
            return runtimeFallbackMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        }

        runtimeFallbackMaterial = new Material(shader)
        {
            name = "RuntimeAfterimageMaterial"
        };
        return runtimeFallbackMaterial;
    }
}
