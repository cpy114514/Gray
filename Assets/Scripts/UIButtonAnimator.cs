using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UIButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float pressedScale = 0.94f;
    [SerializeField] private float selectedScale = 1.05f;
    [SerializeField] private float animationSpeed = 18f;

    private Button button;
    private RectTransform rectTransform;
    private Vector3 baseScale = Vector3.one;
    private bool hovered;
    private bool pressed;
    private bool selected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeButtonAnimations()
    {
        InstallOnAllButtons();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallOnAllButtons();
    }

    public static void InstallOnAllButtons()
    {
        Button[] buttons = FindObjectsOfType<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button targetButton = buttons[i];
            if (targetButton == null || targetButton.GetComponent<UIButtonAnimator>() != null)
            {
                continue;
            }

            targetButton.gameObject.AddComponent<UIButtonAnimator>();
        }
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        CacheComponents();
        ResetState();
    }

    private void OnDisable()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale = baseScale;
        }
    }

    private void Update()
    {
        if (rectTransform == null || button == null)
        {
            return;
        }

        float scale = 1f;
        if (button.interactable)
        {
            if (pressed)
            {
                scale = pressedScale;
            }
            else if (hovered)
            {
                scale = hoverScale;
            }
            else if (selected)
            {
                scale = selectedScale;
            }
        }

        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            baseScale * scale,
            animationSpeed * Time.unscaledDeltaTime);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        pressed = false;
    }

    private void CacheComponents()
    {
        button = button != null ? button : GetComponent<Button>();
        rectTransform = rectTransform != null ? rectTransform : GetComponent<RectTransform>();
        if (rectTransform != null && baseScale == Vector3.one)
        {
            baseScale = rectTransform.localScale;
        }
    }

    private void ResetState()
    {
        hovered = false;
        pressed = false;
        selected = false;
    }
}
