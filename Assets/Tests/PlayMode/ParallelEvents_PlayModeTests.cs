using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Runtime;
using Game.Tests;                    // TestHelpers
using System.Collections.Generic;

public class ParallelEvents_PlayModeTests
{
    GameObject root, emGO, clockGO, locGO, inputGO;
    EventManager em;
    SimpleClock clock;
    SimpleLocationResolver locator;
    TestInputProxy input;
    float _prevScale;

    [SetUp]
    public void SetUp()
    {
        _prevScale = TestHelpers.PauseRealtime();

        root = new GameObject("ROOT");

        clockGO = new GameObject("Clock");
        clock = clockGO.AddComponent<SimpleClock>();
        clockGO.transform.SetParent(root.transform, false);

        locGO = new GameObject("Locator");
        locator = locGO.AddComponent<SimpleLocationResolver>();
        locGO.transform.SetParent(root.transform, false);

        inputGO = new GameObject("Input");
        input = inputGO.AddComponent<TestInputProxy>();
        inputGO.transform.SetParent(root.transform, false);

        emGO = new GameObject("EventManager");
        em = emGO.AddComponent<EventManager>();
        emGO.transform.SetParent(root.transform, false);

        var settings = ScriptableObject.CreateInstance<Game.Config.GlobalSettings>();
        settings.dayLengthSeconds = 1440f;
        TestHelpers.Inject(em, clock, locator, input, settings);
    }

    [TearDown]
    public void TearDown()
    {
        try { TestHelpers.SetPaused(em, false); } catch { }
        Object.DestroyImmediate(root);
        TestHelpers.ResumeRealtime(_prevScale);
    }

    // ===== ヘルパ =====
    private void InitEvents(params EventData[] list)
    {
        em.InitializeForTest(list);
        clock.Jump(0f);   // ※ここでは評価はしない
    }

    private EventData MakeEvent(
        string id, string appear, string startDL, string endDL, string areaId,
        float alt = 0.5f, bool requiresBtn = true, Game.Events.EventType type = Game.Events.EventType.Sub)
    {
        var e = ScriptableObject.CreateInstance<EventData>();
        e.eventId = id;
        e.type = type;
        e.appearAt = appear;
        e.startDeadline = startDL;
        e.endDeadline = endDL;
        e.location = new LocationRef { kind = LocationKind.AreaId, id = areaId };
        e.requiresButtonPress = requiresBtn;
        e.dependencies = new List<string>();
        e.altCompleteThreshold = alt;
        e.weekdayRule = new WeekdayRule();
        return e;
    }

    // ========== テスト ==========

    [Test]
    public void Parallel_TwoSubs_Advance_Independently()
    {
        var a = MakeEvent("Sub.A", "00:00", "00:10", "00:30", "Area/A", 0.5f, true, Game.Events.EventType.Sub);
        var b = MakeEvent("Sub.B", "00:00", "00:10", "00:30", "Area/B", 0.6f, true, Game.Events.EventType.Sub);
        InitEvents(a, b);

        // --- A を開始 ---
        locator.SetArea("Area/A");
        TestHelpers.Tick(em, 1); // Locked → Scheduled
        TestHelpers.Tick(em, 1); // Scheduled → Available
        input.PressOnce();       // ★ Available になった“後”に押す
        TestHelpers.Tick(em, 1); // Available → InProgress
        TestHelpers.AssertState(em, "Sub.A", Game.Events.EventState.InProgress);

        // --- B も開始 ---
        locator.SetArea("Area/B");
        TestHelpers.Tick(em, 1); // Locked → Scheduled
        TestHelpers.Tick(em, 1); // Scheduled → Available
        input.PressOnce();       // ★ 同じく Available 後に押す
        TestHelpers.Tick(em, 1); // Available → InProgress
        TestHelpers.AssertState(em, "Sub.B", Game.Events.EventState.InProgress);

        // A は達成、B は未達
        TestHelpers.GetRuntime(em, "Sub.A").SetProgress(1.0f);

        // ★ 進行前にキャッチャを作る
        using var sig = new Game.Tests.TestHelpers.SignalCatcher();

        // 期限到達
        TestHelpers.AdvanceTo(em, clockGO, "00:30");

        // A 完了
        Assert.AreEqual("Sub.A", sig.Completed);

        // B 失敗（ID と理由まで）
        Assert.IsTrue(sig.Failed.HasValue &&
                      sig.Failed.Value.id == "Sub.B" &&
                      sig.Failed.Value.reason == FailedReason.MissedEndLowProgress,
                      "Bは期限到達かつ進捗未達で MissedEndLowProgress になるはず");

    }


    [Test]
    public void Parallel_MainAndSub_DoNotBlockEachOther()
    {
        var main = MakeEvent("Main.1", "00:00", "00:05", "00:20", "Hub", 0.5f, true, Game.Events.EventType.Main);
        var sub = MakeEvent("Sub.1", "00:00", "00:05", "00:20", "Hub", 0.5f, true, Game.Events.EventType.Sub);
        InitEvents(main, sub);

        // 同エリア
        locator.SetArea("Hub");

        // 1) Locked → Scheduled
        TestHelpers.Tick(em, 1);
        // 2) Scheduled → Available（両方が Available になる）
        TestHelpers.Tick(em, 1);

        // 3) 1回目の入力で どちらか一方 を Start
        input.PressOnce();
        TestHelpers.Tick(em, 1);

        // 4) 2回目の入力で 残りの一方 を Start
        input.PressOnce();
        TestHelpers.Tick(em, 1);

        // どちらも InProgress になっていること（順番は非決定）
        TestHelpers.AssertState(em, "Main.1", Game.Events.EventState.InProgress);
        TestHelpers.AssertState(em, "Sub.1", Game.Events.EventState.InProgress);

        // 両方クリア
        TestHelpers.GetRuntime(em, "Main.1").SetProgress(1f);
        TestHelpers.GetRuntime(em, "Sub.1").SetProgress(1f);

        TestHelpers.AdvanceTo(em, clockGO, "00:20");
        TestHelpers.AssertState(em, "Main.1", Game.Events.EventState.Completed);
        TestHelpers.AssertState(em, "Sub.1", Game.Events.EventState.Completed);
    }



    [Test]
    public void Parallel_OneFails_OtherContinues()
    {
        var a = MakeEvent("Sub.FailFast", "00:00", "00:05", "00:20", "Area/X", 0.5f, true, Game.Events.EventType.Sub);
        var b = MakeEvent("Sub.Ok", "00:00", "00:05", "00:20", "Area/Y", 0.5f, true, Game.Events.EventType.Sub);
        InitEvents(a, b);

        // A開始→即中断
        locator.SetArea("Area/X");
        TestHelpers.Tick(em, 1);
        input.PressOnce();
        TestHelpers.Tick(em, 1);
        TestHelpers.GetRuntime(em, "Sub.FailFast").ForceInterrupt();
        TestHelpers.AssertState(em, "Sub.FailFast", EventState.Failed);

        // B開始→完了
        locator.SetArea("Area/Y");
        TestHelpers.Tick(em, 1);
        input.PressOnce();
        TestHelpers.Tick(em, 1);
        TestHelpers.GetRuntime(em, "Sub.Ok").SetProgress(1f);

        TestHelpers.AdvanceTo(em, clockGO, "00:20");
        TestHelpers.AssertState(em, "Sub.Ok", EventState.Completed);
    }
}
