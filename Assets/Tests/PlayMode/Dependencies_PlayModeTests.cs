// Assets/Tests/PlayMode/Dependencies_PlayModeTests.cs
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Tests;   // PlayModeTestBase / TestHelpers

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 依存イベントの満たされ方と Available / Start の関係を、新仕様（時間でAvailable）で検証。
    /// ポイント:
    /// - 依存OK + 時間到達 + カレンダーOK → Available（場所は不問）
    /// - Start は「場所到達 or インタラクト」
    /// - 開始期限超過は MissedStart
    /// </summary>
    public class Dependencies_PlayModeTests : PlayModeTestBase
    {
        [SetUp] public void Setup2() => BaseSetup();
        [TearDown] public void Teardown2() => BaseTearDown();

        // 依存対象/本体イベントを作るヘルパ
        private static EventData MakeSO(
            string id, string appear, string startDL, string endDL,
            string areaId, bool autoStartOnLocation, bool requiresButtonPress,
            Game.Events.EventType type = Game.Events.EventType.Sub, float alt = 0.5f)
        {
            var e = ScriptableObject.CreateInstance<EventData>();
            e.eventId = id;
            e.type = type;
            e.appearAt = appear;            // "HH:MM"
            e.startDeadline = startDL;
            e.endDeadline = endDL;
            e.location = new LocationRef { kind = LocationKind.AreaId, id = areaId };
            e.autoStartOnLocation = autoStartOnLocation;
            e.requiresButtonPress = requiresButtonPress;
            e.altCompleteThreshold = alt;
            e.weekdayRule = new WeekdayRule(); // 許可
            e.dependencies = new System.Collections.Generic.List<string>();
            return e;
        }

        [Test]
        public void NotAvailable_when_time_reached_but_dependency_not_met()
        {
            var dep = MakeSO("D1", "07:00", "08:59", "10:00", "Square", true, true);
            var target = MakeSO("T1", "08:00", "09:30", "11:00", "Square", true, true);
            target.dependencies.Add("D1");

            em.InitializeForTest(new[] { dep, target });

            using var sig = new TestHelpers.SignalCatcher();

            // 依存が未完のまま 08:00 到達 → T1 は Locked のまま（Available にならない）
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual(EventState.Locked, GetRuntime(em, "T1").State,
                "依存未解決のため T1 は Locked のまま");

            GetRuntime(em, "D1").RestoreForTest(EventState.Completed, FailedReason.None, 1f);

            // Locked→Scheduled（1回目）、Scheduled→Available（2回目）
            TestHelpers.Tick(em, 2);

            Assert.AreEqual(EventState.Available, GetRuntime(em, "T1").State);
            Assert.IsNull(sig.Started); // まだ開始トリガを与えていない
        }

        [Test]
        public void Available_when_dependency_met_then_time_reaches()
        {
            // 先に依存を満たし、時間が来たら Available
            var dep = MakeSO("D2", "07:00", "08:59", "10:00", "Square", true, true);
            var target = MakeSO("T2", "08:00", "09:30", "11:00", "Square", true, true);
            target.dependencies.Add("D2");

            em.InitializeForTest(new[] { dep, target });
            using var sig = new TestHelpers.SignalCatcher();

            // 依存を先に完了
            GetRuntime(em, "D2").RestoreForTest(EventState.Completed, FailedReason.None, 1f);
            TestHelpers.Tick(em, 1);

            // まだ時間に達していないので Available は出ない
            TestHelpers.AdvanceTo(em, clockGO, "07:59");
            Assert.IsNull(sig.Available);

            // 時間到達 → Available（場所は不問）
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual("T2", sig.Available);
        }

        [Test]
        public void Start_by_location_when_autoStartOnLocation_true()
        {
            var dep = MakeSO("D3", "07:00", "08:59", "10:00", "Square", true, true);
            var target = MakeSO("T3", "08:00", "09:30", "11:00", "Square", true, true);
            target.dependencies.Add("D3");

            em.InitializeForTest(new[] { dep, target });
            using var sig = new TestHelpers.SignalCatcher();

            // 依存完了 → 時間到達で Available
            GetRuntime(em, "D3").RestoreForTest(EventState.Completed, FailedReason.None, 1f);
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual("T3", sig.Available);
            Assert.IsNull(sig.Started);

            // 場所到達で Start（autoStartOnLocation = true）
            locator.SetArea("Square");
            TestHelpers.Tick(em, 1);
            Assert.AreEqual("T3", sig.Started);
        }

        [Test]
        public void Start_by_interact_when_autoStartOnLocation_false()
        {
            var dep = MakeSO("D4", "07:00", "08:59", "10:00", "Square", true, true);
            var target = MakeSO("T4", "08:00", "09:30", "11:00", "Square",
                                autoStartOnLocation: false, requiresButtonPress: true);
            target.dependencies.Add("D4");

            em.InitializeForTest(new[] { dep, target });
            using var sig = new TestHelpers.SignalCatcher();

            // 依存完了 → 時間到達で Available
            GetRuntime(em, "D4").RestoreForTest(EventState.Completed, FailedReason.None, 1f);
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual("T4", sig.Available);

            // 場所だけでは開始しない
            locator.SetArea("Square");
            TestHelpers.Tick(em, 1);
            Assert.IsNull(sig.Started);

            // インタラクトで開始
            input.PressOnce();
            TestHelpers.Tick(em, 1);
            Assert.AreEqual("T4", sig.Started);
        }

        [Test]
        public void MissedStart_when_available_but_no_trigger_until_deadline()
        {
            var dep = MakeSO("D5", "07:00", "08:29", "10:00", "Square", true, true);
            var target = MakeSO("T5", "08:00", "08:30", "11:00", "Square", true, true);
            target.dependencies.Add("D5");

            em.InitializeForTest(new[] { dep, target });
            using var sig = new TestHelpers.SignalCatcher();

            // 依存完了 → 時間到達で Available（放置）
            GetRuntime(em, "D5").RestoreForTest(EventState.Completed, FailedReason.None, 1f);
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual("T5", sig.Available);
            Assert.IsNull(sig.Started);

            // 開始期限を「超える」まで待つ（等号は超過に含まれないポリシー）
            TestHelpers.AdvanceTo(em, clockGO, "08:31");
            Assert.AreEqual(("T5", FailedReason.MissedStart), sig.Failed);
        }
    }
}
