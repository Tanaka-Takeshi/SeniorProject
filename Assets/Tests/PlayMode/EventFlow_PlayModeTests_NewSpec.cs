// Assets/Tests/PlayMode/EventFlow_PlayModeTests_NewSpec.cs
using NUnit.Framework;
using Game.Events;
using Game.Data;
using Game.Tests;         // ★ TestHelpers / PlayModeTestBase
using UnityEngine;

public class EventFlow_PlayModeTests_NewSpec : Game.Tests.PlayModeTestBase
{
    [SetUp] public void Setup2() => BaseSetup();
    [TearDown] public void Teardown2() => BaseTearDown();

    EventData MakeSO(bool autoStartOnLocation = true, bool requiresButtonPress = true)
    {
        var d = ScriptableObject.CreateInstance<EventData>();
        d.eventId = "2.1"; d.type = Game.Events.EventType.Main;
        d.appearAt = "08:00"; d.startDeadline = "09:00"; d.endDeadline = "10:00";
        d.location = new LocationRef { kind = LocationKind.AreaId, id = "Square" };
        d.autoStartOnLocation = autoStartOnLocation;
        d.requiresButtonPress = requiresButtonPress;
        d.weekdayRule = new WeekdayRule();
        return d;
    }

    [Test]
    public void Available_is_time_only_then_Start_by_Location()
    {
        em.InitializeForTest(new[] { MakeSO(autoStartOnLocation: true) });

        using var sig = new Game.Tests.TestHelpers.SignalCatcher(); // ★ 先にHook

        // appearAt 到達 → 場所にいなくても Available が出る
        Game.Tests.TestHelpers.AdvanceTo(em, clockGO, "08:00");
        Assert.AreEqual("2.1", sig.Available, "時間だけでAvailableになるはず");  // ★ ここが通る

        // まだ開始していない（場所にいない）
        Assert.IsNull(sig.Started);

        // 場所到達で Start
        locator.SetArea("Square");
        Game.Tests.TestHelpers.Tick(em, 1);
        Assert.AreEqual("2.1", sig.Started);
    }

    [Test]
    public void Start_by_Interact_when_AutoStartOff()
    {
        em.InitializeForTest(new[] { MakeSO(autoStartOnLocation: false, requiresButtonPress: true) });
        using var sig = new TestHelpers.SignalCatcher();

        // 時間で Available
        TestHelpers.AdvanceTo(em, clockGO, "08:00");
        Assert.AreEqual("2.1", sig.Available);

        // 場所到達しても自動開始しない
        locator.SetArea("Square");
        TestHelpers.Tick(em, 1);
        Assert.IsNull(sig.Started);

        // インタラクトで開始
        input.PressOnce();               // ★ これが PressInteractOnce の実体
        TestHelpers.Tick(em, 1);
        Assert.AreEqual("2.1", sig.Started);
    }
}
