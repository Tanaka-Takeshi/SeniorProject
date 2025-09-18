// Assets/Tests/PlayMode/Smoke_PlayMode.cs
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Tests;   // PlayModeTestBase / TestHelpers

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// SMOKE: 新仕様の基本挙動を軽量に確認する。
    /// - Available は「時間のみ」で成立（場所は不問）
    /// - Start は「場所到達」または「インタラクト」
    /// - 期限の取り扱い（StartDL は now > DL / EndDL は now >= DL）
    /// - SignalCatcher は時間を進める前に Hook する
    /// </summary>
    public class Smoke_PlayMode : PlayModeTestBase
    {
        [SetUp] public void Setup2() => BaseSetup();
        [TearDown] public void Teardown2() => BaseTearDown();

        private static EventData MakeSO(
            string id, string appear, string startDL, string endDL,
            string areaId, bool autoStartOnLocation, bool requiresButtonPress,
            Game.Events.EventType type = Game.Events.EventType.Sub, float alt = 0.5f)
        {
            var e = ScriptableObject.CreateInstance<EventData>();
            e.eventId = id;
            e.type = type;
            e.appearAt = appear;                // "HH:MM"
            e.startDeadline = startDL;
            e.endDeadline = endDL;
            e.location = new LocationRef { kind = LocationKind.AreaId, id = areaId };
            e.autoStartOnLocation = autoStartOnLocation;
            e.requiresButtonPress = requiresButtonPress;
            e.altCompleteThreshold = alt;
            e.weekdayRule = new WeekdayRule();
            e.dependencies = new System.Collections.Generic.List<string>();
            return e;
        }

        [Test]
        public void Smoke_TimeBasedAvailable_ThenStartByInteract()
        {
            var ev = MakeSO("SM1", "08:00", "08:30", "09:00", "Square",
                            autoStartOnLocation: false, requiresButtonPress: true, type: Game.Events.EventType.Main);
            InitEvents(ev);

            using var sig = new TestHelpers.SignalCatcher(); // ★Hookは先

            // 時間だけで Available（場所不問）
            TestHelpers.AdvanceTo(em, clockGO, "08:00");
            Assert.AreEqual("SM1", sig.Available);

            // インタラクトで Start
            input.PressOnce();
            TestHelpers.Tick(em, 1);
            Assert.AreEqual("SM1", sig.Started);

            // 進捗を満たして End 到達で Completed
            TestHelpers.GetRuntime(em, "SM1").SetProgress(1.0f);
            TestHelpers.AdvanceTo(em, clockGO, "09:00");
            Assert.AreEqual("SM1", sig.Completed);
        }

        [Test]
        public void Smoke_TimeBasedAvailable_ThenStartByLocation()
        {
            var ev = MakeSO("SM2", "10:00", "10:20", "10:40", "Harbor",
                            autoStartOnLocation: true, requiresButtonPress: true);
            InitEvents(ev);

            using var sig = new TestHelpers.SignalCatcher();

            // 時間で Available
            TestHelpers.AdvanceTo(em, clockGO, "10:00");
            Assert.AreEqual("SM2", sig.Available);
            Assert.IsNull(sig.Started);

            // 場所到達で自動開始
            locator.SetArea("Harbor");
            TestHelpers.Tick(em, 1);
            Assert.AreEqual("SM2", sig.Started);

            // 閾値未満だと Fail、以上で Complete（ここでは成功させる）
            TestHelpers.GetRuntime(em, "SM2").SetProgress(0.8f);
            TestHelpers.AdvanceTo(em, clockGO, "10:40");
            Assert.AreEqual("SM2", sig.Completed);
        }

        [Test]
        public void Smoke_MissedStart_When_NoTrigger_Until_Deadline()
        {
            var ev = MakeSO("SM3", "12:00", "12:15", "13:00", "Plaza",
                            autoStartOnLocation: false, requiresButtonPress: true);
            InitEvents(ev);

            using var sig = new TestHelpers.SignalCatcher();

            // 時間で Available（何もしない）
            TestHelpers.AdvanceTo(em, clockGO, "12:00");
            Assert.AreEqual("SM3", sig.Available);
            Assert.IsNull(sig.Started);

            // 開始期限を「超える」まで待つ（now > startDL）
            TestHelpers.AdvanceTo(em, clockGO, "12:16");
            Assert.AreEqual(("SM3", FailedReason.MissedStart), sig.Failed);
        }
    }
}
