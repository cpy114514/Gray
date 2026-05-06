using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GrayDoorDisplay : MonoBehaviour
{
    [SerializeField] private TutorialTextUI tutorialTextUI;
    [SerializeField] private string warningText = "Gray doors are forbidden.";
    [SerializeField] private float displayTime = 3f;

    private void Awake()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.gray;
        }

        if (tutorialTextUI == null)
        {
            tutorialTextUI = FindObjectOfType<TutorialTextUI>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController2D>() == null)
        {
            return;
        }

        if (tutorialTextUI != null)
        {
            tutorialTextUI.ShowText(warningText, displayTime);
        }
    }
}
