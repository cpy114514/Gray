using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RespawnPoint : MonoBehaviour
{
    [SerializeField] private PlayerColorState respawnColor = PlayerColorState.White;

    public PlayerColorState RespawnColor => respawnColor;

    private void Awake()
    {
        Collider2D pointCollider = GetComponent<Collider2D>();
        pointCollider.isTrigger = true;
        HideVisuals();
    }

    private void OnValidate()
    {
        HideVisuals();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player != null)
        {
            ApplyTo(player);
        }
    }

    public void SetRespawnColor(PlayerColorState newColor)
    {
        respawnColor = newColor;
        HideVisuals();
    }

    public void ApplyTo(PlayerController2D player)
    {
        player.SetSpawn(transform.position, respawnColor);
    }

    private void HideVisuals()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }
    }
}
