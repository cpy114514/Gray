using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GrayDoor : MonoBehaviour
{
    [SerializeField] private bool oneTimeUse;

    [Header("Message")]
    [SerializeField] private string message = "Something changes.";
    [SerializeField] private float messageTime = 3f;
    [SerializeField] private TutorialTextUI tutorialTextUI;

    private bool hasTriggered;
    private readonly Dictionary<PlayerController2D, int> playerTouchCounts = new Dictionary<PlayerController2D, int>();

    private void Awake()
    {
        ApplyDoorSetup();
        if (tutorialTextUI == null)
        {
            tutorialTextUI = FindObjectOfType<TutorialTextUI>();
        }
    }

    private void OnValidate()
    {
        ApplyDoorSetup();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        if (!playerTouchCounts.TryGetValue(player, out int touchCount))
        {
            playerTouchCounts[player] = 1;
            player.EnterGrayDoor();
            ShowMessage();
            return;
        }

        playerTouchCounts[player] = touchCount + 1;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        if (!playerTouchCounts.ContainsKey(player))
        {
            playerTouchCounts[player] = 1;
            player.EnterGrayDoor();
            ShowMessage();
        }
        else
        {
            player.StayInGrayDoor();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        if (!playerTouchCounts.TryGetValue(player, out int touchCount))
        {
            player.ExitGrayDoor();
            return;
        }

        touchCount--;
        if (touchCount > 0)
        {
            playerTouchCounts[player] = touchCount;
            return;
        }

        playerTouchCounts.Remove(player);
        player.ExitGrayDoor();
    }

    private void ShowMessage()
    {
        if (oneTimeUse && hasTriggered)
        {
            return;
        }

        hasTriggered = true;

        if (tutorialTextUI != null && !string.IsNullOrWhiteSpace(message))
        {
            tutorialTextUI.ShowText(message, messageTime);
        }
    }

    private void ApplyDoorSetup()
    {
        Collider2D doorCollider = GetComponent<Collider2D>();
        if (doorCollider != null)
        {
            doorCollider.isTrigger = true;
        }

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = Color.gray;
        }
    }
}
