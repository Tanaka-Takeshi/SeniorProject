using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Runtime;
using Game.Tests; // TestHelpers
using System.Collections.Generic;

/// <summary>
/// EventSignals と（テスト用の簡易）QuestTracker の統合確認。
/// - イベント Completed でステップ前進
/// - すべてのステップが完了で Quest 完了
/// - ステップのイベントが Failed で Quest 失敗（簡易ポリシー）
/// </summary>
public class QuestManager_IntegrationTests
{
    GameObject root, emGO, clockGO, locGO, inputGO, questGO;
    EventManager em;
    SimpleClock clock;
    SimpleLocationResolver locator;
    TestInputProxy input;
    TestQuestTracker tracker;

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

        // Quest tracker（テスト用のシンプル実装）
        questGO = new GameObject("QuestTracker");
        tracker = questGO.AddComponent<TestQuestTracker>();
        questGO.transform.SetParent(root.transform, false);

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

    // ===== 便利ヘルパ =====
    private void InitEvents(params EventData[] list)
    {
        em.InitializeForTest(list);
        clock.Jump(0f);
    }

    private static EventData MakeEvent(string id, string appear, string startDL, string endDL,
                                       string areaId, float alt = 0.5f, bool requiresBtn = true,
                                       Game.Events.EventType type = Game.Events.EventType.Sub)
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

    // ===================== テストケース =====================

    [Test]
    public void Quest_Completes_When_All_Steps_Completed()
    {
        using var sig = new Game.Tests.TestHelpers.SignalCatcher(); // 進行前に購読

        // E1: 00:30 完了 / E2: 00:35 までに開始 → 00:50 で完了
        var e1 = MakeEvent("E1", "00:00", "00:10", "00:30", "Town", 0.5f, true, Game.Events.EventType.Main);
        var e2 = MakeEvent("E2", "00:00", "00:35", "00:50", "Forest", 0.5f, true, Game.Events.EventType.Main);
        InitEvents(e1, e2);

        tracker.LoadQuest("Main.Q1", new[] { "E1", "E2" });

        // --- E1 を完了 ---
        locator.SetArea("Town");
        TestHelpers.Tick(em, 1);  // Locked → Scheduled
        TestHelpers.Tick(em, 1);  // Scheduled → Available
        input.PressOnce();
        TestHelpers.Tick(em, 1);  // Available → InProgress
        Game.Tests.TestHelpers.AssertState(em, "E1", Game.Events.EventState.InProgress);

        TestHelpers.GetRuntime(em, "E1").SetProgress(1f);
        TestHelpers.AdvanceTo(em, clockGO, "00:30"); // 到達(>=)＋確定
        TestHelpers.Tick(em, 1); // 追Tick
        Game.Tests.TestHelpers.AssertState(em, "E1", Game.Events.EventState.Completed);

        Assert.AreEqual(1, tracker.CompletedSteps.Count, "E1完了で1ステップ進む");
        Assert.AreEqual("E2", tracker.CurrentStepId, "次のステップはE2");

        // --- E2 を開始・完了 ---
        // 現在 00:30。開始期限 00:35 以内に開始する
        locator.SetArea("Forest");

        TestHelpers.Tick(em, 1);  // Locked → Scheduled（E2 が即 Scheduled）
        TestHelpers.Tick(em, 1);  // Scheduled → Available
        input.PressOnce();
        TestHelpers.Tick(em, 1);  // Available → InProgress
        Game.Tests.TestHelpers.AssertState(em, "E2", Game.Events.EventState.InProgress);

        TestHelpers.GetRuntime(em, "E2").SetProgress(1f);
        TestHelpers.AdvanceTo(em, clockGO, "00:50");
        TestHelpers.Tick(em, 1); // 追Tick

        // 最終確認
        Assert.IsTrue(tracker.IsQuestCompleted, "全ステップ完了でクエスト完了");
        Assert.IsFalse(tracker.IsQuestFailed, "失敗ではない");
        CollectionAssert.AreEqual(new[] { "E1", "E2" }, tracker.CompletedSteps);
        Game.Tests.TestHelpers.AssertState(em, "E2", Game.Events.EventState.Completed);
    }



    [Test]
    public void Quest_DoesNotAdvance_On_NonCompleted_Signals()
    {
        var e = MakeEvent("E-only", "00:00", "00:10", "00:30", "Square", 0.5f, true, Game.Events.EventType.Sub);
        InitEvents(e);

        tracker.LoadQuest("Sub.Q", new[] { "E-only" });

        // Scheduled / Available / Started では一切進まない
        locator.SetArea("Square");
        TestHelpers.Tick(em, 1); // Scheduled
        Assert.AreEqual("E-only", tracker.CurrentStepId);
        TestHelpers.Tick(em, 1); // Available
        Assert.AreEqual("E-only", tracker.CurrentStepId);
        input.PressOnce();
        TestHelpers.Tick(em, 1); // Started
        Assert.AreEqual("E-only", tracker.CurrentStepId);

        // Completed のみで前進
        TestHelpers.GetRuntime(em, "E-only").SetProgress(1f);
        TestHelpers.AdvanceTo(em, clockGO, "00:30");
        Assert.IsTrue(tracker.IsQuestCompleted);
    }

    [Test]
    public void Quest_Fails_When_Step_Event_Fails()
    {
        var e = MakeEvent("E-fail", "00:00", "00:05", "00:10", "Cave", 0.6f, true, Game.Events.EventType.Sub);
        InitEvents(e);

        tracker.LoadQuest("Sub.FailQ", new[] { "E-fail" });

        locator.SetArea("Cave");
        TestHelpers.Tick(em, 1); // Scheduled
        TestHelpers.Tick(em, 1); // Available
        input.PressOnce();
        TestHelpers.Tick(em, 1); // Start

        // 進捗未達のまま終了時刻を超える → MissedEndLowProgress で失敗
        TestHelpers.AdvanceTo(em, clockGO, "00:11");

        Assert.IsTrue(tracker.IsQuestFailed, "ステップのイベントが失敗でクエストも失敗扱い");
        Assert.IsFalse(tracker.IsQuestCompleted);
        Assert.AreEqual("E-fail", tracker.FailedStepId);
    }
}
