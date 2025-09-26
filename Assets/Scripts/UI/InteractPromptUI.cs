using UnityEngine;
using TMPro;

public class InteractPromptUI : MonoBehaviour
{
    public CanvasGroup group;
    public TextMeshProUGUI label;
    [TextArea] public string template = "[Enter] {0}";

    void Awake()
    {
        if (group) group.alpha = 0f;
    }

    public void Show(string prompt)
    {
        if (label) label.text = string.Format(template, prompt);
        if (group) group.alpha = 1f;
    }

    public void Hide()
    {
        if (group) group.alpha = 0f;
    }
}
