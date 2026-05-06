using UnityEngine;

[RequireComponent(typeof(PlayerController2D))]
public class PlayerMovementEffects : MonoBehaviour
{
    private static Material runtimeFallbackMaterial;

    [SerializeField] private ParticleSystem dustParticles;
    [SerializeField] private TrailRenderer movementTrail;
    [SerializeField] private Material particleMaterial;
    [SerializeField] private Material trailMaterial;
    [SerializeField] private Color dustColor = new Color(0.85f, 0.85f, 0.85f, 0.75f);
    [SerializeField] private float runDustInterval = 0.08f;
    [SerializeField] private int landBurstCount = 4;
    [SerializeField] private int jumpBurstCount = 6;
    [SerializeField] private float feetOffset = 0.72f;

    private PlayerController2D player;
    private bool wasGrounded;
    private float dustTimer;

    private void Awake()
    {
        player = GetComponent<PlayerController2D>();
        EnsureDustParticles();
        EnsureMovementTrail();
        ApplyRendererMaterials();
        wasGrounded = player.IsGrounded;
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
        }
        else
        {
            dustTimer = 0f;
        }

        if (!wasGrounded && grounded)
        {
            EmitDust(landBurstCount, Vector2.up * 1.4f);
        }
        else if (wasGrounded && !grounded && player.Velocity.y > 0.1f)
        {
            EmitDust(jumpBurstCount, Vector2.down * 1.2f);
        }

        wasGrounded = grounded;
        UpdateTrailColor();
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

    private Color GetStateDustColor()
    {
        if (player.CurrentColor == PlayerColorState.White)
        {
            return dustColor;
        }

        return new Color(0.12f, 0.12f, 0.12f, 0.65f);
    }
}
