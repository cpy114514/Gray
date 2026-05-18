using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerDeathRespawnEffect : MonoBehaviour
{
    private const float ForcedFinalShakeDuration = 0.055f;
    private const float ForcedFinalShakeStrength = 0.18f;

    [SerializeField] private int particleCount = 12;
    [SerializeField] private Vector2 particleSizeRange = new Vector2(0.26f, 0.41f);
    [SerializeField] private float burstDuration = 0.32f;
    [SerializeField] private float flyDuration = 0.55f;
    [SerializeField] private float assembleDuration = 0.38f;
    [SerializeField] private float burstForce = 2.65f;
    [SerializeField] private float upwardBurst = 1.15f;
    [SerializeField] private float assembleSpread = 0.34f;
    [SerializeField] private float finalShakeDuration = 0.5f;
    [SerializeField] private float finalShakeStrength = 0.3f;
    [SerializeField] private int finalImpactParticleCount = 10;
    [SerializeField] private float finalAssemblyPopDuration = 0.16f;
    [SerializeField] private float finalAssemblyOvershoot = 1.18f;

    private static Sprite pixelSprite;
    private static Material pixelRenderMaterial;
    private PlayerController2D player;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Collider2D[] playerColliders;
    private Coroutine respawnRoutine;
    private CameraFollow2D cameraFollow;
    private Transform originalCameraTarget;
    private System.Action pendingCompleteCallback;

    public bool IsRespawning => respawnRoutine != null;

    private void Awake()
    {
        player = GetComponent<PlayerController2D>();
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerColliders = GetComponentsInChildren<Collider2D>(true);
        Warmup();
    }

    public void Warmup()
    {
        player = player != null ? player : GetComponent<PlayerController2D>();
        body = body != null ? body : GetComponent<Rigidbody2D>();
        spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        if (playerColliders == null || playerColliders.Length == 0)
        {
            playerColliders = GetComponentsInChildren<Collider2D>(true);
        }

        finalShakeDuration = ForcedFinalShakeDuration;
        finalShakeStrength = ForcedFinalShakeStrength;
        finalAssemblyPopDuration = 0.26f;
        finalAssemblyOvershoot = 1.14f;
        GetPixelSprite();
        GetPixelRenderMaterial();
    }

    public void Play()
    {
        Play(null);
    }

    public bool Play(System.Action onComplete)
    {
        if (respawnRoutine != null)
        {
            return false;
        }

        pendingCompleteCallback = onComplete;
        respawnRoutine = StartCoroutine(PlayRoutine());
        return true;
    }

    private IEnumerator PlayRoutine()
    {
        Vector3 deathPosition = transform.position;
        Vector3 spawnPosition = player.SpawnPosition;
        Bounds playerBounds = spriteRenderer.bounds;
        Color particleColor = ParticleColorUtility.WorldColor(player, deathPosition, Color.white);
        bool originalBodySimulated = body.simulated;

        Transform cameraTarget = CreateCameraTarget(deathPosition);
        AttachCameraToParticles(cameraTarget);

        player.SetControlEnabled(false);
        body.velocity = Vector2.zero;
        body.simulated = false;
        ClearPlayerTrails();
        spriteRenderer.enabled = false;
        SetPlayerCollidersEnabled(false);

        DeathParticle[] particles = CreateParticles(deathPosition, spawnPosition, playerBounds, particleColor);

        float elapsed = 0f;
        while (elapsed < burstDuration)
        {
            elapsed += Time.deltaTime;
            UpdateParticleColors(particles, particleColor);
            UpdateCameraTarget(cameraTarget, particles, deathPosition);
            yield return null;
        }

        FreezeParticlesForFlight(particles);

        elapsed = 0f;
        while (elapsed < flyDuration + assembleDuration)
        {
            elapsed += Time.deltaTime;
            float flyT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, flyDuration));
            float assembleT = Mathf.Clamp01((elapsed - flyDuration) / Mathf.Max(0.01f, assembleDuration));

            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i].Renderer == null)
                {
                    continue;
                }

                float particleT = Smooth01(flyT);
                Vector3 travelTarget = spawnPosition + particles[i].OrbitOffset;
                Vector3 position = Vector3.LerpUnclamped(particles[i].FlightStartPosition, travelTarget, particleT);

                if (assembleT > 0f)
                {
                    float orderedT = GetOrderedAssembleT(assembleT, i, particles.Length);
                    position = Vector3.LerpUnclamped(travelTarget, spawnPosition + particles[i].SettleOffset, orderedT);
                }

                particles[i].Renderer.transform.position = position;
                if (assembleT <= 0.95f)
                {
                    particles[i].Renderer.transform.Rotate(0f, 0f, 260f * Time.deltaTime);
                }

                particles[i].Renderer.color = GetDeathParticleColor(position, particleColor);
            }

            UpdateCameraTarget(cameraTarget, particles, spawnPosition);
            yield return null;
        }

        player.ResetToSpawn();
        player.SetControlEnabled(false);
        body.simulated = false;
        body.velocity = Vector2.zero;
        SetPlayerCollidersEnabled(false);
        spriteRenderer.enabled = true;
        ClearPlayerTrails();
        yield return PlayFinalAssemblySnapEffect(spawnPosition, particleColor);
        yield return new WaitForSeconds(0.08f);
        body.simulated = originalBodySimulated;
        SetPlayerCollidersEnabled(true);
        body.velocity = Vector2.zero;
        player.SetControlEnabled(true);
        RestoreCameraTarget();
        Destroy(cameraTarget != null ? cameraTarget.gameObject : null);

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].Renderer != null)
            {
                Destroy(particles[i].Renderer.gameObject);
            }
        }

        respawnRoutine = null;
        System.Action completeCallback = pendingCompleteCallback;
        pendingCompleteCallback = null;
        completeCallback?.Invoke();
    }

    private DeathParticle[] CreateParticles(Vector3 deathPosition, Vector3 spawnPosition, Bounds bounds, Color color)
    {
        DeathParticle[] particles = new DeathParticle[Mathf.Max(1, particleCount)];
        for (int i = 0; i < particles.Length; i++)
        {
            GameObject particle = new GameObject("DeathRespawnPixel", typeof(SpriteRenderer), typeof(Rigidbody2D));
            float size = Random.Range(particleSizeRange.x, particleSizeRange.y);
            particle.transform.localScale = new Vector3(size, size, 1f);
            particle.transform.position = deathPosition + new Vector3(
                Random.Range(-bounds.extents.x * 0.48f, bounds.extents.x * 0.48f),
                Random.Range(-bounds.extents.y * 0.48f, bounds.extents.y * 0.48f),
                0f);

            SpriteRenderer renderer = particle.GetComponent<SpriteRenderer>();
            renderer.sprite = GetPixelSprite();
            renderer.sharedMaterial = GetPixelRenderMaterial();
            renderer.color = GetDeathParticleColor(particle.transform.position, color);
            renderer.sortingLayerID = spriteRenderer.sortingLayerID;
            renderer.sortingOrder = 32000;

            Rigidbody2D particleBody = particle.GetComponent<Rigidbody2D>();
            particleBody.gravityScale = 0f;
            particleBody.mass = 0.08f;
            particleBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            particleBody.interpolation = RigidbodyInterpolation2D.Interpolate;

            Vector2 away = particle.transform.position - deathPosition;
            if (away.sqrMagnitude < 0.001f)
            {
                away = Random.insideUnitCircle;
            }

            Vector2 velocity = away.normalized * Random.Range(burstForce * 0.65f, burstForce);
            velocity.y += Random.Range(0.2f, upwardBurst);
            particleBody.velocity = velocity;
            particleBody.angularVelocity = Random.Range(-280f, 280f);

            particles[i] = new DeathParticle
            {
                Renderer = renderer,
                Body = particleBody,
                OrbitOffset = Random.insideUnitCircle * assembleSpread,
                SettleOffset = new Vector3(
                    Random.Range(-bounds.extents.x * assembleSpread, bounds.extents.x * assembleSpread),
                    Random.Range(-bounds.extents.y * assembleSpread, bounds.extents.y * assembleSpread),
                    0f)
            };
        }

        return particles;
    }

    private static void FreezeParticlesForFlight(DeathParticle[] particles)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].Renderer == null)
            {
                continue;
            }

            particles[i].FlightStartPosition = particles[i].Renderer.transform.position;
            if (particles[i].Body != null)
            {
                particles[i].Body.velocity = Vector2.zero;
                particles[i].Body.angularVelocity = 0f;
                particles[i].Body.simulated = false;
            }
        }
    }

    private void UpdateParticleColors(DeathParticle[] particles, Color fallbackColor)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].Renderer != null)
            {
                particles[i].Renderer.color = GetDeathParticleColor(particles[i].Renderer.transform.position, fallbackColor);
            }
        }
    }

    private Color GetDeathParticleColor(Vector3 worldPosition, Color fallbackColor)
    {
        return ParticleColorUtility.WorldColor(player, worldPosition, fallbackColor);
    }

    private Transform CreateCameraTarget(Vector3 position)
    {
        GameObject target = new GameObject("DeathRespawnParticleCameraTarget");
        target.transform.position = position;
        return target.transform;
    }

    private void AttachCameraToParticles(Transform target)
    {
        cameraFollow = Camera.main != null
            ? Camera.main.GetComponent<CameraFollow2D>()
            : FindObjectOfType<CameraFollow2D>();

        if (cameraFollow == null || target == null)
        {
            return;
        }

        originalCameraTarget = transform;
        cameraFollow.SetTarget(target, false);
    }

    private void RestoreCameraTarget()
    {
        if (cameraFollow != null)
        {
            cameraFollow.enabled = true;
            cameraFollow.SetTarget(originalCameraTarget != null ? originalCameraTarget : transform, false);
        }

        originalCameraTarget = null;
    }

    private static void UpdateCameraTarget(Transform cameraTarget, DeathParticle[] particles, Vector3 fallbackPosition)
    {
        if (cameraTarget == null)
        {
            return;
        }

        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].Renderer == null)
            {
                continue;
            }

            sum += particles[i].Renderer.transform.position;
            count++;
        }

        cameraTarget.position = count > 0 ? sum / count : fallbackPosition;
    }

    private void SetPlayerCollidersEnabled(bool enabled)
    {
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (playerColliders[i] != null)
            {
                playerColliders[i].enabled = enabled;
            }
        }
    }

    private void ClearPlayerTrails()
    {
        TrailRenderer[] trails = GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] == null)
            {
                continue;
            }

            trails[i].emitting = false;
            trails[i].enabled = false;
            trails[i].Clear();
        }
    }

    private float GetOrderedAssembleT(float t, int index, int total)
    {
        if (total <= 1)
        {
            return Smooth01(t);
        }

        float start = 0.45f * index / (total - 1f);
        return Smooth01((t - start) / 0.55f);
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void ShakeCamera()
    {
        CameraFollow2D cameraFollow = Camera.main != null
            ? Camera.main.GetComponent<CameraFollow2D>()
            : FindObjectOfType<CameraFollow2D>();

        if (cameraFollow != null)
        {
            cameraFollow.Shake(finalShakeDuration, finalShakeStrength);
        }
    }

    private IEnumerator PlayFinalAssemblySnapEffect(Vector3 position, Color fallbackColor)
    {
        Vector3 originalScale = transform.localScale;
        int count = Mathf.Max(0, finalImpactParticleCount);
        SpriteRenderer[] impacts = new SpriteRenderer[count];
        Vector3[] starts = new Vector3[count];
        Vector3[] ends = new Vector3[count];

        Bounds bounds = spriteRenderer.bounds;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.PI * 2f * i / Mathf.Max(1, count);
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            starts[i] = position + direction * Random.Range(0.35f, 0.58f);
            ends[i] = position + new Vector3(
                Random.Range(-bounds.extents.x * 0.28f, bounds.extents.x * 0.28f),
                Random.Range(-bounds.extents.y * 0.28f, bounds.extents.y * 0.28f),
                0f);

            GameObject impact = new GameObject("DeathRespawnAssemblePixel", typeof(SpriteRenderer));
            impact.transform.position = starts[i];
            impact.transform.localScale = Vector3.one * Random.Range(0.09f, 0.17f);

            SpriteRenderer renderer = impact.GetComponent<SpriteRenderer>();
            renderer.sprite = GetPixelSprite();
            renderer.sharedMaterial = GetPixelRenderMaterial();
            renderer.sortingLayerID = spriteRenderer.sortingLayerID;
            renderer.sortingOrder = 32001;
            renderer.color = GetDeathParticleColor(impact.transform.position, fallbackColor);
            impacts[i] = renderer;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, finalAssemblyPopDuration);
        bool firstClickPlayed = false;
        bool secondClickPlayed = false;
        bool finalClickPlayed = false;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = Smooth01(t);

            float pop = GetAssemblyPopScale(t);
            transform.localScale = originalScale * pop;

            if (!firstClickPlayed && t >= 0.18f)
            {
                ShakeCamera(finalShakeDuration * 0.85f, finalShakeStrength * 0.42f);
                firstClickPlayed = true;
            }
            if (!secondClickPlayed && t >= 0.46f)
            {
                ShakeCamera(finalShakeDuration, finalShakeStrength * 0.68f);
                secondClickPlayed = true;
            }
            if (!finalClickPlayed && t >= 0.78f)
            {
                ShakeCamera(finalShakeDuration * 1.15f, finalShakeStrength);
                finalClickPlayed = true;
            }

            for (int i = 0; i < impacts.Length; i++)
            {
                if (impacts[i] == null)
                {
                    continue;
                }

                impacts[i].transform.position = Vector3.LerpUnclamped(starts[i], ends[i], ease);
                Color color = ParticleColorUtility.WorldColor(player, impacts[i].transform.position, fallbackColor, 1f - t);
                impacts[i].color = color;
            }

            yield return null;
        }

        transform.localScale = originalScale;

        for (int i = 0; i < impacts.Length; i++)
        {
            if (impacts[i] != null)
            {
                Destroy(impacts[i].gameObject);
            }
        }
    }

    private float GetAssemblyPopScale(float t)
    {
        if (t < 0.22f)
        {
            return Mathf.Lerp(1.06f, 0.94f, Smooth01(t / 0.22f));
        }

        if (t < 0.48f)
        {
            return Mathf.Lerp(0.94f, finalAssemblyOvershoot, Smooth01((t - 0.22f) / 0.26f));
        }

        if (t < 0.72f)
        {
            return Mathf.Lerp(finalAssemblyOvershoot, 0.97f, Smooth01((t - 0.48f) / 0.24f));
        }

        return Mathf.Lerp(0.97f, 1f, Smooth01((t - 0.72f) / 0.28f));
    }

    private void ShakeCamera(float duration, float strength)
    {
        CameraFollow2D cameraFollow = Camera.main != null
            ? Camera.main.GetComponent<CameraFollow2D>()
            : FindObjectOfType<CameraFollow2D>();

        if (cameraFollow != null)
        {
            cameraFollow.Shake(duration, strength);
        }
    }

    private static Sprite GetPixelSprite()
    {
        if (pixelSprite != null)
        {
            return pixelSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        pixelSprite.name = "Runtime_DeathRespawnPixelSprite";
        return pixelSprite;
    }

    private static Material GetPixelRenderMaterial()
    {
        if (pixelRenderMaterial != null)
        {
            return pixelRenderMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        pixelRenderMaterial = new Material(shader)
        {
            name = "Runtime_DeathRespawnPixelUnlit"
        };
        if (pixelRenderMaterial.HasProperty("_Color"))
        {
            pixelRenderMaterial.SetColor("_Color", Color.white);
        }
        if (pixelRenderMaterial.HasProperty("_BaseColor"))
        {
            pixelRenderMaterial.SetColor("_BaseColor", Color.white);
        }
        return pixelRenderMaterial;
    }

    private struct DeathParticle
    {
        public SpriteRenderer Renderer;
        public Rigidbody2D Body;
        public Vector3 FlightStartPosition;
        public Vector3 OrbitOffset;
        public Vector3 SettleOffset;
    }
}
