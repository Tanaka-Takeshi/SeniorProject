// Assets/Tests/PlayMode/SaveLoad_PlayModeTests.cs
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Runtime;
using Game.Tests;

public class SaveLoad_PlayModeTests : PlayModeTestBase
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
    public void Teardown2() => BaseTearDown();

    // 便利ヘルパ（他のPlayMode系と同等の引数並び）
    private static EventData MakeEvent(
        string id, string appear, string startDL, string endDL, string areaId,
        float alt = 0.5f, bool requiresBtn = true, Game.Events.EventType type = Game.Events.EventType.Sub,
        bool autoStartOnLocation = false // セーブ系は明示的にインタラクト開始に寄せる
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
        e.autoStartOnLocation = autoStartOnLocation;
        e.dependencies = new System.Collections.Generic.List<string>();
        e.altCompleteThreshold = alt;
        e.weekdayRule = new WeekdayRule();
        return e;
    }

    // ========== ケース1：InProgress の途中で保存 → 復元して Completed まで進む ==========
    [Test]
    public void SaveLoad_ResumeMidProgress_ToCompleted()
    {
#if !UNITY_EDITOR
    Assert.Inconclusive("このテストは UNITY_EDITOR 専用APIを使用します。");
    return;
#endif
        using var sig = new TestHelpers.SignalCatcher();

        // 00:10 開始、閾値 0.6、終了 00:30
        var ev = MakeEvent("E.Resume", "00:00", "00:10", "00:30", "Plaza", 0.6f, true, Game.Events.EventType.Sub);
        InitEvents(ev);

        // 時間で Available へ
        TestHelpers.AdvanceTo(em, clockGO, "00:10");

        // ★ 開始前に対象エリアへ移動（Interact要件を満たす）
        locator.SetArea("Plaza");
        TestHelpers.Tick(em, 1);          // 位置反映

        // インタラクトで開始
        input.PressOnce();
        TestHelpers.Tick(em, 1);          // Available → InProgress
        TestHelpers.AssertState(em, "E.Resume", EventState.InProgress);

        // 進捗を閾値以上にして保存
        TestHelpers.GetRuntime(em, "E.Resume").SetProgress(0.6f);
        var snapshot = em.ExportStateForTest();

        // 疑似ロード
        var newEMGO = new GameObject("EventManager2");
        var newEM = newEMGO.AddComponent<EventManager>();
        var newClockGO = new GameObject("Clock2");
        var newClock = newClockGO.AddComponent<SimpleClock>();
        var newLocGO = new GameObject("Locator2");
        var newLoc = newLocGO.AddComponent<SimpleLocationResolver>();
        var newInputGO = new GameObject("Input2");
        var newInput = newInputGO.AddComponent<TestInputProxy>();

        var settings = ScriptableObject.CreateInstance<Game.Config.GlobalSettings>();
        settings.dayLengthSeconds = 1440f;
        TestHelpers.Inject(newEM, newClock, newLoc, newInput, settings);

        newEM.InitializeForTest(new[] { ev });
        newEM.ImportStateForTest(snapshot);

        // 終了刻へ進めて Completed になることを確認
        TestHelpers.AdvanceTo(newEM, newClockGO, "00:30");
        TestHelpers.Tick(newEM, 1);

        Assert.AreEqual("E.Resume", sig.Completed, "復元後も Completed へ到達するはず");
    }


    // ========== ケース2：Available で保存 → 復元して開始（MissedStart にならない） ==========
    [Test]
    public void SaveLoad_ResumeFromAvailable_StartsNormally()
    {
#if !UNITY_EDITOR
    Assert.Inconclusive("このテストは UNITY_EDITOR 専用APIを使用します。");
    return;
#endif
        var ev = MakeEvent("E.Available", "00:00", "00:20", "00:40", "Hill",
                           0.5f, true, Game.Events.EventType.Sub, autoStartOnLocation: false);
        InitEvents(ev);

        using var sig = new TestHelpers.SignalCatcher();

        // 時間で Available（場所は不問）
        TestHelpers.AdvanceTo(em, clockGO, "00:00");
        Assert.AreEqual("E.Available", sig.Available);
        TestHelpers.AssertState(em, "E.Available", EventState.Available);

        // スナップショット保存
        var snapshot = em.ExportStateForTest();

        // 新規マネージャに復元
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

        // ★ 現仕様：インタラクト開始にはロケーション一致が必要
        newLoc.SetArea("Hill");
        TestHelpers.Tick(newEM, 1);   // 位置更新を反映

        // 復元直後にインタラクト → InProgress
        newInput.PressOnce();
        TestHelpers.Tick(newEM, 1);
        TestHelpers.AssertState(newEM, "E.Available", EventState.InProgress);
    }

    // ========== ケース3：Failed 直前で保存 → 復元しても同じ失敗（MissedEndLowProgress） ==========
    [Test]
    public void SaveLoad_ResumeJustBeforeFail_StillFails()
    {
#if !UNITY_EDITOR
    Assert.Inconclusive("このテストは UNITY_EDITOR 専用APIを使用します。");
    return;
#endif
        using var sig = new TestHelpers.SignalCatcher();

        // 終了 00:15、閾値 0.7（低進捗で失敗へ）
        var ev = MakeEvent("E.Fail", "00:00", "00:05", "00:15", "Dock", 0.7f, true, Game.Events.EventType.Sub);
        InitEvents(ev);

        // 時間で Available → ★ 対象エリアに移動してからインタラクト開始
        TestHelpers.AdvanceTo(em, clockGO, "00:05");
        locator.SetArea("Dock");          // ← 重要：ロケーション一致
        TestHelpers.Tick(em, 1);          // 位置反映
        input.PressOnce();
        TestHelpers.Tick(em, 1);          // Available → InProgress
        TestHelpers.AssertState(em, "E.Fail", EventState.InProgress);

        // 進捗低いまま
        TestHelpers.GetRuntime(em, "E.Fail").SetProgress(0.2f);

        // 失敗直前（00:14）で保存
        clock.Jump(14f);
        var snapshot = em.ExportStateForTest();

        // 新規へ復元
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

        // 終了刻へ進める → MissedEndLowProgress で失敗
        TestHelpers.AdvanceTo(newEM, newClockGO, "00:15");
        TestHelpers.Tick(newEM, 1);

        Assert.IsTrue(sig.Failed.HasValue &&
                      sig.Failed.Value.id == "E.Fail" &&
                      sig.Failed.Value.reason == FailedReason.MissedEndLowProgress,
                      "復元後も同じ失敗に到達するはず");
    }
}
