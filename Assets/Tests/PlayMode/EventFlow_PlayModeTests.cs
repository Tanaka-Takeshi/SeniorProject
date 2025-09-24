// Assets/Tests/PlayMode/EventFlow_PlayModeTests.cs
using NUnit.Framework;
using Game.Data;
using Game.Events;
using Game.Tests;     // PlayModeTestBase / TestHelpers
using UnityEngine;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 旧仕様テスト（新仕様 NewSpec で置き換え済み）。
    /// </summary>
    [Ignore("Superseded by EventFlow_PlayModeTests_NewSpec")]
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
        public void Available_is_time_only_then_Start_by_Location() { /* ... */ }

        [Test]
        public void Start_by_Interact_when_AutoStartOff() { /* ... */ }

        [Test]
        public void MissedStart_when_no_trigger_until_deadline() { /* ... */ }

        [Test]
        public void Complete_or_Fail_on_End_by_Progress() { /* ... */ }
    }
}
