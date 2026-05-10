using UnityEngine;
using UnityEngine.UI;

public class MiniMapController2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private RawImage minimapImage;
    [SerializeField] private Transform target;
    [SerializeField] private bool autoFindPlayer = true;

    [Header("View")]
    [SerializeField] private float orthographicSize = 18f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, -10f);
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private LayerMask cullingMask = ~0;

    [Header("Texture")]
    [SerializeField] private int textureWidth = 768;
    [SerializeField] private int textureHeight = 432;
    [SerializeField] private FilterMode filterMode = FilterMode.Point;

    private RenderTexture renderTexture;

    private void Awake()
    {
        CacheReferences();
        ConfigureCamera();
        CreateRenderTexture();
        FindTargetIfNeeded();
    }

    private void OnEnable()
    {
        if (renderTexture == null)
        {
            CreateRenderTexture();
        }
    }

    private void LateUpdate()
    {
        FindTargetIfNeeded();
        if (target == null || minimapCamera == null)
        {
            return;
        }

        Vector3 targetPosition = target.position + cameraOffset;
        minimapCamera.transform.position = targetPosition;
        minimapCamera.transform.rotation = Quaternion.identity;
    }

    private void OnDestroy()
    {
        if (minimapCamera != null)
        {
            minimapCamera.targetTexture = null;
        }

        if (minimapImage != null)
        {
            minimapImage.texture = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void CacheReferences()
    {
        if (minimapCamera == null)
        {
            minimapCamera = GetComponentInChildren<Camera>(true);
        }

        if (minimapImage == null)
        {
            minimapImage = GetComponentInChildren<RawImage>(true);
        }
    }

    private void ConfigureCamera()
    {
        if (minimapCamera == null)
        {
            return;
        }

        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = orthographicSize;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = backgroundColor;
        minimapCamera.cullingMask = cullingMask;
        minimapCamera.depth = -10f;
        minimapCamera.useOcclusionCulling = false;
        minimapCamera.allowHDR = false;
        minimapCamera.allowMSAA = false;
    }

    private void CreateRenderTexture()
    {
        if (minimapCamera == null || minimapImage == null)
        {
            return;
        }

        int safeWidth = Mathf.Clamp(textureWidth, 128, 2048);
        int safeHeight = Mathf.Clamp(textureHeight, 72, 2048);
        renderTexture = new RenderTexture(safeWidth, safeHeight, 16, RenderTextureFormat.ARGB32)
        {
            name = "Runtime_Minimap_RenderTexture",
            filterMode = filterMode,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1
        };

        renderTexture.Create();
        minimapCamera.targetTexture = renderTexture;
        minimapImage.texture = renderTexture;
    }

    private void FindTargetIfNeeded()
    {
        if (target != null || !autoFindPlayer)
        {
            return;
        }

        PlayerController2D player = FindObjectOfType<PlayerController2D>();
        if (player != null)
        {
            target = player.transform;
        }
    }
}
