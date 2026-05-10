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
    private static ColorPlatform[] scenePlatformCache = new ColorPlatform[0];
    private static int scenePlatformCacheFrame = -1;
    private static float scenePlatformCacheTime = -1f;
    private const float ScenePlatformCacheDuration = 0.1f;

    [SerializeField] private PlatformColorType platformColor = PlatformColorType.White;
    [SerializeField] private PhysicsMaterial2D surfaceMaterial;
    [SerializeField] private Color whiteVisualColor = Color.white;
    [SerializeField] private Color blackVisualColor = Color.black;
    [SerializeField] private Color grayVisualColor = Color.gray;
    [SerializeField] private bool boundaryCollisionMode;

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
        if (TryGetExplicitColorAtWorldPosition(worldPosition, out color))
        {
            return true;
        }

        color = PlatformColorType.Black;
        return true;
    }

    public static bool TryGetExplicitColorAtWorldPosition(Vector3 worldPosition, out PlatformColorType color)
    {
        if (TryGetColorFromRegisteredPlatforms(worldPosition, out color))
        {
            return true;
        }

        bool needsSceneScan = Platforms.Count == 0 || !Application.isPlaying;
        if (needsSceneScan && TryGetColorFromScenePlatforms(worldPosition, out color))
        {
            return true;
        }

        color = PlatformColorType.Black;
        return false;
    }

    private static bool TryGetColorFromRegisteredPlatforms(Vector3 worldPosition, out PlatformColorType color)
    {
        for (int i = Platforms.Count - 1; i >= 0; i--)
        {
            ColorPlatform platform = Platforms[i];
            if (TryGetColorFromPlatform(platform, worldPosition, out color))
            {
                return true;
            }
        }

        color = PlatformColorType.Black;
        return false;
    }

    private static bool TryGetColorFromScenePlatforms(Vector3 worldPosition, out PlatformColorType color)
    {
        ColorPlatform[] scenePlatforms = GetScenePlatformCache();
        for (int i = scenePlatforms.Length - 1; i >= 0; i--)
        {
            if (TryGetColorFromPlatform(scenePlatforms[i], worldPosition, out color))
            {
                return true;
            }
        }

        color = PlatformColorType.Black;
        return false;
    }

    private static ColorPlatform[] GetScenePlatformCache()
    {
        int currentFrame = Time.frameCount;
        float currentTime = Time.realtimeSinceStartup;
        bool cacheExpired = scenePlatformCacheFrame != currentFrame
            && currentTime - scenePlatformCacheTime >= ScenePlatformCacheDuration;

        if (scenePlatformCache == null || cacheExpired)
        {
            scenePlatformCache = FindObjectsOfType<ColorPlatform>(true);
            scenePlatformCacheFrame = currentFrame;
            scenePlatformCacheTime = currentTime;
        }

        return scenePlatformCache;
    }

    private static bool TryGetColorFromPlatform(ColorPlatform platform, Vector3 worldPosition, out PlatformColorType color)
    {
        if (platform == null || platform.platformColor == PlatformColorType.Gray)
        {
            color = PlatformColorType.Black;
            return false;
        }

        if (platform.ContainsWorldPosition(worldPosition))
        {
            color = platform.platformColor;
            return true;
        }

        color = PlatformColorType.Black;
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
            PlatformColorType.White => whiteVisualColor,
            PlatformColorType.Black => blackVisualColor,
            _ => grayVisualColor
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
