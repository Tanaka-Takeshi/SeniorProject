using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class ToastUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject panelRoot;      // ← 見た目を載せる子でも可。未指定なら自分
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text messageTMP;

    [Header("Timing")]
    [Min(0f)] public float defaultDuration = 1.2f; // 表示維持時間
    [Min(0f)] public float fadeSec = 0.2f;         // フェード時間

    private readonly Queue<(string msg, float dur)> _queue = new();
    private Coroutine _runner;
    private bool _running;

    // ▼ テスト観測用（任意利用）
    public event Action<string> OnShown;
    public event Action<string> OnHidden;
    public int PendingCount => _queue.Count;
    public bool IsRunning => _running;


    void Awake()
    {
        if (!panelRoot) panelRoot = gameObject;

        // ▼ 「非表示＝SetActive(false)」にしない。常に Active のまま α=0 で隠す
        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        // panelRoot は Active のまま（StartCoroutine を可能にするため）
        if (!panelRoot.activeSelf) panelRoot.SetActive(true);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        enabled = true;
    }

    // 既存互換（1引数）
    public void Show(string message) => Enqueue(message, defaultDuration);
    // 期間指定版（2引数）
    public void Show(string message, float duration) => Enqueue(message, Mathf.Max(0f, duration));

    public void ClearQueue(bool hideNow = false)
    {
        _queue.Clear();
        if (hideNow && _running && _runner != null)
        {
            StopCoroutine(_runner);
            _runner = null;
            _running = false;
            InstantHide();
        }
    }

    private void Enqueue(string message, float duration)
    {
        if (string.IsNullOrEmpty(message)) return;

        _queue.Enqueue((message, duration));

        // ▼ 念のため：非アクティブでも呼ばれたら自己復帰
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!enabled) enabled = true;
        if (panelRoot && !panelRoot.activeSelf) panelRoot.SetActive(true);

        if (!_running) _runner = StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue()
    {
        _running = true;

        while (_queue.Count > 0)
        {
            var (msg, dur) = _queue.Dequeue();

            if (!panelRoot) panelRoot = gameObject;
            if (messageTMP) messageTMP.text = msg;
            OnShown?.Invoke(msg);

            // フェードイン
            if (canvasGroup && fadeSec > 0f)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                for (float t = 0f; t < fadeSec; t += Time.unscaledDeltaTime)
                {
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeSec);
                    yield return null;
                }
                canvasGroup.alpha = 1f;
            }
            else if (canvasGroup)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            // 表示維持
            float hold = (dur > 0f) ? dur : defaultDuration;
            float timer = 0f;
            while (timer < hold)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            // フェードアウト（見た目だけ隠す・オブジェクトは非アクティブにしない）
            if (canvasGroup && fadeSec > 0f)
            {
                for (float t = 0f; t < fadeSec; t += Time.unscaledDeltaTime)
                {
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeSec);
                    yield return null;
                }
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                OnHidden?.Invoke(msg);
            }
            else if (canvasGroup)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                OnHidden?.Invoke(msg);
            }
        }

        _running = false;
        _runner = null;
    }

    private void InstantHide()
    {
        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        // GameObject 自体は非アクティブにしない
    }
}
