using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Tilemap))]
public class BlackWhiteBoundaryCollider2D : MonoBehaviour
{
    [SerializeField] private bool buildOnAwake = true;
    [SerializeField] private float edgeRadius = 0.01f;
    [SerializeField] private Vector2 grayDoorPadding = new Vector2(0.05f, 0.05f);
    [SerializeField] private PhysicsMaterial2D boundaryMaterial;

    private Tilemap sourceTilemap;
    private GameObject runtimeRoot;
    private Coroutine rebuildRoutine;

    private struct Segment
    {
        public Vector2 Start;
        public Vector2 End;

        public Segment(Vector2 start, Vector2 end)
        {
            Start = start;
            End = end;
        }
    }

    private void Awake()
    {
        sourceTilemap = GetComponent<Tilemap>();
        DisableSourceTilemapColliders();
    }

    private void OnEnable()
    {
        AutoBlackTilemap.BlackTilemapGenerated += HandleBlackTilemapGenerated;

        if (buildOnAwake)
        {
            RebuildDelayed();
        }
    }

    private void OnDisable()
    {
        AutoBlackTilemap.BlackTilemapGenerated -= HandleBlackTilemapGenerated;
        if (rebuildRoutine != null)
        {
            StopCoroutine(rebuildRoutine);
            rebuildRoutine = null;
        }
    }

    private void OnDestroy()
    {
        ClearRuntimeColliders();
    }

    [ContextMenu("Build Runtime Boundary Colliders")]
    public void Build()
    {
        if (sourceTilemap == null)
        {
            sourceTilemap = GetComponent<Tilemap>();
        }

        if (sourceTilemap == null)
        {
            return;
        }

        ClearRuntimeColliders();

        runtimeRoot = new GameObject($"{name}_RuntimeBoundaryColliders");
        runtimeRoot.transform.position = Vector3.zero;
        runtimeRoot.transform.rotation = Quaternion.identity;
        runtimeRoot.transform.localScale = Vector3.one;
        runtimeRoot.layer = gameObject.layer;

        Rigidbody2D body = runtimeRoot.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;

        List<Bounds> grayDoorBounds = GetGrayDoorBounds();
        List<Segment> segments = BuildBoundarySegments(grayDoorBounds);

        for (int i = 0; i < segments.Count; i++)
        {
            Segment segment = segments[i];
            GameObject segmentObject = new GameObject($"Boundary_{i}");
            segmentObject.transform.SetParent(runtimeRoot.transform, false);
            segmentObject.layer = gameObject.layer;

            EdgeCollider2D edge = segmentObject.AddComponent<EdgeCollider2D>();
            edge.edgeRadius = edgeRadius;
            edge.sharedMaterial = boundaryMaterial;
            edge.points = new[] { segment.Start, segment.End };
        }
    }

    private List<Segment> BuildBoundarySegments(List<Bounds> grayDoorBounds)
    {
        List<Segment> segments = new List<Segment>();
        BoundsInt bounds = sourceTilemap.cellBounds;

        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (!sourceTilemap.HasTile(cell))
            {
                continue;
            }

            Vector2 bl = sourceTilemap.CellToWorld(cell);
            Vector2 br = sourceTilemap.CellToWorld(cell + Vector3Int.right);
            Vector2 tl = sourceTilemap.CellToWorld(cell + Vector3Int.up);
            Vector2 tr = sourceTilemap.CellToWorld(cell + Vector3Int.right + Vector3Int.up);

            AddSegmentIfBoundary(segments, grayDoorBounds, cell, Vector3Int.up, tl, tr);
            AddSegmentIfBoundary(segments, grayDoorBounds, cell, Vector3Int.down, bl, br);
            AddSegmentIfBoundary(segments, grayDoorBounds, cell, Vector3Int.left, bl, tl);
            AddSegmentIfBoundary(segments, grayDoorBounds, cell, Vector3Int.right, br, tr);
        }

        return segments;
    }

    private void HandleBlackTilemapGenerated()
    {
        if (isActiveAndEnabled && buildOnAwake)
        {
            RebuildDelayed();
        }
    }

    private void RebuildDelayed()
    {
        if (!Application.isPlaying)
        {
            Build();
            return;
        }

        if (rebuildRoutine != null)
        {
            StopCoroutine(rebuildRoutine);
        }

        rebuildRoutine = StartCoroutine(RebuildAfterTilemapsUpdate());
    }

    private IEnumerator RebuildAfterTilemapsUpdate()
    {
        yield return null;
        Build();
        rebuildRoutine = null;
    }

    private void AddSegmentIfBoundary(
        List<Segment> segments,
        List<Bounds> grayDoorBounds,
        Vector3Int cell,
        Vector3Int neighborDirection,
        Vector2 start,
        Vector2 end)
    {
        if (sourceTilemap.HasTile(cell + neighborDirection))
        {
            return;
        }

        Vector2 midpoint = (start + end) * 0.5f;
        if (IsInsideGrayDoor(midpoint, grayDoorBounds))
        {
            return;
        }

        segments.Add(new Segment(start, end));
    }

    private List<Bounds> GetGrayDoorBounds()
    {
        List<Bounds> bounds = new List<Bounds>();
        GrayDoor[] doors = FindObjectsOfType<GrayDoor>();
        foreach (GrayDoor door in doors)
        {
            if (door == null)
            {
                continue;
            }

            Collider2D doorCollider = door.GetComponent<Collider2D>();
            if (doorCollider == null)
            {
                continue;
            }

            Bounds expandedBounds = doorCollider.bounds;
            expandedBounds.Expand(new Vector3(grayDoorPadding.x, grayDoorPadding.y, 0f));
            bounds.Add(expandedBounds);
        }

        return bounds;
    }

    private static bool IsInsideGrayDoor(Vector2 point, List<Bounds> grayDoorBounds)
    {
        for (int i = 0; i < grayDoorBounds.Count; i++)
        {
            if (grayDoorBounds[i].Contains(point))
            {
                return true;
            }
        }

        return false;
    }

    private void DisableSourceTilemapColliders()
    {
        TilemapCollider2D tilemapCollider = GetComponent<TilemapCollider2D>();
        if (tilemapCollider != null)
        {
            tilemapCollider.enabled = false;
        }

        CompositeCollider2D compositeCollider = GetComponent<CompositeCollider2D>();
        if (compositeCollider != null)
        {
            compositeCollider.enabled = false;
        }
    }

    private void ClearRuntimeColliders()
    {
        if (runtimeRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeRoot);
        }
        else
        {
            DestroyImmediate(runtimeRoot);
        }

        runtimeRoot = null;
    }
}
