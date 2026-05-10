#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MainMenuSceneBuilder
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string PrefabFolderPath = "Assets/Prefabs";
    private const string GameplayUiPrefabPath = "Assets/Prefabs/GameplayUI.prefab";

    [MenuItem("Tools/Gray/Build Main Menu Scene")]
    public static void Build()
    {
        EditorSceneManager.OpenScene(MainMenuScenePath);

        DeleteSceneObject("Canvas");
        DeleteSceneObject("EventSystem");
        DeleteSceneObject("MainMenuController");

        EnsureCamera();

        MainMenuController controller = new GameObject("MainMenuController").AddComponent<MainMenuController>();
        GameSettingsUI settingsUI = controller.gameObject.AddComponent<GameSettingsUI>();
        Canvas canvas = CreateCanvas();
        CreateEventSystem();

        GameObject mainPanel = CreatePanel(canvas.transform, "MainMenuPanel", true);
        GameObject levelPanel = CreatePanel(canvas.transform, "LevelSelectPanel", false);
        SettingsUiParts settingsParts = CreateSettingsPanel(canvas.transform);

        CreateText(mainPanel.transform, "Title", "GRAY", 88, new Vector2(0f, 140f), new Vector2(700f, 120f), Color.white);
        Button startButton = CreateButton(mainPanel.transform, "StartButton", "START", new Vector2(0f, 35f));
        Button settingsButton = CreateButton(mainPanel.transform, "SettingsButton", "SETTINGS", new Vector2(0f, -41f));
        Button quitButton = CreateButton(mainPanel.transform, "QuitButton", "QUIT", new Vector2(0f, -117f));

        CreateText(levelPanel.transform, "LevelSelectTitle", "SELECT LEVEL", 58, new Vector2(0f, 165f), new Vector2(700f, 90f), Color.white);
        Button levelButton1 = CreateButton(levelPanel.transform, "LevelButton_1", "LEVEL 1", new Vector2(0f, 60f), "LevelButton_1_Label");
        Button levelButton2 = CreateButton(levelPanel.transform, "LevelButton_2", "LEVEL 2", new Vector2(0f, -8f), "LevelButton_2_Label");
        Button unlockAllButton = CreateButton(levelPanel.transform, "UnlockAllButton", "UNLOCK ALL", new Vector2(0f, -88f));
        Button backButton = CreateButton(levelPanel.transform, "BackButton", "BACK", new Vector2(0f, -156f));

        UnityEventTools.AddPersistentListener(startButton.onClick, controller.ShowLevelSelect);
        UnityEventTools.AddPersistentListener(settingsButton.onClick, controller.ShowSettings);
        UnityEventTools.AddPersistentListener(quitButton.onClick, controller.QuitGame);
        UnityEventTools.AddPersistentListener(levelButton1.onClick, controller.LoadLevel1);
        UnityEventTools.AddPersistentListener(levelButton2.onClick, controller.LoadLevel2);
        UnityEventTools.AddPersistentListener(unlockAllButton.onClick, controller.UnlockAll);
        UnityEventTools.AddPersistentListener(backButton.onClick, controller.ShowMainMenu);
        UnityEventTools.AddPersistentListener(settingsParts.backButton.onClick, settingsUI.Close);
        UnityEventTools.AddPersistentListener(settingsParts.masterVolumeSlider.onValueChanged, settingsUI.SetMasterVolume);
        UnityEventTools.AddPersistentListener(settingsParts.fullscreenToggle.onValueChanged, settingsUI.SetFullscreen);

        AssignControllerReferences(
            controller,
            mainPanel,
            levelPanel,
            settingsUI,
            startButton,
            settingsButton,
            quitButton,
            backButton,
            unlockAllButton,
            new[] { levelButton1, levelButton2 },
            new[]
            {
                levelButton1.transform.Find("LevelButton_1_Label").GetComponent<Text>(),
                levelButton2.transform.Find("LevelButton_2_Label").GetComponent<Text>()
            });
        AssignSettingsReferences(settingsUI, settingsParts, mainPanel);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Gray MainMenu scene rebuilt with real scene buttons.");
    }

    [MenuItem("Tools/Gray/Build Pause UI In Open Scene")]
    public static void BuildPauseUiInOpenScene()
    {
        DeleteSceneObject("PauseCanvas");
        DeleteSceneObject("PauseMenuController");
        DeleteSceneObject("WinPanelController");
        DeleteSceneObject("EventSystem");

        PauseMenuController controller = new GameObject("PauseMenuController").AddComponent<PauseMenuController>();
        GameSettingsUI settingsUI = controller.gameObject.AddComponent<GameSettingsUI>();
        WinPanelController winController = controller.gameObject.AddComponent<WinPanelController>();
        Canvas canvas = CreateCanvas("PauseCanvas", 200);
        CreateEventSystem();

        GameObject pausePanel = CreatePanel(canvas.transform, "PausePanel", false);
        SettingsUiParts settingsParts = CreateSettingsPanel(canvas.transform);
        WinUiParts winParts = CreateWinPanel(canvas.transform);

        CreateText(pausePanel.transform, "PauseTitle", "PAUSED", 64, new Vector2(0f, 160f), new Vector2(700f, 100f), Color.white);
        Button resumeButton = CreateButton(pausePanel.transform, "ResumeButton", "RESUME", new Vector2(0f, 60f));
        Button restartButton = CreateButton(pausePanel.transform, "RestartButton", "RESTART", new Vector2(0f, -8f));
        Button settingsButton = CreateButton(pausePanel.transform, "PauseSettingsButton", "SETTINGS", new Vector2(0f, -76f));
        Button mainMenuButton = CreateButton(pausePanel.transform, "MainMenuButton", "MAIN MENU", new Vector2(0f, -144f));

        UnityEventTools.AddPersistentListener(resumeButton.onClick, controller.Resume);
        UnityEventTools.AddPersistentListener(restartButton.onClick, controller.Restart);
        UnityEventTools.AddPersistentListener(settingsButton.onClick, controller.OpenSettings);
        UnityEventTools.AddPersistentListener(mainMenuButton.onClick, controller.ReturnToMainMenu);
        UnityEventTools.AddPersistentListener(settingsParts.backButton.onClick, settingsUI.Close);
        UnityEventTools.AddPersistentListener(settingsParts.masterVolumeSlider.onValueChanged, settingsUI.SetMasterVolume);
        UnityEventTools.AddPersistentListener(settingsParts.fullscreenToggle.onValueChanged, settingsUI.SetFullscreen);
        UnityEventTools.AddPersistentListener(winParts.mainMenuButton.onClick, winController.ReturnToMainMenu);
        UnityEventTools.AddPersistentListener(winParts.retryButton.onClick, winController.Retry);
        UnityEventTools.AddPersistentListener(winParts.nextLevelButton.onClick, winController.NextLevel);

        AssignPauseControllerReferences(controller, pausePanel, settingsUI, resumeButton, restartButton, settingsButton, mainMenuButton);
        AssignSettingsReferences(settingsUI, settingsParts, pausePanel);
        AssignWinControllerReferences(winController, winParts);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Gray pause, settings, and win UI rebuilt in the open scene with real scene buttons.");
    }

    [MenuItem("Tools/Gray/Build Gameplay UI In All Gameplay Scenes")]
    public static void BuildGameplayUiInAllGameplayScenes()
    {
        for (int buildIndex = SceneFlow.FirstGameplayBuildIndex; buildIndex < EditorBuildSettings.scenes.Length; buildIndex++)
        {
            EditorBuildSettingsScene buildScene = EditorBuildSettings.scenes[buildIndex];
            if (buildScene == null || !buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
            {
                continue;
            }

            EditorSceneManager.OpenScene(buildScene.path);
            BuildPauseUiInOpenScene();
        }

        Debug.Log("Gray gameplay UI rebuilt in all enabled gameplay scenes.");
    }

    [MenuItem("Tools/Gray/Build Gameplay UI Prefab")]
    public static void BuildGameplayUiPrefab()
    {
        EnsurePrefabFolder();

        GameObject root = new GameObject("GameplayUI");
        PauseMenuController controller = root.AddComponent<PauseMenuController>();
        GameSettingsUI settingsUI = root.AddComponent<GameSettingsUI>();
        WinPanelController winController = root.AddComponent<WinPanelController>();

        Canvas canvas = CreateCanvas("PauseCanvas", 200);
        canvas.transform.SetParent(root.transform, false);
        CreateEventSystem(root.transform);

        GameObject pausePanel = CreatePanel(canvas.transform, "PausePanel", false);
        SettingsUiParts settingsParts = CreateSettingsPanel(canvas.transform);
        WinUiParts winParts = CreateWinPanel(canvas.transform);

        CreateText(pausePanel.transform, "PauseTitle", "PAUSED", 64, new Vector2(0f, 160f), new Vector2(700f, 100f), Color.white);
        Button resumeButton = CreateButton(pausePanel.transform, "ResumeButton", "RESUME", new Vector2(0f, 60f));
        Button restartButton = CreateButton(pausePanel.transform, "RestartButton", "RESTART", new Vector2(0f, -8f));
        Button settingsButton = CreateButton(pausePanel.transform, "PauseSettingsButton", "SETTINGS", new Vector2(0f, -76f));
        Button mainMenuButton = CreateButton(pausePanel.transform, "MainMenuButton", "MAIN MENU", new Vector2(0f, -144f));

        UnityEventTools.AddPersistentListener(resumeButton.onClick, controller.Resume);
        UnityEventTools.AddPersistentListener(restartButton.onClick, controller.Restart);
        UnityEventTools.AddPersistentListener(settingsButton.onClick, controller.OpenSettings);
        UnityEventTools.AddPersistentListener(mainMenuButton.onClick, controller.ReturnToMainMenu);
        UnityEventTools.AddPersistentListener(settingsParts.backButton.onClick, settingsUI.Close);
        UnityEventTools.AddPersistentListener(settingsParts.masterVolumeSlider.onValueChanged, settingsUI.SetMasterVolume);
        UnityEventTools.AddPersistentListener(settingsParts.fullscreenToggle.onValueChanged, settingsUI.SetFullscreen);
        UnityEventTools.AddPersistentListener(winParts.mainMenuButton.onClick, winController.ReturnToMainMenu);
        UnityEventTools.AddPersistentListener(winParts.retryButton.onClick, winController.Retry);
        UnityEventTools.AddPersistentListener(winParts.nextLevelButton.onClick, winController.NextLevel);

        AssignPauseControllerReferences(controller, pausePanel, settingsUI, resumeButton, restartButton, settingsButton, mainMenuButton);
        AssignSettingsReferences(settingsUI, settingsParts, pausePanel);
        AssignWinControllerReferences(winController, winParts);

        PrefabUtility.SaveAsPrefabAsset(root, GameplayUiPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Gray gameplay UI prefab saved to {GameplayUiPrefabPath}.");
    }

    private static void AssignControllerReferences(
        MainMenuController controller,
        GameObject mainPanel,
        GameObject levelPanel,
        GameSettingsUI settingsUI,
        Button startButton,
        Button settingsButton,
        Button quitButton,
        Button backButton,
        Button unlockAllButton,
        Button[] levelButtons,
        Text[] levelLabels)
    {
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("mainMenuPanel").objectReferenceValue = mainPanel;
        serialized.FindProperty("levelSelectPanel").objectReferenceValue = levelPanel;
        serialized.FindProperty("settingsUI").objectReferenceValue = settingsUI;
        serialized.FindProperty("startButton").objectReferenceValue = startButton;
        serialized.FindProperty("settingsButton").objectReferenceValue = settingsButton;
        serialized.FindProperty("quitButton").objectReferenceValue = quitButton;
        serialized.FindProperty("backButton").objectReferenceValue = backButton;
        serialized.FindProperty("unlockAllButton").objectReferenceValue = unlockAllButton;

        SerializedProperty buttonsProperty = serialized.FindProperty("levelButtons");
        buttonsProperty.arraySize = levelButtons.Length;
        for (int i = 0; i < levelButtons.Length; i++)
        {
            buttonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = levelButtons[i];
        }

        SerializedProperty labelsProperty = serialized.FindProperty("levelLabels");
        labelsProperty.arraySize = levelLabels.Length;
        for (int i = 0; i < levelLabels.Length; i++)
        {
            labelsProperty.GetArrayElementAtIndex(i).objectReferenceValue = levelLabels[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignPauseControllerReferences(
        PauseMenuController controller,
        GameObject pausePanel,
        GameSettingsUI settingsUI,
        Button resumeButton,
        Button restartButton,
        Button settingsButton,
        Button mainMenuButton)
    {
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("pausePanel").objectReferenceValue = pausePanel;
        serialized.FindProperty("settingsUI").objectReferenceValue = settingsUI;
        serialized.FindProperty("resumeButton").objectReferenceValue = resumeButton;
        serialized.FindProperty("restartButton").objectReferenceValue = restartButton;
        serialized.FindProperty("settingsButton").objectReferenceValue = settingsButton;
        serialized.FindProperty("mainMenuButton").objectReferenceValue = mainMenuButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignSettingsReferences(GameSettingsUI settingsUI, SettingsUiParts parts, GameObject defaultReturnPanel)
    {
        SerializedObject serialized = new SerializedObject(settingsUI);
        serialized.FindProperty("settingsPanel").objectReferenceValue = parts.panel;
        serialized.FindProperty("defaultReturnPanel").objectReferenceValue = defaultReturnPanel;
        serialized.FindProperty("backButton").objectReferenceValue = parts.backButton;
        serialized.FindProperty("masterVolumeSlider").objectReferenceValue = parts.masterVolumeSlider;
        serialized.FindProperty("masterVolumeLabel").objectReferenceValue = parts.masterVolumeLabel;
        serialized.FindProperty("fullscreenToggle").objectReferenceValue = parts.fullscreenToggle;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignWinControllerReferences(WinPanelController controller, WinUiParts parts)
    {
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("winPanel").objectReferenceValue = parts.panel;
        serialized.FindProperty("mainMenuButton").objectReferenceValue = parts.mainMenuButton;
        serialized.FindProperty("retryButton").objectReferenceValue = parts.retryButton;
        serialized.FindProperty("nextLevelButton").objectReferenceValue = parts.nextLevelButton;
        serialized.FindProperty("nextLevelLabel").objectReferenceValue = parts.nextLevelLabel;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Canvas CreateCanvas(string name = "Canvas", int sortingOrder = 100)
    {
        GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static GameObject CreatePanel(Transform parent, string name, bool active)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.94f);
        panel.SetActive(active);
        return panel;
    }

    private static SettingsUiParts CreateSettingsPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "SettingsPanel", false);
        CreateText(panel.transform, "SettingsTitle", "SETTINGS", 58, new Vector2(0f, 165f), new Vector2(700f, 90f), Color.white);

        Text volumeLabel = CreateText(panel.transform, "MasterVolumeLabel", "VOLUME 100%", 26, new Vector2(0f, 65f), new Vector2(420f, 44f), Color.white);
        Slider volumeSlider = CreateSlider(panel.transform, "MasterVolumeSlider", new Vector2(0f, 15f));
        Toggle fullscreenToggle = CreateToggle(panel.transform, "FullscreenToggle", "FULLSCREEN", new Vector2(0f, -62f));
        Button backButton = CreateButton(panel.transform, "SettingsBackButton", "BACK", new Vector2(0f, -150f));

        return new SettingsUiParts
        {
            panel = panel,
            backButton = backButton,
            masterVolumeSlider = volumeSlider,
            masterVolumeLabel = volumeLabel,
            fullscreenToggle = fullscreenToggle
        };
    }

    private static WinUiParts CreateWinPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "WinPanel", false);
        CreateText(panel.transform, "WinTitle", "LEVEL CLEAR", 64, new Vector2(0f, 160f), new Vector2(700f, 100f), Color.white);
        Button nextLevelButton = CreateButton(panel.transform, "WinNextLevelButton", "NEXT LEVEL", new Vector2(0f, 56f), "WinNextLevelButton_Label");
        Button retryButton = CreateButton(panel.transform, "WinRetryButton", "RETRY", new Vector2(0f, -16f));
        Button mainMenuButton = CreateButton(panel.transform, "WinMainMenuButton", "MAIN MENU", new Vector2(0f, -88f));

        return new WinUiParts
        {
            panel = panel,
            mainMenuButton = mainMenuButton,
            retryButton = retryButton,
            nextLevelButton = nextLevelButton,
            nextLevelLabel = nextLevelButton.transform.Find("WinNextLevelButton_Label").GetComponent<Text>()
        };
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position, string labelObjectName = null)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, 54f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.pressedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        colors.disabledColor = new Color(0.16f, 0.16f, 0.16f, 0.9f);
        button.colors = colors;

        CreateText(
            buttonObject.transform,
            labelObjectName ?? $"{name}_Label",
            label,
            24,
            Vector2.zero,
            rect.sizeDelta,
            Color.black);
        return button;
    }

    private static Slider CreateSlider(Transform parent, string name, Vector2 position)
    {
        GameObject sliderObject = new GameObject(name, typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);

        RectTransform rect = sliderObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, 28f);

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(sliderObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.35f);
        backgroundRect.anchorMax = new Vector2(1f, 0.65f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        backgroundObject.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.22f, 1f);

        GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(8f, 0f);
        fillAreaRect.offsetMax = new Vector2(-8f, 0f);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.25f);
        fillRect.anchorMax = new Vector2(1f, 0.75f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillObject.GetComponent<Image>().color = Color.white;

        GameObject handleAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleAreaObject.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(8f, 0f);
        handleAreaRect.offsetMax = new Vector2(-8f, 0f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handleObject.transform.SetParent(handleAreaObject.transform, false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(24f, 34f);
        handleObject.GetComponent<Image>().color = Color.white;

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleObject.GetComponent<Image>();
        return slider;
    }

    private static Toggle CreateToggle(Transform parent, string name, string label, Vector2 position)
    {
        GameObject toggleObject = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        toggleObject.transform.SetParent(parent, false);

        RectTransform rect = toggleObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, 44f);

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(toggleObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(18f, 0f);
        backgroundRect.sizeDelta = new Vector2(32f, 32f);
        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = Color.white;

        GameObject checkmarkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        checkmarkObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
        checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;
        Image checkmarkImage = checkmarkObject.GetComponent<Image>();
        checkmarkImage.color = Color.black;

        CreateText(toggleObject.transform, $"{name}_Label", label, 24, new Vector2(58f, 0f), new Vector2(280f, 44f), Color.white)
            .alignment = TextAnchor.MiddleLeft;

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkmarkImage;
        toggle.isOn = true;
        return toggle;
    }

    private static Text CreateText(Transform parent, string name, string text, int size, Vector2 position, Vector2 dimensions, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.font = GetDefaultFont();
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = color;
        return label;
    }

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private static void EnsureCamera()
    {
        if (Camera.main != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
    }

    private static GameObject CreateEventSystem(Transform parent = null)
    {
        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        if (parent != null)
        {
            eventSystemObject.transform.SetParent(parent, false);
        }

        eventSystemObject.transform.position = Vector3.zero;
        return eventSystemObject;
    }

    private static void EnsurePrefabFolder()
    {
        if (Directory.Exists(PrefabFolderPath))
        {
            return;
        }

        Directory.CreateDirectory(PrefabFolderPath);
        AssetDatabase.Refresh();
    }

    private static void DeleteSceneObject(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = objects.Length - 1; i >= 0; i--)
        {
            GameObject obj = objects[i];
            if (obj.name != objectName || !obj.scene.IsValid())
            {
                continue;
            }

            Object.DestroyImmediate(obj);
        }
    }

    private struct SettingsUiParts
    {
        public GameObject panel;
        public Button backButton;
        public Slider masterVolumeSlider;
        public Text masterVolumeLabel;
        public Toggle fullscreenToggle;
    }

    private struct WinUiParts
    {
        public GameObject panel;
        public Button mainMenuButton;
        public Button retryButton;
        public Button nextLevelButton;
        public Text nextLevelLabel;
    }
}
#endif
