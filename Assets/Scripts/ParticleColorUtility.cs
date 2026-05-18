using UnityEngine;
using UnityEngine.Tilemaps;

public static class ParticleColorUtility
{
    private const float TilemapCacheDuration = 0.15f;
    private static Tilemap[] tilemapCache;
    private static float tilemapCacheTime;

    public static Color PlayerColor(PlayerController2D player, Color fallback, float alpha = -1f)
    {
        Color color = player != null ? player.VisualColor : fallback;
        return ApplyAlpha(color, alpha);
    }

    public static Color WorldColor(PlayerController2D player, Vector3 worldPosition, Color fallback, float alpha = -1f)
    {
        if (player == null)
        {
            return ApplyAlpha(fallback, alpha);
        }

        PlatformColorType backgroundColor = SampleBackgroundColor(worldPosition);
        Color color = player.GetVisualColorForBackgroundColor(backgroundColor);
        return ApplyAlpha(color, alpha);
    }

    public static void RefreshParticleSystemByPlayerColor(ParticleSystem particles, ref ParticleSystem.Particle[] buffer, PlayerController2D player)
    {
        RefreshParticleSystem(particles, ref buffer, player, useWorldPosition: false);
    }

    public static void RefreshParticleSystemByWorldColor(ParticleSystem particles, ref ParticleSystem.Particle[] buffer, PlayerController2D player)
    {
        RefreshParticleSystem(particles, ref buffer, player, useWorldPosition: true);
    }

    private static void RefreshParticleSystem(ParticleSystem particles, ref ParticleSystem.Particle[] buffer, PlayerController2D player, bool useWorldPosition)
    {
        if (particles == null || player == null)
        {
            return;
        }

        int maxParticles = Mathf.Max(1, particles.main.maxParticles);
        if (buffer == null || buffer.Length < maxParticles)
        {
            buffer = new ParticleSystem.Particle[maxParticles];
        }

        int count = particles.GetParticles(buffer);
        for (int i = 0; i < count; i++)
        {
            float alpha = buffer[i].startColor.a / 255f;
            buffer[i].startColor = useWorldPosition
                ? WorldColor(player, buffer[i].position, Color.white, alpha)
                : PlayerColor(player, Color.white, alpha);
        }

        if (count > 0)
        {
            particles.SetParticles(buffer, count);
        }
    }

    private static PlatformColorType SampleBackgroundColor(Vector3 worldPosition)
    {
        if (ColorPlatform.TryGetExplicitColorAtWorldPosition(worldPosition, out PlatformColorType explicitColor))
        {
            return explicitColor;
        }

        Tilemap[] tilemaps = GetTilemaps();
        for (int i = tilemaps.Length - 1; i >= 0; i--)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap == null || !tilemap.HasTile(tilemap.WorldToCell(worldPosition)))
            {
                continue;
            }

            string tilemapName = tilemap.name.ToLowerInvariant();
            if (tilemapName.Contains("white"))
            {
                return PlatformColorType.White;
            }

            if (tilemapName.Contains("black"))
            {
                return PlatformColorType.Black;
            }

            Color tilemapColor = tilemap.color;
            float luminance = tilemapColor.r * 0.299f + tilemapColor.g * 0.587f + tilemapColor.b * 0.114f;
            return luminance >= 0.5f ? PlatformColorType.White : PlatformColorType.Black;
        }

        return PlatformColorType.Black;
    }

    private static Tilemap[] GetTilemaps()
    {
        if (tilemapCache == null || Time.realtimeSinceStartup - tilemapCacheTime > TilemapCacheDuration)
        {
            tilemapCache = Object.FindObjectsOfType<Tilemap>(true);
            tilemapCacheTime = Time.realtimeSinceStartup;
        }

        return tilemapCache;
    }

    private static Color ApplyAlpha(Color color, float alpha)
    {
        if (alpha >= 0f)
        {
            color.a = alpha;
        }

        return color;
    }
}
