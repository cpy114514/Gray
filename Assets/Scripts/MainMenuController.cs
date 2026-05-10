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
    [SerializeField] private Button unlockAllButton;

    [Header("Level buttons built in scene")]
    [SerializeField] private int firstLevelBuildIndex = SceneFlow.FirstGameplayBuildIndex;
    [SerializeField] private Button[] levelButtons;
    [SerializeField] private Text[] levelLabels;

    private void Awake()
    {
        SceneFlow.EnsureInitialUnlock();
        FindSceneObjectsIfMissing();
        WireButtons();
        ShowMainMenu();
        RefreshLevelButtons();
    }

    public void ShowMainMenu()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }

        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(false);
        }
    }

    public void ShowLevelSelect()
    {
        RefreshLevelButtons();

        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }

        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(true);
        }
    }

    public void LoadLevel1()
    {
        SceneFlow.LoadLevel(firstLevelBuildIndex);
    }

    public void LoadLevel2()
    {
        SceneFlow.LoadLevel(firstLevelBuildIndex + 1);
    }

    public void LoadLevel3()
    {
        SceneFlow.LoadLevel(firstLevelBuildIndex + 2);
    }

    public void UnlockAll()
    {
        SceneFlow.UnlockAllLevels();
        RefreshLevelButtons();
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
#else
        Application.Quit();
#endif
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

        if (ShouldAutoWire(unlockAllButton))
        {
            unlockAllButton.onClick.AddListener(UnlockAll);
        }

        for (int i = 0; i < levelButtons.Length; i++)
        {
            int buildIndex = firstLevelBuildIndex + i;
            if (!ShouldAutoWire(levelButtons[i]))
            {
                continue;
            }

            levelButtons[i].onClick.AddListener(() => SceneFlow.LoadLevel(buildIndex));
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
                int levelNumber = i + 1;
                levelLabels[i].text = unlocked ? $"LEVEL {levelNumber}" : $"LEVEL {levelNumber} LOCKED";
            }
        }
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
        backButton = backButton != null ? backButton : FindButton("BackButton");
        unlockAllButton = unlockAllButton != null ? unlockAllButton : FindButton("UnlockAllButton");

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
}
