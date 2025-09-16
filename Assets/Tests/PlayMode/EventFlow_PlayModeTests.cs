// Assets/Tests/PlayMode/EventFlow_PlayModeTests.cs
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Runtime;
using System.Collections.Generic;
using Game.Tests;
using System;

public class EventFlow_PlayModeTests
{
    GameObject root, emGO, clockGO, locGO, inputGO;
    EventManager em;
    SimpleClock clock;
    SimpleLocationResolver locator;
    TestInputProxy input;

    float _prevTimeScale;

    [SetUp]
    public void SetUp()
    {
        // 実時間停止（SimpleClock は Jump のみで進む）
        _prevTimeScale = TestHelpers.PauseRealtime();

        root = new GameObject("ROOT");

        clockGO = new GameObject("Clock");
        clock = clockGO.AddComponent<SimpleClock>();
        clockGO.transform.SetParent(root.transform, false);

        locGO = new GameObject("Locator");
        locator = locGO.AddComponent<SimpleLocationResolver>();
        locGO.transform.SetParent(root.transform, false);

        inputGO = new GameObject("Input");
        input = inputGO.AddComponent<TestInputProxy>();
        inputGO.transform.SetParent(root.transform, false);

        emGO = new GameObject("EventManager");
        em = emGO.AddComponent<EventManager>();
        emGO.transform.SetParent(root.transform, false);

        // GlobalSettings 注入（分＝秒換算 1日=1440）
        var settings = ScriptableObject.CreateInstance<Game.Config.GlobalSettings>();
        settings.dayLengthSeconds = 1440f;

        // 依存（DI）注入
        TestHelpers.Inject(em, clock, locator, input, settings);
    }

    [TearDown]
    public void TearDown()
    {
        // 念のため Pause フラグを解除して次のテストに影響しないようにする
        try
        {
            Game.Tests.TestHelpers.SetPaused(em, false);
        }
        catch
        {
            // em が破棄済み、または TestHelpers が見えないケースでは無視
        }

        UnityEngine.Object.DestroyImmediate(root);
        Time.timeScale = _prevTimeScale; // 元に戻す
    }


    // ===== ヘルパ =====
    private void InitEvents(params EventData[] eventsToUse)
    {
        em.InitializeForTest(eventsToUse);
        // 初期フレームの取りこぼし防止（時刻0で一度評価）
        clock.Jump(0f);
    }

    private EventData MakeEvent(string id, string appear, string startDL, string endDL, string areaId, float alt = 0.5f, bool requiresBtn = true)
    {
        var e = ScriptableObject.CreateInstance<EventData>();
        e.eventId = id;
        e.type = Game.Events.EventType.Sub;
        e.appearAt = appear;          // "HH:MM"（分=秒）
        e.startDeadline = startDL;
        e.endDeadline = endDL;
        e.location = new LocationRef { kind = LocationKind.AreaId, id = areaId };
        e.requiresButtonPress = requiresBtn;
        e.dependencies = new List<string>();
        e.altCompleteThreshold = alt;
        e.weekdayRule = new WeekdayRule();
        return e;
    }

    // ===== テスト =====
    [Test]
    public void GoldenPath_Scheduled_Available_InProgress_Completed()
    {
        var ev = MakeEvent("Sub.Test", "00:10", "01:00", "02:00", "Town/Plaza", 0.5f);
        InitEvents(ev);

        using var sig = new Game.Tests.TestHelpers.SignalCatcher();

        // 00:10 まで進めてから開始操作
        clock.Jump(10f);
        em.EvaluateFrame();

        locator.SetArea("Town/Plaza");
        Game.Tests.TestHelpers.EnsureStarted(em, "Sub.Test", () => input.PressOnce());

        // 閾値超えさせる
        Game.Tests.TestHelpers.GetRuntime(em, "Sub.Test").SetProgress(0.8f);

        // End で Completed
        Game.Tests.TestHelpers.AdvanceTo(em, clockGO, "02:00");
        Assert.AreEqual("Sub.Test", sig.Completed);
    }

    [Test]
    public void MissedEnd_Fails_When_Progress_Below_Threshold()
    {
        var ev = MakeEvent("Sub.Fail", "00:00", "00:05", "00:10", "Field", 0.6f);
        InitEvents(ev);

        Game.Events.FailedReason? got = null;
        using var sig = new Game.Tests.TestHelpers.SignalCatcher();
        EventSignals.OnFailed += (id, r) => { if (id == "Sub.Fail") got = r; };

        locator.SetArea("Field");
        Game.Tests.TestHelpers.EnsureStarted(em, "Sub.Fail", () => input.PressOnce());

        // 進捗は閾値未満のまま
        Game.Tests.TestHelpers.GetRuntime(em, "Sub.Fail").SetProgress(0.2f);

        // 終了到達 → Failed(MissedEndLowProgress)
        Game.Tests.TestHelpers.AdvanceTo(em, clockGO, "00:10");
        Assert.AreEqual(Game.Events.FailedReason.MissedEndLowProgress, got);
    }


    [Test]
    public void Expired_When_Miss_StartWindow()
    {
        var ev = MakeEvent("Sub.Expire", "00:00", "00:05", "00:20", "Field", 0.5f);
        InitEvents(ev);

        Game.Events.FailedReason? got = null;
        using var sig = new TestHelpers.SignalCatcher();
        EventSignals.OnFailed += (id, r) => { if (id == "Sub.Expire") got = r; };

        // 場所にいない → Availableにならないまま開始期限超過
        TestHelpers.AdvanceTo(em, clockGO, "00:06");
        Assert.AreEqual(Game.Events.FailedReason.MissedStart, got);
    }

    // 3) requiresButtonPress=false の自動開始
    [Test]
    public void AutoStart_When_ButtonNotRequired()
    {
        var ev = MakeEvent("Sub.Auto", "00:00", "00:10", "00:20", "A");
        ev.requiresButtonPress = false;
        InitEvents(ev);

        using var sig = new Game.Tests.TestHelpers.SignalCatcher();

        locator.SetArea("A");
        Game.Tests.TestHelpers.EnsureAutoStarted(em, "Sub.Auto");

        // クリアに必要なら進捗を設定
        Game.Tests.TestHelpers.GetRuntime(em, "Sub.Auto").SetProgress(1f);

        Game.Tests.TestHelpers.AdvanceTo(em, clockGO, "00:20");
        Assert.AreEqual("Sub.Auto", sig.Started, "AutoStartで InProgress になっているはず");
        Assert.AreEqual("Sub.Auto", sig.Completed, "終期で Completed になるはず");
    }


    // 4) 進捗＝閾値ちょうどで Completed
    [Test]
    public void Completed_When_Progress_Equals_Threshold()
    {
        var ev = MakeEvent("Sub.Equal", "00:00", "00:10", "00:20", "A", 0.6f);
        InitEvents(ev);

        using var sig = new Game.Tests.TestHelpers.SignalCatcher();

        // 場所をセット
        locator.SetArea("A");

        // ★ ヘルパで確実に Started まで進める
        Game.Tests.TestHelpers.EnsureStarted(em, "Sub.Equal", () => input.PressOnce());

        // 進捗＝閾値ちょうど
        Game.Tests.TestHelpers.GetRuntime(em, "Sub.Equal").SetProgress(0.6f);

        // 終了到達 → Completed
        Game.Tests.TestHelpers.AdvanceTo(em, clockGO, "00:20");
        Assert.AreEqual("Sub.Equal", sig.Completed, "閾値ちょうどで Completed になるはず");
    }


    // 5) ポーズで評価を止める（IsGloballyPaused）
    [Test]
    public void Pause_Stops_Evaluation()
    {
        var ev = MakeEvent("Sub.Pause", "00:00", "00:10", "00:20", "A");
        InitEvents(ev);

        using var sig = new TestHelpers.SignalCatcher();

        // Pause ON（最初に）
        TestHelpers.SetPaused(em, true);

        // 評価しても Scheduled は出ない
        TestHelpers.AdvanceTo(em, clockGO, "00:00");
        Assert.IsNull(sig.Scheduled, "Pause中はScheduledが発火しないはず");
        TestHelpers.AssertState(em, "Sub.Pause", Game.Events.EventState.Locked);

        // Pause OFF → 初めて進む
        TestHelpers.SetPaused(em, false);
        TestHelpers.Tick(em);
        Assert.AreEqual("Sub.Pause", sig.Scheduled, "Pause解除後にScheduledが発火するはず");
    }

    // デバッグ用（必要なときだけ使う）
    private void WireLogs()
    {
        EventSignals.OnScheduled += id => Debug.Log("[SIG] Scheduled " + id);
        EventSignals.OnAvailable += id => Debug.Log("[SIG] Available " + id);
        EventSignals.OnStarted += id => Debug.Log("[SIG] Started " + id);
        EventSignals.OnCompleted += id => Debug.Log("[SIG] Completed " + id);
        EventSignals.OnFailed += (id, r) => Debug.Log("[SIG] Failed " + id + " (" + r + ")");
        EventSignals.OnExpired += id => Debug.Log("[SIG] Expired " + id);
    }
}


//// Assets/Tests/Common/TestHelpers.cs
//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using NUnit.Framework;
//using UnityEngine;
//using Game.Events;
//using Game.Runtime;

//namespace Game.Tests
//{
//    /// <summary>
//    /// 反射・時間・DI・シグナル購読など、テスト共通のヘルパ群。
//    /// EditMode/PlayMode 共通で利用可能。
//    /// </summary>
//    public static class TestHelpers
//    {
//        //===============================
//        // 反射（private field アクセス）
//        //===============================
//        public static FieldInfo PF(Type t, string name)
//            => t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

//        public static void SetPrivateField(object target, string name, object value)
//        {
//            var f = PF(target.GetType(), name);
//            Assert.IsNotNull(f, $"{target.GetType().Name}.{name} が見つかりません");
//            f.SetValue(target, value);
//        }

//        public static T GetPrivateField<T>(object target, string name)
//        {
//            var f = PF(target.GetType(), name);
//            Assert.IsNotNull(f, $"{target.GetType().Name}.{name} が見つかりません");
//            return (T)f.GetValue(target);
//        }

//        // EventManager の内部ディクショナリ取得
//        public static Dictionary<string, EventRuntime> GetRuntimeDict(EventManager em)
//            => GetPrivateField<Dictionary<string, EventRuntime>>(em, "_events");

//        public static EventRuntime GetRuntime(EventManager em, string id)
//        {
//            var dict = GetRuntimeDict(em);
//            Assert.IsTrue(dict.ContainsKey(id), $"_events に {id} が存在しません");
//            return dict[id];
//        }

//        //===============================
//        // DI 注入（フィールド名をここで集約）
//        //===============================
//        public static class EmField
//        {
//            public const string GlobalSettings = "globalSettings";
//            public const string ClockBehaviour = "clockBehaviour";
//            public const string LocationBehaviour = "locationBehaviour";
//            public const string InputBehaviour = "inputBehaviour";
//            public const string TestPause = "testPause"; // Pause用（実装に合わせて）
//        }

//        public static void Inject(EventManager em,
//                                  MonoBehaviour clock,
//                                  MonoBehaviour locator,
//                                  MonoBehaviour input,
//                                  ScriptableObject globalSettings = null)
//        {
//            if (globalSettings != null)
//                SetPrivateField(em, EmField.GlobalSettings, globalSettings);

//            if (clock != null) SetPrivateField(em, EmField.ClockBehaviour, clock);
//            if (locator != null) SetPrivateField(em, EmField.LocationBehaviour, locator);
//            if (input != null) SetPrivateField(em, EmField.InputBehaviour, input);
//        }

//        public static void SetPaused(EventManager em, bool paused)
//            => SetPrivateField(em, EmField.TestPause, paused);

//        //===============================
//        // 時間制御（テスト安定化）
//        //===============================
//        /// <summary>PlayModeで実時間進行を止める（SetUpで呼ぶ）</summary>
//        public static float PauseRealtime()
//        {
//            var prev = Time.timeScale;
//            Time.timeScale = 0f;
//            return prev;
//        }

//        /// <summary>PlayModeで実時間進行を戻す（TearDownで呼ぶ）</summary>
//        public static void ResumeRealtime(float previousTimeScale)
//        {
//            Time.timeScale = previousTimeScale;
//        }

//        /// <summary>「分＝秒」換算の “HH:MM” を秒(float)へ。</summary>
//        public static float HHMM(string s)
//        {
//            if (string.IsNullOrEmpty(s)) return 0f;
//            var sp = s.Split(':');
//            if (sp.Length < 2) return 0f;
//            return int.Parse(sp[0]) * 60 + int.Parse(sp[1]);
//        }

//        /// <summary>ゲーム内時刻を s にジャンプし、到達→確定 の2評価を行う。</summary>
//        public static void AdvanceTo(EventManager em, GameObject clockGO, string s)
//        {
//            // SimpleClock を想定：Jump(float) メソッドを呼ぶ
//            var clock = clockGO.GetComponent<MonoBehaviour>();
//            var mi = clock.GetType().GetMethod("Jump", BindingFlags.Public | BindingFlags.Instance);
//            Assert.IsNotNull(mi, "SimpleClock に Jump(float) がありません");
//            mi.Invoke(clock, new object[] { HHMM(s) });

//            Tick(em, 2); // 到達→確定
//        }

//        /// <summary>EvaluateFrame を n 回呼ぶ。</summary>
//        public static void Tick(EventManager em, int n = 1)
//        {
//            for (int i = 0; i < n; i++) em.EvaluateFrame();
//        }

//        //===============================
//        // シグナル購読（簡易キャプチャ）
//        //===============================
//        public sealed class SignalCatcher : IDisposable
//        {
//            public string Scheduled;
//            public string Available;
//            public string Started;
//            public string Completed;
//            public (string id, FailedReason reason)? Failed;
//            public string Expired;

//            public SignalCatcher(bool wire = true)
//            {
//                if (wire) Wire();
//            }

//            public void Wire()
//            {
//                EventSignals.OnScheduled += OnScheduled;
//                EventSignals.OnAvailable += OnAvailable;
//                EventSignals.OnStarted += OnStarted;
//                EventSignals.OnCompleted += OnCompleted;
//                EventSignals.OnFailed += OnFailed;
//                EventSignals.OnExpired += OnExpired;
//            }

//            public void Unwire()
//            {
//                EventSignals.OnScheduled -= OnScheduled;
//                EventSignals.OnAvailable -= OnAvailable;
//                EventSignals.OnStarted -= OnStarted;
//                EventSignals.OnCompleted -= OnCompleted;
//                EventSignals.OnFailed -= OnFailed;
//                EventSignals.OnExpired -= OnExpired;
//            }

//            void OnScheduled(string id) => Scheduled = id;
//            void OnAvailable(string id) => Available = id;
//            void OnStarted(string id) => Started = id;
//            void OnCompleted(string id) => Completed = id;
//            void OnFailed(string id, FailedReason r) => Failed = (id, r);
//            void OnExpired(string id) => Expired = id;

//            public void Clear()
//            {
//                Scheduled = Available = Started = Completed = Expired = null;
//                Failed = null;
//            }

//            public void Dispose() => Unwire();
//        }

//        //===============================
//        // よく使う定型操作
//        //===============================

//        /// <summary>
//        /// AutoStart（requiresButtonPress=false）で確実に Start させる。
//        /// 場所セット→EvaluateFrame×2（Scheduled→Available→Start）。
//        /// </summary>
//        public static void EnsureAutoStart(EventManager em, string areaId)
//        {
//            // locator.SetArea(areaId) を呼ぶ（SimpleLocationResolver に合わせた反射）
//            // 直接参照できるならテスト側で呼んでもOK。
//            Tick(em, 1); // Scheduled
//            Tick(em, 1); // Available→AutoStart
//        }

//        /// <summary>
//        /// 現在の EventRuntime.State を Asserts 付きで確認。
//        /// </summary>
//        public static void AssertState(EventManager em, string id, Game.Events.EventState expected)
//        {
//            var st = GetRuntime(em, id).State;
//            Assert.AreEqual(expected, st, $"{id} の状態が想定({expected})と異なります: {st}");
//        }
//    }
//}
