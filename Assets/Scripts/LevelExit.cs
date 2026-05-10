using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LevelExit : MonoBehaviour
{
    [SerializeField] private bool loadNextLevel = true;
    [SerializeField] private string targetSceneName;
    [SerializeField] private bool oneTimeUse = true;

    private bool used;

    private void Awake()
    {
        ApplySetup();
    }

    private void OnValidate()
    {
        ApplySetup();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (oneTimeUse && used)
        {
            return;
        }

        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        used = true;
        SceneFlow.UnlockNextLevelFromCurrent();

        if (loadNextLevel)
        {
            SceneFlow.LoadNextLevel();
            return;
        }

        SceneFlow.LoadLevelByName(targetSceneName);
    }

    private void ApplySetup()
    {
        Collider2D exitCollider = GetComponent<Collider2D>();
        if (exitCollider != null)
        {
            exitCollider.isTrigger = true;
        }
    }
}
