using UnityEngine;

[RequireComponent(typeof(PlayerController2D))]
public class PlayerGroundDustEffect : MonoBehaviour
{
    private static Material runtimeFallbackMaterial;

    [SerializeField] private ParticleSystem dustParticles;
    [SerializeField] private Material particleMaterial;
    [SerializeField] private Color dustColor = new Color(0.85f, 0.85f, 0.85f, 0.75f);
    [SerializeField] private float runDustInterval = 0.08f;
    [SerializeField] private int runDustCount = 2;
    [SerializeField] private int landBurstCount = 4;
    [SerializeField] private float feetOffset = 0.72f;
    [SerializeField] private float groundedVerticalSpeedLimit = 0.15f;

    private PlayerController2D player;
    private Collider2D sourceCollider;
    private bool wasGrounded;
    private float dustTimer;
    private ParticleSystem.Particle[] liveParticleBuffer;

    private void Awake()
    {
        Initialize(particleMaterial);
    }

    public void Initialize(Material newParticleMaterial)
    {
        particleMaterial = newParticleMaterial != null ? newParticleMaterial : particleMaterial;
        player = GetComponent<PlayerController2D>();
        sourceCollider = GetComponent<Collider2D>();
        EnsureDustParticles();
        wasGrounded = player != null && player.IsGrounded;
    }

    private void Update()
    {
        if (player == null)
        {
            Initialize(particleMaterial);
            if (player == null)
            {
                return;
            }
        }

        bool grounded = player.IsGrounded;
        bool canUseGroundDust = CanEmitGroundDust(grounded);
        bool running = canUseGroundDust && Mathf.Abs(player.MoveInput) > 0.1f && Mathf.Abs(player.Velocity.x) > 0.1f;

        if (running)
        {
            dustTimer -= Time.deltaTime;
            if (dustTimer <= 0f)
            {
                EmitRunDust();
                dustTimer = runDustInterval;
            }
        }
        else
        {
            dustTimer = 0f;
        }

        if (!wasGrounded && grounded)
        {
            EmitLandingDust(landBurstCount);
        }

        wasGrounded = grounded;
        ParticleColorUtility.RefreshParticleSystemByPlayerColor(dustParticles, ref liveParticleBuffer, player);
    }

    private void EmitRunDust()
    {
        if (dustParticles == null || !CanEmitGroundDust(player != null && player.IsGrounded))
        {
            return;
        }

        float direction = Mathf.Sign(player.Velocity.x);
        if (Mathf.Approximately(direction, 0f))
        {
            direction = 1f;
        }

        EmitDust(runDustCount, new Vector2(-direction * 1.4f, 1.2f), GetFeetPosition());
    }

    private void EmitLandingDust(int count)
    {
        if (dustParticles == null || count <= 0)
        {
            return;
        }

        Vector3 origin = GetFeetPosition();
        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = origin + new Vector3(side * Random.Range(0.04f, 0.18f), 0f, 0f),
                velocity = new Vector2(side * Random.Range(1.1f, 2.4f), Random.Range(0.12f, 0.45f)),
                startColor = ParticleColorUtility.PlayerColor(player, dustColor, dustColor.a),
                startLifetime = Random.Range(0.22f, 0.38f),
                startSize = Random.Range(0.16f, 0.32f)
            };

            dustParticles.Emit(emitParams, 1);
        }
    }

    private void EmitDust(int count, Vector2 baseVelocity, Vector3 origin)
    {
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = origin + new Vector3(Random.Range(-0.25f, 0.25f), 0f, 0f),
                velocity = baseVelocity + new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(0f, 0.5f)),
                startColor = ParticleColorUtility.PlayerColor(player, dustColor, dustColor.a),
                startLifetime = Random.Range(0.22f, 0.38f),
                startSize = Random.Range(0.16f, 0.32f)
            };

            dustParticles.Emit(emitParams, 1);
        }
    }

    private bool CanEmitGroundDust(bool grounded)
    {
        return grounded && player != null && Mathf.Abs(player.Velocity.y) <= groundedVerticalSpeedLimit;
    }

    private Vector3 GetFeetPosition()
    {
        if (sourceCollider != null)
        {
            Bounds bounds = sourceCollider.bounds;
            return new Vector3(bounds.center.x, bounds.min.y + 0.035f, transform.position.z);
        }

        return transform.position + Vector3.down * feetOffset;
    }

    private void EnsureDustParticles()
    {
        if (dustParticles == null)
        {
            dustParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        if (dustParticles == null)
        {
            GameObject dustObject = new GameObject("GroundDust");
            dustObject.transform.SetParent(transform, false);
            dustParticles = dustObject.AddComponent<ParticleSystem>();
        }

        dustParticles.gameObject.SetActive(true);

        ParticleSystem.MainModule main = dustParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = dustParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = dustParticles.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = dustParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 5;
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
            name = "RuntimeGroundDustMaterial"
        };

        return runtimeFallbackMaterial;
    }
}
