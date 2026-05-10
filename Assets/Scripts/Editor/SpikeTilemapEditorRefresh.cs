using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SpikeTilemapEditorRefresh
{
    private const double RefreshInterval = 0.25d;
    private static double nextRefreshTime;

    static SpikeTilemapEditorRefresh()
    {
        EditorApplication.update += RefreshSpikeTilemaps;
    }

    [MenuItem("Tools/Gray/Refresh Spike Tilemap Colors")]
    private static void RefreshSpikeTilemapsFromMenu()
    {
        RefreshSpikeTilemaps(forceRefresh: true);
    }

    private static void RefreshSpikeTilemaps()
    {
        RefreshSpikeTilemaps(forceRefresh: false);
    }

    private static void RefreshSpikeTilemaps(bool forceRefresh)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        double currentTime = EditorApplication.timeSinceStartup;
        if (!forceRefresh && currentTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = currentTime + RefreshInterval;

        SpikeTilemap[] spikeTilemaps = Object.FindObjectsOfType<SpikeTilemap>();
        for (int i = 0; i < spikeTilemaps.Length; i++)
        {
            SpikeTilemap spikeTilemap = spikeTilemaps[i];
            if (spikeTilemap != null && spikeTilemap.ShouldAutoRefreshInEditMode())
            {
                spikeTilemap.RefreshTileColors();
            }
        }

        SceneView.RepaintAll();
    }
}
