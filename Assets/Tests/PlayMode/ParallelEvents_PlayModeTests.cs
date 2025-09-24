// Assets/Tests/PlayMode/ParallelEvents_PlayModeTests.cs
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Tests;   // PlayModeTestBase / TestHelpers
using static Game.Tests.TestHelpers;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 並列時の新仕様検証：
    /// - 時間で複数が同時に Available（場所は不問）
    /// - Start は「場所到達」または「インタラクト」
    /// - インタラクトは 1フレーム1回消費＆Main優先
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
            e.appearAt = appear;            // "HH:MM"（分=秒）
            e.startDeadline = startDL;
            e.endDeadline = endDL;
            e.location = new LocationRef { kind = LocationKind.AreaId, id = areaId };
            e.autoStartOnLocation = autoStartOnLocation;
            e.requiresButtonPress = requiresButtonPress;
            e.altCompleteThreshold = alt;
            e.weekdayRule = new WeekdayRule();
            e.dependencies = new System.Collections.Generic.List<string>();
            // interactNeedsLocation は EventData 側のデフォルト(true)に任せる
            return e;
        }

        [Test]
        public void Both_become_Available_by_time_then_only_one_starts_by_single_interact_preferring_Main()
        {
            // Main/Sub 同時Available。両方とも「インタラクト必須・自動開始なし」。
            var main = MakeSO("M1", Game.Events.EventType.Main, "08:00", "09:00", "10:00", "Square",
                              autoStartOnLocation: false, requiresButtonPress: true);
            var sub = MakeSO("S1", Game.Events.EventType.Sub, "08:00", "09:00", "10:00", "Square",
                              autoStartOnLocation: false, requiresButtonPress: true);
            InitEvents(main, sub);

            using var sig = new TestHelpers.SignalCatcher();

            // 時間で同時に Available（場所不問）
            AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual(EventState.Available, GetRuntime(em, "M1").State);
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S1").State);

            // 指定エリアでインタラクト（消費1回）→ Main が優先して Start、Sub は残る
            locator.SetArea("Square");
            input.PressOnce();
            Tick(em, 1);

            Assert.AreEqual(EventState.InProgress, GetRuntime(em, "M1").State, "Main が優先で開始されるはず");
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S1").State, "同フレームは Sub にまでは波及しない");
        }

        [Test]
        public void Location_starts_only_target_event_when_autoStartOnLocation_true()
        {
            // Sub は「到達で開始」、Main は「インタラクトのみ」で開始。
            var main = MakeSO("M2", Game.Events.EventType.Main, "08:00", "09:00", "10:00", "Square",
                              autoStartOnLocation: false, requiresButtonPress: true);
            var sub = MakeSO("S2", Game.Events.EventType.Sub, "08:00", "09:00", "10:00", "Square",
                              autoStartOnLocation: true, requiresButtonPress: true);
            InitEvents(main, sub);

            using var sig = new TestHelpers.SignalCatcher();

            // 同時に Available（時間のみで到達）
            AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual(EventState.Available, GetRuntime(em, "M2").State);
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S2").State);

            // 場所到達 → autoStartOnLocation=true の Sub だけが開始
            locator.SetArea("Square");
            Tick(em, 1);

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

            // このテストでは「最初のフレームは場所にいなくてもインタラクトで Main を開始」させたいので、
            // Main だけ場所不要にする
            main.interactNeedsLocation = false;

            InitEvents(main, sub);

            // 時間で Available
            AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual(EventState.Available, GetRuntime(em, "M3").State);
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S3").State);

            // フレーム1：場所にいない状態でインタラクト → Main が開始
            input.PressOnce();
            Tick(em, 1);
            Assert.AreEqual(EventState.InProgress, GetRuntime(em, "M3").State);
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S3").State);

            // フレーム2：場所到達 → Sub が自動開始
            locator.SetArea("Square");
            Tick(em, 1);
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
            InitEvents(main, sub);

            using var sig = new TestHelpers.SignalCatcher();

            // 同時に Available
            AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual(EventState.Available, GetRuntime(em, "M4").State);
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S4").State);

            // 指定エリアでインタラクト（消費1回）→ Main が開始、Sub は残る
            locator.SetArea("Square");
            input.PressOnce();
            Tick(em, 1);
            Assert.AreEqual(EventState.InProgress, GetRuntime(em, "M4").State);
            Assert.AreEqual(EventState.Available, GetRuntime(em, "S4").State);

            // 何もせず締切を「超える」→ Sub は MissedStart
            AdvanceTo(em, clockGO, "08:31"); // “>” になる時刻へ
            Assert.AreEqual(("S4", FailedReason.MissedStart), sig.Failed);
        }
    }
}
