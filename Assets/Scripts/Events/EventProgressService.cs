// Assets/Scripts/Events/EventProgressService.cs
using UnityEngine;
using System.Collections.Generic;

public enum EventRunState { Inactive, Active, Completed }

[DefaultExecutionOrder(-200)]
public class EventProgressService : MonoBehaviour
{
    public static EventProgressService Instance { get; private set; }

    [Header("Registry (EventData など)")]
    [Tooltip("eventId フィールド(string)を持つ ScriptableObject を入れる（例：Game.Data.EventData）")]
    public ScriptableObject[] eventsRegistry;

    // 状態と進捗
    private readonly Dictionary<string, EventRunState> _states = new();
    private readonly Dictionary<string, float> _progress = new();

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _states.Clear();
        _progress.Clear();

        if (eventsRegistry != null)
        {
            foreach (var so in eventsRegistry)
            {
                var id = GetEventId(so);
                if (string.IsNullOrEmpty(id)) continue;
                if (!_states.ContainsKey(id)) _states[id] = EventRunState.Inactive;
                if (!_progress.ContainsKey(id)) _progress[id] = 0f;
            }
        }

        Debug.Log($"[EventProgress] Awake. registry={eventsRegistry?.Length ?? 0}");
        Debug.Log($"[EventProgress] Registered: [{string.Join(", ", _states.Keys)}]");
    }

    public EventRunState GetState(string eventId)
    {
        eventId = Normalize(eventId);
        return _states.TryGetValue(eventId, out var s) ? s : EventRunState.Inactive;
    }

    public float GetProgress(string eventId)
    {
        eventId = Normalize(eventId);
        return _progress.TryGetValue(eventId, out var p) ? p : 0f;
    }

    public bool StartEvent(string eventId)
    {
        eventId = Normalize(eventId);
        if (!_states.ContainsKey(eventId))
        {
            Debug.LogWarning($"[Event] StartEvent: not found '{eventId}'");
            return false;
        }

        var cur = GetState(eventId);
        if (cur == EventRunState.Completed)
        {
            Debug.Log($"[Event] StartEvent: already Completed '{eventId}'");
            return true;
        }

        if (cur == EventRunState.Inactive)
        {
            _states[eventId] = EventRunState.Active;
            _progress[eventId] = 0f;
            Debug.Log($"[Event] StateChange: {eventId} Inactive -> Active (StartEvent)");
            Debug.Log($"[Event] Started: {eventId}");
        }
        return true;
    }

    public bool AddProgress(string eventId, float delta)
    {
        eventId = Normalize(eventId);
        if (!_states.ContainsKey(eventId))
        {
            Debug.LogWarning($"[Event] AddProgress: not found '{eventId}'");
            return false;
        }

        var st = GetState(eventId);
        if (st == EventRunState.Inactive)
        {
            // 進捗が来たら自動開始したい場合は StartEvent を許可
            StartEvent(eventId);
        }
        if (GetState(eventId) == EventRunState.Completed) return true;

        var before = GetProgress(eventId);
        var after = Mathf.Clamp01(before + Mathf.Max(0f, delta));
        _progress[eventId] = after;

        Debug.Log($"[Event] Progress: {eventId} {before:0.##} -> {after:0.##} (delta={delta})");

        if (after >= 1f)
        {
            _states[eventId] = EventRunState.Completed;
            Debug.Log($"[Event] StateChange: {eventId} Active -> Completed (progress>=1.0)");
        }
        return true;
    }

    public void CompleteEvent(string eventId)
    {
        eventId = Normalize(eventId);
        if (!_states.ContainsKey(eventId))
        {
            Debug.LogWarning($"[Event] CompleteEvent: not found '{eventId}'");
            return;
        }

        if (_states[eventId] != EventRunState.Completed)
        {
            _states[eventId] = EventRunState.Completed;
            _progress[eventId] = 1f;
            Debug.Log($"[Event] StateChange: {eventId} -> Completed (CompleteEvent)");
        }
    }

    // ===== helpers =====

    private string GetEventId(ScriptableObject so)
    {
        if (!so) return null;
        var f = so.GetType().GetField("eventId");
        return (f != null && f.FieldType == typeof(string))
            ? (string)f.GetValue(so)
            : null;
    }

    private string Normalize(string s) => string.IsNullOrEmpty(s) ? "" : s.Trim();

    /// <summary>
    /// 明示的に状態を変更する共通関数（ログ/進捗整合をここで担保）
    /// </summary>
    public void SetState(string eventId, EventRunState newState, string reason = "SetState()")
    {
        eventId = Normalize(eventId);
        if (string.IsNullOrEmpty(eventId))
        {
            Debug.LogWarning("[Event] SetState: eventId is null/empty");
            return;
        }

        // 未登録なら登録だけして警告
        if (!_states.ContainsKey(eventId))
        {
            Debug.LogWarning($"[Event] SetState: '{eventId}' not in registry. Auto-registering as Inactive.");
            _states[eventId] = EventRunState.Inactive;
        }
        if (!_progress.ContainsKey(eventId))
            _progress[eventId] = 0f;

        var cur = _states[eventId];
        if (cur == newState)
        {
            Debug.Log($"[Event] SetState: {eventId} stays {newState} ({reason})");
            return;
        }

        // 進捗の境界整合
        if (newState == EventRunState.Active && _progress[eventId] > 1f) _progress[eventId] = 0f;
        if (newState == EventRunState.Completed) _progress[eventId] = 1f;

        _states[eventId] = newState;
        Debug.Log($"[Event] StateChange: {eventId} {cur} -> {newState}  ({reason})");
    }

    public void ForceComplete(string eventId, string reason = "ForceComplete()")
    {
        eventId = Normalize(eventId);
        if (string.IsNullOrEmpty(eventId))
        {
            Debug.LogWarning("[Event] ForceComplete: eventId is null/empty");
            return;
        }

        // 現在の状態を取得
        var st = GetState(eventId);

        // 未登録でも警告しつつ進める（辞書の穴埋め）
        if (!_states.ContainsKey(eventId))
        {
            Debug.LogWarning($"[Event] ForceComplete: '{eventId}' not in registry. Auto-registering.");
            _states[eventId] = EventRunState.Inactive;
        }
        if (!_progress.ContainsKey(eventId))
            _progress[eventId] = 0f;

        // すでに完了なら何もしない
        if (st == EventRunState.Completed)
        {
            Debug.Log($"[Event] ForceComplete: already Completed '{eventId}'");
            return;
        }

        // 進捗を 1.0 に揃えてから状態を Completed へ
        _progress[eventId] = 1f;

        // 既存の状態変更ルートを通す（ログ/フック統一）
        this.SetState(eventId, EventRunState.Completed, reason);

        Debug.Log($"[Event] ForceComplete: {eventId}");
    }

    // （別名）
    public void CompleteNow(string eventId) => ForceComplete(eventId, "CompleteNow()");
}
