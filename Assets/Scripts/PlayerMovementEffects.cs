using UnityEngine;

[RequireComponent(typeof(PlayerController2D))]
public class PlayerMovementEffects : MonoBehaviour
{
    [Header("Install")]
    [SerializeField] private bool installGroundDust = true;
    [SerializeField] private bool installJumpParticles = true;
    [SerializeField] private bool installAfterimages = true;

    [Header("Shared Materials")]
    [SerializeField] private Material particleMaterial;

    private PlayerGroundDustEffect groundDust;
    private PlayerJumpParticleEffect jumpParticles;
    private PlayerAfterimageEffect afterimages;

    private void Awake()
    {
        InitializeRuntimeEffects();
    }

    private void OnEnable()
    {
        InitializeRuntimeEffects();
    }

    public void InitializeRuntimeEffects()
    {
        if (installGroundDust)
        {
            groundDust = EnsureEffect(groundDust);
            groundDust.Initialize(particleMaterial);
        }

        if (installJumpParticles)
        {
            jumpParticles = EnsureEffect(jumpParticles);
            jumpParticles.Initialize(particleMaterial);
        }

        if (installAfterimages)
        {
            afterimages = EnsureEffect(afterimages);
            afterimages.Initialize();
        }

        DisableLegacyTrail();
    }

    public void SetEffectMaterials(Material newParticleMaterial, Material newTrailMaterial)
    {
        particleMaterial = newParticleMaterial;
        InitializeRuntimeEffects();
    }

    private T EnsureEffect<T>(T cachedEffect) where T : Behaviour
    {
        if (cachedEffect == null)
        {
            cachedEffect = GetComponent<T>();
        }

        if (cachedEffect == null)
        {
            cachedEffect = gameObject.AddComponent<T>();
        }

        cachedEffect.enabled = true;
        return cachedEffect;
    }

    private void DisableLegacyTrail()
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
}
