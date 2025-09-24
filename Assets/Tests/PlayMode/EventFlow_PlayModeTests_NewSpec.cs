// Assets/Tests/PlayMode/EventFlow_PlayModeTests_NewSpec.cs
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Runtime;
using Game.Tests;
using static Game.Tests.TestHelpers;

public class EventFlow_PlayModeTests_NewSpec : PlayModeTestBase
{
    [SetUp] public void Setup2() => BaseSetup();
    [TearDown] public void Teardown2() => BaseTearDown();

    // 最小形（デフォルトID/時刻）
    EventData MakeSO(bool autoStartOnLocation = true, bool requiresButtonPress = true)
        => MakeSO(
            id: "2.1",
            appear: "08:00",
            start: "09:00",
            end: "10:00",
            area: "Square",
            autoStartOnLocation: autoStartOnLocation,
            requiresButtonPress: requiresButtonPress,
            type: Game.Events.EventType.Main,
            alt: 0.5f
        );

    // フル指定版（ScriptableObject なので with は不可。都度フィールドを代入）
    EventData MakeSO(
        string id,
        string appear,
        string start,
        string end,
        string area,
        bool autoStartOnLocation,
        bool requiresButtonPress,
        Game.Events.EventType type,
        float alt = 0.5f)
    {
        var d = ScriptableObject.CreateInstance<EventData>();
        d.eventId = id;
        d.type = type;
        d.appearAt = appear;
        d.startDeadline = start;
        d.endDeadline = end;
        d.location = new LocationRef { kind = LocationKind.AreaId, id = area };
        d.autoStartOnLocation = autoStartOnLocation;
        d.requiresButtonPress = requiresButtonPress;
        d.altCompleteThreshold = alt;
        d.weekdayRule = new WeekdayRule();
        d.dependencies = new System.Collections.Generic.List<string>();
        return d;
    }

    [Test]
    public void Available_is_time_only_then_Start_by_Location()
    {
        em.InitializeForTest(new[] { MakeSO(autoStartOnLocation: true, requiresButtonPress: true) });

        using var sig = new TestHelpers.SignalCatcher(); // 先にHook

        // appear到達で Available（場所不問）
        AdvanceTo(em, clockGO, "08:00");
        Assert.AreEqual("2.1", sig.Available, "時間だけでAvailableになるはず");

        // まだ開始していない
        Assert.IsNull(sig.Started);

        // 場所到達で Start
        locator.SetArea("Square");
        Tick(em, 1);
        Assert.AreEqual("2.1", sig.Started);
    }

    [Test]
    public void Start_by_Interact_when_AutoStartOff()
    {
        em.InitializeForTest(new[] { MakeSO(autoStartOnLocation: false, requiresButtonPress: true) });
        using var sig = new TestHelpers.SignalCatcher();

        // 時間で Available
        AdvanceTo(em, clockGO, "08:00");
        Assert.AreEqual("2.1", sig.Available);

        // 場所到達しても自動開始しない
        locator.SetArea("Square");
        Tick(em, 1);
        Assert.IsNull(sig.Started);

        // インタラクトで開始
        input.PressOnce();
        Tick(em, 1);
        Assert.AreEqual("2.1", sig.Started);
    }

    [Test]
    public void Complete_or_Fail_on_End_by_Progress()
    {
        // 自動開始型で作成（場所到達ですぐ InProgress）
        var e = MakeSO(
            id: "2.4",
            appear: "08:00",
            start: "09:00",
            end: "10:00",
            area: "Square",
            autoStartOnLocation: true,
            requiresButtonPress: false,
            type: Game.Events.EventType.Main,
            alt: 0.5f);

        em.InitializeForTest(new[] { e });
        using var sig = new Game.Tests.TestHelpers.SignalCatcher(); // ★先にHook

        // --- InProgress まで ---
        Game.Tests.TestHelpers.AdvanceTo(em, clockGO, "08:00"); // Available
        locator.SetArea("Square");
        Game.Tests.TestHelpers.Tick(em, 2);                     // Available→InProgress を確実に
        AssertState(em, "2.4", EventState.InProgress, "InProgress になっていること");

        // --- (a) Completed：閾値以上で End 到達 ---
        Debug.Log($"[StepA] state={GetRuntime(em, "2.4").State} sig.Available={sig.Available} sig.Started={sig.Started}");
        GetRuntime(em, "2.4").SetProgress(0.6f);

        // “ちょうど 10:00” は境界取りこぼしが起きる環境があるので +1分して評価2回
        Game.Tests.TestHelpers.AdvanceTo(em, clockGO, "10:01");
        Game.Tests.TestHelpers.Tick(em, 2);

        Debug.Log($"[StepB] state={GetRuntime(em, "2.4").State} sig.Completed={sig.Completed} sig.Failed={sig.Failed?.id}");

        AssertState(em, "2.4", EventState.Completed, "Completed に遷移していること");
        Assert.AreEqual("2.4", sig.Completed, "Completed シグナルが取れること");

#if UNITY_EDITOR
        // --- (b) Failed：復元→閾値未満→End+ε ---
        GetRuntime(em, "2.4").RestoreForTest(EventState.InProgress, FailedReason.None, 0.4f);
        Game.Tests.TestHelpers.AdvanceTo(em, clockGO, "10:02");
        Game.Tests.TestHelpers.Tick(em, 2);

        AssertState(em, "2.4", EventState.Failed, "Failed に遷移していること");
        Assert.AreEqual("2.4", sig.Failed?.id, "Failed シグナルが取れること");
        Assert.AreEqual(FailedReason.MissedEndLowProgress, sig.Failed?.reason);
#endif
    }

}
