using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        InitializeAllPlayers();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeAllPlayers();
    }

    private static void InitializeAllPlayers()
    {
        PlayerController2D[] players = Object.FindObjectsOfType<PlayerController2D>(true);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                players[i].InitializeRuntimeSystems();
            }
        }
    }
}
