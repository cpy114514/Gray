using UnityEngine;

[RequireComponent(typeof(PlayerController2D))]
public class PlayerJumpParticleEffect : MonoBehaviour
{
    private static Material runtimeFallbackMaterial;

    [SerializeField] private ParticleSystem jumpParticles;
    [SerializeField] private Material particleMaterial;
    [SerializeField] private Color particleColor = new Color(0.85f, 0.85f, 0.85f, 0.75f);
    [SerializeField] private int groundJumpCount = 6;
    [SerializeField] private int airJumpCount = 10;
    [SerializeField] private int wallJumpCount = 12;

    private PlayerController2D player;
    private Collider2D sourceCollider;
    private bool subscribed;
    private ParticleSystem.Particle[] liveParticleBuffer;

    private void Awake()
    {
        Initialize(particleMaterial);
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        if (player != null && subscribed)
        {
            player.Jumped -= OnPlayerJumped;
        }

        subscribed = false;
    }

    public void Initialize(Material newParticleMaterial)
    {
        particleMaterial = newParticleMaterial != null ? newParticleMaterial : particleMaterial;
        player = GetComponent<PlayerController2D>();
        sourceCollider = GetComponent<Collider2D>();
        EnsureParticles();
        Subscribe();
    }

    private void Update()
    {
        ParticleColorUtility.RefreshParticleSystemByPlayerColor(jumpParticles, ref liveParticleBuffer, player);
    }

    private void Subscribe()
    {
        if (player == null || subscribed)
        {
            return;
        }

        player.Jumped += OnPlayerJumped;
        subscribed = true;
    }

    private void OnPlayerJumped(PlayerJumpType jumpType, Vector2 direction)
    {
        switch (jumpType)
        {
            case PlayerJumpType.Ground:
                EmitBodyBurst(groundJumpCount, Vector2.down * 1.2f);
                break;
            case PlayerJumpType.Air:
                EmitSideBurst(airJumpCount);
                break;
            case PlayerJumpType.Wall:
                EmitWallBurst(direction.x);
                break;
        }
    }

    private void EmitBodyBurst(int count, Vector2 baseVelocity)
    {
        if (jumpParticles == null || count <= 0)
        {
            return;
        }

        Vector3 origin = GetBodyPosition();
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = origin + new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(-0.18f, 0.18f), 0f),
                velocity = baseVelocity + Random.insideUnitCircle * Random.Range(0.35f, 1.2f),
                startColor = ParticleColorUtility.PlayerColor(player, particleColor, particleColor.a),
                startLifetime = Random.Range(0.18f, 0.32f),
                startSize = Random.Range(0.12f, 0.26f)
            };

            jumpParticles.Emit(emitParams, 1);
        }
    }

    private void EmitWallBurst(float jumpDirection)
    {
        if (jumpParticles == null || wallJumpCount <= 0)
        {
            return;
        }

        float wallSide = -Mathf.Sign(jumpDirection);
        Vector3 origin = transform.position + new Vector3(wallSide * 0.38f, -0.05f, 0f);
        for (int i = 0; i < wallJumpCount; i++)
        {
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = origin + new Vector3(0f, Random.Range(-0.45f, 0.45f), 0f),
                velocity = new Vector2(jumpDirection * Random.Range(1.4f, 2.8f), Random.Range(0.2f, 1.7f)),
                startColor = ParticleColorUtility.PlayerColor(player, particleColor, particleColor.a),
                startLifetime = Random.Range(0.2f, 0.34f),
                startSize = Random.Range(0.14f, 0.3f)
            };

            jumpParticles.Emit(emitParams, 1);
        }
    }

    private void EmitSideBurst(int count)
    {
        if (jumpParticles == null || count <= 0)
        {
            return;
        }

        Vector3 origin = GetBodyPosition();
        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = origin + new Vector3(side * Random.Range(0.04f, 0.18f), Random.Range(-0.08f, 0.08f), 0f),
                velocity = new Vector2(side * Random.Range(1.2f, 2.5f), Random.Range(-0.15f, 0.45f)),
                startColor = ParticleColorUtility.PlayerColor(player, particleColor, particleColor.a),
                startLifetime = Random.Range(0.18f, 0.32f),
                startSize = Random.Range(0.12f, 0.26f)
            };

            jumpParticles.Emit(emitParams, 1);
        }
    }

    private Vector3 GetBodyPosition()
    {
        if (sourceCollider != null)
        {
            Bounds bounds = sourceCollider.bounds;
            return new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
        }

        return transform.position;
    }

    private void EnsureParticles()
    {
        if (jumpParticles == null)
        {
            Transform existing = transform.Find("JumpParticles");
            if (existing != null)
            {
                jumpParticles = existing.GetComponent<ParticleSystem>();
            }
        }

        if (jumpParticles == null)
        {
            GameObject particleObject = new GameObject("JumpParticles");
            particleObject.transform.SetParent(transform, false);
            jumpParticles = particleObject.AddComponent<ParticleSystem>();
        }

        jumpParticles.gameObject.SetActive(true);

        ParticleSystem.MainModule main = jumpParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = jumpParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = jumpParticles.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = jumpParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 6;
            renderer.sharedMaterial = particleMaterial != null ? particleMaterial : GetRuntimeFallbackMaterial();
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
            name = "RuntimeJumpParticleMaterial"
        };

        return runtimeFallbackMaterial;
    }
}
