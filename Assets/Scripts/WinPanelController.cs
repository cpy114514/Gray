using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WinPanelController : MonoBehaviour
{
    [SerializeField] private GameObject winPanel;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Text nextLevelLabel;

    [Header("Floating Win Buttons")]
    [SerializeField] private bool useFloatingButtons = true;
    [SerializeField] private bool hidePanelBackground = true;
    [SerializeField] private bool hideWinTitle = true;
    [SerializeField] private Vector2 bottomRightPadding = new Vector2(36f, 38f);
    [SerializeField] private Vector2 floatingButtonSize = new Vector2(280f, 44f);
    [SerializeField] private float buttonSpacing = 56f;
    [SerializeField] private float floatInDistance = 92f;
    [SerializeField] private float floatInDuration = 0.34f;
    [SerializeField] private float floatInStagger = 0.055f;
    [SerializeField] private string winTitleObjectName = "WinTitle";

    [Header("Retry Effect")]
    [SerializeField] private float retryZoomDuration = 0.38f;
    [SerializeField] private float retryZoomOrthographicSize = 4.2f;
    [SerializeField] private Vector2 retryCameraOffset = new Vector2(0f, 0.35f);

    public bool IsShowing => winPanel != null && winPanel.activeSelf;

    private readonly Dictionary<Button, CanvasGroup> buttonGroups = new Dictionary<Button, CanvasGroup>();
    private Coroutine floatingButtonsRoutine;
    private bool isLeavingToNextLevel;
    private bool isRetryingLevel;
    private bool isReturningToMainMenu;
    private CameraFollow2D retryCameraFollow;

    private void Awake()
    {
        FindSceneObjectsIfMissing();
        WireButtons();
        ApplyFloatingPanelStyle();
        HideWinPanel();
    }

    public void ShowWin()
    {
        isRetryingLevel = false;
        isLeavingToNextLevel = false;
        isReturningToMainMenu = false;
        SceneFlow.UnlockNextLevelFromCurrent();
        Time.timeScale = 0f;
        SetWinButtonsInteractable(true);
        RefreshNextLevelButton();

        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }

        if (useFloatingButtons)
        {
            ShowFloatingButtons();
        }
    }

    public void ReturnToMainMenu()
    {
        if (isReturningToMainMenu)
        {
            return;
        }

        isReturningToMainMenu = true;
        Time.timeScale = 1f;
        SetWinButtonsInteractable(false);
        HideWinPanel();

        if (!SceneShatterTransition.PlayToMainMenu(this))
        {
            SceneFlow.ReturnToMainMenu();
        }
    }

    public void Retry()
    {
        if (isRetryingLevel)
        {
            return;
        }

        isRetryingLevel = true;
        SetWinButtonsInteractable(false);
        HideWinPanel();
        StartCoroutine(PlayRetryEffectThenRestart());
    }

    public void NextLevel()
    {
        if (isLeavingToNextLevel)
        {
            return;
        }

        isLeavingToNextLevel = true;
        SetWinButtonsInteractable(false);
        HideWinPanel();

        PlayerController2D player = FindObjectOfType<PlayerController2D>();
        if (player == null)
        {
            Time.timeScale = 1f;
            SceneFlow.LoadNextLevel();
            return;
        }

        PlayerLevelTransitionShatterEffect effect = player.GetComponent<PlayerLevelTransitionShatterEffect>();
        if (effect == null)
        {
            effect = player.gameObject.AddComponent<PlayerLevelTransitionShatterEffect>();
        }

        if (!effect.Play(SceneFlow.LoadNextLevel))
        {
            Time.timeScale = 1f;
            SceneFlow.LoadNextLevel();
        }
    }

    private void HideWinPanel()
    {
        if (floatingButtonsRoutine != null)
        {
            StopCoroutine(floatingButtonsRoutine);
            floatingButtonsRoutine = null;
        }

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }
    }

    private void RefreshNextLevelButton()
    {
        int nextBuildIndex = SceneManager.GetActiveScene().buildIndex + 1;
        bool hasNextLevel = nextBuildIndex >= SceneFlow.FirstGameplayBuildIndex &&
                            nextBuildIndex < SceneManager.sceneCountInBuildSettings;

        if (nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(hasNextLevel);
            nextLevelButton.interactable = hasNextLevel;
        }

        if (nextLevelLabel != null && hasNextLevel)
        {
            nextLevelLabel.text = "NEXT LEVEL";
        }
    }

    private void WireButtons()
    {
        if (ShouldAutoWire(mainMenuButton))
        {
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }

        if (ShouldAutoWire(retryButton))
        {
            retryButton.onClick.AddListener(Retry);
        }

        if (ShouldAutoWire(nextLevelButton))
        {
            nextLevelButton.onClick.AddListener(NextLevel);
        }
    }

    private void FindSceneObjectsIfMissing()
    {
        winPanel = winPanel != null ? winPanel : FindInactiveObject("WinPanel");
        mainMenuButton = mainMenuButton != null ? mainMenuButton : FindButton("WinMainMenuButton");
        retryButton = retryButton != null ? retryButton : FindButton("WinRetryButton");
        nextLevelButton = nextLevelButton != null ? nextLevelButton : FindButton("WinNextLevelButton");
        nextLevelLabel = nextLevelLabel != null ? nextLevelLabel : FindText("WinNextLevelButton_Label");
    }

    private void ApplyFloatingPanelStyle()
    {
        if (!useFloatingButtons || winPanel == null)
        {
            return;
        }

        if (hidePanelBackground)
        {
            Image panelImage = winPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.enabled = false;
                panelImage.raycastTarget = false;
            }
        }

        if (hideWinTitle)
        {
            GameObject titleObject = FindInactiveObject(winTitleObjectName);
            if (titleObject != null)
            {
                titleObject.SetActive(false);
            }
        }
    }

    private void ShowFloatingButtons()
    {
        ApplyFloatingPanelStyle();
        Button[] visibleButtons = GetVisibleFloatingButtons();

        for (int i = 0; i < visibleButtons.Length; i++)
        {
            PrepareFloatingButton(visibleButtons[i], i);
        }

        if (floatingButtonsRoutine != null)
        {
            StopCoroutine(floatingButtonsRoutine);
        }

        floatingButtonsRoutine = StartCoroutine(AnimateFloatingButtons(visibleButtons));
    }

    private Button[] GetVisibleFloatingButtons()
    {
        List<Button> buttons = new List<Button>(3);
        AddIfVisible(buttons, mainMenuButton);
        AddIfVisible(buttons, retryButton);
        AddIfVisible(buttons, nextLevelButton);
        return buttons.ToArray();
    }

    private static void AddIfVisible(List<Button> buttons, Button button)
    {
        if (button != null && button.gameObject.activeSelf)
        {
            buttons.Add(button);
        }
    }

    private void PrepareFloatingButton(Button button, int index)
    {
        RectTransform rect = button.transform as RectTransform;
        if (rect == null)
        {
            return;
        }

        Vector2 finalPosition = GetFloatingButtonFinalPosition(index);
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = floatingButtonSize;
        rect.anchoredPosition = finalPosition - new Vector2(0f, floatInDistance);
        ResizeButtonLabels(button);

        CanvasGroup group = GetButtonCanvasGroup(button);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private IEnumerator AnimateFloatingButtons(Button[] buttons)
    {
        float duration = Mathf.Max(0.01f, floatInDuration);
        float totalDuration = duration + Mathf.Max(0f, floatInStagger) * Mathf.Max(0, buttons.Length - 1);
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < buttons.Length; i++)
            {
                AnimateFloatingButton(buttons[i], i, elapsed, duration);
            }

            yield return null;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            SetFloatingButtonFinal(buttons[i], i);
        }

        floatingButtonsRoutine = null;
    }

    private void AnimateFloatingButton(Button button, int index, float elapsed, float duration)
    {
        RectTransform rect = button != null ? button.transform as RectTransform : null;
        if (rect == null)
        {
            return;
        }

        float delay = Mathf.Max(0f, floatInStagger) * index;
        float t = Smooth01((elapsed - delay) / duration);
        Vector2 finalPosition = GetFloatingButtonFinalPosition(index);
        Vector2 startPosition = finalPosition - new Vector2(0f, floatInDistance);
        rect.anchoredPosition = Vector2.Lerp(startPosition, finalPosition, t);

        CanvasGroup group = GetButtonCanvasGroup(button);
        group.alpha = t;
    }

    private void SetFloatingButtonFinal(Button button, int index)
    {
        RectTransform rect = button != null ? button.transform as RectTransform : null;
        if (rect != null)
        {
            rect.sizeDelta = floatingButtonSize;
            rect.anchoredPosition = GetFloatingButtonFinalPosition(index);
            ResizeButtonLabels(button);
        }

        CanvasGroup group = GetButtonCanvasGroup(button);
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private void SetWinButtonsInteractable(bool interactable)
    {
        SetButtonInteractable(mainMenuButton, interactable);
        SetButtonInteractable(retryButton, interactable);
        SetButtonInteractable(nextLevelButton, interactable);
    }

    private IEnumerator PlayRetryEffectThenRestart()
    {
        Time.timeScale = 1f;

        PlayerController2D player = FindObjectOfType<PlayerController2D>();
        if (player == null)
        {
            SceneFlow.RestartLevel();
            yield break;
        }

        yield return ZoomCameraToPlayer(player.transform.position);
        if (retryCameraFollow != null)
        {
            retryCameraFollow.enabled = true;
            retryCameraFollow.SetTarget(player.transform, false);
        }

        PlayerDeathRespawnEffect deathEffect = player.GetComponent<PlayerDeathRespawnEffect>();
        if (deathEffect == null)
        {
            deathEffect = player.gameObject.AddComponent<PlayerDeathRespawnEffect>();
        }

        if (!deathEffect.Play(FinishRetryRespawn))
        {
            FinishRetryRespawn();
        }
    }

    private void FinishRetryRespawn()
    {
        isRetryingLevel = false;
        Time.timeScale = 1f;
        RestoreGameplayAfterRetry();
    }

    private void RestoreGameplayAfterRetry()
    {
        PlayerController2D player = FindObjectOfType<PlayerController2D>();
        if (player != null)
        {
            player.SetControlEnabled(true);
        }

        RestoreCameraAfterRetry(player);
        RestoreMiniMaps();
        ResetLevelGoals();
    }

    private void RestoreCameraAfterRetry(PlayerController2D player)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        CameraFollow2D cameraFollow = mainCamera.GetComponent<CameraFollow2D>();
        if (cameraFollow == null)
        {
            return;
        }

        cameraFollow.enabled = true;
        cameraFollow.RestoreDefaultZoom();
        if (player != null)
        {
            cameraFollow.SetTarget(player.transform, false);
        }
    }

    private static void RestoreMiniMaps()
    {
        MiniMapController2D[] miniMaps = Resources.FindObjectsOfTypeAll<MiniMapController2D>();
        for (int i = 0; i < miniMaps.Length; i++)
        {
            MiniMapController2D miniMap = miniMaps[i];
            if (miniMap == null || miniMap.hideFlags != HideFlags.None || !miniMap.gameObject.scene.IsValid())
            {
                continue;
            }

            Transform current = miniMap.transform;
            while (current != null)
            {
                current.gameObject.SetActive(true);
                current = current.parent;
            }
        }
    }

    private static void ResetLevelGoals()
    {
        LevelGoal[] goals = FindObjectsOfType<LevelGoal>(true);
        for (int i = 0; i < goals.Length; i++)
        {
            if (goals[i] != null)
            {
                goals[i].ResetGoal();
            }
        }
    }

    private IEnumerator ZoomCameraToPlayer(Vector3 playerPosition)
    {
        retryCameraFollow = null;
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            yield break;
        }

        retryCameraFollow = mainCamera.GetComponent<CameraFollow2D>();
        if (retryCameraFollow != null)
        {
            retryCameraFollow.enabled = false;
        }

        Vector3 startPosition = mainCamera.transform.position;
        float startSize = mainCamera.orthographicSize;
        Vector3 targetPosition = new Vector3(
            playerPosition.x + retryCameraOffset.x,
            playerPosition.y + retryCameraOffset.y,
            startPosition.z);
        float targetSize = Mathf.Max(0.5f, retryZoomOrthographicSize);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, retryZoomDuration);
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

    private Vector2 GetFloatingButtonFinalPosition(int index)
    {
        return new Vector2(-bottomRightPadding.x, bottomRightPadding.y + buttonSpacing * index);
    }

    private void ResizeButtonLabels(Button button)
    {
        if (button == null)
        {
            return;
        }

        Text[] labels = button.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            RectTransform labelRect = labels[i].transform as RectTransform;
            if (labelRect == null)
            {
                continue;
            }

            labelRect.sizeDelta = floatingButtonSize;
            labels[i].fontSize = Mathf.Min(labels[i].fontSize, 22);
        }
    }

    private CanvasGroup GetButtonCanvasGroup(Button button)
    {
        if (button == null)
        {
            return null;
        }

        if (!buttonGroups.TryGetValue(button, out CanvasGroup group) || group == null)
        {
            group = button.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = button.gameObject.AddComponent<CanvasGroup>();
            }

            buttonGroups[button] = group;
        }

        return group;
    }

    private static bool ShouldAutoWire(Button button)
    {
        return button != null && button.onClick.GetPersistentEventCount() == 0;
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
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
}
