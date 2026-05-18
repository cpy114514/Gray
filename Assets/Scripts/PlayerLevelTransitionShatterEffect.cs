using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerLevelTransitionShatterEffect : MonoBehaviour
{
    [Header("Pieces")]
    [SerializeField] private int pieceCount = 14;
    [SerializeField] private Vector2 pieceSizeRange = new Vector2(0.22f, 0.36f);
    [SerializeField] private float burstForce = 2.1f;
    [SerializeField] private float upwardBurst = 1.2f;
    [SerializeField] private float gravityScale = 3.1f;
    [SerializeField] private float fallDuration = 1.15f;

    [Header("Camera")]
    [SerializeField] private float zoomDuration = 0.45f;
    [SerializeField] private float zoomOrthographicSize = 4.2f;
    [SerializeField] private Vector2 cameraOffset = new Vector2(0f, 0.35f);
    [SerializeField] private float cameraFollowPiecesStrength = 0.65f;

    private static Sprite pixelSprite;
    private static Material pixelMaterial;

    private PlayerController2D player;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Collider2D[] playerColliders;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        CacheReferences();
        GetPixelSprite();
        GetPixelMaterial();
    }

    public bool Play(System.Action onComplete)
    {
        if (isPlaying)
        {
            return false;
        }

        CacheReferences();
        StartCoroutine(PlayRoutine(onComplete));
        return true;
    }

    private IEnumerator PlayRoutine(System.Action onComplete)
    {
        isPlaying = true;
        Time.timeScale = 1f;

        Camera mainCamera = Camera.main;
        CameraFollow2D cameraFollow = mainCamera != null ? mainCamera.GetComponent<CameraFollow2D>() : null;
        if (cameraFollow != null)
        {
            cameraFollow.enabled = false;
        }

        if (player != null)
        {
            player.SetControlEnabled(false);
        }

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.simulated = false;
        }

        SetPlayerCollidersEnabled(false);
        ClearTrails();

        Vector3 shatterPosition = transform.position;
        Bounds bounds = spriteRenderer != null ? spriteRenderer.bounds : new Bounds(shatterPosition, Vector3.one);
        yield return ZoomCameraToPlayer(mainCamera, shatterPosition);

        Color fallbackColor = player != null ? player.VisualColor : Color.white;
        ShatterPiece[] pieces = CreatePieces(shatterPosition, bounds, fallbackColor);
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, fallDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            UpdatePieceColors(pieces, fallbackColor);
            FollowPiecesWithCamera(mainCamera, pieces, shatterPosition);
            yield return null;
        }

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i].Renderer != null)
            {
                Destroy(pieces[i].Renderer.gameObject);
            }
        }

        onComplete?.Invoke();
    }

    private IEnumerator ZoomCameraToPlayer(Camera mainCamera, Vector3 playerPosition)
    {
        if (mainCamera == null)
        {
            yield break;
        }

        Vector3 startPosition = mainCamera.transform.position;
        float startSize = mainCamera.orthographicSize;
        Vector3 targetPosition = new Vector3(
            playerPosition.x + cameraOffset.x,
            playerPosition.y + cameraOffset.y,
            startPosition.z);
        float targetSize = Mathf.Max(0.5f, zoomOrthographicSize);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, zoomDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / duration);
            mainCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            mainCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
            yield return null;
        }

        mainCamera.transform.position = targetPosition;
        mainCamera.orthographicSize = targetSize;
    }

    private ShatterPiece[] CreatePieces(Vector3 center, Bounds bounds, Color fallbackColor)
    {
        ShatterPiece[] pieces = new ShatterPiece[Mathf.Max(1, pieceCount)];
        for (int i = 0; i < pieces.Length; i++)
        {
            GameObject piece = new GameObject("LevelTransitionPlayerPiece", typeof(SpriteRenderer), typeof(Rigidbody2D));
            piece.transform.position = center + new Vector3(
                Random.Range(-bounds.extents.x * 0.45f, bounds.extents.x * 0.45f),
                Random.Range(-bounds.extents.y * 0.45f, bounds.extents.y * 0.45f),
                0f);

            float size = Random.Range(pieceSizeRange.x, pieceSizeRange.y);
            piece.transform.localScale = new Vector3(size, size, 1f);

            SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
            renderer.sprite = GetPixelSprite();
            renderer.sharedMaterial = GetPixelMaterial();
            renderer.color = GetPieceColor(piece.transform.position, fallbackColor);
            if (spriteRenderer != null)
            {
                renderer.sortingLayerID = spriteRenderer.sortingLayerID;
            }
            renderer.sortingOrder = 32000;

            Rigidbody2D pieceBody = piece.GetComponent<Rigidbody2D>();
            pieceBody.gravityScale = gravityScale;
            pieceBody.mass = 0.08f;
            pieceBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            pieceBody.interpolation = RigidbodyInterpolation2D.Interpolate;

            Vector2 away = piece.transform.position - center;
            if (away.sqrMagnitude < 0.001f)
            {
                away = Random.insideUnitCircle;
            }

            Vector2 velocity = away.normalized * Random.Range(burstForce * 0.45f, burstForce);
            velocity.y += Random.Range(0.25f, upwardBurst);
            pieceBody.velocity = velocity;
            pieceBody.angularVelocity = Random.Range(-260f, 260f);

            pieces[i] = new ShatterPiece
            {
                Renderer = renderer,
                Body = pieceBody
            };
        }

        return pieces;
    }

    private void UpdatePieceColors(ShatterPiece[] pieces, Color fallbackColor)
    {
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i].Renderer == null)
            {
                continue;
            }

            pieces[i].Renderer.color = GetPieceColor(pieces[i].Renderer.transform.position, fallbackColor);
        }
    }

    private Color GetPieceColor(Vector3 worldPosition, Color fallbackColor)
    {
        return ParticleColorUtility.WorldColor(player, worldPosition, fallbackColor);
    }

    private void FollowPiecesWithCamera(Camera mainCamera, ShatterPiece[] pieces, Vector3 fallbackPosition)
    {
        if (mainCamera == null)
        {
            return;
        }

        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i].Renderer == null)
            {
                continue;
            }

            sum += pieces[i].Renderer.transform.position;
            count++;
        }

        Vector3 center = count > 0 ? sum / count : fallbackPosition;
        Vector3 target = new Vector3(center.x + cameraOffset.x, center.y + cameraOffset.y, mainCamera.transform.position.z);
        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, target, Time.deltaTime * cameraFollowPiecesStrength);
    }

    private void SetPlayerCollidersEnabled(bool enabled)
    {
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (playerColliders[i] != null)
            {
                playerColliders[i].enabled = enabled;
            }
        }
    }

    private void ClearTrails()
    {
        TrailRenderer[] trails = GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] == null)
            {
                continue;
            }

            trails[i].emitting = false;
            trails[i].enabled = false;
            trails[i].Clear();
        }
    }

    private void CacheReferences()
    {
        player = player != null ? player : GetComponent<PlayerController2D>();
        body = body != null ? body : GetComponent<Rigidbody2D>();
        spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        if (playerColliders == null || playerColliders.Length == 0)
        {
            playerColliders = GetComponentsInChildren<Collider2D>(true);
        }
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static Sprite GetPixelSprite()
    {
        if (pixelSprite != null)
        {
            return pixelSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        pixelSprite.name = "Runtime_LevelTransitionPieceSprite";
        return pixelSprite;
    }

    private static Material GetPixelMaterial()
    {
        if (pixelMaterial != null)
        {
            return pixelMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        pixelMaterial = new Material(shader)
        {
            name = "Runtime_LevelTransitionPieceMaterial"
        };
        return pixelMaterial;
    }

    private struct ShatterPiece
    {
        public SpriteRenderer Renderer;
        public Rigidbody2D Body;
    }
}
