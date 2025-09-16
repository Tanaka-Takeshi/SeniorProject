using UnityEngine;
using Game.Events;   // ★ EventSignals が入っている名前空間

/// <summary>
/// EventSignals の各シグナルを購読して Console に流すデバッグ用コンポーネント
/// </summary>
public class EventSignalLogger : MonoBehaviour
{
    private void OnEnable()
    {
        EventSignals.OnScheduled += OnScheduled;
        EventSignals.OnAvailable += OnAvailable;
        EventSignals.OnStarted += OnStarted;
        EventSignals.OnCompleted += OnCompleted;
        EventSignals.OnFailed += OnFailed;
        EventSignals.OnExpired += OnExpired;
        EventSignals.OnProgress += OnProgress;
    }

    private void OnDisable()
    {
        EventSignals.OnScheduled -= OnScheduled;
        EventSignals.OnAvailable -= OnAvailable;
        EventSignals.OnStarted -= OnStarted;
        EventSignals.OnCompleted -= OnCompleted;
        EventSignals.OnFailed -= OnFailed;
        EventSignals.OnExpired -= OnExpired;
        EventSignals.OnProgress -= OnProgress;
    }

    private void OnScheduled(string id)
        => Debug.Log($"[SIG] Scheduled {id}");
    private void OnAvailable(string id)
        => Debug.Log($"[SIG] Available {id}");
    private void OnStarted(string id)
        => Debug.Log($"[SIG] Started {id}");
    private void OnCompleted(string id)
        => Debug.Log($"[SIG] Completed {id}");
    private void OnFailed(string id, FailedReason reason)
        => Debug.Log($"[SIG] Failed {id} ({reason})");
    private void OnExpired(string id)
        => Debug.Log($"[SIG] Expired {id}");
    private void OnProgress(string id, float progress01)
        => Debug.Log($"[SIG] Progress {id}: {progress01:P0}");
}

