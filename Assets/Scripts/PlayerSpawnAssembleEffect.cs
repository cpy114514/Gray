using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class PlayerSpawnAssembleEffect : MonoBehaviour
{
    [SerializeField] private int particleCount = 12;
    [SerializeField] private float introDuration = 2.35f;
    [SerializeField] private float fallPhase = 0.64f;
    [SerializeField] private float groundHoldPhase = 0.18f;
    [SerializeField] private Vector2 startSpread = new Vector2(1.45f, 0.65f);
    [SerializeField] private float gatherSpread = 0.34f;
    [SerializeField] private Vector2 particleSizeRange = new Vector2(0.26f, 0.41f);
    [SerializeField] private float assembleSequencePortion = 0.68f;
    [SerializeField] private float assembleShakeDuration = 0.16f;
    [SerializeField] private float assembleShakeStrength = 0.055f;
    [SerializeField] private float finalAssemblyPopDuration = 0.26f;
    [SerializeField] private float finalAssemblyOvershoot = 1.14f;
    [SerializeField] private Vector2 landingBlockSize = new Vector2(2.1f, 0.24f);
    [SerializeField] private LayerMask groundMask = ~0;

    private static Sprite pixelSprite;
    private static Material pixelRenderMaterial;
    private static PhysicsMaterial2D pixelPhysicsMaterial;
    private PlayerController2D player;
    private SpriteRenderer playerRenderer;
    private Rigidbody2D body;
    private bool originalSimulated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForLoadedScene()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToPlayerIfNeeded(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToPlayerIfNeeded(scene);
    }

    private static void AttachToPlayerIfNeeded(Scene scene)
    {
        if (!scene.IsValid() || scene.buildIndex == SceneFlow.MainMenuBuildIndex || scene.name == "MainMenu")
        {
            return;
        }

        PlayerController2D player = FindObjectOfType<PlayerController2D>();
        if (player == null || player.GetComponent<PlayerSpawnAssembleEffect>() != null)
        {
            return;
        }

        player.gameObject.AddComponent<PlayerSpawnAssembleEffect>();
    }

    private void Awake()
    {
        ApplyRuntimeDefaults();
        player = GetComponent<PlayerController2D>();
        playerRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
    }

    public void InitializeRuntimeDefaults()
    {
        ApplyRuntimeDefaults();
    }

    private void Start()
    {
        if (player == null || playerRenderer == null)
        {
            return;
        }

        StartCoroutine(PlayIntro());
    }

    private IEnumerator PlayIntro()
    {
        Vector3 targetPosition = transform.position;
        player.RefreshColorFromBackgroundNow();
        Color targetColor = ParticleColorUtility.WorldColor(player, targetPosition, Color.white);
        Bounds playerBounds = GetPlayerBounds(targetPosition);

        player.SetControlEnabled(false);
        playerRenderer.enabled = false;
        if (body != null)
        {
            originalSimulated = body.simulated;
            body.velocity = Vector2.zero;
            body.simulated = false;
        }

        GameObject landingBlock = HasSceneGroundBelow(targetPosition, playerBounds)
            ? null
            : CreateLandingBlock(targetPosition, playerBounds);
        SpawnParticle[] particles = CreateParticles(targetPosition, targetColor, playerBounds);

        float duration = Mathf.Max(0.1f, introDuration);
        float elapsed = 0f;
        bool gatheringStarted = false;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float gatherT = Mathf.Clamp01((t - fallPhase - groundHoldPhase) / Mathf.Max(0.05f, 1f - fallPhase - groundHoldPhase));
            float gatherEase = gatherT * gatherT * (3f - 2f * gatherT);

            if (!gatheringStarted && gatherT > 0f)
            {
                FreezeParticlesForGather(particles);
                gatheringStarted = true;
            }

            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i].Renderer == null)
                {
                    continue;
                }

                if (!gatheringStarted)
                {
                    PreventGroundTunneling(ref particles[i]);
                    StopParticleSpinOnGround(particles[i].Body);
                }

                if (gatheringStarted)
                {
                    float particleT = GetParticleGatherT(gatherT, i, particles.Length);
                    Vector3 gatheredPosition = Vector3.LerpUnclamped(
                        particles[i].GatherStartPosition,
                        targetPosition + particles[i].SettleOffset,
                        particleT);
                    particles[i].Renderer.transform.position = gatheredPosition;
                    particles[i].Renderer.transform.Rotate(0f, 0f, 220f * Time.deltaTime);
                }

                particles[i].Renderer.color = GetParticleColor(particles[i].Renderer.transform.position, targetColor);
            }

            yield return null;
        }

        if (body != null)
        {
            body.simulated = originalSimulated;
            body.velocity = Vector2.zero;
        }

        playerRenderer.enabled = true;
        yield return PlayFinalAssemblySnapEffect(targetPosition);
        player.SetControlEnabled(true);
        Destroy(landingBlock);

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].Renderer != null)
            {
                Destroy(particles[i].Renderer.gameObject);
            }
        }
    }

    private SpawnParticle[] CreateParticles(Vector3 targetPosition, Color targetColor, Bounds bounds)
    {
        SpawnParticle[] particles = new SpawnParticle[Mathf.Max(1, particleCount)];

        for (int i = 0; i < particles.Length; i++)
        {
            GameObject particle = new GameObject("SpawnPixel", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D));
            particle.transform.localScale = Vector3.one * Random.Range(particleSizeRange.x, particleSizeRange.y);

            SpriteRenderer renderer = particle.GetComponent<SpriteRenderer>();
            renderer.sprite = GetPixelSprite();
            renderer.sharedMaterial = GetPixelRenderMaterial();
            renderer.color = GetParticleColor(particle.transform.position, targetColor);
            renderer.sortingLayerID = playerRenderer.sortingLayerID;
            renderer.sortingOrder = 32000;
            renderer.enabled = true;

            BoxCollider2D particleCollider = particle.GetComponent<BoxCollider2D>();
            particleCollider.sharedMaterial = GetPixelPhysicsMaterial();
            particleCollider.isTrigger = false;

            Rigidbody2D particleBody = particle.GetComponent<Rigidbody2D>();
            particleBody.bodyType = RigidbodyType2D.Dynamic;
            particleBody.gravityScale = 3.2f;
            particleBody.mass = 0.08f;
            particleBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            particleBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            particleBody.sleepMode = RigidbodySleepMode2D.NeverSleep;
            ForceSceneCollisions(particleCollider);

            Vector3 localSettle = new Vector3(
                Random.Range(-bounds.extents.x * gatherSpread, bounds.extents.x * gatherSpread),
                Random.Range(-bounds.extents.y * gatherSpread, bounds.extents.y * gatherSpread),
                0f);
            Vector3 ground = FindGroundPosition(targetPosition, bounds, i, particles.Length);
            Vector3 start = GetCameraTopSpawnPosition(targetPosition, ground);

            particle.transform.position = start;
            renderer.color = GetParticleColor(start, targetColor);
            float explodeDirection = Mathf.Sign(start.x - targetPosition.x);
            if (Mathf.Approximately(explodeDirection, 0f))
            {
                explodeDirection = Random.value < 0.5f ? -1f : 1f;
            }

            particleBody.velocity = new Vector2(
                explodeDirection * Random.Range(0.65f, 1.15f) + Random.Range(-0.16f, 0.16f),
                Random.Range(0.42f, 1.08f));
            particleBody.angularVelocity = Random.Range(-220f, 220f);

            particles[i] = new SpawnParticle
            {
                Renderer = renderer,
                Body = particleBody,
                Collider = particleCollider,
                PreviousPosition = particle.transform.position,
                SettleOffset = localSettle
            };
        }

        return particles;
    }

    private Color GetParticleColor(Vector3 worldPosition, Color fallbackColor)
    {
        return ParticleColorUtility.WorldColor(player, worldPosition, fallbackColor);
    }

    private Bounds GetPlayerBounds(Vector3 targetPosition)
    {
        Bounds bounds = playerRenderer.bounds;
        if (bounds.size == Vector3.zero)
        {
            bounds = new Bounds(targetPosition, Vector3.one);
        }

        return bounds;
    }

    private GameObject CreateLandingBlock(Vector3 targetPosition, Bounds playerBounds)
    {
        GameObject block = new GameObject("SpawnParticleInvisibleBlock", typeof(BoxCollider2D), typeof(Rigidbody2D));
        float width = Mathf.Max(landingBlockSize.x, playerBounds.size.x * 1.65f);
        float height = Mathf.Max(0.08f, landingBlockSize.y);
        block.transform.position = new Vector3(targetPosition.x, targetPosition.y - playerBounds.extents.y - height * 0.5f - 0.035f, targetPosition.z);

        BoxCollider2D collider = block.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(width, height);
        collider.sharedMaterial = GetPixelPhysicsMaterial();

        Rigidbody2D blockBody = block.GetComponent<Rigidbody2D>();
        blockBody.bodyType = RigidbodyType2D.Static;
        blockBody.simulated = true;
        return block;
    }

    private bool HasSceneGroundBelow(Vector3 targetPosition, Bounds playerBounds)
    {
        Vector2 origin = new Vector2(targetPosition.x, targetPosition.y + playerBounds.extents.y + 0.25f);
        float distance = playerBounds.size.y + 1.25f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, distance, groundMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (IsValidSceneGroundCollider(hitCollider))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValidSceneGroundCollider(Collider2D collider)
    {
        if (collider == null || collider.isTrigger)
        {
            return false;
        }

        if (collider.GetComponentInParent<PlayerController2D>() != null)
        {
            return false;
        }

        return collider.GetComponent<TilemapCollider2D>() != null
            || collider.GetComponent<CompositeCollider2D>() != null
            || collider.GetComponent<EdgeCollider2D>() != null
            || collider.GetComponentInParent<ColorPlatform>() != null
            || collider.name.Contains("Boundary")
            || collider.name.Contains("Tilemap");
    }

    private void PreventGroundTunneling(ref SpawnParticle particle)
    {
        if (particle.Body == null || particle.Collider == null)
        {
            return;
        }

        Vector2 currentPosition = particle.Body.position;
        Vector2 previousPosition = particle.PreviousPosition == Vector3.zero
            ? currentPosition
            : (Vector2)particle.PreviousPosition;
        Vector2 movement = currentPosition - previousPosition;
        particle.PreviousPosition = currentPosition;

        if (movement.y >= -0.001f)
        {
            return;
        }

        Bounds bounds = particle.Collider.bounds;
        Vector2 castSize = new Vector2(
            Mathf.Max(0.04f, bounds.size.x * 0.92f),
            Mathf.Max(0.04f, bounds.size.y * 0.92f));
        float castDistance = movement.magnitude + Mathf.Max(bounds.extents.y, 0.04f) + 0.03f;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(previousPosition, castSize, 0f, movement.normalized, castDistance, groundMask);

        float bestY = float.NegativeInfinity;
        bool foundGround = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null
                || hitCollider == particle.Collider
                || hitCollider.transform == particle.Collider.transform
                || hitCollider.name.Contains("SpawnPixel")
                || !IsValidSceneGroundCollider(hitCollider))
            {
                continue;
            }

            if (hits[i].normal.y < 0.15f)
            {
                continue;
            }

            if (hits[i].point.y > bestY)
            {
                bestY = hits[i].point.y;
                foundGround = true;
            }
        }

        if (!foundGround)
        {
            return;
        }

        float halfHeight = Mathf.Max(bounds.extents.y, 0.03f);
        Vector2 snappedPosition = new Vector2(currentPosition.x, bestY + halfHeight + 0.012f);
        particle.Body.position = snappedPosition;
        particle.Body.velocity = new Vector2(particle.Body.velocity.x * 0.45f, 0f);
        particle.Body.angularVelocity = 0f;
        particle.Body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        particle.PreviousPosition = snappedPosition;
    }

    private static void StopParticleSpinOnGround(Rigidbody2D particleBody)
    {
        if (particleBody == null)
        {
            return;
        }

        if (Mathf.Abs(particleBody.velocity.y) > 0.05f)
        {
            return;
        }

        particleBody.angularVelocity = Mathf.Lerp(particleBody.angularVelocity, 0f, 0.35f);
        if (Mathf.Abs(particleBody.angularVelocity) < 8f)
        {
            particleBody.angularVelocity = 0f;
            particleBody.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }
    }

    private static void ForceSceneCollisions(Collider2D particleCollider)
    {
        if (particleCollider == null)
        {
            return;
        }

        Collider2D[] sceneColliders = FindObjectsOfType<Collider2D>();
        for (int i = 0; i < sceneColliders.Length; i++)
        {
            Collider2D sceneCollider = sceneColliders[i];
            if (sceneCollider == null
                || sceneCollider == particleCollider
                || sceneCollider.isTrigger
                || sceneCollider.GetComponentInParent<PlayerController2D>() != null
                || sceneCollider.GetComponentInParent<PlayerSpawnAssembleEffect>() != null)
            {
                continue;
            }

            Physics2D.IgnoreCollision(particleCollider, sceneCollider, false);
        }
    }

    private static void FreezeParticlesForGather(SpawnParticle[] particles)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].Renderer == null)
            {
                continue;
            }

            particles[i].GatherStartPosition = particles[i].Renderer.transform.position;

            if (particles[i].Body != null)
            {
                particles[i].Body.velocity = Vector2.zero;
                particles[i].Body.angularVelocity = 0f;
                particles[i].Body.simulated = false;
            }

            if (particles[i].Collider != null)
            {
                particles[i].Collider.enabled = false;
            }
        }
    }

    private float GetParticleGatherT(float gatherT, int index, int total)
    {
        if (total <= 1)
        {
            return Smooth01(gatherT);
        }

        float sequencePortion = Mathf.Clamp01(assembleSequencePortion);
        float movePortion = Mathf.Max(0.05f, 1f - sequencePortion);
        float startT = sequencePortion * index / (total - 1f);
        return Smooth01((gatherT - startT) / movePortion);
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void ShakeCamera()
    {
        ShakeCamera(assembleShakeDuration, assembleShakeStrength);
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

    private IEnumerator PlayFinalAssemblySnapEffect(Vector3 targetPosition)
    {
        Vector3 originalScale = transform.localScale;
        float duration = Mathf.Max(0.01f, finalAssemblyPopDuration);
        float elapsed = 0f;
        bool firstClickPlayed = false;
        bool secondClickPlayed = false;
        bool finalClickPlayed = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = originalScale * GetAssemblyPopScale(t);

            if (!firstClickPlayed && t >= 0.18f)
            {
                ShakeCamera(assembleShakeDuration * 0.32f, assembleShakeStrength * 0.7f);
                firstClickPlayed = true;
            }
            if (!secondClickPlayed && t >= 0.46f)
            {
                ShakeCamera(assembleShakeDuration * 0.36f, assembleShakeStrength * 1.05f);
                secondClickPlayed = true;
            }
            if (!finalClickPlayed && t >= 0.78f)
            {
                ShakeCamera(assembleShakeDuration * 0.42f, assembleShakeStrength * 1.35f);
                finalClickPlayed = true;
            }

            yield return null;
        }

        transform.localScale = originalScale;
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

    private void ApplyRuntimeDefaults()
    {
        assembleShakeDuration = 0.16f;
        assembleShakeStrength = 0.055f;
        finalAssemblyPopDuration = 0.26f;
        finalAssemblyOvershoot = 1.14f;
    }

    private Vector3 FindGroundPosition(Vector3 targetPosition, Bounds playerBounds, int index, int total)
    {
        float normalized = total <= 1 ? 0.5f : index / (float)(total - 1);
        float xOffset = Mathf.Lerp(-playerBounds.extents.x * 0.65f, playerBounds.extents.x * 0.65f, normalized);
        xOffset += Random.Range(-0.06f, 0.06f);
        Vector3 origin = targetPosition + new Vector3(xOffset, playerBounds.extents.y + 1.8f, 0f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 8f, groundMask);
        if (hit.collider != null && !hit.collider.isTrigger)
        {
            return new Vector3(origin.x, hit.point.y + 0.045f, targetPosition.z);
        }

        return new Vector3(origin.x, targetPosition.y - playerBounds.extents.y + 0.045f, targetPosition.z);
    }

    private Vector3 GetCameraTopSpawnPosition(Vector3 targetPosition, Vector3 groundPosition)
    {
        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic)
        {
            return targetPosition + new Vector3(
                Random.Range(-startSpread.x, startSpread.x),
                Random.Range(4.5f, 7.2f),
                0f);
        }

        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        Vector3 cameraPosition = camera.transform.position;
        float x = Mathf.Clamp(
            groundPosition.x + Random.Range(-startSpread.x, startSpread.x),
            cameraPosition.x - halfWidth + 0.5f,
            cameraPosition.x + halfWidth - 0.5f);
        float y = cameraPosition.y + halfHeight + Random.Range(0.35f, 1.6f);
        return new Vector3(x, y, targetPosition.z);
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
        pixelSprite.name = "Runtime_SpawnPixelSprite";
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
            name = "Runtime_SpawnPixelUnlit"
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

    private static PhysicsMaterial2D GetPixelPhysicsMaterial()
    {
        if (pixelPhysicsMaterial != null)
        {
            return pixelPhysicsMaterial;
        }

        pixelPhysicsMaterial = new PhysicsMaterial2D("Runtime_SpawnPixelPhysics")
        {
            friction = 0.42f,
            bounciness = 0.18f
        };
        return pixelPhysicsMaterial;
    }

    private struct SpawnParticle
    {
        public SpriteRenderer Renderer;
        public Rigidbody2D Body;
        public Collider2D Collider;
        public Vector3 PreviousPosition;
        public Vector3 GatherStartPosition;
        public Vector3 SettleOffset;
    }
}
