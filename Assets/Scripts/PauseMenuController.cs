using UnityEngine;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameSettingsUI settingsUI;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

    private WinPanelController winPanelController;
    private bool paused;
    private bool returningToMainMenu;

    private void Awake()
    {
        FindSceneObjectsIfMissing();
        WireButtons();
        HidePausePanel();
    }

    private void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            HandlePauseKey();
        }
    }

    private void HandlePauseKey()
    {
        if (winPanelController != null && winPanelController.IsShowing)
        {
            return;
        }

        if (settingsUI != null && settingsUI.IsOpen)
        {
            settingsUI.Close();
            paused = true;
            return;
        }

        TogglePause();
    }

    public void TogglePause()
    {
        if (paused)
        {
            Resume();
            return;
        }

        Pause();
    }

    public void Pause()
    {
        paused = true;
        Time.timeScale = 0f;

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
    }

    public void Resume()
    {
        paused = false;
        Time.timeScale = 1f;

        if (settingsUI != null && settingsUI.IsOpen)
        {
            settingsUI.Close();
        }

        HidePausePanel();
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneFlow.RestartLevel();
    }

    public void OpenSettings()
    {
        if (settingsUI != null)
        {
            settingsUI.OpenFromPanel(pausePanel);
        }
    }

    public void ReturnToMainMenu()
    {
        if (returningToMainMenu)
        {
            return;
        }

        returningToMainMenu = true;
        paused = false;
        Time.timeScale = 1f;
        HidePausePanel();
        if (settingsUI != null && settingsUI.IsOpen)
        {
            settingsUI.Close();
        }

        if (!SceneShatterTransition.PlayToMainMenu(this))
        {
            SceneFlow.ReturnToMainMenu();
        }
    }

    private void HidePausePanel()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    private void WireButtons()
    {
        if (ShouldAutoWire(resumeButton))
        {
            resumeButton.onClick.AddListener(Resume);
        }

        if (ShouldAutoWire(restartButton))
        {
            restartButton.onClick.AddListener(Restart);
        }

        if (ShouldAutoWire(settingsButton))
        {
            settingsButton.onClick.AddListener(OpenSettings);
        }

        if (ShouldAutoWire(mainMenuButton))
        {
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    private void FindSceneObjectsIfMissing()
    {
        pausePanel = pausePanel != null ? pausePanel : FindInactiveObject("PausePanel");
        settingsUI = settingsUI != null ? settingsUI : FindObjectOfType<GameSettingsUI>(true);
        winPanelController = winPanelController != null ? winPanelController : FindObjectOfType<WinPanelController>(true);
        resumeButton = resumeButton != null ? resumeButton : FindButton("ResumeButton");
        restartButton = restartButton != null ? restartButton : FindButton("RestartButton");
        settingsButton = settingsButton != null ? settingsButton : FindButton("PauseSettingsButton");
        mainMenuButton = mainMenuButton != null ? mainMenuButton : FindButton("MainMenuButton");
    }

    private static bool ShouldAutoWire(Button button)
    {
        return button != null && button.onClick.GetPersistentEventCount() == 0;
    }

    private static Button FindButton(string objectName)
    {
        GameObject obj = FindInactiveObject(objectName);
        return obj != null ? obj.GetComponent<Button>() : null;
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
