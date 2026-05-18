using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class LevelCompleteReveal2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private CameraFollow2D cameraFollow;
    [SerializeField] private MiniMapController2D miniMap;
    [SerializeField] private Tilemap whiteTilemap;

    [Header("Reveal")]
    [SerializeField] private bool playOnLevelComplete = true;
    [SerializeField] private float revealDuration = 1.15f;
    [SerializeField] private float padding = 2.5f;
    [SerializeField] private float minOrthographicSize = 7f;
    [SerializeField] private float maxOrthographicSize = 80f;
    [SerializeField] private float holdBeforeWinPanel = 0.15f;
    [SerializeField] private string whiteTilemapName = "WhiteTilemap";

    [Header("Player")]
    [SerializeField] private bool disablePlayerControl = true;

    private Coroutine revealRoutine;

    public bool PlayReveal(Action onComplete)
    {
        if (!playOnLevelComplete)
        {
            return false;
        }

        CacheReferences();

        if (targetCamera == null || whiteTilemap == null)
        {
            return false;
        }

        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
        }

        revealRoutine = StartCoroutine(PlayRevealRoutine(onComplete));
        return true;
    }

    private IEnumerator PlayRevealRoutine(Action onComplete)
    {
        HideMiniMap();
        DisablePlayerControl();

        if (cameraFollow != null)
        {
            cameraFollow.enabled = false;
        }

        Bounds targetBounds = CalculateWhiteTilemapBounds();
        Vector3 startPosition = targetCamera.transform.position;
        float startSize = targetCamera.orthographicSize;

        Vector3 targetPosition = targetBounds.center;
        targetPosition.z = startPosition.z;

        float targetSize = CalculateOrthographicSize(targetBounds);
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, revealDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / duration);

            targetCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            targetCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
            yield return null;
        }

        targetCamera.transform.position = targetPosition;
        targetCamera.orthographicSize = targetSize;

        if (holdBeforeWinPanel > 0f)
        {
            yield return new WaitForSecondsRealtime(holdBeforeWinPanel);
        }

        revealRoutine = null;
        onComplete?.Invoke();
    }

    private void CacheReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null && cameraFollow == null)
        {
            cameraFollow = targetCamera.GetComponent<CameraFollow2D>();
        }

        if (miniMap == null)
        {
            miniMap = FindObjectOfType<MiniMapController2D>(true);
        }

        if (whiteTilemap == null)
        {
            whiteTilemap = FindWhiteTilemap();
        }
    }

    private Tilemap FindWhiteTilemap()
    {
        Tilemap[] tilemaps = FindObjectsOfType<Tilemap>(true);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap candidate = tilemaps[i];
            if (candidate != null && candidate.name == whiteTilemapName)
            {
                return candidate;
            }
        }

        return null;
    }

    private Bounds CalculateWhiteTilemapBounds()
    {
        BoundsInt cellBounds = whiteTilemap.cellBounds;
        bool hasTile = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int x = cellBounds.xMin; x < cellBounds.xMax; x++)
        {
            for (int y = cellBounds.yMin; y < cellBounds.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (!whiteTilemap.HasTile(cell))
                {
                    continue;
                }

                Vector3 worldMin = whiteTilemap.CellToWorld(cell);
                Vector3 worldMax = whiteTilemap.CellToWorld(cell + new Vector3Int(1, 1, 0));

                if (!hasTile)
                {
                    min = worldMin;
                    max = worldMax;
                    hasTile = true;
                    continue;
                }

                min = Vector3.Min(min, worldMin);
                max = Vector3.Max(max, worldMax);
            }
        }

        if (!hasTile)
        {
            return new Bounds(whiteTilemap.transform.position, Vector3.one);
        }

        Bounds bounds = new Bounds();
        bounds.SetMinMax(min, max);
        return bounds;
    }

    private float CalculateOrthographicSize(Bounds bounds)
    {
        float aspect = targetCamera != null ? targetCamera.aspect : 16f / 9f;
        float sizeForHeight = bounds.extents.y + padding;
        float sizeForWidth = bounds.extents.x / Mathf.Max(0.01f, aspect) + padding;
        return Mathf.Clamp(Mathf.Max(sizeForHeight, sizeForWidth), minOrthographicSize, maxOrthographicSize);
    }

    private void HideMiniMap()
    {
        if (miniMap != null)
        {
            miniMap.gameObject.SetActive(false);
            return;
        }

        GameObject miniMapObject = GameObject.Find("MiniMap");
        if (miniMapObject != null)
        {
            miniMapObject.SetActive(false);
        }
    }

    private void DisablePlayerControl()
    {
        if (!disablePlayerControl)
        {
            return;
        }

        PlayerController2D player = FindObjectOfType<PlayerController2D>();
        if (player != null)
        {
            player.SetControlEnabled(false);
        }
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
