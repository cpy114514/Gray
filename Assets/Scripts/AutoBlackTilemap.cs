using UnityEngine;
using UnityEngine.Tilemaps;
using System;

[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
public class AutoBlackTilemap : MonoBehaviour
{
    public static event Action BlackTilemapGenerated;

    [SerializeField] private Tilemap whiteTilemap;
    [SerializeField] private TileBase blackTile;
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearBeforeGenerate = true;
    [SerializeField] private Vector3Int minCell = new Vector3Int(-30, -15, 0);
    [SerializeField] private Vector3Int size = new Vector3Int(80, 35, 1);

    private Tilemap blackTilemap;

    private void Awake()
    {
        blackTilemap = GetComponent<Tilemap>();
    }

    private void Start()
    {
        if (Application.isPlaying && generateOnStart)
        {
            Generate();
        }
    }

    public void Configure(Tilemap sourceWhiteTilemap, TileBase sourceBlackTile)
    {
        whiteTilemap = sourceWhiteTilemap;
        blackTile = sourceBlackTile;
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        blackTilemap = GetComponent<Tilemap>();
    }

    [ContextMenu("Generate Black Tiles")]
    public void Generate()
    {
        if (blackTilemap == null)
        {
            blackTilemap = GetComponent<Tilemap>();
        }

        if (blackTilemap == null)
        {
            return;
        }

        TileBase tileToPaint = blackTile != null ? blackTile : FindSourceTileFromWhiteTilemap();
        if (tileToPaint == null)
        {
            return;
        }

        blackTilemap.color = Color.black;

        if (clearBeforeGenerate)
        {
            blackTilemap.ClearAllTiles();
        }

        int width = Mathf.Max(0, size.x);
        int height = Mathf.Max(0, size.y);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3Int cell = new Vector3Int(minCell.x + x, minCell.y + y, minCell.z);
                if (whiteTilemap != null && whiteTilemap.HasTile(cell))
                {
                    blackTilemap.SetTile(cell, null);
                    continue;
                }

                blackTilemap.SetTile(cell, tileToPaint);
            }
        }

        blackTilemap.CompressBounds();
        BlackTilemapGenerated?.Invoke();
    }

    private TileBase FindSourceTileFromWhiteTilemap()
    {
        if (whiteTilemap == null)
        {
            return null;
        }

        BoundsInt bounds = whiteTilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            TileBase sourceTile = whiteTilemap.GetTile(cell);
            if (sourceTile != null)
            {
                return sourceTile;
            }
        }

        return null;
    }
}
