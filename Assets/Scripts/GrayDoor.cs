using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GrayDoor : MonoBehaviour
{
    private static Material sharedParticleMaterial;

    [Header("Message")]
    [SerializeField] private bool showMessageOnce = true;
    [SerializeField] private string message = "Something changes.";
    [SerializeField] private float messageTime = 2f;
    [SerializeField] private TutorialTextUI tutorialTextUI;

    [Header("Particles")]
    [SerializeField] private bool createParticles = true;
    [SerializeField] private ParticleSystem passParticles;
    [SerializeField] private int passParticleCount = 6;
    [SerializeField] private float emitInterval = 0.05f;
    [SerializeField] private Color fallbackParticleColor = Color.white;

    private readonly Dictionary<PlayerController2D, int> touches = new Dictionary<PlayerController2D, int>();
    private readonly Dictionary<PlayerController2D, float> nextEmitTime = new Dictionary<PlayerController2D, float>();
    private Collider2D triggerCollider;
    private bool messageShown;
    private ParticleSystem.Particle[] particleBuffer;

    private void Awake()
    {
        Configure();
    }

    private void OnEnable()
    {
        Configure();
    }

    private void OnValidate()
    {
        Configure();
    }

    private void Update()
    {
        PlayerController2D player = PlayerController2D.Players.Count > 0 ? PlayerController2D.Players[0] : null;
        ParticleColorUtility.RefreshParticleSystemByPlayerColor(passParticles, ref particleBuffer, player);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        if (!touches.TryGetValue(player, out int count))
        {
            touches[player] = 1;
            player.EnterGrayDoor();
            ShowMessage();
            Emit(player, force: true);
            return;
        }

        touches[player] = count + 1;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        if (!touches.ContainsKey(player))
        {
            touches[player] = 1;
            player.EnterGrayDoor();
        }
        else
        {
            player.StayInGrayDoor();
        }

        Emit(player, force: false);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        if (!touches.TryGetValue(player, out int count))
        {
            player.ExitGrayDoor();
            return;
        }

        count--;
        if (count > 0)
        {
            touches[player] = count;
            return;
        }

        touches.Remove(player);
        nextEmitTime.Remove(player);
        player.ExitGrayDoor();
    }

    public void RebuildEffects()
    {
        createParticles = true;
        EnsureParticles();
    }

    private void Configure()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = Color.gray;
        }

        if (tutorialTextUI == null && Application.isPlaying)
        {
            tutorialTextUI = FindObjectOfType<TutorialTextUI>();
        }

        EnsureParticles();
    }

    private void ShowMessage()
    {
        if (showMessageOnce && messageShown)
        {
            return;
        }

        messageShown = true;
        if (tutorialTextUI != null && !string.IsNullOrWhiteSpace(message))
        {
            tutorialTextUI.ShowText(message, messageTime);
        }
    }

    private void Emit(PlayerController2D player, bool force)
    {
        if (player == null || passParticles == null)
        {
            return;
        }

        float now = Time.time;
        if (!force && nextEmitTime.TryGetValue(player, out float allowedTime) && now < allowedTime)
        {
            return;
        }

        nextEmitTime[player] = now + Mathf.Max(0.01f, emitInterval);

        Vector2 velocity = player.Velocity.sqrMagnitude > 0.01f
            ? player.Velocity.normalized * 0.7f
            : Vector2.right * Mathf.Sign(player.MoveInput == 0f ? 1f : player.MoveInput);
        Color color = ParticleColorUtility.PlayerColor(player, fallbackParticleColor, 0.9f);

        for (int i = 0; i < Mathf.Max(1, passParticleCount); i++)
        {
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = player.transform.position + (Vector3)(Random.insideUnitCircle * 0.18f),
                velocity = velocity + Random.insideUnitCircle * 0.25f,
                startColor = color,
                startLifetime = Random.Range(0.12f, 0.24f),
                startSize = Random.Range(0.12f, 0.28f)
            };

            passParticles.Emit(emitParams, 1);
        }
    }

    private void EnsureParticles()
    {
        if (!createParticles)
        {
            return;
        }

        if (passParticles == null)
        {
            Transform existing = transform.Find("GrayDoorPassParticles");
            if (existing != null)
            {
                passParticles = existing.GetComponent<ParticleSystem>();
            }
        }

        if (passParticles == null)
        {
            GameObject particleObject = new GameObject("GrayDoorPassParticles");
            particleObject.transform.SetParent(transform, false);
            passParticles = particleObject.AddComponent<ParticleSystem>();
        }

        ParticleSystem.MainModule main = passParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = passParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = passParticles.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = passParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 8;
            renderer.sharedMaterial = GetParticleMaterial();
        }
    }

    private static Material GetParticleMaterial()
    {
        if (sharedParticleMaterial != null)
        {
            return sharedParticleMaterial;
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

        sharedParticleMaterial = new Material(shader)
        {
            name = "Runtime_GrayDoorPassParticleMaterial"
        };

        if (sharedParticleMaterial.HasProperty("_Color"))
        {
            sharedParticleMaterial.SetColor("_Color", Color.white);
        }
        if (sharedParticleMaterial.HasProperty("_BaseColor"))
        {
            sharedParticleMaterial.SetColor("_BaseColor", Color.white);
        }

        return sharedParticleMaterial;
    }
}
