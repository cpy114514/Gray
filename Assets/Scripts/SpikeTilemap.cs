using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
[RequireComponent(typeof(TilemapCollider2D))]
public class SpikeTilemap : MonoBehaviour
{
    [Header("Collision")]
    [SerializeField] private bool killOnTouch = true;

    [Header("Visuals")]
    [SerializeField] private Color whiteSpikeColor = Color.white;
    [SerializeField] private Color blackSpikeColor = Color.black;

    [Header("Refresh")]
    [SerializeField] private bool autoRefreshInPlayMode = true;
    [SerializeField] private bool autoRefreshInEditMode = true;
    [SerializeField] private float refreshInterval = 0.2f;
    [SerializeField] private bool useSceneColorPlatforms = true;
    [SerializeField] private Tilemap whiteTilemap;
    [SerializeField] private Tilemap blackTilemap;
    [SerializeField] private bool autoFindBlackTilemap = true;
    [SerializeField] private bool autoFindWhiteTilemap = true;

    private Tilemap tilemap;
    private TilemapCollider2D tilemapCollider;
    private CompositeCollider2D compositeCollider;
    private Rigidbody2D body;
    private float nextRefreshTime;

    private void Awake()
    {
        CacheComponents();
        ConfigureCollider();
        RefreshTileColors();
    }

    private void OnEnable()
    {
        AutoBlackTilemap.BlackTilemapGenerated += HandleBlackTilemapGenerated;
        CacheComponents();
        ConfigureCollider();
        RefreshTileColors();
    }

    private void OnDisable()
    {
        AutoBlackTilemap.BlackTilemapGenerated -= HandleBlackTilemapGenerated;
    }

    private void Start()
    {
        RefreshTileColors();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            if (!autoRefreshInPlayMode)
            {
                return;
            }
        }
        else if (!autoRefreshInEditMode)
        {
            return;
        }

        if (Time.realtimeSinceStartup < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.realtimeSinceStartup + Mathf.Max(0.05f, refreshInterval);
        RefreshTileColors();
    }

    private void OnValidate()
    {
        CacheComponents();
        ConfigureCollider();
        RefreshTileColors();
    }

    [ContextMenu("Refresh Spike Colors")]
    public void RefreshTileColors()
    {
        CacheComponents();

        if (tilemap == null)
        {
            return;
        }

        tilemap.color = Color.white;

        bool changed = false;
        BoundsInt bounds = tilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cell))
            {
                continue;
            }

            Color spikeColor = IsOnWhiteBackground(cell)
                ? blackSpikeColor
                : whiteSpikeColor;

            TileFlags currentFlags = tilemap.GetTileFlags(cell);
            TileFlags unlockedFlags = currentFlags & ~TileFlags.LockColor;
            if (currentFlags != unlockedFlags)
            {
                tilemap.SetTileFlags(cell, unlockedFlags);
                changed = true;
            }

            if (tilemap.GetColor(cell) != spikeColor)
            {
                tilemap.SetColor(cell, spikeColor);
                changed = true;
            }
        }

        if (changed)
        {
            tilemap.RefreshAllTiles();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(tilemap);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }
    }

    public bool ShouldAutoRefreshInEditMode()
    {
        return autoRefreshInEditMode && isActiveAndEnabled;
    }

    public void SetBlackTilemap(Tilemap sourceBlackTilemap)
    {
        blackTilemap = sourceBlackTilemap;
    }

    public void SetWhiteTilemap(Tilemap sourceWhiteTilemap)
    {
        whiteTilemap = sourceWhiteTilemap;
    }

    private void HandleBlackTilemapGenerated()
    {
        RefreshTileColors();
    }

    private bool IsOnWhiteBackground(Vector3Int cell)
    {
        if (TrySampleWhiteTilemap(cell))
        {
            return true;
        }

        if (!useSceneColorPlatforms)
        {
            return false;
        }

        return TrySampleBackgroundCell(cell, out PlatformColorType backgroundColor)
            && backgroundColor == PlatformColorType.White;
    }

    private bool TrySampleWhiteTilemap(Vector3Int cell)
    {
        if (whiteTilemap == null && autoFindWhiteTilemap)
        {
            whiteTilemap = FindTilemapByName("WhiteTilemap");
        }

        if (whiteTilemap == null)
        {
            return false;
        }

        Vector3 worldPosition = tilemap.GetCellCenterWorld(cell);
        Vector3Int whiteCell = whiteTilemap.WorldToCell(worldPosition);
        return whiteTilemap.HasTile(whiteCell);
    }

    private bool TrySampleBlackTilemap(Vector3Int cell)
    {
        if (blackTilemap == null && autoFindBlackTilemap)
        {
            blackTilemap = FindTilemapByName("BlackTilemap");
        }

        if (blackTilemap == null)
        {
            return false;
        }

        Vector3 worldPosition = tilemap.GetCellCenterWorld(cell);
        Vector3Int blackCell = blackTilemap.WorldToCell(worldPosition);
        return blackTilemap.HasTile(blackCell);
    }

    private bool TrySampleBackgroundCell(Vector3Int cell, out PlatformColorType backgroundColor)
    {
        Vector3 worldPosition = tilemap.GetCellCenterWorld(cell);
        return ColorPlatform.TryGetColorAtWorldPosition(worldPosition, out backgroundColor);
    }

    private Tilemap FindTilemapByName(string tilemapName)
    {
        if (transform.parent != null)
        {
            Tilemap[] siblingTilemaps = transform.parent.GetComponentsInChildren<Tilemap>(true);
            for (int i = 0; i < siblingTilemaps.Length; i++)
            {
                Tilemap candidate = siblingTilemaps[i];
                if (candidate != null && candidate != tilemap && candidate.name == tilemapName)
                {
                    return candidate;
                }
            }
        }

        Tilemap[] sceneTilemaps = FindObjectsOfType<Tilemap>(true);
        for (int i = 0; i < sceneTilemaps.Length; i++)
        {
            Tilemap candidate = sceneTilemaps[i];
            if (candidate != null && candidate != tilemap && candidate.name == tilemapName)
            {
                return candidate;
            }
        }

        return null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!killOnTouch)
        {
            return;
        }

        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player != null)
        {
            player.RespawnToSpawnWithParticles();
        }
    }

    private void CacheComponents()
    {
        if (tilemap == null)
        {
            tilemap = GetComponent<Tilemap>();
        }

        if (tilemapCollider == null)
        {
            tilemapCollider = GetComponent<TilemapCollider2D>();
        }

        if (compositeCollider == null)
        {
            compositeCollider = GetComponent<CompositeCollider2D>();
        }
    }

    private void ConfigureCollider()
    {
        if (tilemapCollider == null)
        {
            return;
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;

        tilemapCollider.usedByComposite = true;
        tilemapCollider.isTrigger = true;

        if (compositeCollider == null)
        {
            compositeCollider = GetComponent<CompositeCollider2D>();
        }

        if (compositeCollider == null)
        {
            compositeCollider = gameObject.AddComponent<CompositeCollider2D>();
        }

        compositeCollider.isTrigger = true;
    }
}
