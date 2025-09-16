namespace Game.Tests
{
    // Assets/Tests/Common/TestHelpers.cs
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEngine;
    using Game.Events;
    using Game.Runtime;


    /// <summary>
    /// 反射・時間・DI・シグナル購読など、テスト共通のヘルパ群。
    /// EditMode/PlayMode 共通で利用可能。
    /// </summary>
    public static class TestHelpers
    {
        //===============================
        // 反射（private field アクセス）
        //===============================
        public static FieldInfo PF(Type t, string name)
            => t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        public static void SetPrivateField(object target, string name, object value)
        {
            var f = PF(target.GetType(), name);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{name} が見つかりません");
            f.SetValue(target, value);
        }

        public static T GetPrivateField<T>(object target, string name)
        {
            var f = PF(target.GetType(), name);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{name} が見つかりません");
            return (T)f.GetValue(target);
        }

        // EventManager の内部ディクショナリ取得
        public static Dictionary<string, EventRuntime> GetRuntimeDict(Game.Runtime.EventManager em)
            => GetPrivateField<Dictionary<string, EventRuntime>>(em, "_events");

        public static EventRuntime GetRuntime(Game.Runtime.EventManager em, string id)
        {
            var dict = GetRuntimeDict(em);
            Assert.IsTrue(dict.ContainsKey(id), $"_events に {id} が存在しません");
            return dict[id];
        }

        //===============================
        // DI 注入（フィールド名をここで集約）
        //===============================
        public static class EmField
        {
            public const string GlobalSettings = "globalSettings";
            public const string ClockBehaviour = "clockBehaviour";
            public const string LocationBehaviour = "locationBehaviour";
            public const string InputBehaviour = "inputBehaviour";
            public const string TestPause = "testPause"; // Pause用（実装に合わせて）
        }

        public static void Inject(Game.Runtime.EventManager em,
                                    MonoBehaviour clock,
                                    MonoBehaviour locator,
                                    MonoBehaviour input,
                                    ScriptableObject globalSettings = null)
        {
            if (globalSettings != null)
                SetPrivateField(em, EmField.GlobalSettings, globalSettings);

            if (clock != null) SetPrivateField(em, EmField.ClockBehaviour, clock);
            if (locator != null) SetPrivateField(em, EmField.LocationBehaviour, locator);
            if (input != null) SetPrivateField(em, EmField.InputBehaviour, input);
        }

        public static void SetPaused(Game.Runtime.EventManager em, bool paused)
            => SetPrivateField(em, EmField.TestPause, paused);

        //===============================
        // 時間制御（テスト安定化）
        //===============================
        /// <summary>PlayModeで実時間進行を止める（SetUpで呼ぶ）</summary>
        public static float PauseRealtime()
        {
            var prev = Time.timeScale;
            Time.timeScale = 0f;
            return prev;
        }

        /// <summary>PlayModeで実時間進行を戻す（TearDownで呼ぶ）</summary>
        public static void ResumeRealtime(float previousTimeScale)
        {
            Time.timeScale = previousTimeScale;
        }

        /// <summary>「分＝秒」換算の “HH:MM” を秒(float)へ。</summary>
        public static float HHMM(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            var sp = s.Split(':');
            if (sp.Length < 2) return 0f;
            return int.Parse(sp[0]) * 60 + int.Parse(sp[1]);
        }

        /// <summary>ゲーム内時刻を s にジャンプし、到達→確定 の2評価を行う。</summary>
        public static void AdvanceTo(Game.Runtime.EventManager em, GameObject clockGO, string s)
        {
            // SimpleClock を想定：Jump(float) メソッドを呼ぶ
            var clock = clockGO.GetComponent<MonoBehaviour>();
            var mi = clock.GetType().GetMethod("Jump", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(mi, "SimpleClock に Jump(float) がありません");
            mi.Invoke(clock, new object[] { HHMM(s) });

            Tick(em, 2); // 到達→確定
        }

        /// <summary>EvaluateFrame を n 回呼ぶ。</summary>
        public static void Tick(Game.Runtime.EventManager em, int n = 1)
        {
            for (int i = 0; i < n; i++) em.EvaluateFrame();
        }

        //===============================
        // シグナル購読（簡易キャプチャ）
        //===============================
        public sealed class SignalCatcher : IDisposable
        {
            // 単発（最後の1件）
            public string Scheduled;
            public string Available;
            public string Started;
            public string Completed;
            public (string id, FailedReason reason)? Failed;
            public string Expired;

            // ★ 履歴（複数回発火を時系列に保持）追加
            public readonly List<string> ScheduledList = new();
            public readonly List<string> AvailableList = new();
            public readonly List<string> StartedList = new();
            public readonly List<string> CompletedList = new();
            public readonly List<(string id, FailedReason reason)> FailedList = new();
            public readonly List<string> ExpiredList = new();

            public SignalCatcher(bool wire = true)
            {
                if (wire) Wire();
            }

            public void Wire()
            {
                EventSignals.OnScheduled += OnScheduled;
                EventSignals.OnAvailable += OnAvailable;
                EventSignals.OnStarted += OnStarted;
                EventSignals.OnCompleted += OnCompleted;
                EventSignals.OnFailed += OnFailed;
                EventSignals.OnExpired += OnExpired;
            }

            public void Unwire()
            {
                EventSignals.OnScheduled -= OnScheduled;
                EventSignals.OnAvailable -= OnAvailable;
                EventSignals.OnStarted -= OnStarted;
                EventSignals.OnCompleted -= OnCompleted;
                EventSignals.OnFailed -= OnFailed;
                EventSignals.OnExpired -= OnExpired;
            }

            void OnScheduled(string id) { Scheduled = id; ScheduledList.Add(id); }
            void OnAvailable(string id) { Available = id; AvailableList.Add(id); }
            void OnStarted(string id) { Started = id; StartedList.Add(id); }
            void OnCompleted(string id) { Completed = id; CompletedList.Add(id); }
            void OnFailed(string id, FailedReason reason) { Failed = (id, reason); FailedList.Add((id, reason)); }
            void OnExpired(string id) { Expired = id; ExpiredList.Add(id); }

            public void Clear()
            {
                Scheduled = Available = Started = Completed = Expired = null;
                Failed = null;
                ScheduledList.Clear();
                AvailableList.Clear();
                StartedList.Clear();
                CompletedList.Clear();
                FailedList.Clear();
                ExpiredList.Clear();
            }

            public void Dispose() => Unwire();
        }

        //===============================
        // よく使う定型操作
        //===============================

        /// <summary>
        /// AutoStart（requiresButtonPress=false）で確実に Start させる。
        /// 場所セット→EvaluateFrame×2（Scheduled→Available→Start）。
        /// </summary>
        public static void EnsureAutoStart(EventManager em, SimpleLocationResolver locator, string areaId)
        {
            locator.SetArea(areaId);
            Tick(em, 1); // Locked→Scheduled
            Tick(em, 1); // Scheduled→Available（requiresButtonPress=false ならこのフレームでStart）
                         // 実装によってはもう1Tick必要な場合もあるので、呼び出し側で追加Tickして調整してもOK
        }
        public static void PressToStart(EventManager em, TestInputProxy input)
        {
            input.PressOnce();
            Tick(em, 1); // Available→InProgress
        }
        public static void AdvanceTo(EventManager em, SimpleClock clock, string hhmm)
        {
            clock.Jump(HHMM(hhmm));
            Tick(em, 2);
        }

        // 目標状態になるまで最大Nフレーム回す（デバッグ用）
        public static void TickUntil(EventManager em, string id, EventState target, int maxTicks = 8)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                if (GetRuntime(em, id).State == target) return;
                Tick(em, 1);
            }
            Assert.Fail($"{id} が {target} に到達しませんでした（{maxTicks} ticks 走査）");
        }

        /// <summary>
        /// 現在の EventRuntime.State を Asserts 付きで確認。
        /// </summary>
        public static void AssertState(Game.Runtime.EventManager em, string id, Game.Events.EventState expected)
        {
            var st = GetRuntime(em, id).State;
            Assert.AreEqual(expected, st, $"{id} の状態が想定({expected})と異なります: {st}");
        }
        public static void AssertState(Game.Runtime.EventManager em, string id, EventState expected, string msg)
        {
            var st = GetRuntime(em, id).State;
            Assert.AreEqual(expected, st, msg);
        }

        /// <summary>
        /// イベントを確実に Started 状態にする（ボタン必要）
        /// Locked→Scheduled→Available→(入力)→InProgress までをまとめる。
        /// </summary>
        public static void EnsureStarted(Game.Runtime.EventManager em,
                                         string eventId,
                                         System.Action pressInput)
        {
            Tick(em); // Locked → Scheduled
            Tick(em); // Scheduled → Available

            pressInput?.Invoke();
            Tick(em); // Available → InProgress

            // 念のための保険（環境差で取りこぼしがある場合）
            if (GetRuntime(em, eventId).State != Game.Events.EventState.InProgress)
                Tick(em);
        }

        /// <summary>
        /// AutoStart（requiresButtonPress=false）版：
        /// Locked→Scheduled→Available→(自動開始)→InProgress まで進める。
        /// </summary>
        public static void EnsureAutoStarted(Game.Runtime.EventManager em, string eventId)
        {
            Tick(em); // Locked → Scheduled
            Tick(em); // Scheduled → Available（この先で自動開始実装なら InProgress へ）
                      // 実装差の吸収（Available→InProgress が次フレームになるケースに対応）
            if (GetRuntime(em, eventId).State != Game.Events.EventState.InProgress)
                Tick(em);
        }
    }
}
