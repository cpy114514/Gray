using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public class SceneFlow : MonoBehaviour
{
    public const int MainMenuBuildIndex = 0;
    public const int FirstGameplayBuildIndex = 1;

    private const string HighestUnlockedKey = "Gray.HighestUnlockedBuildIndex";

    public static int LastGameplayBuildIndex => SceneManager.sceneCountInBuildSettings - 1;

    public static void EnsureInitialUnlock()
    {
        if (GetHighestUnlockedLevel() < FirstGameplayBuildIndex)
        {
            PlayerPrefs.SetInt(HighestUnlockedKey, FirstGameplayBuildIndex);
            PlayerPrefs.Save();
        }
    }

    public static int GetHighestUnlockedLevel()
    {
        return PlayerPrefs.GetInt(HighestUnlockedKey, FirstGameplayBuildIndex);
    }

    public static bool IsLevelUnlocked(int buildIndex)
    {
        EnsureInitialUnlock();
        return buildIndex >= FirstGameplayBuildIndex &&
               buildIndex < SceneManager.sceneCountInBuildSettings &&
               buildIndex <= GetHighestUnlockedLevel();
    }

    public static string GetLevelDisplayName(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            return $"LEVEL {Mathf.Max(1, buildIndex - FirstGameplayBuildIndex + 1)}";
        }

        string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        string sceneName = Path.GetFileNameWithoutExtension(scenePath);
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return $"LEVEL {Mathf.Max(1, buildIndex - FirstGameplayBuildIndex + 1)}";
        }

        return sceneName;
    }

    public static void UnlockLevel(int buildIndex)
    {
        if (buildIndex < FirstGameplayBuildIndex || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            return;
        }

        if (buildIndex <= GetHighestUnlockedLevel())
        {
            return;
        }

        PlayerPrefs.SetInt(HighestUnlockedKey, buildIndex);
        PlayerPrefs.Save();
    }

    public static void UnlockNextLevelFromCurrent()
    {
        UnlockLevel(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public static void UnlockAllLevels()
    {
        EnsureInitialUnlock();

        if (LastGameplayBuildIndex < FirstGameplayBuildIndex)
        {
            return;
        }

        PlayerPrefs.SetInt(HighestUnlockedKey, LastGameplayBuildIndex);
        PlayerPrefs.Save();
    }

    public static void LoadLevel(int buildIndex)
    {
        if (!IsLevelUnlocked(buildIndex))
        {
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(buildIndex);
    }

    public static void LoadNextLevel()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        UnlockLevel(nextIndex);

        if (nextIndex >= FirstGameplayBuildIndex && nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(nextIndex);
            return;
        }

        ReturnToMainMenu();
    }

    public static void LoadLevelByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public static void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenuBuildIndex);
    }

    public void LoadLevelFromButton(int buildIndex)
    {
        LoadLevel(buildIndex);
    }

    public void RestartLevelFromButton()
    {
        RestartLevel();
    }

    public void ReturnToMainMenuFromButton()
    {
        ReturnToMainMenu();
    }
}
