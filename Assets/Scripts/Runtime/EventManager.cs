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

        public bool LocationSatisfied(Game.Events.LocationRef loc)  // 発生場所にいるかの判定
        {
            return Locator == null || Locator.IsSatisfied(loc);
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
#endif


    }
}