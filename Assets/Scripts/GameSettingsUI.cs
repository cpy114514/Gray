using UnityEngine;
using UnityEngine.UI;

public class GameSettingsUI : MonoBehaviour
{
    private const string MasterVolumeKey = "Gray.MasterVolume";
    private const string FullscreenKey = "Gray.Fullscreen";

    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject defaultReturnPanel;
    [SerializeField] private Button backButton;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Text masterVolumeLabel;
    [SerializeField] private Toggle fullscreenToggle;

    private GameObject returnPanel;

    public bool IsOpen => settingsPanel != null && settingsPanel.activeSelf;

    private void Awake()
    {
        FindSceneObjectsIfMissing();
        WireControls();
        LoadSettingsIntoControls();

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void OpenFromDefaultReturn()
    {
        OpenFromPanel(defaultReturnPanel);
    }

    public void OpenFromPanel(GameObject panel)
    {
        returnPanel = panel != null ? panel : defaultReturnPanel;

        if (returnPanel != null)
        {
            returnPanel.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void Close()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        GameObject targetPanel = returnPanel != null ? returnPanel : defaultReturnPanel;
        if (targetPanel != null)
        {
            targetPanel.SetActive(true);
        }
    }

    public void SetMasterVolume(float value)
    {
        float volume = Mathf.Clamp01(value);
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(MasterVolumeKey, volume);
        PlayerPrefs.Save();
        UpdateVolumeLabel(volume);
    }

    public void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void WireControls()
    {
        if (backButton != null && backButton.onClick.GetPersistentEventCount() == 0)
        {
            backButton.onClick.AddListener(Close);
        }

        if (masterVolumeSlider != null && masterVolumeSlider.onValueChanged.GetPersistentEventCount() == 0)
        {
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (fullscreenToggle != null && fullscreenToggle.onValueChanged.GetPersistentEventCount() == 0)
        {
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
    }

    private void LoadSettingsIntoControls()
    {
        float volume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

        AudioListener.volume = volume;
        Screen.fullScreen = fullscreen;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(volume);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
        }

        UpdateVolumeLabel(volume);
    }

    private void UpdateVolumeLabel(float volume)
    {
        if (masterVolumeLabel != null)
        {
            masterVolumeLabel.text = $"VOLUME {Mathf.RoundToInt(volume * 100f)}%";
        }
    }

    private void FindSceneObjectsIfMissing()
    {
        settingsPanel = settingsPanel != null ? settingsPanel : FindInactiveObject("SettingsPanel");
        defaultReturnPanel = defaultReturnPanel != null ? defaultReturnPanel : FindInactiveObject("MainMenuPanel");
        backButton = backButton != null ? backButton : FindButton("SettingsBackButton");
        masterVolumeSlider = masterVolumeSlider != null ? masterVolumeSlider : FindSlider("MasterVolumeSlider");
        masterVolumeLabel = masterVolumeLabel != null ? masterVolumeLabel : FindText("MasterVolumeLabel");
        fullscreenToggle = fullscreenToggle != null ? fullscreenToggle : FindToggle("FullscreenToggle");
    }

    private static Button FindButton(string objectName)
    {
        GameObject obj = FindInactiveObject(objectName);
        return obj != null ? obj.GetComponent<Button>() : null;
    }

    private static Slider FindSlider(string objectName)
    {
        GameObject obj = FindInactiveObject(objectName);
        return obj != null ? obj.GetComponent<Slider>() : null;
    }

    private static Toggle FindToggle(string objectName)
    {
        GameObject obj = FindInactiveObject(objectName);
        return obj != null ? obj.GetComponent<Toggle>() : null;
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
