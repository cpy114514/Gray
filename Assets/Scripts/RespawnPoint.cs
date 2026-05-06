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
        ApplyVisualColor();
    }

    private void OnValidate()
    {
        ApplyVisualColor();
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
        ApplyVisualColor();
    }

    public void ApplyTo(PlayerController2D player)
    {
        player.SetSpawn(transform.position, respawnColor);
    }

    private void ApplyVisualColor()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.color = respawnColor == PlayerColorState.White
            ? new Color(0.85f, 0.95f, 1f)
            : new Color(0.12f, 0.14f, 0.18f);
    }
}
