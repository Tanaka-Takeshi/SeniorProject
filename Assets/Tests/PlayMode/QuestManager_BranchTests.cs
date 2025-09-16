using NUnit.Framework;
using UnityEngine;
using Game.Events;
using Game.Runtime;
using Game.Tests;

[TestFixture]
public class QuestManager_BranchTests : PlayModeTestBase
{
    private TestQuestTracker tracker;

    [SetUp]
    public void Setup()
    {
        BaseSetup(); // 共通初期化 (TestHelpers経由)
        tracker = root.AddComponent<TestQuestTracker>();
    }

    [TearDown]
    public void Teardown()
    {
        BaseTearDown();
    }

    [Test]
    public void BranchQuest_Completes_When_Either_Path_Finished()
    {
        using var sig = new Game.Tests.TestHelpers.SignalCatcher();

        // E1 → (E2 OR E3)
        var e1 = MakeEvent("E1", "00:00", "00:10", "00:20", "Town", 0.5f, true, Game.Events.EventType.Main);
        var e2 = MakeEvent("E2", "00:00", "00:35", "00:50", "Forest", 0.5f, true, Game.Events.EventType.Sub);
        var e3 = MakeEvent("E3", "00:00", "00:10", "00:30", "Cave", 0.5f, true, Game.Events.EventType.Sub);

        InitEvents(e1, e2, e3);

        // クエスト定義: OR条件
        tracker.LoadQuest("Quest.Branch", new[] { "E1", "E2|E3" });

        // --- E1 完了 ---
        locator.SetArea("Town");

        // Locked → Scheduled
        TickTo(em, clockGO, "00:00");      // 既に0なら不要だが安全のため
        TestHelpers.Tick(em, 1);           // Locked→Scheduled

        // Scheduled → Available
        TestHelpers.Tick(em, 1);

        // 押下 → InProgress（押下は Available 判定の直前or直後フレームで）
        input.PressOnce();
        TestHelpers.Tick(em, 1);           // Available→InProgress

        // 進捗を1.0に
        GetRuntime(em, "E1").SetProgress(1f);

        // 終了時刻到達 → 確定（内部で2Tick入るが念のため追Tick）
        TickTo(em, clockGO, "00:20");
        TestHelpers.Tick(em, 1);

        // ここで E1 は Completed のはず
        Game.Tests.TestHelpers.AssertState(em, "E1", Game.Events.EventState.Completed);
        Assert.Contains("E1", tracker.CompletedSteps, "E1完了で1ステップ進む");
        Assert.AreEqual("E2|E3", tracker.CurrentStepId, "次のステップは E2|E3");

        // --- OR条件: 今回は E2 を選んで進める ---

        // E2 開始
        locator.SetArea("Forest");
        TestHelpers.Tick(em, 1);  // Locked→Scheduled
        TestHelpers.Tick(em, 1);  // Scheduled→Available
        input.PressOnce();
        TestHelpers.Tick(em, 1);  // Available→InProgress
        GetRuntime(em, "E2").SetProgress(1f);
        TickTo(em, clockGO, "00:50");
        TestHelpers.Tick(em, 1);

        // 最終確認
        Assert.IsTrue(tracker.IsQuestCompleted, "E1 + (E2|E3) 達成でクエスト完了");
        CollectionAssert.AreEqual(new[] { "E1", "E2" }, tracker.CompletedSteps);
    }
}
