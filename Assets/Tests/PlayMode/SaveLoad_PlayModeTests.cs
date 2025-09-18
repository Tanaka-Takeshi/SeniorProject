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
        // （必要なら）トラッカーをぶら下げる
        tracker = new GameObject("QuestTracker").AddComponent<TestQuestTracker>();
        tracker.transform.SetParent(root.transform, false);
    }

    [TearDown]
    public void Teardown2() => BaseTearDown();

    // ========== ケース1：InProgress の途中で保存 → 復元して Completed まで進む ==========
    [Test]
    public void SaveLoad_ResumeMidProgress_ToCompleted()
    {
#if !UNITY_EDITOR
        Assert.Inconclusive("このテストは UNITY_EDITOR 専用APIを使用します。");
        return;
#endif
        using var sig = new TestHelpers.SignalCatcher();

        // 00:10 で開始、閾値 0.6、終了 00:30
        var ev = MakeEvent("E.Resume", "00:00", "00:10", "00:30", "Plaza", 0.6f, true, Game.Events.EventType.Sub);
        InitEvents(ev);

        // 新仕様：時間で Available。開始は「場所到達 or インタラクト」。
        // ここではインタラクトで開始させる。
        TestHelpers.AdvanceTo(em, clockGO, "00:10");  // appearAt 到達
        input.PressOnce();
        TestHelpers.Tick(em, 1);                      // Available→InProgress
        Assert.AreEqual(EventState.InProgress, TestHelpers.GetRuntime(em, "E.Resume").State);

        // 進捗を閾値以上にして保存
        TestHelpers.GetRuntime(em, "E.Resume").SetProgress(0.6f);
        var snapshot = em.ExportStateForTest();

        // 疑似ロード：新しい EventManager/Clock/Locator/Input を用意して Import
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
        TestHelpers.Inject(newEM, newClock, newLoc, newInput, settings);  // 依存注入（DI）:contentReference[oaicite:2]{index=2}

        // 定義（同一ID）をセットしてスナップショット適用
        newEM.InitializeForTest(new[] { ev });
        newEM.ImportStateForTest(snapshot);

        // 終了刻へ進めて確定
        TestHelpers.AdvanceTo(newEM, newClockGO, "00:30");
        TestHelpers.Tick(newEM, 1);

        Assert.AreEqual("E.Resume", sig.Completed, "復元後も正常に Completed へ到達するはず");
    }

    // ========== ケース2：Available で保存 → 復元して開始（MissedStart にならない） ==========
    [Test]
    public void SaveLoad_ResumeFromAvailable_StartsNormally()
    {
#if !UNITY_EDITOR
        Assert.Inconclusive("このテストは UNITY_EDITOR 専用APIを使用します。");
        return;
#endif
        // appearAt=00:00, startDL=00:20, end=00:40
        var ev = MakeEvent("E.Available", "00:00", "00:20", "00:40", "Hill", 0.5f, true, Game.Events.EventType.Sub);
        InitEvents(ev);

        // 時間で Available（場所は不問）
        using var sig = new TestHelpers.SignalCatcher();
        TestHelpers.AdvanceTo(em, clockGO, "00:00");
        Assert.AreEqual("E.Available", sig.Available);
        Assert.AreEqual(EventState.Available, TestHelpers.GetRuntime(em, "E.Available").State);

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

        // 復元直後に開始トリガ（インタラクト）→ Start になること（MissedStart しない）
        newInput.PressOnce();
        TestHelpers.Tick(newEM, 1);
        TestHelpers.AssertState(newEM, "E.Available", EventState.InProgress);
    }

    // ========== ケース3：Failed 直前で保存 → 復元しても同じ失敗（MissedEndLowProgress）になる ==========
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

        // 時間で Available → インタラクトで開始 → 進捗低いまま
        TestHelpers.AdvanceTo(em, clockGO, "00:05");
        input.PressOnce();
        TestHelpers.Tick(em, 1); // Start
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

        // 終了刻へ進める → Failed(MissedEndLowProgress) に到達する
        TestHelpers.AdvanceTo(newEM, newClockGO, "00:15");
        TestHelpers.Tick(newEM, 1);

        Assert.IsTrue(sig.Failed.HasValue &&
                      sig.Failed.Value.id == "E.Fail" &&
                      sig.Failed.Value.reason == FailedReason.MissedEndLowProgress,
                      "復元後も同じ失敗に到達するはず");
    }
}
