using Game.Data;
using Game.Events;
using Game.Runtime;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Tests.EditMode
{
    // 既存 IEvalContext ベースの最小モック
    class Ctx : IEvalContext
    {
        public bool IsGloballyPaused { get; set; }
        public bool PolicyTreatStartOverAsExpired { get; set; } = false;

        public HashSet<string> completed = new();
        public bool depsOK = true;
        public bool nowOK = true;
        public bool startDeadlineExceeded = false;
        public bool endDeadlineReached = false;
        public bool calendarOK = true;
        public bool locationOK = false;

        public bool inputEdge = false;   // このフレームだけ true にする

        public bool DependenciesSatisfied(List<string> ids) => depsOK;
        public bool NowReached(string gameDateTime) => nowOK;
        public bool StartDeadlineExceeded(string gameDateTime) => startDeadlineExceeded;
        public bool EndDeadlineReached(string gameDateTime) => endDeadlineReached;
        public bool CalendarAllowed(WeekdayRule rule) => calendarOK;
        public bool LocationSatisfied(LocationRef loc) => locationOK;

        public bool InteractionPossible(EventData data) => true; // 互換のため未使用
        public bool StartInputReceived() => inputEdge;
        public bool TryConsumeStartInput() { var v = inputEdge; inputEdge = false; return v; }
    }

    public class EventRuntimeTests_NewSpec
    {
        EventData MakeData(bool autoStartOnLocation = true, bool requiresButtonPress = true)
        {
            var d = ScriptableObject.CreateInstance<EventData>();
            d.eventId = "1.1";
            d.appearAt = "0000-12:00";
            d.startDeadline = "0000-18:00";
            d.endDeadline = "0000-20:00";
            d.location = new LocationRef { kind = LocationKind.AreaId, id = "Square" };
            d.autoStartOnLocation = autoStartOnLocation;
            d.requiresButtonPress = requiresButtonPress;
            d.altCompleteThreshold = 0.5f;
            d.weekdayRule = new WeekdayRule(); // 許可
            return d;
        }

        [Test]
        public void Locked_to_Scheduled_when_Deps_Time_Calendar_OK()
        {
            var ctx = new Ctx { depsOK = true, nowOK = true, calendarOK = true };
            var rt = new EventRuntime(MakeData());
            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.Scheduled, rt.State);
        }

        [Test]
        public void Scheduled_to_Available_is_Time_only()
        {
            var ctx = new Ctx { depsOK = true, nowOK = true, calendarOK = true };
            var rt = new EventRuntime(MakeData());
            // Locked -> Scheduled
            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.Scheduled, rt.State);

            // ★旧仕様では場所OK等が必要だったが、新仕様では時間のみで Available
            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.Available, rt.State);
        }

        [Test]
        public void Available_to_InProgress_by_Location_or_Interact()
        {
            var ctx = new Ctx { depsOK = true, nowOK = true, calendarOK = true };
            var rt = new EventRuntime(MakeData(autoStartOnLocation: true));

            // L->S->A
            rt.Evaluate(ctx); // -> Scheduled
            rt.Evaluate(ctx); // -> Available
            Assert.AreEqual(EventState.Available, rt.State);

            // (A) 場所到達で開始
            ctx.locationOK = true;
            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.InProgress, rt.State);
        }

        [Test]
        public void Available_to_InProgress_by_Interact_when_AutoStartOff()
        {
            var ctx = new Ctx { depsOK = true, nowOK = true, calendarOK = true };
            var rt = new EventRuntime(MakeData(autoStartOnLocation: false, requiresButtonPress: true));

            // L->S->A
            rt.Evaluate(ctx);
            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.Available, rt.State);

            // 位置だけでは開始しない
            ctx.locationOK = true;
            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.Available, rt.State);

            // (B) インタラクトで開始
            ctx.inputEdge = true;
            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.InProgress, rt.State);
        }

        [Test]
        public void MissedStart_when_Stayed_Available_until_StartDeadline()
        {
            var ctx = new Ctx { depsOK = true, nowOK = true, calendarOK = true };
            var rt = new EventRuntime(MakeData());

            // L->S->A
            rt.Evaluate(ctx);
            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.Available, rt.State);

            // 開始せず開始期限超過
            ctx.startDeadlineExceeded = true;
            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.Failed, rt.State);
            Assert.AreEqual(FailedReason.MissedStart, rt.FailedReason);
        }

        [Test]
        public void Complete_or_Failed_on_EndDeadline_by_Progress()
        {
            var ctx = new Ctx { depsOK = true, nowOK = true, calendarOK = true };
            var rt = new EventRuntime(MakeData());
            // -> InProgress まで進める
            rt.Evaluate(ctx); rt.Evaluate(ctx);
            ctx.inputEdge = true; rt.Evaluate(ctx);
            Assert.AreEqual(EventState.InProgress, rt.State);

            // (a) 閾値以上で Completed
            rt.SetProgress(0.6f);
            ctx.endDeadlineReached = true;
            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.Completed, rt.State);

            // リセットして (b) 閾値未満で Failed
            rt.RestoreForTest(EventState.InProgress, FailedReason.None, 0.4f);
            ctx.endDeadlineReached = true;
            rt.Evaluate(ctx);
            Assert.AreEqual(EventState.Failed, rt.State);
            Assert.AreEqual(FailedReason.MissedEndLowProgress, rt.FailedReason);
        }
    }
}
