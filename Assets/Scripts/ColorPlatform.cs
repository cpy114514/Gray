using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum PlatformColorType
{
    White,
    Black,
    Gray
}

[DisallowMultipleComponent]
public class ColorPlatform : MonoBehaviour
{
    private static readonly List<ColorPlatform> Platforms = new List<ColorPlatform>();
    private static readonly Dictionary<Collider2D, ColorPlatform> PlatformByCollider = new Dictionary<Collider2D, ColorPlatform>();

    [SerializeField] private PlatformColorType platformColor = PlatformColorType.White;
    [SerializeField] private PhysicsMaterial2D surfaceMaterial;
    [SerializeField] private Color whiteVisualColor = Color.white;
    [SerializeField] private Color blackVisualColor = Color.black;
    [SerializeField] private Color grayVisualColor = Color.gray;
    [SerializeField] private bool applyVisualColor = true;

    private Collider2D[] colliders;
    private Tilemap tilemap;
    private SpriteRenderer spriteRenderer;

    public PlatformColorType PlatformColor => platformColor;

    private void Awake()
    {
        Configure();
    }

    private void OnEnable()
    {
        Configure();
        if (!Platforms.Contains(this))
        {
            Platforms.Add(this);
        }
        RefreshForActivePlayers();
    }

    private void OnDisable()
    {
        Platforms.Remove(this);
        UnregisterColliders();
    }

    private void OnValidate()
    {
        CacheComponents();
        ApplyVisual();
        ApplyMaterial();
    }

    public void SetPlatformColor(PlatformColorType newColor)
    {
        platformColor = newColor;
        ApplyVisual();
        RefreshForActivePlayers();
    }

    public void SetSurfaceMaterial(PhysicsMaterial2D newSurfaceMaterial)
    {
        surfaceMaterial = newSurfaceMaterial;
        ApplyMaterial();
    }

    public bool CanPlayerCollide(PlayerController2D player)
    {
        if (player == null)
        {
            return false;
        }

        if (player.IsInGrayDoorGroundTransition)
        {
            return false;
        }

        if (platformColor == PlatformColorType.Gray)
        {
            return true;
        }

        return (platformColor == PlatformColorType.White && player.CurrentColor == PlayerColorState.White)
            || (platformColor == PlatformColorType.Black && player.CurrentColor == PlayerColorState.Black);
    }

    public static void RefreshAllForPlayer(PlayerController2D player)
    {
        for (int i = Platforms.Count - 1; i >= 0; i--)
        {
            if (Platforms[i] == null)
            {
                Platforms.RemoveAt(i);
                continue;
            }

            Platforms[i].RefreshForPlayer(player);
        }
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

    public static bool TryGetColorAtWorldPosition(Vector3 worldPosition, out PlatformColorType color)
    {
        if (TryGetExplicitColorAtWorldPosition(worldPosition, out color))
        {
            return true;
        }

        color = PlatformColorType.Black;
        return true;
    }

    public static bool TryGetExplicitColorAtWorldPosition(Vector3 worldPosition, out PlatformColorType color)
    {
        for (int i = Platforms.Count - 1; i >= 0; i--)
        {
            ColorPlatform platform = Platforms[i];
            if (platform == null)
            {
                Platforms.RemoveAt(i);
                continue;
            }

            if (platform.platformColor == PlatformColorType.Gray)
            {
                continue;
            }

            if (platform.ContainsWorldPosition(worldPosition))
            {
                color = platform.platformColor;
                return true;
            }
        }

        if (!Application.isPlaying)
        {
            ColorPlatform[] scenePlatforms = FindObjectsOfType<ColorPlatform>(true);
            for (int i = scenePlatforms.Length - 1; i >= 0; i--)
            {
                ColorPlatform platform = scenePlatforms[i];
                if (platform != null
                    && platform.platformColor != PlatformColorType.Gray
                    && platform.ContainsWorldPosition(worldPosition))
                {
                    color = platform.platformColor;
                    return true;
                }
            }
        }

        color = PlatformColorType.Black;
        return false;
    }

    private void Configure()
    {
        CacheComponents();
        ConfigureTilemapPhysics();
        RegisterColliders();
        ApplyMaterial();
        ApplyVisual();
    }

    private void CacheComponents()
    {
        tilemap = GetComponent<Tilemap>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void RegisterColliders()
    {
        UnregisterColliders();
        colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                PlatformByCollider[colliders[i]] = this;
            }
        }
    }

    private void UnregisterColliders()
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D platformCollider = colliders[i];
            if (platformCollider != null
                && PlatformByCollider.TryGetValue(platformCollider, out ColorPlatform owner)
                && owner == this)
            {
                PlatformByCollider.Remove(platformCollider);
            }
        }
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

        if (colliders == null)
        {
            RegisterColliders();
        }

        Collider2D[] playerColliders = player.PlayerColliders;
        if (playerColliders == null)
        {
            return;
        }

        bool shouldCollide = CanPlayerCollide(player);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D platformCollider = colliders[i];
            if (platformCollider == null)
            {
                continue;
            }

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider2D playerCollider = playerColliders[j];
                if (playerCollider != null && !playerCollider.isTrigger)
                {
                    Physics2D.IgnoreCollision(platformCollider, playerCollider, !shouldCollide);
                }
            }
        }
    }

    private bool ContainsWorldPosition(Vector3 worldPosition)
    {
        if (tilemap != null)
        {
            return tilemap.HasTile(tilemap.WorldToCell(worldPosition));
        }

        if (spriteRenderer != null)
        {
            return spriteRenderer.bounds.Contains(worldPosition);
        }

        if (colliders == null)
        {
            RegisterColliders();
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].OverlapPoint(worldPosition))
            {
                return true;
            }
        }

        return false;
    }

    private void ConfigureTilemapPhysics()
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

        CompositeCollider2D composite = GetComponent<CompositeCollider2D>();
        if (composite != null)
        {
            tilemapCollider.usedByComposite = true;
            composite.sharedMaterial = surfaceMaterial;
        }
    }

    private void ApplyMaterial()
    {
        if (colliders == null)
        {
            colliders = GetComponentsInChildren<Collider2D>(true);
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].sharedMaterial = surfaceMaterial;
            }
        }
    }

    private void ApplyVisual()
    {
        if (!applyVisualColor)
        {
            return;
        }

        Color color = platformColor switch
        {
            PlatformColorType.White => whiteVisualColor,
            PlatformColorType.Black => blackVisualColor,
            _ => grayVisualColor
        };

        if (tilemap != null)
        {
            tilemap.color = color;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }
}
