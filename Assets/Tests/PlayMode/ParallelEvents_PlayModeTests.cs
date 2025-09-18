// Assets/Tests/PlayMode/ParallelEvents_PlayModeTests.cs
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Tests;   // PlayModeTestBase / TestHelpers

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 複数イベントが並列に評価されるときの新仕様検証：
    /// - 時間で複数が同時に Available（場所は不問）
    /// - Start は「場所到達」または「インタラクト」
    /// - インタラクトは 1フレーム1回消費かつ Main→Sub の優先で開始
    /// </summary>
    public class ParallelEvents_PlayModeTests : PlayModeTestBase
    {
        [SetUp] public void Setup2() => BaseSetup();
        [TearDown] public void Teardown2() => BaseTearDown();

        private static EventData MakeSO(
            string id, Game.Events.EventType type,
            string appear, string startDL, string endDL,
            string areaId,
            bool autoStartOnLocation, bool requiresButtonPress,
            float alt = 0.5f)
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
        public void Both_become_Available_by_time_then_only_one_starts_by_single_interact_preferring_Main()
        {
            // Main/Sub ともに同時刻にAvailable。両方とも「インタラクト必須・自動開始無し」。
            var main = MakeSO("M1", Game.Events.EventType.Main, "08:00", "09:00", "10:00", "Square", autoStartOnLocation: false, requiresButtonPress: true);
            var sub = MakeSO("S1", Game.Events.EventType.Sub, "08:00", "09:00", "10:00", "Square", autoStartOnLocation: false, requiresButtonPress: true);
            em.InitializeForTest(new[] { main, sub });

            // Hookは時間を進める前に
            using var sig = new TestHelpers.SignalCatcher();

            // 時間で同時に Available（場所不問）
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual(EventState.Available, GetRuntime(em, "M1").State);
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S1").State);

            // インタラクトは 1フレーム1回消費 → Main が優先して Start、Sub は残る
            input.PressOnce();
            TestHelpers.Tick(em, 1);

            Assert.AreEqual(EventState.InProgress, GetRuntime(em, "M1").State, "Main が優先で開始されるはず");
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S1").State, "Sub はまだ Available のまま");
        }

        [Test]
        public void Location_starts_only_target_event_when_autoStartOnLocation_true()
        {
            // Sub は「到達で開始」、Main は「インタラクトのみ」で開始。
            var main = MakeSO("M2", Game.Events.EventType.Main, "08:00", "09:00", "10:00", "Square",
                              autoStartOnLocation: false, requiresButtonPress: true);
            var sub = MakeSO("S2", Game.Events.EventType.Sub, "08:00", "09:00", "10:00", "Square",
                              autoStartOnLocation: true, requiresButtonPress: true);
            em.InitializeForTest(new[] { main, sub });

            using var sig = new TestHelpers.SignalCatcher();

            // 同時に Available
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual(EventState.Available, GetRuntime(em, "M2").State);
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S2").State);

            // 場所到達 → autoStartOnLocation=true の Sub だけが開始
            locator.SetArea("Square");
            TestHelpers.Tick(em, 1);

            Assert.AreEqual(EventState.Available, GetRuntime(em, "M2").State);
            Assert.AreEqual(EventState.InProgress, GetRuntime(em, "S2").State);
        }

        [Test]
        public void Interact_then_Location_in_next_frame_starts_remaining_event()
        {
            // まずインタラクトで Main を開始 → 次フレームに場所到達で Sub を開始
            var main = MakeSO("M3", Game.Events.EventType.Main, "08:00", "09:00", "10:00", "Square",
                              autoStartOnLocation: false, requiresButtonPress: true);
            var sub = MakeSO("S3", Game.Events.EventType.Sub, "08:00", "09:00", "10:00", "Square",
                              autoStartOnLocation: true, requiresButtonPress: true);
            em.InitializeForTest(new[] { main, sub });

            // 時間で Available
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual(EventState.Available, GetRuntime(em, "M3").State);
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S3").State);

            // フレーム1：インタラクト → Main が開始
            input.PressOnce();
            TestHelpers.Tick(em, 1);
            Assert.AreEqual(EventState.InProgress, GetRuntime(em, "M3").State);
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S3").State);

            // フレーム2：場所到達 → Sub が開始
            locator.SetArea("Square");
            TestHelpers.Tick(em, 1);
            Assert.AreEqual(EventState.InProgress, GetRuntime(em, "S3").State);
        }

        [Test]
        public void If_no_trigger_Sub_misses_start_while_Main_starts_by_interact()
        {
            // Main だけインタラクトで開始、Sub は何もせず開始期限を超過して失敗
            var main = MakeSO("M4", Game.Events.EventType.Main, "08:00", "08:30", "10:00", "Square",
                              autoStartOnLocation: false, requiresButtonPress: true);
            var sub = MakeSO("S4", Game.Events.EventType.Sub, "08:00", "08:30", "10:00", "Square",
                              autoStartOnLocation: false, requiresButtonPress: true);
            em.InitializeForTest(new[] { main, sub });

            using var sig = new TestHelpers.SignalCatcher();

            // 同時に Available
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual(EventState.Available, GetRuntime(em, "M4").State);
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S4").State);

            // Main をインタラクトで開始
            input.PressOnce();
            TestHelpers.Tick(em, 1);
            Assert.AreEqual(EventState.InProgress, GetRuntime(em, "M4").State);
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S4").State);

            // 何もせず締切を「超える」→ Sub は MissedStart
            TestHelpers.AdvanceTo(em, clockGO, "08:31"); // 等号は超過に含めない
            Assert.AreEqual(("S4", FailedReason.MissedStart), sig.Failed);
        }
    }
}
