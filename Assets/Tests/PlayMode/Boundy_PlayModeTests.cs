using NUnit.Framework;
using UnityEngine;
using Game.Events;
using Game.Runtime;
using Game.Tests;
using System.Collections.Generic;

/// <summary>
/// EventRuntime / EventManager の境界値テスト集
/// ・進捗と閾値の境界（-, =, +）
/// ・開始期限/終了期限の「ちょうど」境界（==）
/// </summary>
public class Boundary_PlayModeTests : PlayModeTestBase
{
    const float EPS = 1e-4f;

    [SetUp]
    public void Setup2()
    {
        BaseSetup();
        clock.Jump(0f);
    }

    [TearDown]
    public void Teardown2()
    {
        BaseTearDown();
    }

    // ヘルパ（最小イベント）
    private Game.Data.EventData Evt(string id, string appear, string startDL, string endDL,
                                    string area, float threshold, bool requiresBtn = true,
                                    Game.Events.EventType type = Game.Events.EventType.Sub)
    {
        var e = ScriptableObject.CreateInstance<Game.Data.EventData>();
        e.eventId = id;
        e.type = type;
        e.appearAt = appear;
        e.startDeadline = startDL;
        e.endDeadline = endDL;
        e.location = new LocationRef { kind = LocationKind.AreaId, id = area };
        e.requiresButtonPress = requiresBtn;
        e.dependencies = new List<string>();
        e.altCompleteThreshold = threshold;
        e.weekdayRule = new WeekdayRule();
        return e;
    }

    private void MakeAvailableAndStart(string id)
    {
        // Locked→Scheduled→Available→Start まで進める
        TestHelpers.Tick(em, 1);
        TestHelpers.Tick(em, 1);
        input.PressOnce();
        TestHelpers.Tick(em, 1);
        TestHelpers.AssertState(em, id, EventState.InProgress);
    }

    // ===== 1) 進捗と閾値の境界 =====

    [Test]
    public void Progress_JustBelowThreshold_Fails()
    {
        var e = Evt("E.Pminus", "00:00", "00:05", "00:10", "A", 0.6f);
        em.InitializeForTest(new[] { e });

        locator.SetArea("A");
        MakeAvailableAndStart("E.Pminus");

        // 閾値 - ε
        TestHelpers.GetRuntime(em, "E.Pminus").SetProgress(0.6f - EPS);

        TestHelpers.AdvanceTo(em, clockGO, "00:10");
        TestHelpers.Tick(em, 1);

        TestHelpers.AssertState(em, "E.Pminus", EventState.Failed);
    }

    [Test]
    public void Progress_ExactlyAtThreshold_Completes()
    {
        var e = Evt("E.Pe", "00:00", "00:05", "00:10", "A", 0.6f);
        em.InitializeForTest(new[] { e });

        locator.SetArea("A");
        MakeAvailableAndStart("E.Pe");

        // 閾値 ちょうど
        TestHelpers.GetRuntime(em, "E.Pe").SetProgress(0.6f);

        TestHelpers.AdvanceTo(em, clockGO, "00:10");
        TestHelpers.Tick(em, 1);

        TestHelpers.AssertState(em, "E.Pe", EventState.Completed);
    }

    [Test]
    public void Progress_JustAboveThreshold_Completes()
    {
        var e = Evt("E.Pplus", "00:00", "00:05", "00:10", "A", 0.6f);
        em.InitializeForTest(new[] { e });

        locator.SetArea("A");
        MakeAvailableAndStart("E.Pplus");

        // 閾値 + ε
        TestHelpers.GetRuntime(em, "E.Pplus").SetProgress(0.6f + EPS);

        TestHelpers.AdvanceTo(em, clockGO, "00:10");
        TestHelpers.Tick(em, 1);

        TestHelpers.AssertState(em, "E.Pplus", EventState.Completed);
    }

    // ===== 2) 期限の境界（==） =====
    // ポリシー（既存実装前提）:
    //   StartDeadlineExceeded: now > start      （== では超過にしない）
    //   EndDeadlineReached   : now >= end       （== で到達とする）

    [Test]
    public void StartDeadline_Equals_Now_IsNotExceeded()
    {
        // start=00:05 ちょうどの時刻で Available。== では MissedStart にならないこと
        var e = Evt("E.SD_EQ", "00:00", "00:05", "00:20", "A", 0.0f);
        em.InitializeForTest(new[] { e });

        // 00:05 まで進める
        TestHelpers.AdvanceTo(em, clockGO, "00:05");

        locator.SetArea("A");
        TestHelpers.Tick(em, 1); // Scheduled→Available

        // ここで押して開始できること（== は超過扱いでない）
        input.PressOnce();
        TestHelpers.Tick(em, 1);
        TestHelpers.AssertState(em, "E.SD_EQ", EventState.InProgress);
    }

    [Test]
    public void StartDeadline_LessThan_Now_IsExceeded()
    {
        // start=00:05 を過ぎて 00:06 で Available → MissedStart になること
        var e = Evt("E.SD_GT", "00:00", "00:05", "00:20", "A", 0.0f);
        em.InitializeForTest(new[] { e });

        TestHelpers.AdvanceTo(em, clockGO, "00:06");

        locator.SetArea("A");
        TestHelpers.Tick(em, 1); // Scheduled→Available
        // 押さない（開始しない）→ このフレームで MissedStart 判定へ
        TestHelpers.Tick(em, 1);

        TestHelpers.AssertState(em, "E.SD_GT", EventState.Failed);
    }

    [Test]
    public void EndDeadline_Equals_Now_IsReached()
    {
        // end=00:10 ちょうどで Completed/Failed を確定する（>=）
        var e = Evt("E.ED_EQ", "00:00", "00:05", "00:10", "A", 0.0f);
        em.InitializeForTest(new[] { e });

        locator.SetArea("A");
        MakeAvailableAndStart("E.ED_EQ");

        // 進捗＝1.0 → Completed 確定
        TestHelpers.GetRuntime(em, "E.ED_EQ").SetProgress(1.0f);

        TestHelpers.AdvanceTo(em, clockGO, "00:10");
        TestHelpers.Tick(em, 1);
        TestHelpers.AssertState(em, "E.ED_EQ", EventState.Completed);
    }

    [Test]
    public void EndDeadline_Equals_Now_WithLowProgress_Fails()
    {
        var e = Evt("E.ED_EQ_F", "00:00", "00:05", "00:10", "A", 0.6f);
        em.InitializeForTest(new[] { e });

        locator.SetArea("A");
        MakeAvailableAndStart("E.ED_EQ_F");

        // 閾値未満
        TestHelpers.GetRuntime(em, "E.ED_EQ_F").SetProgress(0.5f);

        TestHelpers.AdvanceTo(em, clockGO, "00:10");
        TestHelpers.Tick(em, 1);
        TestHelpers.AssertState(em, "E.ED_EQ_F", EventState.Failed);
    }
}
