using UnityEngine;
using Game.Events;

public sealed class EventHUDBinder : MonoBehaviour
{
    [SerializeField] private ToastUI toast;

    void OnEnable()
    {
        EventSignals.OnAvailable += OnAvailable;
        EventSignals.OnStarted += OnStarted;
        EventSignals.OnCompleted += OnCompleted;
        EventSignals.OnFailed += OnFailed;
        EventSignals.OnExpired += OnExpired;
    }
    void OnDisable()
    {
        EventSignals.OnAvailable -= OnAvailable;
        EventSignals.OnStarted -= OnStarted;
        EventSignals.OnCompleted -= OnCompleted;
        EventSignals.OnFailed -= OnFailed;
        EventSignals.OnExpired -= OnExpired;
    }

    void OnAvailable(string id) { if (toast) toast.Show($"利用可能：{id}", 0.8f); }
    void OnStarted(string id) { if (toast) toast.Show($"開始：{id}", 1.0f); }
    void OnCompleted(string id) { if (toast) toast.Show($"完了：{id}", 1.2f); }
    void OnExpired(string id) { if (toast) toast.Show($"期限切れ：{id}", 1.2f); }
    void OnFailed(string id, FailedReason r)
    {
        if (toast) toast.Show($"失敗：{id}（{r}）", 1.2f);
    }
}
