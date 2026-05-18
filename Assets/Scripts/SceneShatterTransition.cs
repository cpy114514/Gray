using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneShatterTransition : MonoBehaviour
{
    [Header("Capture Pieces")]
    [SerializeField] private int columns = 14;
    [SerializeField] private int rows = 8;
    [SerializeField] private float pieceDepth = 0f;
    [SerializeField] private int sortingOrder = 32000;

    [Header("Motion")]
    [SerializeField] private float duration = 1.05f;
    [SerializeField] private float burstForce = 2.8f;
    [SerializeField] private float upwardBurst = 1.2f;
    [SerializeField] private float gravityScale = 3.4f;
    [SerializeField] private float angularVelocity = 120f;

    [Header("Camera")]
    [SerializeField] private float shakeDuration = 0.18f;
    [SerializeField] private float shakeStrength = 0.11f;

    private static Sprite pixelSprite;
    private static Material pixelMaterial;

    private bool isPlaying;

    public static bool PlayToMainMenu(MonoBehaviour owner)
    {
        if (owner == null)
        {
            SceneFlow.ReturnToMainMenu();
            return false;
        }

        SceneShatterTransition transition = FindObjectOfType<SceneShatterTransition>();
        if (transition == null)
        {
            GameObject obj = new GameObject("SceneShatterTransition");
            transition = obj.AddComponent<SceneShatterTransition>();
        }

        return transition.Play(SceneFlow.ReturnToMainMenu);
    }

    public bool Play(System.Action onComplete)
    {
        if (isPlaying)
        {
            return false;
        }

        StartCoroutine(PlayRoutine(onComplete));
        return true;
    }

    private IEnumerator PlayRoutine(System.Action onComplete)
    {
        isPlaying = true;
        Time.timeScale = 1f;

        yield return new WaitForEndOfFrame();

        Camera mainCamera = Camera.main;
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        if (capture == null || mainCamera == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        capture.filterMode = FilterMode.Point;
        CanvasState[] disabledCanvases = DisableActiveCanvases();
        CreateBackgroundBlocker(mainCamera);
        ShatterPiece[] pieces = CreatePieces(capture, mainCamera);
        ShakeCamera();

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i].Renderer != null)
            {
                Destroy(pieces[i].Renderer.gameObject);
            }
        }

        RestoreCanvases(disabledCanvases);
        Destroy(capture);
        onComplete?.Invoke();
    }

    private ShatterPiece[] CreatePieces(Texture2D capture, Camera mainCamera)
    {
        int safeColumns = Mathf.Clamp(columns, 2, 32);
        int safeRows = Mathf.Clamp(rows, 2, 20);
        int pieceWidth = Mathf.Max(1, capture.width / safeColumns);
        int pieceHeight = Mathf.Max(1, capture.height / safeRows);
        float pixelsPerUnit = GetPixelsPerUnit(capture, mainCamera);

        List<ShatterPiece> pieces = new List<ShatterPiece>(safeColumns * safeRows);
        Vector3 cameraCenter = mainCamera.transform.position;

        for (int y = 0; y < safeRows; y++)
        {
            for (int x = 0; x < safeColumns; x++)
            {
                int rectX = x * pieceWidth;
                int rectY = y * pieceHeight;
                int rectWidth = x == safeColumns - 1 ? capture.width - rectX : pieceWidth;
                int rectHeight = y == safeRows - 1 ? capture.height - rectY : pieceHeight;
                if (rectWidth <= 0 || rectHeight <= 0)
                {
                    continue;
                }

                Rect rect = new Rect(rectX, rectY, rectWidth, rectHeight);
                Sprite sprite = Sprite.Create(capture, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit);
                sprite.name = "Runtime_SceneShatterPieceSprite";

                GameObject piece = new GameObject("SceneShatterPiece", typeof(SpriteRenderer), typeof(Rigidbody2D));
                piece.transform.position = GetPieceWorldPosition(mainCamera, capture, rect);

                SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = GetPixelMaterial();
                renderer.sortingOrder = sortingOrder;

                Rigidbody2D body = piece.GetComponent<Rigidbody2D>();
                body.gravityScale = gravityScale;
                body.mass = 0.08f;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;

                Vector2 away = (Vector2)(piece.transform.position - cameraCenter);
                if (away.sqrMagnitude < 0.001f)
                {
                    away = Random.insideUnitCircle;
                }

                Vector2 velocity = away.normalized * Random.Range(burstForce * 0.35f, burstForce);
                velocity.y += Random.Range(0.15f, upwardBurst);
                body.velocity = velocity;
                body.angularVelocity = Random.Range(-angularVelocity, angularVelocity);

                pieces.Add(new ShatterPiece
                {
                    Renderer = renderer,
                    Body = body
                });
            }
        }

        return pieces.ToArray();
    }

    private Vector3 GetPieceWorldPosition(Camera mainCamera, Texture2D capture, Rect rect)
    {
        float viewportX = (rect.x + rect.width * 0.5f) / capture.width;
        float viewportY = (rect.y + rect.height * 0.5f) / capture.height;
        float distance = Mathf.Abs(pieceDepth - mainCamera.transform.position.z);
        Vector3 world = mainCamera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, distance));
        world.z = pieceDepth;
        return world;
    }

    private float GetPixelsPerUnit(Texture2D capture, Camera mainCamera)
    {
        if (mainCamera != null && mainCamera.orthographic)
        {
            return capture.height / Mathf.Max(0.01f, mainCamera.orthographicSize * 2f);
        }

        return 100f;
    }

    private void CreateBackgroundBlocker(Camera mainCamera)
    {
        if (mainCamera == null || !mainCamera.orthographic)
        {
            return;
        }

        GameObject blocker = new GameObject("SceneShatterBackgroundBlocker", typeof(SpriteRenderer));
        blocker.transform.position = new Vector3(mainCamera.transform.position.x, mainCamera.transform.position.y, pieceDepth + 0.1f);
        blocker.transform.localScale = new Vector3(mainCamera.orthographicSize * 2f * mainCamera.aspect, mainCamera.orthographicSize * 2f, 1f);

        SpriteRenderer renderer = blocker.GetComponent<SpriteRenderer>();
        renderer.sprite = GetPixelSprite();
        renderer.sharedMaterial = GetPixelMaterial();
        renderer.color = Color.black;
        renderer.sortingOrder = sortingOrder - 1;
        Destroy(blocker, duration + 0.2f);
    }

    private void ShakeCamera()
    {
        CameraFollow2D cameraFollow = Camera.main != null
            ? Camera.main.GetComponent<CameraFollow2D>()
            : FindObjectOfType<CameraFollow2D>();

        if (cameraFollow != null)
        {
            cameraFollow.Shake(shakeDuration, shakeStrength);
        }
    }

    private static CanvasState[] DisableActiveCanvases()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        CanvasState[] states = new CanvasState[canvases.Length];
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            states[i] = new CanvasState
            {
                Canvas = canvas,
                Enabled = canvas != null && canvas.enabled
            };

            if (canvas != null)
            {
                canvas.enabled = false;
            }
        }

        return states;
    }

    private static void RestoreCanvases(CanvasState[] states)
    {
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].Canvas != null)
            {
                states[i].Canvas.enabled = states[i].Enabled;
            }
        }
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
        pixelSprite.name = "Runtime_SceneShatterPixelSprite";
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
            name = "Runtime_SceneShatterMaterial"
        };
        return pixelMaterial;
    }

    private struct ShatterPiece
    {
        public SpriteRenderer Renderer;
        public Rigidbody2D Body;
    }

    private struct CanvasState
    {
        public Canvas Canvas;
        public bool Enabled;
    }
}
