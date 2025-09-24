using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Game.Events;
using Game.Runtime;
using Game.Data;

//
// EventManager より“後”に走るよう明示
//
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(HUDController))]
public sealed class SingleEventTracker : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EventManager eventManager;
    [SerializeField] private HUDController hud;

    [Header("Options")]
    [Tooltip("Available も追跡対象にするか")]
    public bool showAvailable = true;
    [Tooltip("対象が無いとき HUD を自動で隠すか")]
    public bool hideWhenNone = true;
    [Tooltip("Main を Sub より優先して表示（今回の仕様では未使用）")]
    public bool mainHasPriority = false;

    [Header("Stability")]
    [Tooltip("候補がゼロでも、指定フレーム数は隠さず待つ（レース回避）")]
    [Min(0)] public int emptyGraceFrames = 1;

    [Header("Debug")]
    public bool debugLogSelection = false;

    private class Entry
    {
        public string id;
        public Game.Events.EventType type;
        public EventState state;
        public uint serial;
    }

    private readonly Dictionary<string, Entry> _active = new();
    private uint _serialCounter = 0;
    private string _currentId = null;
    private int _consecutiveEmpty = 0;

    void Reset()
    {
        if (!hud) hud = GetComponent<HUDController>();
#if UNITY_2023_1_OR_NEWER
        if (!eventManager) eventManager = Object.FindFirstObjectByType<EventManager>();
#else
        if (!eventManager) eventManager = Object.FindObjectByType<EventManager>();
#endif
    }

    void Awake()
    {
        if (!hud) hud = GetComponent<HUDController>();
#if UNITY_2023_1_OR_NEWER
        if (!eventManager) eventManager = Object.FindFirstObjectByType<EventManager>();
#else
        if (!eventManager) eventManager = Object.FindObjectByType<EventManager>();
#endif
    }

    void OnEnable()
    {
        EventSignals.OnAvailable += OnAvailable;
        EventSignals.OnStarted += OnStarted;
        EventSignals.OnCompleted += OnCompleted;
        EventSignals.OnFailed += OnFailed;
        EventSignals.OnExpired += OnExpired;
        EventSignals.OnProgress += OnProgress;

        RebuildFromManager();

        // 初期はフレーム末尾で再選択（全コンポーネントのUpdate後）
        StartCoroutine(ReselectEndOfFrame());
    }

    void OnDisable()
    {
        EventSignals.OnAvailable -= OnAvailable;
        EventSignals.OnStarted -= OnStarted;
        EventSignals.OnCompleted -= OnCompleted;
        EventSignals.OnFailed -= OnFailed;
        EventSignals.OnExpired -= OnExpired;
        EventSignals.OnProgress -= OnProgress;
    }

    // ==== ポーリングは LateUpdate で ====
    void LateUpdate()
    {
        if (eventManager == null)
        {
            // 1フレームに1回だけリバインドを試行
            if (Time.frameCount != _rebindTryFrame)
            {
                _rebindTryFrame = Time.frameCount;
                TryRebindManager();
            }
            // ここでまだ null なら何もしない
            return;
        }

        // 1) 現在のランタイムと同期
        MergeFromManager();

        // 2) ベスト候補と表示のズレを解消
        var bestId = ComputeBestId();
        if (!string.IsNullOrEmpty(bestId))
        {
            if (_currentId != bestId)
            {
                _currentId = bestId;
                hud.AcquireOwner(this);
                hud.SetTitleFrom(this, BuildTitle(bestId));
                hud.SetBodyFrom(this, BuildBody(bestId));
                hud.SetVisible(true);
            }
            else
            {
                hud.AcquireOwner(this);
                hud.SetBodyFrom(this, BuildBody(bestId));
                hud.SetVisible(true);
            }
            _consecutiveEmpty = 0;
        }
        else
        {
            // 候補ゼロ：連続カウントが閾値を超えたら、末尾でもう一度だけ確認してから Hide
            _consecutiveEmpty++;
            if (hideWhenNone && _consecutiveEmpty > emptyGraceFrames)
                StartCoroutine(HideIfStillNoneEndOfFrame());
        }
    }

    IEnumerator ReselectEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        MergeFromManager();
        ForceReselectRender();
    }

    IEnumerator HideIfStillNoneEndOfFrame()
    {
        yield return new WaitForEndOfFrame();

        MergeFromManager();
        var bestAgain = ComputeBestId();
        if (string.IsNullOrEmpty(bestAgain))
            hud.HideFrom(this);     // ← ここは Owner 保護版を使う
        else
            ForceReselectRender();  // 復活
    }

    // ===== Signals =====
    void OnAvailable(string id) { if (showAvailable) TryAddAndReselect(id); }
    void OnStarted(string id) { TryAddAndReselect(id); }
    void OnCompleted(string id) { RemoveAndSwitch(id, "完了しました。"); }
    void OnExpired(string id) { RemoveAndSwitch(id, "期限切れ"); }
    void OnFailed(string id, FailedReason r) { RemoveAndSwitch(id, $"失敗：{r}"); }

    void OnProgress(string id, float p)
    {
        if (_currentId == id)
        {
            hud.SetBodyFrom(this, $"{BuildBody(id)} 進捗 {(int)(Mathf.Clamp01(p) * 100)}%");
        }
    }

    void TryAddAndReselect(string id)
    {
        if (!TrySnapshot(id, out var snap)) return;
        AddOrUpdate(id, snap);

        // Hideは絶対に行わず、描画のみ更新
        ForceReselectRender();
    }

    void RemoveAndSwitch(string id, string finalText)
    {
        _active.Remove(id);

        if (_currentId == id)
        {
            _currentId = null;

            // まず現在の一言を出す（隠さない）
            hud.AcquireOwner(this);
            hud.SetBodyFrom(this, finalText);
            hud.SetVisible(true);

            // 末尾で最新に切り替え（なければ HideIfStillNone）
            StartCoroutine(ReselectEndOfFrame());
        }
        // 表示中でないIDの終了は LateUpdate で同期される
    }

    // ===== Core =====
    void AddOrUpdate(string id, (Game.Events.EventType type, EventState state) s)
    {
        if (_active.TryGetValue(id, out var e))
        {
            // 状態が後退する場合（例：Startedの後に遅延Availableが来た）を無視
            if (e.state == EventState.InProgress && s.state == EventState.Available)
                return;

            e.state = s.state;
            e.type = s.type;
            if (s.state == EventState.Available || s.state == EventState.InProgress)
                e.serial = ++_serialCounter;
        }
        else
        {
            _active[id] = new Entry
            {
                id = id,
                type = s.type,
                state = s.state,
                serial = (s.state == EventState.Available || s.state == EventState.InProgress)
                         ? ++_serialCounter : _serialCounter
            };
        }
    }

    void ForceReselectRender()
    {
        var bestId = ComputeBestId();
        if (string.IsNullOrEmpty(bestId)) return;

        if (_currentId != bestId)
        {
            _currentId = bestId;
            hud.AcquireOwner(this);
            hud.SetTitleFrom(this, BuildTitle(bestId));
            hud.SetBodyFrom(this, BuildBody(bestId));
        }
        else
        {
            hud.AcquireOwner(this);
            hud.SetBodyFrom(this, BuildBody(bestId));
        }
        hud.SetVisible(true);
        _consecutiveEmpty = 0;
    }

    string ComputeBestId()
    {
        string bestId = null;
        Entry best = null;

        // 状態→点数
        int Score(EventState s)
            => (s == EventState.InProgress) ? 100 :
               (s == EventState.Available) ? 10 : 0;

        foreach (var kv in _active)
        {
            var e = kv.Value;
            // 監視対象は Available / InProgress のみ
            if (e.state != EventState.InProgress && e.state != EventState.Available) continue;

            if (best == null) { best = e; bestId = e.id; continue; }

            var sb = Score(best.state);
            var se = Score(e.state);

            // 1) 状態優先（InProgress > Available）
            if (se != sb)
            {
                if (se > sb) { best = e; bestId = e.id; }
                continue;
            }

            // 2) 同じ状態 ─ ここで分岐
            if (e.state == EventState.Available)
            {
                // Available 同士は Main 優先（必要なら）
                if (mainHasPriority && best.type != e.type)
                {
                    if (e.type == Game.Events.EventType.Main) { best = e; bestId = e.id; }
                    else if (best.type == Game.Events.EventType.Main) { /* 変更なし */ }
                    else
                    {
                        // 両方 Sub 等 → serial で決める
                        if (e.serial > best.serial) { best = e; bestId = e.id; }
                    }
                    continue;
                }

                // 型が同じ or 優先なし → serial（新しい方）
                if (e.serial > best.serial) { best = e; bestId = e.id; }
            }
            else
            {
                // InProgress 同士は **常に** 新しい方（型は無視）
                if (e.serial > best.serial) { best = e; bestId = e.id; }
            }
        }

        if (debugLogSelection)
            Debug.Log($"[Tracker] best={(bestId ?? "none")} current={_currentId ?? "none"} active=[{string.Join(",", _active.Keys)}]");

        return bestId;
    }


    // ===== Helpers =====
    bool TrySnapshot(string id, out (Game.Events.EventType type, EventState state) snap)
    {
        snap = default;
        if (!eventManager) return false;
        if (!eventManager.TryGetRuntime(id, out var rt) || rt == null) return false;
        snap = (rt.Data.type, rt.State);
        return true;
    }

    string BuildTitle(string id)
    {
        if (eventManager && eventManager.TryGetRuntime(id, out var rt) && rt != null)
        {
            var tag = (rt.Data.type == Game.Events.EventType.Main) ? "[Main]" : "[Sub]";
            return $"{tag} {rt.Data.eventId}";
        }
        return id;
    }

    string BuildBody(string id)
    {
        if (eventManager && eventManager.TryGetRuntime(id, out var rt) && rt != null)
        {
            var d = rt.Data;

            // 表示用に "A|B|C" → "A / B / C"
            string PlaceLabel(string raw)
            {
                if (string.IsNullOrEmpty(raw)) return "目的地";
                if (!raw.Contains("|")) return raw;
                var parts = raw.Split('|');
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
                return string.Join(" / ", parts);
            }

            var place = PlaceLabel(d.location.id);

            if (rt.State == EventState.InProgress) return $"進行中：{place}";
            if (d.autoStartOnLocation) return $"{place} に到達すると自動で開始";
            if (d.requiresButtonPress) return $"{place} で [E] で開始";
            return "開始条件を満たすと開始";
        }
        return "";
    }

    void RebuildFromManager()
    {
        _active.Clear();
        _currentId = null;
        MergeFromManager();
        _consecutiveEmpty = 0;
    }

    void MergeFromManager()
    {
        if (!eventManager) return;
        try
        {
            var seen = new HashSet<string>();
            foreach (var rt in eventManager.AllRuntimes())
            {
                if (rt == null || rt.Data == null) continue;
                var id = rt.Data.eventId;
                if (string.IsNullOrEmpty(id)) continue;
                seen.Add(id);

                if (rt.State == EventState.InProgress || (showAvailable && rt.State == EventState.Available))
                    AddOrUpdate(id, (rt.Data.type, rt.State));
                else
                    _active.Remove(id);
            }

            // ソースから消えたIDも掃除
            using (var it = _active.Keys.GetEnumerator())
            {
                var toRemove = new List<string>();
                foreach (var kv in _active) if (!seen.Contains(kv.Key)) toRemove.Add(kv.Key);
                foreach (var rid in toRemove) _active.Remove(rid);
            }
        }
        catch (System.Exception ex)
        {
            if (debugLogSelection) Debug.LogWarning($"[Tracker] MergeFromManager error: {ex.Message}", this);
        }
    }

    // ===== Rebind support =====
    [SerializeField] private bool autoRebindManager = true;
    private int _rebindTryFrame = -1;

    private bool TryRebindManager()
    {
        if (!autoRebindManager) return false;
#if UNITY_2023_1_OR_NEWER
        var found = Object.FindFirstObjectByType<EventManager>();
#else
    var found = Object.FindObjectOfType<EventManager>();
#endif
        if (found != null && found != eventManager)
        {
            eventManager = found;
            if (debugLogSelection) Debug.Log($"[Tracker] Rebound EventManager: {found.GetInstanceID()}");
            RebuildFromManager();
            return true;
        }
        return false;
    }

#if UNITY_EDITOR
    public void AssignForTest(Game.Runtime.EventManager emRef, HUDController hudRef)
    {
        this.eventManager = emRef;
        this.hud = hudRef;
    }

    public void RebuildFromManager_ForTest()
    {
        // 内部の RebuildFromManager をテストから叩けるようにする
        RebuildFromManager();
    }

    public void ForceRefreshForTest()
    {
        MergeFromManager();
        ForceReselectRender();
    }

    public void DebugDumpFromManager()
    {
        if (!eventManager) { Debug.Log("[TrackerDump] em=null"); return; }
        int total = 0;
        foreach (var rt in eventManager.AllRuntimes())
        {
            if (rt == null || rt.Data == null) continue;
            total++;
            Debug.Log($"[TrackerDump] id={rt.Data.eventId} state={rt.State} type={rt.Data.type}");
        }
        Debug.Log($"[TrackerDump] total={total} showAvailable={showAvailable}");
        Debug.Log($"[TrackerDump] activeKeys=[{string.Join(",", _active.Keys)}]");
    }
#endif
}
