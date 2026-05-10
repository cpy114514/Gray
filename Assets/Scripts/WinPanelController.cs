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

    public bool IsShowing => winPanel != null && winPanel.activeSelf;

    private void Awake()
    {
        FindSceneObjectsIfMissing();
        WireButtons();
        HideWinPanel();
    }

    public void ShowWin()
    {
        SceneFlow.UnlockNextLevelFromCurrent();
        Time.timeScale = 0f;
        RefreshNextLevelButton();

        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneFlow.ReturnToMainMenu();
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        SceneFlow.RestartLevel();
    }

    public void NextLevel()
    {
        Time.timeScale = 1f;
        SceneFlow.LoadNextLevel();
    }

    private void HideWinPanel()
    {
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

    private static bool ShouldAutoWire(Button button)
    {
        return button != null && button.onClick.GetPersistentEventCount() == 0;
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
