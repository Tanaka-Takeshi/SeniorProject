// Assets/Tests/PlayMode/QuestManager_BranchTests.cs
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Runtime;
using Game.Tests;   // PlayModeTestBase / TestHelpers
using System.Collections.Generic;

[TestFixture]
public class QuestManager_BranchTests : PlayModeTestBase
{
    private TestQuestTracker tracker;

    [SetUp]
    public void Setup()
    {
        BaseSetup(); // 共通初期化
        tracker = root.AddComponent<TestQuestTracker>();
    }

    [TearDown]
    public void Teardown()
    {
        BaseTearDown();
    }

    // ===== テスト内ヘルパ（新仕様用：autoStartOnLocation 対応） =====
    private static EventData MakeEvent(
        string id, string appear, string startDL, string endDL,
        string areaId, float alt = 0.5f,
        bool requiresBtn = true,
        Game.Events.EventType type = Game.Events.EventType.Sub,
        bool autoStartOnLocation = true
    )
    {
        var e = ScriptableObject.CreateInstance<EventData>();
        e.eventId = id;
        e.type = type;
        e.appearAt = appear;              // "HH:MM"
        e.startDeadline = startDL;
        e.endDeadline = endDL;
        e.location = new LocationRef { kind = LocationKind.AreaId, id = areaId };
        e.requiresButtonPress = requiresBtn;
        e.autoStartOnLocation = autoStartOnLocation; // ★新仕様ポイント
        e.dependencies = new List<string>();
        e.altCompleteThreshold = alt;
        e.weekdayRule = new WeekdayRule();
        return e;
    }

    [Test]
    public void BranchQuest_Completes_When_Either_Path_Finished()
    {
        using var sig = new Game.Tests.TestHelpers.SignalCatcher(); // 進行前に購読

        // E1 → (E2 OR E3) の分岐
        // E1 はインタラクトで開始させる（autoStartOnLocation=false）
        // E2/E3 は「到達で自動開始」可能にしておく（true）
        var e1 = MakeEvent("E1", "00:00", "00:10", "00:20", "Town",
                           alt: 0.5f, requiresBtn: true, type: Game.Events.EventType.Main,
                           autoStartOnLocation: false);
        var e2 = MakeEvent("E2", "00:00", "00:35", "00:50", "Forest",
                           alt: 0.5f, requiresBtn: true, type: Game.Events.EventType.Sub,
                           autoStartOnLocation: true);
        var e3 = MakeEvent("E3", "00:00", "00:10", "00:30", "Cave",
                           alt: 0.5f, requiresBtn: true, type: Game.Events.EventType.Sub,
                           autoStartOnLocation: true);

        InitEvents(e1, e2, e3);

        // クエスト定義: OR条件
        tracker.LoadQuest("Quest.Branch", new[] { "E1", "E2|E3" });

        // --- E1 完了（新仕様：時間で Available → インタラクトで Start）---
        TestHelpers.AdvanceTo(em, clockGO, "00:00");                 // 時間で Available
        input.PressOnce();
        TestHelpers.Tick(em, 1);                                     // Available → InProgress
        GetRuntime(em, "E1").SetProgress(1f);
        TestHelpers.AdvanceTo(em, clockGO, "00:20");                 // End 到達（>=）
        TestHelpers.Tick(em, 1);                                     // 確定
        Game.Tests.TestHelpers.AssertState(em, "E1", EventState.Completed);
        Assert.Contains("E1", tracker.CompletedSteps, "E1完了で1ステップ進む");
        Assert.AreEqual("E2|E3", tracker.CurrentStepId, "次のステップは E2|E3");

        // --- OR条件：今回は E2 を選んで進める ---
        // すでに時間は十分経っているので E2/E3 は Available。E2の場所へ到達させて自動開始。
        locator.SetArea("Forest");
        TestHelpers.Tick(em, 1);                                     // E2: Available → InProgress
        GetRuntime(em, "E2").SetProgress(1f);
        TestHelpers.AdvanceTo(em, clockGO, "00:50");                 // E2 の終了刻
        TestHelpers.Tick(em, 1);                                     // 確定

        // 最終確認：E1 + E2 でクエスト完了（E3 は未着手のままでOK）
        Assert.IsTrue(tracker.IsQuestCompleted, "E1 + (E2|E3) 達成でクエスト完了");
        CollectionAssert.AreEqual(new[] { "E1", "E2" }, tracker.CompletedSteps);
    }
}
