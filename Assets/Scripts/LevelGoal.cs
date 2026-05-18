using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class LevelGoal : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] private bool oneTimeUse = true;
    [SerializeField] private WinPanelController winPanelController;
    [SerializeField] private LevelCompleteReveal2D completionReveal;
    [SerializeField] private bool loadNextLevelIfNoWinPanel = false;

    [Header("Visual Squares")]
    [SerializeField] private Transform outerSquare;
    [SerializeField] private Transform innerSquare;
    [SerializeField] private SpriteRenderer outerRenderer;
    [SerializeField] private SpriteRenderer innerRenderer;
    [SerializeField] private float outerRotationSpeed = 90f;
    [SerializeField] private float innerRotationSpeed = -135f;

    [Header("Background Sampling")]
    [SerializeField] private Transform backgroundSamplePoint;
    [SerializeField] private float colorRefreshInterval = 0.15f;
    [SerializeField] private Color whiteColor = Color.white;
    [SerializeField] private Color blackColor = Color.black;

    private PlatformColorType lastBackgroundColor;
    private float nextColorRefreshTime;
    private bool hasLastBackgroundColor;
    private bool used;

    private void Awake()
    {
        ApplySetup();
        CacheVisualReferences();
        RefreshVisualColors(force: true);
    }

    private void OnValidate()
    {
        ApplySetup();
        CacheVisualReferences();
        RefreshVisualColors(force: true);
    }

    private void Update()
    {
        RotateSquares();

        if (Time.unscaledTime >= nextColorRefreshTime)
        {
            nextColorRefreshTime = Time.unscaledTime + colorRefreshInterval;
            RefreshVisualColors(force: false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCompleteFromCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryCompleteFromCollider(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryCompleteFromCollider(collision.collider);
    }

    public void CompleteLevel()
    {
        if (oneTimeUse && used)
        {
            return;
        }

        used = true;

        LevelCompleteReveal2D reveal = GetCompletionReveal();
        if (reveal != null && reveal.PlayReveal(ShowCompletionResult))
        {
            return;
        }

        ShowCompletionResult();
    }

    public void ResetGoal()
    {
        used = false;
    }

    private void ShowCompletionResult()
    {
        WinPanelController panel = GetWinPanelController();
        if (panel != null)
        {
            panel.ShowWin();
            return;
        }

        SceneFlow.UnlockNextLevelFromCurrent();
        if (loadNextLevelIfNoWinPanel)
        {
            SceneFlow.LoadNextLevel();
            return;
        }

        Debug.LogWarning("LevelGoal completed, but no WinPanelController was found. Add Gameplay UI to this scene, or enable Load Next Level If No Win Panel.", this);
    }

    private void TryCompleteFromCollider(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        CompleteLevel();
    }

    private WinPanelController GetWinPanelController()
    {
        if (winPanelController != null)
        {
            return winPanelController;
        }

        winPanelController = FindObjectOfType<WinPanelController>(true);
        return winPanelController;
    }

    private LevelCompleteReveal2D GetCompletionReveal()
    {
        if (completionReveal != null)
        {
            return completionReveal;
        }

        completionReveal = GetComponent<LevelCompleteReveal2D>();
        if (completionReveal == null)
        {
            completionReveal = gameObject.AddComponent<LevelCompleteReveal2D>();
        }

        return completionReveal;
    }

    private void RotateSquares()
    {
        float delta = Time.deltaTime;

        if (outerSquare != null)
        {
            outerSquare.Rotate(0f, 0f, outerRotationSpeed * delta, Space.Self);
        }

        if (innerSquare != null)
        {
            innerSquare.Rotate(0f, 0f, innerRotationSpeed * delta, Space.Self);
        }
    }

    private void RefreshVisualColors(bool force)
    {
        Vector3 samplePosition = backgroundSamplePoint != null
            ? backgroundSamplePoint.position
            : transform.position;

        if (!ColorPlatform.TryGetColorAtWorldPosition(samplePosition, out PlatformColorType backgroundColor))
        {
            backgroundColor = PlatformColorType.Black;
        }

        if (!force && hasLastBackgroundColor && lastBackgroundColor == backgroundColor)
        {
            return;
        }

        hasLastBackgroundColor = true;
        lastBackgroundColor = backgroundColor;

        bool backgroundIsBlack = backgroundColor != PlatformColorType.White;
        Color outerColor = backgroundIsBlack ? whiteColor : blackColor;
        Color innerColor = backgroundIsBlack ? blackColor : whiteColor;

        if (outerRenderer != null)
        {
            outerRenderer.color = outerColor;
        }

        if (innerRenderer != null)
        {
            innerRenderer.color = innerColor;
        }
    }

    private void CacheVisualReferences()
    {
        if (outerSquare != null && outerRenderer == null)
        {
            outerRenderer = outerSquare.GetComponent<SpriteRenderer>();
        }

        if (innerSquare != null && innerRenderer == null)
        {
            innerRenderer = innerSquare.GetComponent<SpriteRenderer>();
        }
    }

    private void ApplySetup()
    {
        Collider2D goalCollider = GetComponent<Collider2D>();
        if (goalCollider != null)
        {
            goalCollider.isTrigger = true;
        }
    }
}
