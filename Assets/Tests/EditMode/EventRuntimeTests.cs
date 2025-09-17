using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Events;
using Game.Data;
using Game.Runtime;

namespace Tests.EditMode
{
    // テストごとに振る舞いを切り替えるためのモック
    internal class FakeEvalContext : IEvalContext
    {
        public bool IsGloballyPaused { get; set; }
        public bool PolicyTreatStartOverAsExpired { get; set; }

        // フラグを切り替えて遷移分岐を検証
        public bool depsSatisfied = true;
        public bool appearReached = true;
        public bool startDeadlineExceeded = false;
        public bool endDeadlineReached = false;
        public bool calendarAllowed = true;
        public bool locationSatisfied = true;
        public bool interactionPossible = true;

        // 入力（このフレームで押下させたいとき true にする）
        public bool startInputReceived = false;
        private bool _startConsumed = false; // そのフレームでもう消費したか

        public bool DependenciesSatisfied(List<string> ids) => depsSatisfied;
        public bool NowReached(string gameDateTime) => appearReached;
        public bool StartDeadlineExceeded(string gameDateTime) => startDeadlineExceeded;
        public bool EndDeadlineReached(string gameDateTime) => endDeadlineReached;
        public bool CalendarAllowed(WeekdayRule rule) => calendarAllowed;
        public bool LocationSatisfied(LocationRef loc) => locationSatisfied;
        public bool InteractionPossible(Game.Data.EventData data) => interactionPossible;

        // ★新規：消費型入力（1フレームで最初の1回だけ true）
        public bool TryConsumeStartInput()
        {
            if (!startInputReceived || _startConsumed) return false;
            _startConsumed = true;
            return true;
        }

        // ★互換：既存テスト用。内部で消費型を利用
        public bool StartInputReceived() => TryConsumeStartInput();

        // （任意）テスト補助：次フレームへ進める前に呼ぶと扱いやすい
        public void NextFrame()
        {
            startInputReceived = false;
            _startConsumed = false;
        }

        // （任意）このフレームで1回だけ押す
        public void PressOnceThisFrame()
        {
            startInputReceived = true;
            _startConsumed = false;
        }
    }

    [TestFixture]
    public class EventRuntimeTests
    {
        private Game.Data.EventData MakeEventData(
            string id = "Test.1.1",
            Game.Events.EventType type = Game.Events.EventType.Sub,
            float altThreshold = 0.5f,
            bool requiresButtonPress = true)
        {
            var so = ScriptableObject.CreateInstance<Game.Data.EventData>();
            so.eventId = id;
            so.type = type;

            so.appearAt = "0001-00:00";
            so.startDeadline = "0001-00:00";
            so.endDeadline = "0001-00:00";

            so.location = new LocationRef { kind = LocationKind.AreaId, id = "Town/Plaza", worldPos = Vector3.zero };
            so.requiresButtonPress = requiresButtonPress;
            so.dependencies = new List<string>();
            so.altCompleteThreshold = altThreshold;
            so.weekdayRule = new WeekdayRule();
            so.notes = "unit test";

            return so;
        }

        // テスト1：Locked → Scheduled
        [Test]
        public void Locked_to_Scheduled_When_Dependencies_Appear_Calendar_OK()
        {
            var data = MakeEventData();
            var rt = new EventRuntime(data);
            var ctx = new FakeEvalContext { depsSatisfied = true, appearReached = true, calendarAllowed = true };

            string scheduledId = null;
            void OnScheduled(string id) => scheduledId = id;

            EventSignals.OnScheduled += OnScheduled;
            try
            {
                Assert.AreEqual(EventState.Locked, rt.State);
                rt.Evaluate(ctx);
                Assert.AreEqual(EventState.Scheduled, rt.State);
                Assert.AreEqual(data.eventId, scheduledId);
            }
            finally
            {
                EventSignals.OnScheduled -= OnScheduled;
                UnityEngine.Object.DestroyImmediate(data); // SO掃除（EditMode)
            }
        }

        // テスト2：依存未達なら Locked のまま
        [Test]
        public void Remains_Locked_When_Dependency_Not_Satisfied()
        {
            var data = MakeEventData();
            var rt = new EventRuntime(data);
            var ctx = new FakeEvalContext { depsSatisfied = false, appearReached = true, calendarAllowed = true };

            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.Locked, rt.State);

            UnityEngine.Object.DestroyImmediate(data);
        }

        // テスト3：Scheduled → Available
        [Test]
        public void Scheduled_to_Available_When_Location_And_Interaction_Possible()
        {
            var data = MakeEventData();
            var rt = new EventRuntime(data);
            var ctx = new FakeEvalContext();

            // Locked → Scheduled
            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.Scheduled, rt.State);

            string availableId = null;
            void OnAvailable(string id) => availableId = id;
            EventSignals.OnAvailable += OnAvailable;

            try
            {
                ctx.locationSatisfied = true;
                ctx.interactionPossible = true;
                rt.Evaluate(ctx);

                Assert.AreEqual(EventState.Available, rt.State);
                Assert.AreEqual(data.eventId, availableId);
            }
            finally
            {
                EventSignals.OnAvailable -= OnAvailable;
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        // テスト4：Available → InProgress
        [Test]
        public void Available_to_InProgress_On_Interact()
        {
            var data = MakeEventData();
            var rt = new EventRuntime(data);
            var ctx = new FakeEvalContext();

            rt.Evaluate(ctx); // Scheduled
            rt.Evaluate(ctx); // Available
            Assert.AreEqual(EventState.Available, rt.State);

            string startedId = null;
            void OnStarted(string id) => startedId = id;
            EventSignals.OnStarted += OnStarted;

            try
            {
                ctx.startInputReceived = true;
                rt.Evaluate(ctx);

                Assert.AreEqual(EventState.InProgress, rt.State);
                Assert.AreEqual(data.eventId, startedId);
            }
            finally
            {
                EventSignals.OnStarted -= OnStarted;
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        // テスト5：InProgress → Completed
        [Test]
        public void InProgress_To_Completed_When_EndDeadline_And_Progress_Above_Threshold()
        {
            var data = MakeEventData(altThreshold: 0.5f);
            var rt = new EventRuntime(data);
            var ctx = new FakeEvalContext();

            rt.Evaluate(ctx); // Scheduled
            rt.Evaluate(ctx); // Available
            ctx.startInputReceived = true;
            rt.Evaluate(ctx); // InProgress

            rt.SetProgress(0.75f);

            string completedId = null;
            void OnCompleted(string id) => completedId = id;
            EventSignals.OnCompleted += OnCompleted;

            try
            {
                ctx.endDeadlineReached = true;
                rt.Evaluate(ctx);

                Assert.AreEqual(EventState.Completed, rt.State);
                Assert.AreEqual(data.eventId, completedId);
            }
            finally
            {
                EventSignals.OnCompleted -= OnCompleted;
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        // テスト6：InProgress → Failed
        [Test]
        public void InProgress_To_Failed_When_EndDeadline_And_Progress_Below_Threshold()
        {
            var data = MakeEventData(altThreshold: 0.6f);
            var rt = new EventRuntime(data);
            var ctx = new FakeEvalContext();

            rt.Evaluate(ctx); // Scheduled
            rt.Evaluate(ctx); // Available
            ctx.startInputReceived = true;
            rt.Evaluate(ctx); // InProgress

            rt.SetProgress(0.5f);

            FailedReason? gotReason = null;
            void OnFailed(string id, FailedReason r) => gotReason = r;
            EventSignals.OnFailed += OnFailed;

            try
            {
                ctx.endDeadlineReached = true;
                rt.Evaluate(ctx);

                Assert.AreEqual(EventState.Failed, rt.State);
                Assert.AreEqual(FailedReason.MissedEndLowProgress, gotReason);
            }
            finally
            {
                EventSignals.OnFailed -= OnFailed;
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        // テスト7：開始期限超過 → Failed orExpired
        [Test]
        public void Scheduled_MissedStart_Becomes_Failed_Or_Expired_By_Policy()
        {
            var data = MakeEventData(); // appearAt/startDeadline/endDeadline を適切に持つ EventData

            // 1) まず Scheduled へ
            var primeCtx = new FakeEvalContext
            {
                depsSatisfied = true,
                appearReached = true,
                calendarAllowed = true,
                locationSatisfied = false,   // ★この時点では場所にいない（Availableに行かせない）
                interactionPossible = false
            };
            var rt = new EventRuntime(data);
            rt.Evaluate(primeCtx); // Locked -> Scheduled
            Assert.AreEqual(EventState.Scheduled, rt.State, "前提: Scheduled に到達していること");

            // ===== Failed(MissedStart) パス =====
            FailedReason? reason = null;
            void OnFailed(string id, FailedReason r) => reason = r;
            EventSignals.OnFailed += OnFailed;

            var ctxFailed = new FakeEvalContext
            {
                // 期限超過を最優先で拾わせる
                startDeadlineExceeded = true,
                PolicyTreatStartOverAsExpired = false,
                // 場所は引き続きNG（Availableに行かせない）
                locationSatisfied = false,
                interactionPossible = false
            };

            rt.Evaluate(ctxFailed);
            Assert.AreEqual(EventState.Failed, rt.State);
            Assert.AreEqual(FailedReason.MissedStart, reason);

            EventSignals.OnFailed -= OnFailed;

            // ===== Expired パス =====
            // 新規インスタンスで再検証
            rt = new EventRuntime(data);
            rt.Evaluate(primeCtx); // 再び Scheduled
            Assert.AreEqual(EventState.Scheduled, rt.State);

            bool expired = false;
            void OnExpired(string id) => expired = true;
            EventSignals.OnExpired += OnExpired;

            var ctxExpired = new FakeEvalContext
            {
                startDeadlineExceeded = true,
                PolicyTreatStartOverAsExpired = true,
                locationSatisfied = false,
                interactionPossible = false
            };

            rt.Evaluate(ctxExpired);
            Assert.AreEqual(EventState.Expired, rt.State);
            Assert.IsTrue(expired, "Expired シグナルが発火していること");

            EventSignals.OnExpired -= OnExpired;

            UnityEngine.Object.DestroyImmediate(data);
        }


        // テスト8：タイマー凍結の挙
        [Test]
        public void FreezeTimers_Stops_Evaluate_And_Raises_Signals()
        {
            var data = MakeEventData();
            var rt = new EventRuntime(data);
            var ctx = new FakeEvalContext();

            bool froze = false, resumed = false;
            void OnFrozen(string id) => froze = true;
            void OnResumed(string id) => resumed = true;

            EventSignals.OnTimerFrozen += OnFrozen;
            EventSignals.OnTimerResumed += OnResumed;

            try
            {
                rt.FreezeTimers(true);
                Assert.IsTrue(froze);

                rt.Evaluate(ctx); // 凍結中 → 変化なし
                Assert.AreEqual(EventState.Locked, rt.State);

                rt.FreezeTimers(false);
                Assert.IsTrue(resumed);

                rt.Evaluate(ctx); // 解除後 → Scheduled へ
                Assert.AreEqual(EventState.Scheduled, rt.State);
            }
            finally
            {
                EventSignals.OnTimerFrozen -= OnFrozen;
                EventSignals.OnTimerResumed -= OnResumed;
                UnityEngine.Object.DestroyImmediate(data);
            }
        }
    }
}