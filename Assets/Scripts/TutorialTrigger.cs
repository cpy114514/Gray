using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TutorialTrigger : MonoBehaviour
{
    [TextArea]
    [SerializeField] private string tutorialText = "Use A and D to move.";
    [SerializeField] private TutorialTextUI tutorialTextUI;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private float displayTime = 3f;

    private bool hasTriggered;

    private void Awake()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;

        if (tutorialTextUI == null)
        {
            tutorialTextUI = FindObjectOfType<TutorialTextUI>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerOnce && hasTriggered)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerController2D>() == null)
        {
            return;
        }

        hasTriggered = true;
        if (tutorialTextUI != null)
        {
            tutorialTextUI.ShowText(tutorialText, displayTime);
        }
    }
}
