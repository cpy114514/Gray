using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels built in MainMenu scene")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject levelSelectPanel;
    [SerializeField] private GameSettingsUI settingsUI;

    [Header("Main buttons built in scene")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Text titleText;

    [Header("Level buttons built in scene")]
    [SerializeField] private int firstLevelBuildIndex = SceneFlow.FirstGameplayBuildIndex;
    [SerializeField] private Button[] levelButtons;
    [SerializeField] private Text[] levelLabels;

    [Header("Level select animation")]
    [SerializeField] private float menuTransitionDuration = 0.64f;
    [SerializeField] private float titleMoveDelay = 0f;
    [SerializeField] private float startMoveDelay = 0.1f;
    [SerializeField] private float exitMoveDelay = 0.16f;
    [SerializeField] private float settingsFadeDelay = 0.22f;
    [SerializeField] private float levelPanelMoveDelay = 0.24f;
    [SerializeField] private float elementMoveDuration = 0.42f;
    [SerializeField] private float levelButtonFloatDistance = 90f;
    [SerializeField] private float levelButtonStagger = 0.055f;
    [SerializeField] private Vector2 sideTitlePosition = new Vector2(-410f, 170f);
    [SerializeField] private Vector2 sideStartPosition = new Vector2(-410f, -35f);
    [SerializeField] private Vector2 sideExitPosition = new Vector2(-410f, -185f);
    [SerializeField] private Vector2 levelSelectOpenPosition = new Vector2(170f, 0f);
    [SerializeField, Range(0.35f, 1f)] private float sideTitleAlpha = 0.52f;
    [SerializeField, Range(0.5f, 1f)] private float sideButtonAlpha = 0.86f;

    [Header("Startup intro animation")]
    [SerializeField] private bool playStartupIntro = true;
    [SerializeField] private float startupFloatDistance = 92f;
    [SerializeField] private float startupElementMoveDuration = 0.46f;
    [SerializeField] private float startupTitleDelay = 0f;
    [SerializeField] private float startupStartDelay = 0.08f;
    [SerializeField] private float startupSettingsDelay = 0.14f;
    [SerializeField] private float startupExitDelay = 0.2f;

    [Header("Level button particles")]
    [SerializeField] private int levelButtonParticleCount = 12;
    [SerializeField] private float levelButtonParticleDuration = 1.25f;
    [SerializeField] private Vector2 levelButtonParticleSizeRange = new Vector2(22f, 34f);
    [SerializeField] private float levelButtonParticleSpread = 62f;
    [SerializeField] private float levelButtonParticleBurstForce = 1.35f;
    [SerializeField] private float levelButtonParticleFall = 0.62f;
    [SerializeField] private float levelLoadFadeDuration = 0.28f;

    private static Sprite pixelSprite;
    private static PhysicsMaterial2D pixelPhysicsMaterial;
    private static Material pixelRenderMaterial;
    private RectTransform titleRect;
    private RectTransform startRect;
    private RectTransform settingsRect;
    private RectTransform quitRect;
    private RectTransform levelPanelRect;
    private RectTransform[] levelItemRects = new RectTransform[0];
    private Vector2 titleHomePosition;
    private Vector2 startHomePosition;
    private Vector2 settingsHomePosition;
    private Vector2 quitHomePosition;
    private Vector2 levelPanelHomePosition;
    private Vector2[] levelItemHomePositions = new Vector2[0];
    private CanvasGroup titleGroup;
    private CanvasGroup startGroup;
    private CanvasGroup settingsGroup;
    private CanvasGroup quitGroup;
    private CanvasGroup levelPanelGroup;
    private CanvasGroup[] levelItemGroups = new CanvasGroup[0];
    private Coroutine transitionRoutine;
    private bool isLoadingLevel;

    private void Awake()
    {
        SceneFlow.EnsureInitialUnlock();
        FindSceneObjectsIfMissing();
        CacheAnimationReferences();
        WireButtons();
        ShowMainMenuInstant();
        RefreshLevelButtons();
    }

    private void Start()
    {
        if (!playStartupIntro || isLoadingLevel)
        {
            return;
        }

        StopTransitionIfRunning();
        transitionRoutine = StartCoroutine(AnimateStartupIntro());
    }

    public void ShowMainMenu()
    {
        StopTransitionIfRunning();

        bool canAnimateBack = levelSelectPanel != null && levelSelectPanel.activeInHierarchy && GetAlpha(levelPanelGroup) > 0.01f;
        if (canAnimateBack)
        {
            transitionRoutine = StartCoroutine(AnimateToMainMenu());
            return;
        }

        ShowMainMenuInstant();
    }

    private void ShowMainMenuInstant()
    {
        StopTransitionIfRunning();

        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }

        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(false);
        }

        SetMainMenuInstant(false);
        SetLevelSelectInstant(false);
    }

    public void ShowLevelSelect()
    {
        RefreshLevelButtons();

        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }

        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(true);
        }

        StopTransitionIfRunning();
        transitionRoutine = StartCoroutine(AnimateToLevelSelect());
    }

    public void LoadLevel1()
    {
        PlayLevelButtonEffectThenLoad(0);
    }

    public void LoadLevel2()
    {
        PlayLevelButtonEffectThenLoad(1);
    }

    public void LoadLevel3()
    {
        PlayLevelButtonEffectThenLoad(2);
    }

    public void ShowSettings()
    {
        if (settingsUI != null)
        {
            settingsUI.OpenFromPanel(mainMenuPanel);
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        GrayExitPage();
#else
        Application.Quit();
#endif
    }

    public void UnlockAll()
    {
        QuitGame();
    }

    private void WireButtons()
    {
        if (ShouldAutoWire(startButton))
        {
            startButton.onClick.AddListener(ShowLevelSelect);
        }

        if (ShouldAutoWire(settingsButton))
        {
            settingsButton.onClick.AddListener(ShowSettings);
        }

        if (ShouldAutoWire(quitButton))
        {
            quitButton.onClick.AddListener(QuitGame);
        }

        if (ShouldAutoWire(backButton))
        {
            backButton.onClick.AddListener(ShowMainMenu);
        }

        for (int i = 0; i < levelButtons.Length; i++)
        {
            int buildIndex = firstLevelBuildIndex + i;
            if (!ShouldAutoWire(levelButtons[i]))
            {
                continue;
            }

            int levelIndex = i;
            levelButtons[i].onClick.AddListener(() => PlayLevelButtonEffectThenLoad(levelIndex));
        }
    }

    private static bool ShouldAutoWire(Button button)
    {
        return button != null && button.onClick.GetPersistentEventCount() == 0;
    }

    private void RefreshLevelButtons()
    {
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int buildIndex = firstLevelBuildIndex + i;
            bool sceneExists = buildIndex < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
            bool unlocked = sceneExists && SceneFlow.IsLevelUnlocked(buildIndex);

            if (levelButtons[i] != null)
            {
                levelButtons[i].interactable = unlocked;
            }

            if (i < levelLabels.Length && levelLabels[i] != null)
            {
                string displayName = sceneExists
                    ? SceneFlow.GetLevelDisplayName(buildIndex)
                    : $"LEVEL {i + 1}";
                levelLabels[i].text = unlocked ? displayName : $"{displayName} LOCKED";
            }
        }

    }

    private void PlayLevelButtonEffectThenLoad(int levelIndex)
    {
        if (isLoadingLevel)
        {
            return;
        }

        int buildIndex = firstLevelBuildIndex + levelIndex;
        if (!SceneFlow.IsLevelUnlocked(buildIndex))
        {
            return;
        }

        Button sourceButton = levelIndex >= 0 && levelIndex < levelButtons.Length
            ? levelButtons[levelIndex]
            : null;

        StartCoroutine(PlayLevelButtonEffectThenLoadRoutine(sourceButton, buildIndex));
    }

    private IEnumerator PlayLevelButtonEffectThenLoadRoutine(Button sourceButton, int buildIndex)
    {
        isLoadingLevel = true;
        SetCanvasRaycasts(levelPanelGroup, false);

        if (sourceButton == null)
        {
            SceneFlow.LoadLevel(buildIndex);
            yield break;
        }

        RectTransform buttonRect = sourceButton.GetComponent<RectTransform>();
        Canvas parentCanvas = sourceButton.GetComponentInParent<Canvas>();
        if (buttonRect == null || parentCanvas == null)
        {
            SceneFlow.LoadLevel(buildIndex);
            yield break;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            SceneFlow.LoadLevel(buildIndex);
            yield break;
        }

        GameObject particleRoot = new GameObject($"{sourceButton.name}_PhysicalPixelBurst");
        GameObject particleGround = CreateParticleGround(camera, levelButtonParticleFall);
        GameObject visualRoot = new GameObject($"{sourceButton.name}_FrontPixelVisuals", typeof(RectTransform));
        visualRoot.transform.SetParent(parentCanvas.transform, false);
        visualRoot.transform.SetAsLastSibling();
        RectTransform visualRootRect = visualRoot.GetComponent<RectTransform>();
        visualRootRect.anchorMin = Vector2.zero;
        visualRootRect.anchorMax = Vector2.one;
        visualRootRect.offsetMin = Vector2.zero;
        visualRootRect.offsetMax = Vector2.zero;

        Vector3[] corners = new Vector3[4];
        buttonRect.GetWorldCorners(corners);
        Vector3 worldBottomLeft = UiWorldToMenuWorld(parentCanvas, camera, corners[0]);
        Vector3 worldTopLeft = UiWorldToMenuWorld(parentCanvas, camera, corners[1]);
        Vector3 worldTopRight = UiWorldToMenuWorld(parentCanvas, camera, corners[2]);
        Vector3 worldCenter = (worldBottomLeft + worldTopRight) * 0.5f;
        Vector2 worldSize = new Vector2(
            Mathf.Abs(worldTopRight.x - worldTopLeft.x),
            Mathf.Abs(worldTopLeft.y - worldBottomLeft.y));

        sourceButton.gameObject.SetActive(false);

        LevelButtonParticle[] particles = new LevelButtonParticle[Mathf.Max(1, levelButtonParticleCount)];
        Color particleColor = Color.white;
        for (int i = 0; i < particles.Length; i++)
        {
            GameObject pixel = new GameObject("PhysicalPixel", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D));
            pixel.transform.SetParent(particleRoot.transform, true);
            float pixelSize = UnityEngine.Random.Range(levelButtonParticleSizeRange.x, levelButtonParticleSizeRange.y) * 0.012f;
            pixel.transform.localScale = new Vector3(pixelSize, pixelSize, 1f);
            pixel.transform.position = worldCenter + new Vector3(
                UnityEngine.Random.Range(-worldSize.x * 0.45f, worldSize.x * 0.45f),
                UnityEngine.Random.Range(-worldSize.y * 0.35f, worldSize.y * 0.35f),
                0f);

            SpriteRenderer pixelRenderer = pixel.GetComponent<SpriteRenderer>();
            pixelRenderer.sprite = GetPixelSprite();
            pixelRenderer.sharedMaterial = GetPixelRenderMaterial();
            pixelRenderer.color = particleColor;
            pixelRenderer.sortingOrder = 32000;
            pixelRenderer.enabled = false;

            GameObject visual = new GameObject("FrontPixel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            visual.transform.SetParent(visualRoot.transform, false);
            RectTransform visualRect = visual.GetComponent<RectTransform>();
            visualRect.anchorMin = new Vector2(0.5f, 0.5f);
            visualRect.anchorMax = new Vector2(0.5f, 0.5f);
            visualRect.sizeDelta = Vector2.one * UnityEngine.Random.Range(levelButtonParticleSizeRange.x, levelButtonParticleSizeRange.y);
            Image visualImage = visual.GetComponent<Image>();
            visualImage.color = Color.white;
            visualImage.raycastTarget = false;

            BoxCollider2D pixelCollider = pixel.GetComponent<BoxCollider2D>();
            pixelCollider.sharedMaterial = GetPixelPhysicsMaterial();

            Rigidbody2D pixelBody = pixel.GetComponent<Rigidbody2D>();
            pixelBody.gravityScale = 3.2f;
            pixelBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            pixelBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            Vector2 offsetFromCenter = pixel.transform.position - worldCenter;
            if (offsetFromCenter.sqrMagnitude < 0.001f)
            {
                offsetFromCenter = UnityEngine.Random.insideUnitCircle;
            }

            Vector2 burstDirection = (offsetFromCenter.normalized + UnityEngine.Random.insideUnitCircle * 0.35f).normalized;
            float sideKick = UnityEngine.Random.Range(0.8f, levelButtonParticleBurstForce);
            float upwardKick = UnityEngine.Random.Range(1.25f, 2.25f) + Mathf.Max(0f, burstDirection.y) * 0.65f;
            pixelBody.velocity = new Vector2(
                burstDirection.x * sideKick + UnityEngine.Random.Range(-levelButtonParticleSpread, levelButtonParticleSpread) * 0.0035f,
                upwardKick);
            pixelBody.angularVelocity = UnityEngine.Random.Range(-220f, 220f);

            particles[i] = new LevelButtonParticle
            {
                Renderer = pixelRenderer,
                Body = pixelBody,
                VisualRect = visualRect,
                VisualImage = visualImage
            };
        }

        float duration = Mathf.Max(0.05f, levelButtonParticleDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < particles.Length; i++)
            {
                StopParticleSpinOnGround(particles[i].Body);

                if (particles[i].Renderer != null)
                {
                    particles[i].Renderer.color = Color.white;
                }

                if (particles[i].VisualImage != null)
                {
                    particles[i].VisualImage.color = Color.white;
                }

                if (particles[i].VisualRect != null && particles[i].Body != null)
                {
                    particles[i].VisualRect.anchoredPosition = WorldToCanvasPosition(parentCanvas, camera, particles[i].Body.position);
                    particles[i].VisualRect.localRotation = Quaternion.Euler(0f, 0f, particles[i].Body.rotation);
                }
            }

            yield return null;
        }

        Destroy(particleRoot);
        Destroy(particleGround);
        Destroy(visualRoot);
        yield return FadeSceneToBlack(parentCanvas);
        SceneFlow.LoadLevel(buildIndex);
    }

    private static GameObject CreateParticleGround(Camera camera, float extraFallDistance)
    {
        GameObject ground = new GameObject("LevelButtonParticleGround", typeof(BoxCollider2D));
        float halfHeight = camera.orthographic ? camera.orthographicSize : 5f;
        float halfWidth = halfHeight * camera.aspect;
        Vector3 cameraPosition = camera.transform.position;
        ground.transform.position = new Vector3(cameraPosition.x, cameraPosition.y - halfHeight + Mathf.Max(0.2f, extraFallDistance), 0f);

        BoxCollider2D collider = ground.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(halfWidth * 2f + 4f, 0.24f);
        collider.sharedMaterial = GetPixelPhysicsMaterial();
        return ground;
    }

    private static void StopParticleSpinOnGround(Rigidbody2D body)
    {
        if (body == null)
        {
            return;
        }

        if (Mathf.Abs(body.velocity.y) > 0.05f)
        {
            return;
        }

        body.angularVelocity = Mathf.Lerp(body.angularVelocity, 0f, 0.35f);
        if (Mathf.Abs(body.angularVelocity) < 8f)
        {
            body.angularVelocity = 0f;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }
    }

    private static Vector3 UiWorldToMenuWorld(Canvas canvas, Camera camera, Vector3 uiWorldPosition)
    {
        Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(canvasCamera, uiWorldPosition);
        float zDistance = Mathf.Abs(camera.transform.position.z);
        Vector3 worldPosition = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, zDistance));
        worldPosition.z = 0f;
        return worldPosition;
    }

    private static Vector2 WorldToCanvasPosition(Canvas canvas, Camera camera, Vector3 worldPosition)
    {
        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            canvasCamera,
            out Vector2 localPoint);
        return localPoint;
    }

    private IEnumerator FadeSceneToBlack(Canvas parentCanvas)
    {
        if (parentCanvas == null || levelLoadFadeDuration <= 0f)
        {
            yield break;
        }

        GameObject overlayObject = new GameObject("LevelLoadFade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayObject.transform.SetParent(parentCanvas.transform, false);
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayObject.transform.SetAsLastSibling();

        Image overlay = overlayObject.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0f);
        overlay.raycastTarget = true;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, levelLoadFadeDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / duration);
            overlay.color = new Color(0f, 0f, 0f, t);
            yield return null;
        }
    }

    private static Color GetButtonParticleColor(Button sourceButton)
    {
        Graphic targetGraphic = sourceButton.targetGraphic;
        if (targetGraphic != null)
        {
            return targetGraphic.color;
        }

        return Color.white;
    }

    private IEnumerator AnimateToLevelSelect()
    {
        SetCanvasInput(startGroup, false);
        SetCanvasInput(settingsGroup, false);
        SetCanvasInput(quitGroup, false);

        PrepareLevelSelectForAnimation();

        float duration = Mathf.Max(0.01f, menuTransitionDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float titleT = GetDelayedT(elapsed, titleMoveDelay);
            float startT = GetDelayedT(elapsed, startMoveDelay);
            float exitT = GetDelayedT(elapsed, exitMoveDelay);
            float settingsT = GetDelayedT(elapsed, settingsFadeDelay);
            float levelPanelT = GetDelayedT(elapsed, levelPanelMoveDelay);

            Move(titleRect, titleHomePosition, sideTitlePosition, titleT);
            Move(startRect, startHomePosition, sideStartPosition, startT);
            Move(quitRect, quitHomePosition, sideExitPosition, exitT);
            Move(levelPanelRect, levelPanelHomePosition, levelSelectOpenPosition, levelPanelT);
            SetAlpha(titleGroup, Mathf.Lerp(1f, sideTitleAlpha, titleT));
            SetAlpha(startGroup, Mathf.Lerp(1f, sideButtonAlpha, startT));
            SetAlpha(settingsGroup, 1f - settingsT);
            SetAlpha(quitGroup, Mathf.Lerp(1f, sideButtonAlpha, exitT));
            SetAlpha(levelPanelGroup, levelPanelT);

            AnimateLevelItems(elapsed - levelPanelMoveDelay);
            yield return null;
        }

        SetMainMenuInstant(true);
        SetLevelSelectInstant(true);
        SetButtonInteractable(backButton, true);
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] != null)
            {
                int buildIndex = firstLevelBuildIndex + i;
                bool sceneExists = buildIndex < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
                levelButtons[i].interactable = sceneExists && SceneFlow.IsLevelUnlocked(buildIndex);
            }
        }

        transitionRoutine = null;
    }

    private IEnumerator AnimateStartupIntro()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }

        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(false);
        }

        SetLevelSelectInstant(false);
        SetCanvasInput(startGroup, false);
        SetCanvasInput(settingsGroup, false);
        SetCanvasInput(quitGroup, false);

        Move(titleRect, titleHomePosition + Vector2.down * startupFloatDistance);
        Move(startRect, startHomePosition + Vector2.down * startupFloatDistance);
        Move(settingsRect, settingsHomePosition + Vector2.down * startupFloatDistance);
        Move(quitRect, quitHomePosition + Vector2.down * startupFloatDistance);
        SetAlpha(titleGroup, 0f);
        SetAlpha(startGroup, 0f);
        SetAlpha(settingsGroup, 0f);
        SetAlpha(quitGroup, 0f);

        float duration = GetStartupIntroDuration();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            AnimateStartupElement(titleRect, titleGroup, titleHomePosition, elapsed, startupTitleDelay);
            AnimateStartupElement(startRect, startGroup, startHomePosition, elapsed, startupStartDelay);
            AnimateStartupElement(settingsRect, settingsGroup, settingsHomePosition, elapsed, startupSettingsDelay);
            AnimateStartupElement(quitRect, quitGroup, quitHomePosition, elapsed, startupExitDelay);
            yield return null;
        }

        SetMainMenuInstant(false);
        SetLevelSelectInstant(false);
        transitionRoutine = null;
    }

    private void AnimateStartupElement(RectTransform rect, CanvasGroup group, Vector2 homePosition, float elapsed, float delay)
    {
        float t = Smooth01((elapsed - delay) / Mathf.Max(0.01f, startupElementMoveDuration));
        Move(rect, homePosition + Vector2.down * startupFloatDistance, homePosition, t);
        SetAlpha(group, t);
    }

    private float GetStartupIntroDuration()
    {
        float maxDelay = Mathf.Max(startupTitleDelay, startupStartDelay, startupSettingsDelay, startupExitDelay);
        return Mathf.Max(0.01f, maxDelay + startupElementMoveDuration);
    }

    private IEnumerator AnimateToMainMenu()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }

        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(true);
        }

        SetCanvasInput(startGroup, false);
        SetCanvasInput(levelPanelGroup, false);
        SetButtonInteractable(backButton, false);
        for (int i = 0; i < levelButtons.Length; i++)
        {
            SetButtonInteractable(levelButtons[i], false);
        }

        Vector2 titleFrom = GetPosition(titleRect);
        Vector2 startFrom = GetPosition(startRect);
        Vector2 quitFrom = GetPosition(quitRect);
        Vector2 levelPanelFrom = GetPosition(levelPanelRect);
        float settingsFromAlpha = GetAlpha(settingsGroup);
        float quitFromAlpha = GetAlpha(quitGroup);
        float startFromAlpha = GetAlpha(startGroup);
        float titleFromAlpha = GetAlpha(titleGroup);
        float levelPanelFromAlpha = GetAlpha(levelPanelGroup);
        Vector2[] levelItemFromPositions = new Vector2[levelItemRects.Length];
        float[] levelItemFromAlphas = new float[levelItemGroups.Length];
        for (int i = 0; i < levelItemRects.Length; i++)
        {
            levelItemFromPositions[i] = GetPosition(levelItemRects[i]);
            levelItemFromAlphas[i] = GetAlpha(levelItemGroups[i]);
        }

        float duration = Mathf.Max(0.01f, menuTransitionDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float levelPanelT = GetDelayedT(elapsed, titleMoveDelay);
            float exitT = GetDelayedT(elapsed, startMoveDelay);
            float startT = GetDelayedT(elapsed, exitMoveDelay);
            float settingsT = GetDelayedT(elapsed, settingsFadeDelay);
            float titleT = GetDelayedT(elapsed, levelPanelMoveDelay);

            Move(levelPanelRect, levelPanelFrom, levelPanelHomePosition, levelPanelT);
            Move(quitRect, quitFrom, quitHomePosition, exitT);
            Move(startRect, startFrom, startHomePosition, startT);
            Move(titleRect, titleFrom, titleHomePosition, titleT);
            SetAlpha(levelPanelGroup, Mathf.Lerp(levelPanelFromAlpha, 0f, levelPanelT));
            SetAlpha(startGroup, Mathf.Lerp(startFromAlpha, 1f, startT));
            SetAlpha(settingsGroup, Mathf.Lerp(settingsFromAlpha, 1f, settingsT));
            SetAlpha(quitGroup, Mathf.Lerp(quitFromAlpha, 1f, exitT));
            SetAlpha(titleGroup, Mathf.Lerp(titleFromAlpha, 1f, titleT));
            AnimateLevelItemsOut(elapsed - titleMoveDelay, levelItemFromPositions, levelItemFromAlphas);

            yield return null;
        }

        transitionRoutine = null;
        ShowMainMenuInstant();
    }

    private void PrepareLevelSelectForAnimation()
    {
        SetAlpha(levelPanelGroup, 0f);
        SetCanvasInput(levelPanelGroup, false);

        for (int i = 0; i < levelItemRects.Length; i++)
        {
            if (levelItemRects[i] != null)
            {
                levelItemRects[i].anchoredPosition = levelItemHomePositions[i] + Vector2.down * levelButtonFloatDistance;
            }

            SetAlpha(levelItemGroups[i], 0f);
        }

        SetButtonInteractable(backButton, false);
        for (int i = 0; i < levelButtons.Length; i++)
        {
            SetButtonInteractable(levelButtons[i], false);
        }
    }

    private void AnimateLevelItems(float elapsed)
    {
        for (int i = 0; i < levelItemRects.Length; i++)
        {
            float itemT = Smooth01((elapsed - (i * levelButtonStagger)) / Mathf.Max(0.01f, menuTransitionDuration));
            if (levelItemRects[i] != null)
            {
                Vector2 from = levelItemHomePositions[i] + Vector2.down * levelButtonFloatDistance;
                levelItemRects[i].anchoredPosition = Vector2.LerpUnclamped(from, levelItemHomePositions[i], itemT);
            }

            SetAlpha(levelItemGroups[i], itemT);
        }
    }

    private void AnimateLevelItemsOut(float elapsed, Vector2[] fromPositions, float[] fromAlphas)
    {
        for (int i = 0; i < levelItemRects.Length; i++)
        {
            float itemT = Smooth01((elapsed - (i * levelButtonStagger)) / Mathf.Max(0.01f, menuTransitionDuration));
            Vector2 hiddenPosition = levelItemHomePositions[i] + Vector2.down * levelButtonFloatDistance;
            if (levelItemRects[i] != null)
            {
                levelItemRects[i].anchoredPosition = Vector2.LerpUnclamped(fromPositions[i], hiddenPosition, itemT);
            }

            SetAlpha(levelItemGroups[i], Mathf.Lerp(fromAlphas[i], 0f, itemT));
        }
    }

    private void SetMainMenuInstant(bool sideLayout)
    {
        Move(titleRect, sideLayout ? sideTitlePosition : titleHomePosition);
        Move(startRect, sideLayout ? sideStartPosition : startHomePosition);
        Move(settingsRect, settingsHomePosition);
        Move(quitRect, sideLayout ? sideExitPosition : quitHomePosition);
        Move(levelPanelRect, sideLayout ? levelSelectOpenPosition : levelPanelHomePosition);
        SetAlpha(titleGroup, sideLayout ? sideTitleAlpha : 1f);
        SetAlpha(startGroup, sideLayout ? sideButtonAlpha : 1f);
        SetAlpha(settingsGroup, sideLayout ? 0f : 1f);
        SetAlpha(quitGroup, sideLayout ? sideButtonAlpha : 1f);
        SetCanvasInput(startGroup, !sideLayout);
        SetCanvasInput(settingsGroup, !sideLayout);
        SetCanvasInput(quitGroup, !sideLayout);
        SetButtonInteractable(startButton, true);
        SetButtonInteractable(settingsButton, true);
        SetButtonInteractable(quitButton, true);
    }

    private void SetLevelSelectInstant(bool visible)
    {
        SetAlpha(levelPanelGroup, visible ? 1f : 0f);
        SetCanvasInput(levelPanelGroup, visible);
        Move(levelPanelRect, visible ? levelSelectOpenPosition : levelPanelHomePosition);

        for (int i = 0; i < levelItemRects.Length; i++)
        {
            Move(levelItemRects[i], levelItemHomePositions[i]);
            SetAlpha(levelItemGroups[i], visible ? 1f : 0f);
        }
    }

    private void CacheAnimationReferences()
    {
        titleText = titleText != null ? titleText : FindText("Title");
        titleRect = titleText != null ? titleText.GetComponent<RectTransform>() : null;
        startRect = startButton != null ? startButton.GetComponent<RectTransform>() : null;
        settingsRect = settingsButton != null ? settingsButton.GetComponent<RectTransform>() : null;
        quitRect = quitButton != null ? quitButton.GetComponent<RectTransform>() : null;
        levelPanelRect = levelSelectPanel != null ? levelSelectPanel.GetComponent<RectTransform>() : null;

        titleHomePosition = GetPosition(titleRect);
        startHomePosition = GetPosition(startRect);
        settingsHomePosition = GetPosition(settingsRect);
        quitHomePosition = GetPosition(quitRect);
        levelPanelHomePosition = GetPosition(levelPanelRect);

        titleGroup = EnsureCanvasGroup(titleText != null ? titleText.gameObject : null);
        startGroup = EnsureCanvasGroup(startButton != null ? startButton.gameObject : null);
        settingsGroup = EnsureCanvasGroup(settingsButton != null ? settingsButton.gameObject : null);
        quitGroup = EnsureCanvasGroup(quitButton != null ? quitButton.gameObject : null);
        levelPanelGroup = EnsureCanvasGroup(levelSelectPanel);
        MakeLevelPanelBackgroundTransparent();
        CacheLevelItems();
    }

    private void CacheLevelItems()
    {
        if (levelPanelRect == null)
        {
            levelItemRects = new RectTransform[0];
            levelItemHomePositions = new Vector2[0];
            levelItemGroups = new CanvasGroup[0];
            return;
        }

        int childCount = levelPanelRect.childCount;
        levelItemRects = new RectTransform[childCount];
        levelItemHomePositions = new Vector2[childCount];
        levelItemGroups = new CanvasGroup[childCount];

        for (int i = 0; i < childCount; i++)
        {
            RectTransform child = levelPanelRect.GetChild(i) as RectTransform;
            levelItemRects[i] = child;
            levelItemHomePositions[i] = GetPosition(child);
            levelItemGroups[i] = child != null ? EnsureCanvasGroup(child.gameObject) : null;
        }
    }

    private void MakeLevelPanelBackgroundTransparent()
    {
        if (levelSelectPanel == null)
        {
            return;
        }

        Image background = levelSelectPanel.GetComponent<Image>();
        if (background == null)
        {
            return;
        }

        Color color = background.color;
        color.a = 0f;
        background.color = color;
        background.raycastTarget = false;
    }

    private void StopTransitionIfRunning()
    {
        if (transitionRoutine == null)
        {
            return;
        }

        StopCoroutine(transitionRoutine);
        transitionRoutine = null;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.AddComponent<CanvasGroup>();
    }

    private static Vector2 GetPosition(RectTransform rect)
    {
        return rect != null ? rect.anchoredPosition : Vector2.zero;
    }

    private static void Move(RectTransform rect, Vector2 position)
    {
        if (rect != null)
        {
            rect.anchoredPosition = position;
        }
    }

    private static void Move(RectTransform rect, Vector2 from, Vector2 to, float t)
    {
        if (rect != null)
        {
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
        }
    }

    private static void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group != null)
        {
            group.alpha = Mathf.Clamp01(alpha);
        }
    }

    private static float GetAlpha(CanvasGroup group)
    {
        return group != null ? group.alpha : 1f;
    }

    private static void SetCanvasInput(CanvasGroup group, bool enabled)
    {
        if (group == null)
        {
            return;
        }

        group.interactable = enabled;
        group.blocksRaycasts = enabled;
    }

    private static void SetCanvasRaycasts(CanvasGroup group, bool enabled)
    {
        if (group != null)
        {
            group.blocksRaycasts = enabled;
        }
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private float GetDelayedT(float elapsed, float delay)
    {
        return Smooth01((elapsed - delay) / Mathf.Max(0.01f, elementMoveDuration));
    }

    private struct LevelButtonParticle
    {
        public SpriteRenderer Renderer;
        public Rigidbody2D Body;
        public RectTransform VisualRect;
        public Image VisualImage;
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
        pixelSprite.name = "Runtime_MenuPixelSprite";
        return pixelSprite;
    }

    private static Material GetPixelRenderMaterial()
    {
        if (pixelRenderMaterial != null)
        {
            return pixelRenderMaterial;
        }

        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        pixelRenderMaterial = new Material(shader)
        {
            name = "Runtime_MenuPixelUnlit"
        };
        if (pixelRenderMaterial.HasProperty("_Color"))
        {
            pixelRenderMaterial.SetColor("_Color", Color.white);
        }
        if (pixelRenderMaterial.HasProperty("_BaseColor"))
        {
            pixelRenderMaterial.SetColor("_BaseColor", Color.white);
        }
        return pixelRenderMaterial;
    }

    private static PhysicsMaterial2D GetPixelPhysicsMaterial()
    {
        if (pixelPhysicsMaterial != null)
        {
            return pixelPhysicsMaterial;
        }

        pixelPhysicsMaterial = new PhysicsMaterial2D("Runtime_PixelPhysics")
        {
            friction = 0.42f,
            bounciness = 0.18f
        };
        return pixelPhysicsMaterial;
    }

    private void FindSceneObjectsIfMissing()
    {
        if (mainMenuPanel == null)
        {
            mainMenuPanel = FindInactiveObject("MainMenuPanel");
        }

        if (levelSelectPanel == null)
        {
            levelSelectPanel = FindInactiveObject("LevelSelectPanel");
        }

        settingsUI = settingsUI != null ? settingsUI : FindObjectOfType<GameSettingsUI>(true);
        startButton = startButton != null ? startButton : FindButton("StartButton");
        settingsButton = settingsButton != null ? settingsButton : FindButton("SettingsButton");
        quitButton = quitButton != null ? quitButton : FindButton("QuitButton");
        quitButton = quitButton != null ? quitButton : FindButton("ExitButton");
        backButton = backButton != null ? backButton : FindButton("BackButton");
        titleText = titleText != null ? titleText : FindText("Title");

        if (levelButtons == null || levelButtons.Length == 0)
        {
            levelButtons = new[]
            {
                FindButton("LevelButton_1"),
                FindButton("LevelButton_2"),
                FindButton("LevelButton_3")
            };
        }

        if (levelLabels == null || levelLabels.Length == 0)
        {
            levelLabels = new[]
            {
                FindText("LevelButton_1_Label"),
                FindText("LevelButton_2_Label"),
                FindText("LevelButton_3_Label")
            };
        }
    }

    private static Button FindButton(string objectName)
    {
        GameObject obj = FindInactiveObject(objectName);
        return obj != null ? obj.GetComponent<Button>() : null;
    }

    private static Text FindText(string objectName)
    {
        GameObject obj = FindInactiveObject(objectName);
        return obj != null ? obj.GetComponent<Text>() : null;
    }

    private static GameObject FindInactiveObject(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current.hideFlags != HideFlags.None || current.name != objectName)
            {
                continue;
            }

            if (!current.gameObject.scene.IsValid())
            {
                continue;
            }

            return current.gameObject;
        }

        return null;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void GrayExitPage();
#else
    private static void GrayExitPage()
    {
    }
#endif
}
