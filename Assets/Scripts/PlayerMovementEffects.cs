using UnityEngine;

[RequireComponent(typeof(PlayerController2D))]
public class PlayerMovementEffects : MonoBehaviour
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

    [Header("References")]
    [SerializeField] private ParticleSystem dustParticles;
    [SerializeField] private TrailRenderer movementTrail;
    [SerializeField] private Material particleMaterial;
    [SerializeField] private Material trailMaterial;
    [SerializeField] private CameraFollow2D cameraFollow;
    [SerializeField] private bool useMovementTrail;

    [Header("Dust")]
    [SerializeField] private Color dustColor = new Color(0.85f, 0.85f, 0.85f, 0.75f);
    [SerializeField] private float runDustInterval = 0.08f;
    [SerializeField] private int landBurstCount = 4;
    [SerializeField] private int jumpBurstCount = 6;
    [SerializeField] private int airJumpBurstCount = 10;
    [SerializeField] private int wallJumpBurstCount = 12;
    [SerializeField] private int heavyLandBurstCount = 10;
    [SerializeField] private int skidBurstCount = 4;
    [SerializeField] private int turnBurstCount = 3;
    [SerializeField] private float feetOffset = 0.72f;
    [SerializeField] private float hardLandThreshold = 5.5f;
    [SerializeField] private float slamLandThreshold = 13f;
    [SerializeField] private float turnSpeedThreshold = 2.6f;

    [Header("Afterimages")]
    [SerializeField] private bool enableAfterimages = true;
    [SerializeField] private bool useBlockAfterimages = true;
    [SerializeField] private int afterimagePoolSize = 6;
    [SerializeField] private float afterimageSpawnInterval = 0.05f;
    [SerializeField] private float afterimageLifetime = 0.22f;
    [SerializeField] private Vector2 fallbackAfterimageBlockSize = new Vector2(1f, 1f);
    [SerializeField] private Color afterimageWhiteColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color afterimageBlackColor = new Color(0.08f, 0.08f, 0.08f, 0.3f);

    [Header("Camera")]
    [SerializeField] private bool shakeCamera = true;
    [SerializeField] private float landShakeStrength = 0.06f;
    [SerializeField] private float slamShakeStrength = 0.08f;
    [SerializeField] private float jumpShakeStrength = 0.03f;
    [SerializeField] private float airJumpShakeStrength = 0.04f;
    [SerializeField] private float wallJumpShakeStrength = 0.05f;
    [SerializeField] private float turnShakeStrength = 0.02f;
    [SerializeField] private float shakeDuration = 0.08f;

    private PlayerController2D player;
    private SpriteRenderer sourceRenderer;
    private Collider2D sourceCollider;
    private AfterimageSlot[] afterimages;
    private Transform afterimageRoot;
    private bool wasGrounded;
    private float dustTimer;
    private float afterimageTimer;
    private float lastMoveInput;
    private float maxFallSpeedSinceAirborne;

    private void Awake()
    {
        player = GetComponent<PlayerController2D>();
        sourceRenderer = GetComponent<SpriteRenderer>();
        sourceCollider = GetComponent<Collider2D>();
        EnsureDustParticles();
        if (useMovementTrail)
        {
            EnsureMovementTrail();
        }
        else
        {
            DisableMovementTrail();
        }
        EnsureAfterimages();
        EnsureCameraFollow();
        ApplyRendererMaterials();
        wasGrounded = player.IsGrounded;
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.Jumped += OnPlayerJumped;
        }
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.Jumped -= OnPlayerJumped;
        }
    }

    public void SetEffectMaterials(Material newParticleMaterial, Material newTrailMaterial)
    {
        particleMaterial = newParticleMaterial;
        trailMaterial = newTrailMaterial;
        ApplyRendererMaterials();
    }

    private void Update()
    {
        bool grounded = player.IsGrounded;
        bool running = grounded && Mathf.Abs(player.MoveInput) > 0.1f && Mathf.Abs(player.Velocity.x) > 0.1f;

        if (running)
        {
            dustTimer -= Time.deltaTime;
            if (dustTimer <= 0f)
            {
                EmitDust(2, new Vector2(-Mathf.Sign(player.Velocity.x) * 1.4f, 1.2f));
                dustTimer = runDustInterval;
            }

            afterimageTimer -= Time.deltaTime;
            if (enableAfterimages && afterimageTimer <= 0f)
            {
                SpawnAfterimage();
                afterimageTimer = afterimageSpawnInterval;
            }
        }
        else
        {
            dustTimer = 0f;
            afterimageTimer = 0f;
        }

        if (!wasGrounded && grounded)
        {
            float landingSpeed = maxFallSpeedSinceAirborne;
            if (landingSpeed >= slamLandThreshold)
            {
                EmitLandingDust(heavyLandBurstCount);
                EmitSlamDust(landingSpeed);
                SpawnAfterimage(2);
                ShakeCamera(slamShakeStrength);
            }
            else
            {
                EmitLandingDust(landBurstCount + (landingSpeed > hardLandThreshold ? 4 : 0));
                SpawnAfterimage(landingSpeed > hardLandThreshold ? 2 : 1);
                ShakeCamera(landShakeStrength * Mathf.Clamp01(landingSpeed / hardLandThreshold));
            }
        }
        if (grounded && Mathf.Abs(player.MoveInput) > 0.1f)
        {
            bool changedDirection = lastMoveInput != 0f && Mathf.Sign(player.MoveInput) != Mathf.Sign(lastMoveInput);
            bool movingFast = Mathf.Abs(player.Velocity.x) > turnSpeedThreshold;
            if (changedDirection && movingFast)
            {
                EmitDust(skidBurstCount, new Vector2(-Mathf.Sign(player.Velocity.x) * 1.8f, 0.65f));
                SpawnAfterimage(turnBurstCount);
                ShakeCamera(turnShakeStrength);
            }
        }

        if (!grounded)
        {
            maxFallSpeedSinceAirborne = Mathf.Max(maxFallSpeedSinceAirborne, -player.Velocity.y);
        }
        else if (!wasGrounded)
        {
            maxFallSpeedSinceAirborne = 0f;
        }

        wasGrounded = grounded;
        lastMoveInput = player.MoveInput;
        if (useMovementTrail)
        {
            UpdateTrailColor();
        }
        UpdateAfterimages();
    }

    private void OnPlayerJumped(PlayerJumpType jumpType, Vector2 direction)
    {
        switch (jumpType)
        {
            case PlayerJumpType.Ground:
                EmitDust(jumpBurstCount, Vector2.down * 1.35f);
                SpawnAfterimage(1);
                ShakeCamera(jumpShakeStrength);
                break;
            case PlayerJumpType.Air:
                EmitDust(airJumpBurstCount, Vector2.down * 0.8f);
                EmitRadialBurst(airJumpBurstCount, 1.6f);
                SpawnAfterimage(2);
                ShakeCamera(airJumpShakeStrength);
                break;
            case PlayerJumpType.Wall:
                EmitDust(wallJumpBurstCount, new Vector2(-direction.x * 2.2f, 0.9f));
                EmitWallBurst(direction.x);
                SpawnAfterimage(3);
                ShakeCamera(wallJumpShakeStrength);
                break;
        }
    }

    private void EmitDust(int count, Vector2 baseVelocity)
    {
        if (dustParticles == null)
        {
            return;
        }

        Vector3 feetPosition = transform.position + Vector3.down * feetOffset;
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = feetPosition + new Vector3(Random.Range(-0.25f, 0.25f), 0f, 0f),
                velocity = baseVelocity + new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(0f, 0.5f)),
                startColor = GetStateDustColor(),
                startLifetime = Random.Range(0.22f, 0.38f),
                startSize = Random.Range(0.08f, 0.16f)
            };

            dustParticles.Emit(emitParams, 1);
        }
    }

    private void EmitLandingDust(int count)
    {
        if (dustParticles == null || count <= 0)
        {
            return;
        }

        Vector3 feetPosition = transform.position + Vector3.down * feetOffset;
        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = feetPosition + new Vector3(side * Random.Range(0.04f, 0.18f), 0f, 0f),
                velocity = new Vector2(side * Random.Range(1.1f, 2.4f), Random.Range(0.1f, 0.45f)),
                startColor = GetStateDustColor(),
                startLifetime = Random.Range(0.22f, 0.38f),
                startSize = Random.Range(0.09f, 0.18f)
            };

            dustParticles.Emit(emitParams, 1);
        }
    }

    private void EmitSlamDust(float landingSpeed)
    {
        if (dustParticles == null)
        {
            return;
        }

        Vector3 feetPosition = transform.position + Vector3.down * feetOffset;
        float strength = Mathf.Clamp01(landingSpeed / (slamLandThreshold * 1.6f));
        int count = Mathf.RoundToInt(Mathf.Lerp(4, 10, strength));

        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = feetPosition + new Vector3(side * Random.Range(0.1f, 0.28f), Random.Range(-0.03f, 0.08f), 0f),
                velocity = new Vector2(side * Random.Range(1.4f, 2.8f), Random.Range(0.08f, 0.45f)),
                startColor = GetStateDustColor(),
                startLifetime = Random.Range(0.2f, 0.36f),
                startSize = Random.Range(0.09f, 0.18f)
            };

            dustParticles.Emit(emitParams, 1);
        }
    }

    private void EmitRadialBurst(int count, float speed)
    {
        if (dustParticles == null || count <= 0)
        {
            return;
        }

        Vector3 position = transform.position;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.PI * 2f * i / count;
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = GetStateDustColor(),
                startLifetime = Random.Range(0.18f, 0.3f),
                startSize = Random.Range(0.06f, 0.13f)
            };

            dustParticles.Emit(emitParams, 1);
        }
    }

    private void EmitWallBurst(float jumpDirection)
    {
        if (dustParticles == null)
        {
            return;
        }

        float wallSide = -Mathf.Sign(jumpDirection);
        Vector3 position = transform.position + new Vector3(wallSide * 0.38f, -0.05f, 0f);
        for (int i = 0; i < wallJumpBurstCount; i++)
        {
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = position + new Vector3(0f, Random.Range(-0.45f, 0.45f), 0f),
                velocity = new Vector2(jumpDirection * Random.Range(1.4f, 2.8f), Random.Range(0.2f, 1.7f)),
                startColor = GetStateDustColor(),
                startLifetime = Random.Range(0.2f, 0.34f),
                startSize = Random.Range(0.07f, 0.15f)
            };

            dustParticles.Emit(emitParams, 1);
        }
    }

    private void SpawnAfterimage(int count = 1)
    {
        if (!enableAfterimages || sourceRenderer == null || afterimages == null || afterimages.Length == 0)
        {
            return;
        }

        for (int n = 0; n < count; n++)
        {
            int slotIndex = FindInactiveAfterimageSlot();
            AfterimageSlot slot = afterimages[slotIndex];
            if (slot.Renderer == null)
            {
                continue;
            }

            slot.Renderer.sprite = useBlockAfterimages ? GetRuntimeBlockSprite() : sourceRenderer.sprite;
            slot.Renderer.flipX = sourceRenderer.flipX;
            slot.Renderer.flipY = sourceRenderer.flipY;
            slot.Renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            slot.Renderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            slot.Renderer.transform.position = transform.position;
            slot.Renderer.transform.rotation = useBlockAfterimages ? Quaternion.identity : transform.rotation;
            slot.Renderer.transform.localScale = useBlockAfterimages ? GetBlockAfterimageScale() : transform.localScale;
            slot.Renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            slot.Renderer.enabled = true;
            slot.Active = true;
            slot.Duration = afterimageLifetime;
            slot.RemainingTime = afterimageLifetime;
            slot.Renderer.color = GetAfterimageColor();
            afterimages[slotIndex] = slot;
        }
    }

    private Vector3 GetBlockAfterimageScale()
    {
        if (sourceCollider == null)
        {
            return new Vector3(fallbackAfterimageBlockSize.x, fallbackAfterimageBlockSize.y, 1f);
        }

        Vector2 size = sourceCollider.bounds.size;
        return new Vector3(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y),
            1f);
    }

    private void EnsureDustParticles()
    {
        if (dustParticles != null)
        {
            return;
        }

        dustParticles = GetComponentInChildren<ParticleSystem>(true);
        if (dustParticles != null)
        {
            return;
        }

        GameObject dustObject = new GameObject("MovementDust");
        dustObject.transform.SetParent(transform, false);
        dustParticles = dustObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = dustParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = dustParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = dustParticles.shape;
        shape.enabled = false;

        ApplyParticleMaterial();
    }

    private void EnsureMovementTrail()
    {
        if (movementTrail != null)
        {
            return;
        }

        movementTrail = GetComponentInChildren<TrailRenderer>(true);
        if (movementTrail != null)
        {
            return;
        }

        GameObject trailObject = new GameObject("MovementTrail");
        trailObject.transform.SetParent(transform, false);
        movementTrail = trailObject.AddComponent<TrailRenderer>();
        movementTrail.time = 0.18f;
        movementTrail.startWidth = 0.42f;
        movementTrail.endWidth = 0f;
        movementTrail.sortingOrder = 4;
        movementTrail.numCapVertices = 2;
        movementTrail.numCornerVertices = 2;
        ApplyTrailMaterial();
        UpdateTrailColor();
    }

    private void DisableMovementTrail()
    {
        if (movementTrail == null)
        {
            movementTrail = GetComponentInChildren<TrailRenderer>(true);
        }

        if (movementTrail != null)
        {
            movementTrail.emitting = false;
            movementTrail.enabled = false;
        }
    }

    private void EnsureAfterimages()
    {
        if (!enableAfterimages || sourceRenderer == null || afterimagePoolSize <= 0)
        {
            return;
        }

        afterimages = new AfterimageSlot[afterimagePoolSize];

        Transform parent = transform.parent;
        GameObject rootObject = new GameObject("MovementAfterimages");
        afterimageRoot = rootObject.transform;
        afterimageRoot.SetParent(parent, false);
        afterimageRoot.position = transform.position;

        for (int i = 0; i < afterimagePoolSize; i++)
        {
            GameObject slotObject = new GameObject($"Afterimage_{i}");
            slotObject.transform.SetParent(afterimageRoot, false);
            SpriteRenderer renderer = slotObject.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            renderer.sharedMaterial = sourceRenderer.sharedMaterial != null ? sourceRenderer.sharedMaterial : GetRuntimeFallbackMaterial();
            renderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            afterimages[i] = new AfterimageSlot
            {
                Renderer = renderer,
                RemainingTime = 0f,
                Duration = afterimageLifetime,
                Active = false
            };
        }
    }

    private void EnsureCameraFollow()
    {
        if (cameraFollow != null)
        {
            return;
        }

        cameraFollow = FindObjectOfType<CameraFollow2D>();
    }

    private void ApplyRendererMaterials()
    {
        ApplyParticleMaterial();
        ApplyTrailMaterial();
    }

    private void ApplyParticleMaterial()
    {
        if (dustParticles == null)
        {
            return;
        }

        ParticleSystemRenderer renderer = dustParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = particleMaterial != null ? particleMaterial : GetRuntimeFallbackMaterial();
        }
    }

    private void ApplyTrailMaterial()
    {
        if (movementTrail != null)
        {
            movementTrail.sharedMaterial = trailMaterial != null ? trailMaterial : GetRuntimeFallbackMaterial();
        }
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
            name = "RuntimeEffectFallbackMaterial"
        };

        return runtimeFallbackMaterial;
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

        runtimeBlockSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        runtimeBlockSprite.name = "RuntimeAfterimageBlockSprite";

        return runtimeBlockSprite;
    }

    private void UpdateTrailColor()
    {
        if (movementTrail == null)
        {
            return;
        }

        Color stateColor = player.CurrentColor == PlayerColorState.White
            ? new Color(1f, 1f, 1f, 0.55f)
            : new Color(0.05f, 0.05f, 0.05f, 0.55f);

        movementTrail.startColor = stateColor;
        movementTrail.endColor = new Color(stateColor.r, stateColor.g, stateColor.b, 0f);
        movementTrail.startWidth = Mathf.Lerp(0.18f, 0.42f, Mathf.InverseLerp(0.2f, 8f, player.Velocity.magnitude));
        movementTrail.emitting = Mathf.Abs(player.Velocity.x) > 0.2f || Mathf.Abs(player.Velocity.y) > 0.2f;
    }

    private void UpdateAfterimages()
    {
        if (!enableAfterimages || afterimages == null)
        {
            return;
        }

        for (int i = 0; i < afterimages.Length; i++)
        {
            AfterimageSlot slot = afterimages[i];
            if (!slot.Active || slot.Renderer == null)
            {
                continue;
            }

            slot.RemainingTime -= Time.deltaTime;
            if (slot.RemainingTime <= 0f)
            {
                slot.Active = false;
                slot.Renderer.enabled = false;
                afterimages[i] = slot;
                continue;
            }

            float t = Mathf.Clamp01(slot.RemainingTime / slot.Duration);
            Color color = slot.Renderer.color;
            color.a = GetAfterimageColor().a * t;
            slot.Renderer.color = color;
            afterimages[i] = slot;
        }
    }

    private int FindInactiveAfterimageSlot()
    {
        for (int i = 0; i < afterimages.Length; i++)
        {
            if (!afterimages[i].Active)
            {
                return i;
            }
        }

        int oldestIndex = 0;
        float oldestTime = afterimages[0].RemainingTime;
        for (int i = 1; i < afterimages.Length; i++)
        {
            if (afterimages[i].RemainingTime < oldestTime)
            {
                oldestTime = afterimages[i].RemainingTime;
                oldestIndex = i;
            }
        }

        return oldestIndex;
    }

    private Color GetAfterimageColor()
    {
        return player.CurrentColor == PlayerColorState.White
            ? afterimageWhiteColor
            : afterimageBlackColor;
    }

    private void ShakeCamera(float strength)
    {
        if (!shakeCamera || cameraFollow == null || strength <= 0f)
        {
            return;
        }

        cameraFollow.Shake(shakeDuration, strength);
    }

    private Color GetStateDustColor()
    {
        if (player.CurrentColor == PlayerColorState.White)
        {
            return dustColor;
        }

        return new Color(0.12f, 0.12f, 0.12f, 0.65f);
    }
}
