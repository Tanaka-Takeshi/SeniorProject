using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Events;

namespace Game.Runtime
{
    public sealed class EventManager : MonoBehaviour, IEvalContext
    {
        [SerializeField] private Game.Config.GlobalSettings globalSettings;
        [SerializeField] private List<Game.Data.EventData> eventSources = new();

        private readonly Dictionary<string, EventRuntime> _events = new();
        private readonly List<EventRuntime> _activeWatch = new();               // 同時監視上限管理

        // 追加：テストや実装から注入するためのフィールド
        [SerializeField] private MonoBehaviour clockBehaviour;        // IClock
        [SerializeField] private MonoBehaviour locationBehaviour;     // ILocationResolver
        [SerializeField] private MonoBehaviour inputBehaviour;        // IInputProxy

        private Game.Runtime.IClock Clock => clockBehaviour as Game.Runtime.IClock;
        private Game.Runtime.ILocationResolver Locator => locationBehaviour as Game.Runtime.ILocationResolver;
        private Game.Runtime.IInputProxy InputProxy => inputBehaviour as Game.Runtime.IInputProxy;

        private bool _startEdgeThisFrame;
        private bool _startConsumed;

        public bool TryGetRuntime(string id, out EventRuntime rt) => _events.TryGetValue(id, out rt);
        public System.Collections.Generic.IEnumerable<EventRuntime> AllRuntimes() => _events.Values;
        public IClock ClockRef => Clock;


        // ライフサイクル
        private void Awake()
        {
            foreach (var e in eventSources)
            {
                if (string.IsNullOrEmpty(e.eventId)) continue;
                var rt = new EventRuntime(e);
                _events[e.eventId] = rt;
                // 必要なら起動時に Locked→Scheduled 判定を一度実施
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _bootTime = System.DateTime.UtcNow;
            ValidateOnStart();
#endif
        }

        private void Update()
        {
            BeginEvalFrame();
            EvaluateAllInOrder();
        }

        // 毎フレームの先頭で呼ぶ
        private void BeginEvalFrame()
        {
            _startEdgeThisFrame = InputProxy != null && InputProxy.StartPressedThisFrame();
            _startConsumed = false;
        }

        // IEvalContext 実装
        public bool TryConsumeStartInput()
        {
            if (!_startEdgeThisFrame || _startConsumed) return false;
            _startConsumed = true;
            return true;
        }
        private void EvaluateAllInOrder()
        {
            // 1パス目: Main のみ
            foreach (var rt in _events.Values)
            {
                if (rt.Data.type == Game.Events.EventType.Main)
                    rt.Evaluate(this);
            }
            // 2パス目: Main 以外（Sub/World/Tutorial など）
            foreach (var rt in _events.Values)
            {
                if (rt.Data.type != Game.Events.EventType.Main)
                    rt.Evaluate(this);
            }
        }

        // テスト用
        // 1) 1フレーム評価
        public void EvaluateFrame()
        {
            BeginEvalFrame();
            EvaluateAllInOrder();
        }

        // 2) テスト/外部用 初期化（Awake相当）
        public void InitializeForTest(System.Collections.Generic.IEnumerable<Game.Data.EventData> eventsToUse)
        {
            _events.Clear();
            foreach (var e in eventsToUse)
            {
                if (e == null || string.IsNullOrEmpty(e.eventId)) continue;
                _events[e.eventId] = new EventRuntime(e);
            }
        }
        
        private static float ParseGameSeconds(string hhmm)
        {
            if (string.IsNullOrEmpty(hhmm)) return 0f;
            var sp = hhmm.Split(':');
            if (sp.Length < 2) return 0f;
            int hh = int.Parse(sp[0]);
            int mm = int.Parse(sp[1]);
            return (hh * 60 + mm); // 分＝秒換算
        }

        // IEvalContext実装（プロジェクトに合わせて具体化）
        [SerializeField] private bool testPause = false;
        public bool IsGloballyPaused => testPause;
        public void SetPausedForTest(bool paused) => testPause = paused;

        public bool PolicyTreatStartOverAsExpired => false;     // 期限切れ判定

        public bool DependenciesSatisfied(List<string> ids)     // 依存先イベントが達成されているか判定
        {
            if (ids == null || ids.Count == 0) return true;
            foreach (var id in ids)
            {
                if (!_events.TryGetValue(id, out var dep)) return false;
                if (dep.State != Game.Events.EventState.Completed) return false;
            }
            return true;
        }

        public bool NowReached(string gameDateTime)             // 時間到達の判定
        {
            var now = Clock != null ? Clock.NowGameSeconds : 0f;
            return now >= ParseGameSeconds(gameDateTime);
        }

        public bool StartDeadlineExceeded(string gameDateTime)  // 開始期限超過判定
        {
            var now = Clock != null ? Clock.NowGameSeconds : 0f;
            return now > ParseGameSeconds(gameDateTime);
        }
        public bool EndDeadlineReached(string gameDateTime)     // 終了期限超過判定
        {
            var now = Clock != null ? Clock.NowGameSeconds : 0f;
            return now >= ParseGameSeconds(gameDateTime);
        }

        public bool CalendarAllowed(Game.Events.WeekdayRule rule)   // カレンダー許可
        {
            // ToDo : globalSettings.useWeekdaySystem で切替
            return true;
        }

        public bool LocationSatisfied(Game.Events.LocationRef loc)
        {
            // ロケーション未指定は「どこでもOK」
            if (loc.kind == Game.Events.LocationKind.AreaId && string.IsNullOrEmpty(loc.id))
                return true;

            // 既存の Locator が無いなら判定不能 → とりあえず OK（従来踏襲）
            if (Locator == null) return true;

            // "A|B|C" を OR として解釈
            if (loc.kind == Game.Events.LocationKind.AreaId && loc.id != null && loc.id.Contains("|"))
            {
                var parts = loc.id.Split('|');
                foreach (var pRaw in parts)
                {
                    var p = pRaw.Trim();
                    if (string.IsNullOrEmpty(p)) continue;

                    var sub = new Game.Events.LocationRef
                    {
                        kind = Game.Events.LocationKind.AreaId,
                        id = p
                    };
                    if (Locator.IsSatisfied(sub))
                        return true; // 1つでも満たせばOK
                }
                return false;
            }

            // 単一IDは従来通り
            return Locator.IsSatisfied(loc);
        }
        public bool InteractionPossible(Game.Data.EventData data)             // インタラクト可能状態か判定
        {
            // TODO: プレイヤーが対象エリアにいる＋開始可能UI状態 等
            return true;
        }

        public bool StartInputReceived()                            // イベント開始の入力があったか判定
        {
            // ToDo : Input SystemのInteract Actionを参照
            return InputProxy != null && InputProxy.StartPressedThisFrame();
        }

#if UNITY_EDITOR
        [System.Serializable]
        private class EventEntryStateDto
        {
            public string id;
            public Game.Events.EventState state;
            public Game.Events.FailedReason failed;
            public float progress;
        }
        [System.Serializable]
        private class SnapshotDto
        {
            public float now;
            public System.Collections.Generic.List<EventEntryStateDto> events = new();
        }

        [System.Serializable]
        public struct WhyLockedInfo
        {
            public string id;
            public bool depsOK;
            public bool appearOK;
            public bool calendarOK;
            public float now;
            public float appearSec;
            public string[] deps;
            public string depsStates; // "E1:Completed,E2:..."
        }

        // Locked → Scheduled の条件それぞれが今 true か確認
        public WhyLockedInfo ExplainWhyLockedForTest(string id)
        {
            var info = new WhyLockedInfo { id = id, now = Clock != null ? Clock.NowGameSeconds : 0f };

            if (!_events.TryGetValue(id, out var rt)) return info;

            info.appearSec = ParseGameSeconds(rt.Data.appearAt);
            info.appearOK = NowReached(rt.Data.appearAt);
            info.calendarOK = CalendarAllowed(rt.Data.weekdayRule);

            var list = rt.Data.dependencies ?? new System.Collections.Generic.List<string>();
            info.deps = list.ToArray();

            bool ok = true;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                var depId = list[i];
                if (!_events.TryGetValue(depId, out var dep))
                {
                    ok = false; sb.Append(depId).Append(":<missing>");
                }
                else
                {
                    if (dep.State != Game.Events.EventState.Completed) ok = false;
                    sb.Append(depId).Append(":").Append(dep.State);
                }
                if (i < list.Count - 1) sb.Append(",");
            }
            info.depsOK = ok;
            info.depsStates = sb.ToString();
            return info;
        }

        // テスト用：現在状態をJSON文字列で書き出し
        public string ExportStateForTest()
        {
            var dto = new SnapshotDto();
            dto.now = (Clock != null) ? Clock.NowGameSeconds : 0f;

            foreach (var kv in _events)
            {
                var rt = kv.Value;
                dto.events.Add(new EventEntryStateDto
                {
                    id = kv.Key,
                    state = rt.State,
                    failed = rt.FailedReason,
                    progress = rt.Progress
                });
            }
            return JsonUtility.ToJson(dto);
        }

        // テスト用：JSON文字列から状態を復元
        public void ImportStateForTest(string json)
        {
            var dto = JsonUtility.FromJson<SnapshotDto>(json);
            if (dto == null) return;

            // 時刻を復元（SimpleClock が注入されている前提）
            if (Clock is SimpleClock sc)
            {
                sc.Jump(dto.now);
            }

            foreach (var e in dto.events)
            {
                if (_events.TryGetValue(e.id, out var rt))
                {
                    rt.RestoreForTest(e.state, e.failed, e.progress);
                }
            }
        }

        public void Inject(IClock clock, ILocationResolver locator, IInputProxy input,
            Game.Config.GlobalSettings settings)
        {
            this.clockBehaviour = clock as MonoBehaviour;
            this.locationBehaviour = locator as MonoBehaviour;
            this.inputBehaviour = input as MonoBehaviour;
            this.globalSettings = settings;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (clockBehaviour == null) Debug.LogWarning("[EventManager.Inject] clock が MonoBehaviour ではありません。");
            if (locationBehaviour == null) Debug.LogWarning("[EventManager.Inject] locator が MonoBehaviour ではありません。");
            if (inputBehaviour == null) Debug.LogWarning("[EventManager.Inject] input が MonoBehaviour ではありません。");
            if (globalSettings == null) Debug.LogWarning("[EventManager.Inject] GlobalSettings が null です。");
#endif
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Debug / Trace")]
        [SerializeField] private bool enableRuntimeTrace = false;
        [SerializeField, Min(10)] private int traceCapacity = 256;

        private readonly System.Collections.Generic.Queue<string> _trace = new();
        private System.DateTime _bootTime;

        // Editorから On/Off切り替え用
        public void EnableRuntimeTrace(bool on) => enableRuntimeTrace = on;

        // 1行追加（リングバッファ）
        private void Trace(string msg)
        {
            if (!enableRuntimeTrace) return;
            var since = (System.DateTime.UtcNow - _bootTime).TotalSeconds;
            string line = $"[{since,6:0.00}s] {msg}";
            _trace.Enqueue(line);
            while (_trace.Count > traceCapacity) _trace.Dequeue();
            // Consoleにも出したい時は ↓ をコメント外す
            // Debug.Log(line, this);
        }

        public string[] GetRecentTrace() => _trace.ToArray();
        private void ValidateOnStart()
        {
            var seen = new HashSet<string>();
            foreach (var e in eventSources)
            {
                if (e == null)
                {
                    Debug.LogWarning("[EventData] Null entry");
                    continue;
                }

                // 空IDチェック
                if (string.IsNullOrEmpty(e.eventId))
                {
                    Debug.LogWarning("[EventData] Empty id", e);
                    continue;
                }

                // 重複IDチェック
                if (!seen.Add(e.eventId))
                {
                    Debug.LogWarning($"[EventData] DuplicateId: {e.eventId}", e);
                }

                // 時系列チェック
                float ap = ParseGameSeconds(e.appearAt);
                float sd = ParseGameSeconds(e.startDeadline);
                float ed = ParseGameSeconds(e.endDeadline);
                if (!(ap <= sd && sd <= ed))
                {
                    Debug.LogWarning($"[EventData] OrderViolation: {e.eventId} appear={e.appearAt} start={e.startDeadline} end={e.endDeadline}", e);
                }

                // altCompleteThreshold 範囲
                if (e.altCompleteThreshold < 0f || e.altCompleteThreshold > 1f)
                {
                    Debug.LogWarning($"[EventData] AltThreshold out of range (0..1): {e.eventId}={e.altCompleteThreshold}", e);
                }

                // 開始不可な組合せ
                if (!e.requiresButtonPress && !e.autoStartOnLocation)
                {
                    Debug.LogWarning($"[EventData] NoStartPath: {e.eventId}", e);
                }

                // 依存先存在確認
                if (e.dependencies != null)
                {
                    foreach (var dep in e.dependencies)
                    {
                        if (!string.IsNullOrEmpty(dep) && !eventSources.Exists(x => x && x.eventId == dep))
                        {
                            Debug.LogWarning($"[EventData] MissingDependency: {e.eventId} -> {dep}", e);
                        }
                    }
                }
            }

            // 循環依存チェック
            DetectCycles(eventSources);
        }

        /// <summary>循環依存をDFSで検出</summary>
        private void DetectCycles(List<Game.Data.EventData> sources)
        {
            var map = new Dictionary<string, List<string>>();
            foreach (var e in sources)
            {
                if (e == null || string.IsNullOrEmpty(e.eventId)) continue;
                map[e.eventId] = e.dependencies ?? new List<string>();
            }

            var visiting = new HashSet<string>();
            var visited = new HashSet<string>();

            foreach (var id in map.Keys)
            {
                if (HasCycle(id, map, visiting, visited))
                {
                    Debug.LogWarning($"[EventData] Cycle detected starting at: {id}");
                }
            }
        }

        private static bool HasCycle(string id, Dictionary<string, List<string>> map, HashSet<string> visiting, HashSet<string> visited)
        {
            if (visited.Contains(id)) return false;
            if (!map.TryGetValue(id, out var deps) || deps.Count == 0)
            {
                visited.Add(id);
                return false;
            }

            if (!visiting.Add(id)) return true; // 再訪問 → サイクル
            foreach (var d in deps)
            {
                if (map.ContainsKey(d) && HasCycle(d, map, visiting, visited)) return true;
            }
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }

        [System.Serializable]
        private class EventStateRow
        {
            public string id;
            public EventState state;
            public FailedReason reason;
            public string type;     // Main/Sub など
            public float progress;  // 0..1
        }

        public string ExportSnapshotJson()
        {
            var list = new System.Collections.Generic.List<EventStateRow>();
            foreach (var kv in _events)
            {
                var rt = kv.Value;
                list.Add(new EventStateRow
                {
                    id = kv.Key,
                    state = rt.State,
                    reason = rt.FailedReason,
                    type = rt.Data.type.ToString(),
                    progress = rt.Progress
                });
            }
            return JsonUtility.ToJson(new Wrapper<EventStateRow> { items = list.ToArray() }, true);
        }

        [System.Serializable]
        private class Wrapper<T> { public T[] items; }

        public string ExportSnapshotCsv()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("id,state,reason,type,progress");
            foreach (var kv in _events)
            {
                var rt = kv.Value;
                sb.Append(kv.Key).Append(',')
                  .Append(rt.State).Append(',')
                  .Append(rt.FailedReason).Append(',')
                  .Append(rt.Data.type).Append(',')
                  .Append(rt.Progress.ToString("0.00"))
                  .AppendLine();
            }
            return sb.ToString();
        }

        // Console にドンと出す
        public void DumpSnapshotToConsole()
        {
            Debug.Log("[EventManager] --- SNAPSHOT(JSON) ---\n" + ExportSnapshotJson(), this);
            Debug.Log("[EventManager] --- TRACE(Latest) ---\n" + string.Join("\n", GetRecentTrace()), this);
        }

        [ContextMenu("Debug/Dump Snapshot To Console")]
        private void CtxDump() => DumpSnapshotToConsole();

        [ContextMenu("Debug/Toggle Runtime Trace")]
        private void CtxToggleTrace() => enableRuntimeTrace = !enableRuntimeTrace;
#endif



    }
}