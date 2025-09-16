using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Runtime;
using Game.Tests;
using System.Collections.Generic;

public class SaveLoad_PlayModeTests : Game.Tests.PlayModeTestBase
{
    TestQuestTracker tracker;

    [SetUp]
    public void Setup2()
    {
        BaseSetup();
        tracker = new GameObject("QuestTracker").AddComponent<TestQuestTracker>();
        tracker.transform.SetParent(root.transform, false);
    }

    [TearDown]
    public void Teardown2()
    {
        BaseTearDown();
    }

    // ========== ケース1：InProgressの途中で保存→復元してCompletedまで進む ==========
    [Test]
    public void SaveLoad_ResumeMidProgress_ToCompleted()
    {
#if !UNITY_EDITOR
        Assert.Inconclusive("このテストは UNITY_EDITOR 専用APIを使用します。");
        return;
#endif
        using var sig = new TestHelpers.SignalCatcher();

        // 00:10 で Available → 開始、閾値 0.6。 終了 00:30
        var ev = MakeEvent("E.Resume", "00:00", "00:10", "00:30", "Plaza", 0.6f, true, Game.Events.EventType.Sub);
        InitEvents(ev);

        // 開始まで進める
        locator.SetArea("Plaza");
        TestHelpers.Tick(em, 1); // Locked→Scheduled
        TestHelpers.Tick(em, 1); // Scheduled→Available
        input.PressOnce();
        TestHelpers.Tick(em, 1); // Available→InProgress

        // 進捗を半分にして保存
        TestHelpers.GetRuntime(em, "E.Resume").SetProgress(0.6f);
        var snapshot = em.ExportStateForTest();

        // ★新しい EventManager にロード（疑似ロードシーン）
        var newRoot = new GameObject("NEWROOT");
        var newEMGO = new GameObject("EventManager2");
        var newEM = newEMGO.AddComponent<EventManager>();
        var newClockGO = new GameObject("Clock2");
        var newClock = newClockGO.AddComponent<SimpleClock>();
        var newLocGO = new GameObject("Locator2");
        var newLoc = newLocGO.AddComponent<SimpleLocationResolver>();
        var newInputGO = new GameObject("Input2");
        var newInput = newInputGO.AddComponent<TestInputProxy>();

        // 設定とDI
        var settings = ScriptableObject.CreateInstance<Game.Config.GlobalSettings>();
        settings.dayLengthSeconds = 1440f;
        TestHelpers.Inject(newEM, newClock, newLoc, newInput, settings);

        // 同じイベント定義を突っ込む（ID一致が必要）
        newEM.InitializeForTest(new[] { ev });

        // スナップショット適用
        newEM.ImportStateForTest(snapshot);

        // 継続：終了刻へ
        newLoc.SetArea("Plaza");
        TestHelpers.AdvanceTo(newEM, newClockGO, "00:30");
        TestHelpers.Tick(newEM, 1);

        // 完了シグナル出ているはず
        Assert.AreEqual("E.Resume", sig.Completed);
    }

    // ========== ケース2：Availableで保存→復元して開始→MissedStartにならない ==========
    [Test]
    public void SaveLoad_ResumeFromAvailable_StartsNormally()
    {
#if !UNITY_EDITOR
        Assert.Inconclusive("このテストは UNITY_EDITOR 専用APIを使用します。");
        return;
#endif
        var ev = MakeEvent("E.Available", "00:00", "00:20", "00:40", "Hill", 0.5f, true, Game.Events.EventType.Sub);
        InitEvents(ev);

        // Available まで
        locator.SetArea("Hill");
        TestHelpers.Tick(em, 1); // Scheduled
        TestHelpers.Tick(em, 1); // Available

        // 保存
        var snapshot = em.ExportStateForTest();

        // 新規マネージャに同じイベントを用意して復元
        var newEMGO = new GameObject("EventManager3");
        var newEM = newEMGO.AddComponent<EventManager>();
        var newClockGO = new GameObject("Clock3");
        var newClock = newClockGO.AddComponent<SimpleClock>();
        var newLocGO = new GameObject("Locator3");
        var newLoc = newLocGO.AddComponent<SimpleLocationResolver>();
        var newInputGO = new GameObject("Input3");
        var newInput = newInputGO.AddComponent<TestInputProxy>();

        var settings = ScriptableObject.CreateInstance<Game.Config.GlobalSettings>();
        settings.dayLengthSeconds = 1440f;
        TestHelpers.Inject(newEM, newClock, newLoc, newInput, settings);
        newEM.InitializeForTest(new[] { ev });

        newEM.ImportStateForTest(snapshot);

        // すぐ開始できる
        newLoc.SetArea("Hill");
        newInput.PressOnce();
        TestHelpers.Tick(newEM, 1); // Start
        Game.Tests.TestHelpers.AssertState(newEM, "E.Available", Game.Events.EventState.InProgress);
    }

    // ========== ケース3：Failed 直前で保存→復元しても Failed になる ==========
    [Test]
    public void SaveLoad_ResumeJustBeforeFail_StillFails()
    {
#if !UNITY_EDITOR
        Assert.Inconclusive("このテストは UNITY_EDITOR 専用APIを使用します。");
        return;
#endif
        using var sig = new TestHelpers.SignalCatcher();

        // 終了 00:15、閾値 0.7、進捗低いまま
        var ev = MakeEvent("E.Fail", "00:00", "00:05", "00:15", "Dock", 0.7f, true, Game.Events.EventType.Sub);
        InitEvents(ev);

        locator.SetArea("Dock");
        TestHelpers.Tick(em, 1); // Scheduled
        TestHelpers.Tick(em, 1); // Available
        input.PressOnce();
        TestHelpers.Tick(em, 1); // Start
        TestHelpers.GetRuntime(em, "E.Fail").SetProgress(0.2f);

        // 失敗直前の時刻（00:14）で保存
        clock.Jump(14f);
        var snapshot = em.ExportStateForTest();

        // 新規に復元
        var newEMGO = new GameObject("EventManager4");
        var newEM = newEMGO.AddComponent<EventManager>();
        var newClockGO = new GameObject("Clock4");
        var newClock = newClockGO.AddComponent<SimpleClock>();
        var newLocGO = new GameObject("Locator4");
        var newLoc = newLocGO.AddComponent<SimpleLocationResolver>();
        var newInputGO = new GameObject("Input4");
        var newInput = newInputGO.AddComponent<TestInputProxy>();

        var settings = ScriptableObject.CreateInstance<Game.Config.GlobalSettings>();
        settings.dayLengthSeconds = 1440f;
        TestHelpers.Inject(newEM, newClock, newLoc, newInput, settings);
        newEM.InitializeForTest(new[] { ev });
        newEM.ImportStateForTest(snapshot);

        // 1分進める → 失敗
        TestHelpers.AdvanceTo(newEM, newClockGO, "00:15");
        TestHelpers.Tick(newEM, 1);
        Assert.IsTrue(sig.Failed.HasValue && sig.Failed.Value.id == "E.Fail"
                      && sig.Failed.Value.reason == FailedReason.MissedEndLowProgress,
                      "復元後も同じ失敗に到達するはず");
    }
}
