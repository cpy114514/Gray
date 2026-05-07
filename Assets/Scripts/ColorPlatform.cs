using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum PlatformColorType
{
    White,
    Black,
    Gray
}

[RequireComponent(typeof(Collider2D))]
public class ColorPlatform : MonoBehaviour
{
    private static readonly List<ColorPlatform> Platforms = new List<ColorPlatform>();
    private static readonly Dictionary<Collider2D, ColorPlatform> PlatformByCollider = new Dictionary<Collider2D, ColorPlatform>();

    [SerializeField] private PlatformColorType platformColor = PlatformColorType.White;
    [SerializeField] private PhysicsMaterial2D surfaceMaterial;

    private Collider2D[] platformColliders;
    private Tilemap tilemap;
    private SpriteRenderer spriteRenderer;

    public PlatformColorType PlatformColor => platformColor;

    private void Awake()
    {
        ConfigureTilemapCollider();
        CacheVisualComponents();
        CacheColliders();
        ApplyVisualColor();
        ApplySurfaceMaterial();
    }

    private void OnEnable()
    {
        if (!Platforms.Contains(this))
        {
            Platforms.Add(this);
        }

        CacheVisualComponents();
        CacheColliders();
        RefreshForActivePlayers();
    }

    private void OnDisable()
    {
        Platforms.Remove(this);
        UnregisterColliders();
    }

    private void OnValidate()
    {
        CacheColliders();
        CacheVisualComponents();
        ApplyVisualColor();
        ApplySurfaceMaterial();
    }

    public bool CanPlayerCollide(PlayerController2D player)
    {
        if (player == null)
        {
            return false;
        }

        if (player.IsTouchingGrayDoor)
        {
            return platformColor == PlatformColorType.Gray;
        }

        if (player.IsInGrayDoorGroundTransition)
        {
            return true;
        }

        if (platformColor == PlatformColorType.Gray)
        {
            return true;
        }

        return (platformColor == PlatformColorType.White && player.CurrentColor == PlayerColorState.White)
            || (platformColor == PlatformColorType.Black && player.CurrentColor == PlayerColorState.Black);
    }

    public void SetPlatformColor(PlatformColorType newColor)
    {
        platformColor = newColor;
        ApplyVisualColor();
        RefreshForActivePlayers();
    }

    public void SetSurfaceMaterial(PhysicsMaterial2D newSurfaceMaterial)
    {
        surfaceMaterial = newSurfaceMaterial;
        ApplySurfaceMaterial();
    }

    public static void RefreshAllForPlayer(PlayerController2D player)
    {
        foreach (ColorPlatform platform in Platforms)
        {
            if (platform != null)
            {
                platform.RefreshForPlayer(player);
            }
        }
    }

    public static bool TryGetColorAtWorldPosition(Vector3 worldPosition, out PlatformColorType color)
    {
        for (int i = Platforms.Count - 1; i >= 0; i--)
        {
            ColorPlatform platform = Platforms[i];
            if (platform == null || platform.platformColor == PlatformColorType.Gray)
            {
                continue;
            }

            if (platform.ContainsWorldPosition(worldPosition))
            {
                color = platform.platformColor;
                return true;
            }
        }

        color = PlatformColorType.Gray;
        return false;
    }

    public static bool TryGetPlatformForCollider(Collider2D platformCollider, out ColorPlatform platform)
    {
        if (platformCollider == null)
        {
            platform = null;
            return false;
        }

        return PlatformByCollider.TryGetValue(platformCollider, out platform);
    }

    private void RefreshForActivePlayers()
    {
        IReadOnlyList<PlayerController2D> players = PlayerController2D.Players;
        for (int i = 0; i < players.Count; i++)
        {
            RefreshForPlayer(players[i]);
        }
    }

    private void RefreshForPlayer(PlayerController2D player)
    {
        if (player == null)
        {
            return;
        }

        if (platformColliders == null || platformColliders.Length == 0)
        {
            CacheColliders();
        }

        bool shouldCollide = CanPlayerCollide(player);
        foreach (Collider2D platformCollider in platformColliders)
        {
            Collider2D[] playerColliders = player.PlayerColliders;
            if (playerColliders == null)
            {
                continue;
            }

            foreach (Collider2D playerCollider in playerColliders)
            {
                if (platformCollider != null && playerCollider != null)
                {
                    Physics2D.IgnoreCollision(platformCollider, playerCollider, !shouldCollide);
                }
            }
        }
    }

    private void ApplyVisualColor()
    {
        Color color = platformColor switch
        {
            PlatformColorType.White => Color.white,
            PlatformColorType.Black => Color.black,
            _ => Color.gray
        };

        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }

        if (tilemap != null)
        {
            tilemap.color = color;
        }
    }

    private void ApplySurfaceMaterial()
    {
        CacheColliders();
        foreach (Collider2D platformCollider in platformColliders)
        {
            if (platformCollider != null)
            {
                platformCollider.sharedMaterial = surfaceMaterial;
            }
        }
    }

    private void CacheColliders()
    {
        UnregisterColliders();
        platformColliders = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D platformCollider in platformColliders)
        {
            if (platformCollider != null)
            {
                PlatformByCollider[platformCollider] = this;
            }
        }
    }

    private void UnregisterColliders()
    {
        if (platformColliders == null)
        {
            return;
        }

        foreach (Collider2D platformCollider in platformColliders)
        {
            if (platformCollider != null
                && PlatformByCollider.TryGetValue(platformCollider, out ColorPlatform platform)
                && platform == this)
            {
                PlatformByCollider.Remove(platformCollider);
            }
        }
    }

    private void CacheVisualComponents()
    {
        tilemap = GetComponent<Tilemap>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private bool ContainsWorldPosition(Vector3 worldPosition)
    {
        if (tilemap != null)
        {
            Vector3Int cell = tilemap.WorldToCell(worldPosition);
            return tilemap.HasTile(cell);
        }

        return spriteRenderer != null && spriteRenderer.bounds.Contains(worldPosition);
    }

    private void ConfigureTilemapCollider()
    {
        TilemapCollider2D tilemapCollider = GetComponent<TilemapCollider2D>();
        if (tilemapCollider == null)
        {
            return;
        }

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;

        CompositeCollider2D compositeCollider = GetComponent<CompositeCollider2D>();
        if (compositeCollider == null)
        {
            compositeCollider = gameObject.AddComponent<CompositeCollider2D>();
        }

        tilemapCollider.usedByComposite = true;

        if (surfaceMaterial != null)
        {
            compositeCollider.sharedMaterial = surfaceMaterial;
        }
    }
}
