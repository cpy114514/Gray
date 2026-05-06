using System.Collections;
using TMPro;
using UnityEngine;

public class TutorialTextUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textLabel;
    [SerializeField] private float defaultDisplayTime = 3f;
    [SerializeField] private bool hideOnAwake = true;

    private Coroutine hideRoutine;

    private void Awake()
    {
        if (textLabel == null)
        {
            textLabel = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (hideOnAwake)
        {
            HideText();
        }
    }

    public void ShowText(string text)
    {
        ShowText(text, defaultDisplayTime);
    }

    public void ShowText(string text, float displayTime)
    {
        if (textLabel == null)
        {
            Debug.LogWarning("TutorialTextUI has no TextMeshProUGUI assigned.", this);
            return;
        }

        if (textLabel.text != text)
        {
            textLabel.text = text;
        }

        if (!textLabel.gameObject.activeSelf)
        {
            textLabel.gameObject.SetActive(true);
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        if (displayTime > 0f)
        {
            hideRoutine = StartCoroutine(HideAfterDelay(displayTime));
        }
    }

    public void HideText()
    {
        if (textLabel != null)
        {
            if (!string.IsNullOrEmpty(textLabel.text))
            {
                textLabel.text = string.Empty;
            }

            if (textLabel.gameObject.activeSelf)
            {
                textLabel.gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideText();
        hideRoutine = null;
    }
}
