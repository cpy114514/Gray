using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Collider2D))]
public class GrayDoor : MonoBehaviour
{
    [SerializeField] private bool oneTimeUse;

    [Header("Message")]
    [SerializeField] private string message = "Something changes.";
    [SerializeField] private float messageTime = 3f;
    [SerializeField] private TutorialTextUI tutorialTextUI;

    [Header("Effects")]
    [SerializeField] private bool autoCreateParticles = true;
    [SerializeField] private ParticleSystem idleParticles;
    [SerializeField] private ParticleSystem burstParticles;
    [SerializeField] private Color particleColor = new Color(0.68f, 0.68f, 0.68f, 0.72f);
    [SerializeField] private int burstParticleCount = 18;

    private static Material sharedParticleMaterial;
    private bool hasTriggered;
    private readonly Dictionary<PlayerController2D, int> playerTouchCounts = new Dictionary<PlayerController2D, int>();
    private Collider2D doorCollider;

    private void Awake()
    {
        ApplyDoorSetup();
        EnsureParticles();
        if (tutorialTextUI == null)
        {
            tutorialTextUI = FindObjectOfType<TutorialTextUI>();
        }
    }

    private void OnValidate()
    {
        ApplyDoorSetup();
        ConfigureExistingParticles();
    }

    private void OnEnable()
    {
        ApplyDoorSetup();
        EnsureParticles();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        if (!playerTouchCounts.TryGetValue(player, out int touchCount))
        {
            playerTouchCounts[player] = 1;
            player.EnterGrayDoor();
            PlayBurstEffect();
            ShowMessage();
            return;
        }

        playerTouchCounts[player] = touchCount + 1;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        if (!playerTouchCounts.ContainsKey(player))
        {
            playerTouchCounts[player] = 1;
            player.EnterGrayDoor();
            PlayBurstEffect();
            ShowMessage();
        }
        else
        {
            player.StayInGrayDoor();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        if (!playerTouchCounts.TryGetValue(player, out int touchCount))
        {
            player.ExitGrayDoor();
            return;
        }

        touchCount--;
        if (touchCount > 0)
        {
            playerTouchCounts[player] = touchCount;
            return;
        }

        playerTouchCounts.Remove(player);
        player.ExitGrayDoor();
    }

    private void ShowMessage()
    {
        if (oneTimeUse && hasTriggered)
        {
            return;
        }

        hasTriggered = true;

        if (tutorialTextUI != null && !string.IsNullOrWhiteSpace(message))
        {
            tutorialTextUI.ShowText(message, messageTime);
        }
    }

    private void EnsureParticles()
    {
        if (!autoCreateParticles)
        {
            return;
        }

        if (idleParticles == null)
        {
            idleParticles = CreateParticleSystem("GrayDoor_IdleParticles");
            ConfigureIdleParticles(idleParticles);
        }

        if (burstParticles == null)
        {
            burstParticles = CreateParticleSystem("GrayDoor_BurstParticles");
            ConfigureBurstParticles(burstParticles);
        }

        ConfigureParticleRenderer(idleParticles, 3);
        ConfigureParticleRenderer(burstParticles, 4);

        if (!idleParticles.isPlaying)
        {
            idleParticles.Play();
        }
    }

    public void RebuildEffects()
    {
        autoCreateParticles = true;
        idleParticles = FindParticleSystem("GrayDoor_IdleParticles");
        burstParticles = FindParticleSystem("GrayDoor_BurstParticles");
        EnsureParticles();
    }

    private ParticleSystem CreateParticleSystem(string objectName)
    {
        Transform existing = transform.Find(objectName);
        GameObject particleObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName);

        particleObject.transform.SetParent(transform, false);
        particleObject.transform.localPosition = Vector3.zero;
        particleObject.transform.localRotation = Quaternion.identity;
        particleObject.transform.localScale = Vector3.one;

        ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = particleObject.AddComponent<ParticleSystem>();
        }

        return particles;
    }

    private ParticleSystem FindParticleSystem(string objectName)
    {
        Transform existing = transform.Find(objectName);
        return existing != null ? existing.GetComponent<ParticleSystem>() : null;
    }

    private void ConfigureExistingParticles()
    {
        if (idleParticles != null)
        {
            ConfigureIdleParticles(idleParticles);
            ConfigureParticleRenderer(idleParticles, 3);
        }

        if (burstParticles != null)
        {
            ConfigureBurstParticles(burstParticles);
            ConfigureParticleRenderer(burstParticles, 4);
        }
    }

    private void ConfigureIdleParticles(ParticleSystem particles)
    {
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.42f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
        main.startColor = particleColor;
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 14f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = GetDoorParticleBoxSize(0.9f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = CreateFadeGradient(particleColor);
    }

    private void ConfigureBurstParticles(ParticleSystem particles)
    {
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startColor = particleColor;
        main.maxParticles = 96;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = GetDoorParticleBoxSize(0.75f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = CreateFadeGradient(particleColor);
    }

    private Vector3 GetDoorParticleBoxSize(float scale)
    {
        Vector3 localScale = transform.localScale;
        float width = Mathf.Max(0.35f, Mathf.Abs(localScale.x));
        float height = Mathf.Max(1.2f, Mathf.Abs(localScale.y));
        return new Vector3(width * scale, height * scale, 0.08f);
    }

    private static ParticleSystem.MinMaxGradient CreateFadeGradient(Color color)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 0.65f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a, 0.18f),
                new GradientAlphaKey(0f, 1f)
            });

        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private void ConfigureParticleRenderer(ParticleSystem particles, int sortingOrder)
    {
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = sortingOrder;
        renderer.sharedMaterial = GetParticleMaterial();
    }

    private static Material GetParticleMaterial()
    {
        if (sharedParticleMaterial != null)
        {
            return sharedParticleMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        sharedParticleMaterial = new Material(shader)
        {
            name = "Runtime_GrayDoorParticleMaterial"
        };
        return sharedParticleMaterial;
    }

    private void PlayBurstEffect()
    {
        EnsureParticles();
        if (burstParticles == null)
        {
            return;
        }

        burstParticles.Emit(Mathf.Max(1, burstParticleCount));
    }

    private void ApplyDoorSetup()
    {
        doorCollider = GetComponent<Collider2D>();
        if (doorCollider != null)
        {
            doorCollider.isTrigger = true;
        }

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = Color.gray;
        }
    }
}
