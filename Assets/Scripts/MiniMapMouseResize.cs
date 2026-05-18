using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class MiniMapMouseResize : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Size")]
    [SerializeField] private Vector2 defaultSize = new Vector2(420f, 236f);
    [SerializeField] private Vector2 minSize = new Vector2(260f, 146f);
    [SerializeField] private Vector2 maxSize = new Vector2(760f, 428f);
    [SerializeField] private bool applyDefaultSizeOnAwake = true;

    [Header("Drag")]
    [SerializeField] private bool keepAspectRatio = true;
    [SerializeField] private float dragSensitivity = 1f;

    private RectTransform rectTransform;
    private Vector2 dragStartPointer;
    private Vector2 dragStartSize;
    private float aspectRatio = 16f / 9f;
    private bool isDragging;
    private bool configured;

    private void Awake()
    {
        CacheReferences();
        PrepareRaycastTarget();
        ApplyDefaultSizeIfNeeded();
    }

    public void Configure(Vector2 newDefaultSize, Vector2 newMinSize, Vector2 newMaxSize, bool shouldApplyDefaultSize)
    {
        defaultSize = newDefaultSize;
        minSize = newMinSize;
        maxSize = newMaxSize;
        applyDefaultSizeOnAwake = shouldApplyDefaultSize;
        configured = true;

        CacheReferences();
        PrepareRaycastTarget();
        ApplyDefaultSizeIfNeeded();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        CacheReferences();

        isDragging = true;
        dragStartPointer = eventData.position;
        dragStartSize = rectTransform.sizeDelta;
        aspectRatio = Mathf.Max(0.01f, dragStartSize.x / Mathf.Max(1f, dragStartSize.y));
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || rectTransform == null)
        {
            return;
        }

        Vector2 pointerDelta = eventData.position - dragStartPointer;
        float widthDelta = pointerDelta.x * dragSensitivity;
        float heightDelta = -pointerDelta.y * dragSensitivity;
        float dominantDelta = Mathf.Abs(widthDelta) >= Mathf.Abs(heightDelta) ? widthDelta : heightDelta;

        Vector2 targetSize;
        if (keepAspectRatio)
        {
            float width = Mathf.Clamp(dragStartSize.x + dominantDelta, minSize.x, maxSize.x);
            targetSize = new Vector2(width, Mathf.Clamp(width / aspectRatio, minSize.y, maxSize.y));
        }
        else
        {
            targetSize = new Vector2(
                Mathf.Clamp(dragStartSize.x + widthDelta, minSize.x, maxSize.x),
                Mathf.Clamp(dragStartSize.y + heightDelta, minSize.y, maxSize.y));
        }

        rectTransform.sizeDelta = targetSize;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
    }

    private void PrepareRaycastTarget()
    {
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = true;
        }
    }

    private void ApplyDefaultSizeIfNeeded()
    {
        if (!applyDefaultSizeOnAwake || rectTransform == null)
        {
            return;
        }

        rectTransform.sizeDelta = ClampSize(defaultSize);
        aspectRatio = Mathf.Max(0.01f, rectTransform.sizeDelta.x / Mathf.Max(1f, rectTransform.sizeDelta.y));

        // Runtime auto-added components run Awake before Configure, so avoid applying
        // the old serialized default again after the controller has configured values.
        applyDefaultSizeOnAwake = !configured && applyDefaultSizeOnAwake;
    }

    private Vector2 ClampSize(Vector2 size)
    {
        return new Vector2(
            Mathf.Clamp(size.x, minSize.x, maxSize.x),
            Mathf.Clamp(size.y, minSize.y, maxSize.y));
    }
}
