using UnityEngine;
using System.Collections;
using TMPro;              // © ’Ç‰Á
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text titleTMP;   // © ‚à‚¤ðŒ‚È‚µ
    [SerializeField] private TMP_Text bodyTMP;    // © ‚à‚¤ðŒ‚È‚µ
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Show/Hide Options")]
    [SerializeField] private bool startHidden = false;
    [SerializeField] private float fadeSec = 0.25f;
    private Coroutine _fadeCo;

    void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = startHidden ? 0f : 1f;
            canvasGroup.interactable = !startHidden;
            canvasGroup.blocksRaycasts = !startHidden;
        }
        if (startHidden) panelRoot.SetActive(false);
    }

    public void Show(string title, string body, bool instant = false)
    {
        SetTitle(title);
        SetBody(body);
        SetVisible(true, instant);
    }

    public void Hide(bool instant = false) => SetVisible(false, instant);

    public void SetTitle(string title) { if (titleTMP) titleTMP.text = title; }
    public void SetBody(string body) { if (bodyTMP) bodyTMP.text = body; }
    public void AppendBody(string t, bool nl = true)
    {
        if (!bodyTMP) return;
        bodyTMP.text += nl ? ("\n" + t) : t;
    }
    public void SwapTitleAndBody()
    {
        if (!titleTMP || !bodyTMP) return;
        var t = titleTMP.text; titleTMP.text = bodyTMP.text; bodyTMP.text = t;
    }

    public void SetVisible(bool visible, bool instant = false)
    {
        if (panelRoot == null) return;
        if (canvasGroup == null || fadeSec <= 0f || instant)
        {
            panelRoot.SetActive(visible);
            if (canvasGroup)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            return;
        }
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        panelRoot.SetActive(true);
        _fadeCo = StartCoroutine(CoFade(visible));
    }

    private IEnumerator CoFade(bool toVisible)
    {
        float start = canvasGroup.alpha;
        float end = toVisible ? 1f : 0f;
        float t = 0f;
        while (t < fadeSec)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, end, t / fadeSec);
            yield return null;
        }
        canvasGroup.alpha = end;
        canvasGroup.interactable = toVisible;
        canvasGroup.blocksRaycasts = toVisible;
        if (!toVisible) panelRoot.SetActive(false);
        _fadeCo = null;
    }
}
