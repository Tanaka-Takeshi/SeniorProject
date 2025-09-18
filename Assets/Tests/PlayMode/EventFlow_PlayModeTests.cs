// Assets/Tests/PlayMode/EventFlow_PlayModeTests.cs
using NUnit.Framework;
using Game.Data;
using Game.Events;
using Game.Tests;     // PlayModeTestBase / TestHelpers
using UnityEngine;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 新仕様（時間でAvailable → 場所 or インタラクトでStart）に対応したプレイモード基本動作テスト。
    /// </summary>
    public class EventFlow_PlayModeTests : PlayModeTestBase
    {
        [SetUp] public void Setup2() => BaseSetup();
        [TearDown] public void Teardown2() => BaseTearDown();

        private static EventData MakeSO(string id, string appear, string startDL, string endDL,
                                        string areaId, bool autoStartOnLocation, bool requiresButtonPress,
                                        Game.Events.EventType type = Game.Events.EventType.Sub, float alt = 0.5f)
        {
            var e = ScriptableObject.CreateInstance<EventData>();
            e.eventId = id;
            e.type = type;
            e.appearAt = appear;          // "HH:MM"（分=秒）
            e.startDeadline = startDL;
            e.endDeadline = endDL;
            e.location = new LocationRef { kind = LocationKind.AreaId, id = areaId };
            e.autoStartOnLocation = autoStartOnLocation;
            e.requiresButtonPress = requiresButtonPress;
            e.altCompleteThreshold = alt;
            e.dependencies = new System.Collections.Generic.List<string>();
            e.weekdayRule = new WeekdayRule();
            return e;
        }

        [Test]
        public void Available_is_time_only_then_Start_by_Location()
        {
            // イベント登録：場所到達で自動開始を許可
            var ev = MakeSO("2.1", "08:00", "09:00", "10:00", "Square",
                            autoStartOnLocation: true, requiresButtonPress: true, type: Game.Events.EventType.Main);
            InitEvents(ev);

            // ★ シグナルはここでHook（時間を進める前）
            using var sig = new TestHelpers.SignalCatcher();

            // appearAt 到達 → 場所にいなくても Available
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual("2.1", sig.Available, "時間のみで Available になるはず");
            Assert.IsNull(sig.Started, "まだ開始していない（場所未到達）");

            // 場所到達で Start
            locator.SetArea("Square");
            TestHelpers.Tick(em, 1);
            Assert.AreEqual("2.1", sig.Started, "場所到達で開始するはず");
        }

        [Test]
        public void Start_by_Interact_when_AutoStartOff()
        {
            // イベント登録：場所到達では開始しない（インタラクト必須）
            var ev = MakeSO("2.2", "08:00", "09:00", "10:00", "Square",
                            autoStartOnLocation: false, requiresButtonPress: true);
            InitEvents(ev);

            using var sig = new TestHelpers.SignalCatcher();

            // 時間で Available（場所不問）
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual("2.2", sig.Available);
            Assert.IsNull(sig.Started);

            // 同期しても開始しない（autoStartOff）
            locator.SetArea("Square");
            TestHelpers.Tick(em, 1);
            Assert.IsNull(sig.Started, "autoStartOff なので場所到達だけでは開始しない");

            // インタラクトで開始
            input.PressOnce();
            TestHelpers.Tick(em, 1);
            Assert.AreEqual("2.2", sig.Started);
        }

        [Test]
        public void MissedStart_when_no_trigger_until_deadline()
        {
            var ev = MakeSO("2.3", "08:00", "08:30", "10:00", "Square",
                            autoStartOnLocation: true, requiresButtonPress: true);
            InitEvents(ev);

            using var sig = new TestHelpers.SignalCatcher();

            // 時間で Available（何もせず放置）
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual("2.3", sig.Available);
            Assert.IsNull(sig.Started);

            // 開始期限を超過させる → MissedStart
            // StartDeadlineExceeded は now > deadline のとき true になるので
            // 「08:30」ちょうどではなく「08:31」など deadline を超える時刻へ進める
            TestHelpers.AdvanceTo(em, clockGO, "08:31");

            Assert.AreEqual(("2.3", FailedReason.MissedStart), sig.Failed);
        }

        [Test]
        public void Complete_or_Fail_on_End_by_Progress()
        {
            var ev = MakeSO("2.4", "08:00", "09:00", "10:00", "Square",
                            autoStartOnLocation: true, requiresButtonPress: true);
            InitEvents(ev);

            using var sig = new TestHelpers.SignalCatcher();

            // Available → Start（インタラクト）
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            input.PressOnce();
            TestHelpers.Tick(em, 1);
            Assert.AreEqual("2.4", sig.Started);

            // (a) 閾値以上で Completed
            GetRuntime(em, "2.4").SetProgress(0.7f);
            TestHelpers.AdvanceTo(em, clockGO, "10:00");
            Assert.AreEqual("2.4", sig.Completed);

            // (b) 閾値未満で Failed(MissedEndLowProgress)
            GetRuntime(em, "2.4").RestoreForTest(EventState.InProgress, FailedReason.None, 0.3f);
            TestHelpers.AdvanceTo(em, clockGO, "10:00");
            Assert.AreEqual(("2.4", FailedReason.MissedEndLowProgress), sig.Failed);
        }
    }
}
