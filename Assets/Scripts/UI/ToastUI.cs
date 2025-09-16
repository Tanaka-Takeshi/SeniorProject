// Assets/Scripts/UI/ToastUI.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
using TMPro;
#endif

public class ToastUI : MonoBehaviour
{
#if TMP_PRESENT || TEXTMESHPRO_PRESENT
    [SerializeField] private TMP_Text label;
#else
    [SerializeField] private Text label;
#endif
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float showSec = 1.2f;
    [SerializeField] private float fadeSec = 0.35f;

    private Coroutine _co;

    private void OnEnable()
    {
        Game.Events.EventSignals.OnScheduled += id => Show($"Scheduled: {id}");
        Game.Events.EventSignals.OnAvailable += id => Show($"Available: {id}");
        Game.Events.EventSignals.OnStarted += id => Show($"Started: {id}");
        Game.Events.EventSignals.OnCompleted += id => Show($"Completed: {id}");
        Game.Events.EventSignals.OnFailed += (id, r) => Show($"Failed: {id} ({r})");
        Game.Events.EventSignals.OnExpired += id => Show($"Expired: {id}");
    }

    private void OnDisable()
    {
        Game.Events.EventSignals.OnScheduled -= id => Show($"Scheduled: {id}");
        Game.Events.EventSignals.OnAvailable -= id => Show($"Available: {id}");
        Game.Events.EventSignals.OnStarted -= id => Show($"Started: {id}");
        Game.Events.EventSignals.OnCompleted -= id => Show($"Completed: {id}");
        Game.Events.EventSignals.OnFailed -= (id, r) => Show($"Failed: {id} ({r})");
        Game.Events.EventSignals.OnExpired -= id => Show($"Expired: {id}");
    }

    private void Awake()
    {
        if (group) { group.alpha = 0f; }
    }

    public void Show(string text)
    {
        if (label) label.text = text;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoShow());
    }

    private IEnumerator CoShow()
    {
        if (!group) yield break;

        // Fade In
        float t = 0f;
        while (t < fadeSec)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(0f, 1f, t / fadeSec);
            yield return null;
        }
        group.alpha = 1f;

        // Hold
        yield return new WaitForSecondsRealtime(showSec);

        // Fade Out
        t = 0f;
        while (t < fadeSec)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(1f, 0f, t / fadeSec);
            yield return null;
        }
        group.alpha = 0f;
    }
}
