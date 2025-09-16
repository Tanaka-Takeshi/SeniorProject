using NUnit.Framework;
using UnityEngine;
using Game.Events;
using Game.Runtime;
using Game.Tests;
using System.Collections.Generic;

public class Dependencies_PlayModeTests : PlayModeTestBase
{
    [SetUp] public void SetUp2() { BaseSetup(); clock.Jump(0f); }
    [TearDown] public void TearDown2() { BaseTearDown(); }

    // 依存ありイベントを簡単に作るヘルパ
    private Game.Data.EventData DepEvt(string id, string appear, string startDL, string endDL,
        string area, float thresh, IEnumerable<string> deps)
    {
        var e = MakeEvent(id, appear, startDL, endDL, area, thresh, true, Game.Events.EventType.Sub);
        e.dependencies = new List<string>(deps);
        return e;
    }

    // ========== 1) 単一依存：E2 は E1 Completed まで Locked のまま ==========
    [Test]
    public void Dependent_StaysLocked_UntilDependencyCompleted()
    {
        var e1 = MakeEvent("E1", "00:00", "00:05", "00:20", "A", 0.0f, true, Game.Events.EventType.Sub);
        var e2 = DepEvt("E2", "00:00", "00:35", "00:50", "A", 0.0f, new[]{ "E1" });
        InitEvents(e1, e2);

        // E2 の時間/場所は満たしても、E1 未完了なら Locked のまま
        locator.SetArea("A");
        TestHelpers.Tick(em, 1); // E1,E2: Locked→Scheduled(候補)
        TestHelpers.Tick(em, 1);
        TestHelpers.AssertState(em, "E2", EventState.Locked, "依存未完了の間は E2 は Locked のまま");

        // まず E1 を完了
        input.PressOnce();
        TestHelpers.Tick(em, 1);                 // E1: Start
        TestHelpers.GetRuntime(em, "E1").SetProgress(1f);
        TestHelpers.AdvanceTo(em, clockGO, "00:20");
        TestHelpers.Tick(em, 1);
        TestHelpers.AssertState(em, "E1", EventState.Completed);

        // 依存が満たされたので、E2 が解放→Available→Start できる
        TestHelpers.Tick(em, 1);                 // E2: Locked→Scheduled
        TestHelpers.Tick(em, 1);                 // E2: Scheduled→Available
        input.PressOnce();
        TestHelpers.Tick(em, 1);                 // E2: Available→InProgress
        TestHelpers.AssertState(em, "E2", EventState.InProgress);
    }

    // ========== 2) 複数依存（AND）：両方 Completed になるまで Locked ==========
    [Test]
    public void MultipleDependencies_AND_AllMustBeCompleted()
    {
        // Aは00:10で終わる短いイベント、Bはその直後から開始可能、Cはさらに後ろ
        var a = MakeEvent("A", "00:00", "00:05", "00:10", "X", 0f, true, Game.Events.EventType.Sub);
        var b = MakeEvent("B", "00:00", "00:10", "00:20", "Y", 0f, true, Game.Events.EventType.Sub);
        var c = MakeEvent("C", "00:00", "00:25", "00:40", "Z", 0f, true, Game.Events.EventType.Sub);
        c.dependencies = new System.Collections.Generic.List<string> { "A", "B" };
        InitEvents(a, b, c);

        // --- A を Completed へ ---
        locator.SetArea("X");
        TestHelpers.TickUntil(em, "A", Game.Events.EventState.Scheduled, 3);
        TestHelpers.TickUntil(em, "A", Game.Events.EventState.Available, 3);
        input.PressOnce();
        TestHelpers.Tick(em, 1); // InProgress
        TestHelpers.AssertState(em, "A", Game.Events.EventState.InProgress);

        TestHelpers.AdvanceTo(em, clockGO, "00:10"); // Aのend=00:10
        TestHelpers.Tick(em, 1);                     // 確定
        TestHelpers.AssertState(em, "A", Game.Events.EventState.Completed);

        // --- B を Completed へ（A完了直後の窓で開始できる） ---
        locator.SetArea("Y");
        // 現在時刻は 00:10。Bの start=00:10 なので超過ではない
        TestHelpers.TickUntil(em, "B", Game.Events.EventState.Scheduled, 3);
        TestHelpers.TickUntil(em, "B", Game.Events.EventState.Available, 3);
        input.PressOnce();
        TestHelpers.Tick(em, 1); // InProgress
        TestHelpers.AssertState(em, "B", Game.Events.EventState.InProgress);

        TestHelpers.AdvanceTo(em, clockGO, "00:20"); // Bのend=00:20
        TestHelpers.Tick(em, 1);                     // 確定
        TestHelpers.AssertState(em, "B", Game.Events.EventState.Completed);

        // --- 依存(A,B)が両方 Completed → C が解放される ---
        locator.SetArea("Z");
        TestHelpers.TickUntil(em, "C", Game.Events.EventState.Scheduled, 3);
        TestHelpers.TickUntil(em, "C", Game.Events.EventState.Available, 3);
        input.PressOnce();
        TestHelpers.Tick(em, 1); // InProgress まで確認
        TestHelpers.AssertState(em, "C", Game.Events.EventState.InProgress);
    }



    // ========== 3) 依存先が Failed の場合：Completed 以外は満たしたと見なさない ==========
    [Test]
    public void DependencyFailed_DoesNotUnlock()
    {
        var dep = MakeEvent("Dep", "00:00", "00:01", "00:05", "A", 0.7f, true, Game.Events.EventType.Sub);
        var tgt = DepEvt("Tgt", "00:00", "00:10", "00:30", "A", 0.0f, new[] { "Dep" });
        InitEvents(dep, tgt);

        // Dep を低進捗で終了時刻へ → Failed
        locator.SetArea("A");
        input.PressOnce();
        TestHelpers.Tick(em, 1); // Dep Start
        TestHelpers.GetRuntime(em, "Dep").SetProgress(0.2f);
        TestHelpers.AdvanceTo(em, clockGO, "00:05");
        TestHelpers.Tick(em, 1);
        TestHelpers.AssertState(em, "Dep", EventState.Failed);

        // 依存が Completed でないため、Tgt は Locked のまま
        TestHelpers.Tick(em, 1);
        TestHelpers.AssertState(em, "Tgt", EventState.Locked);
    }

    // ========== 4) Save/Load を跨いでも依存が維持される ==========
    [Test]
    public void SaveLoad_PreservesDependencyUnlocking()
    {
#if !UNITY_EDITOR
        Assert.Inconclusive("UNITY_EDITOR 専用APIを使用します。");
        return;
#endif
        // 時刻は現行のままでもOK（E1: end=00:20 / E2: start=00:10）
        var e1 = MakeEvent("E1", "00:00", "00:05", "00:20", "A", 0, true, Game.Events.EventType.Sub);
        var e2 = DepEvt("E2", "00:00", "00:35", "00:50", "B", 0, new[] { "E1" });
        InitEvents(e1, e2);

        // --- E1 を確実に Completed にする ---
        locator.SetArea("A");

        // 1) E1: Locked→Scheduled 到達まで待つ
        Game.Tests.TestHelpers.TickUntil(em, "E1", EventState.Scheduled, 3);
        // 2) E1: Scheduled→Available 到達まで待つ
        Game.Tests.TestHelpers.TickUntil(em, "E1", EventState.Available, 3);
        // 3) 次フレームで押下して Start
        input.PressOnce();
        Game.Tests.TestHelpers.Tick(em, 1); // InProgress へ
        Game.Tests.TestHelpers.AssertState(em, "E1", EventState.InProgress);

        // 4) endDeadline 到達→確定
        Game.Tests.TestHelpers.AdvanceTo(em, clockGO, "00:20"); // 到達
        Game.Tests.TestHelpers.Tick(em, 1);                     // 確定
        Game.Tests.TestHelpers.AssertState(em, "E1", EventState.Completed);

        // --- スナップショット ---
        var snapshot = em.ExportStateForTest();

        // --- 復元用の新規 EM を構築 ---
        var newEMGO = new GameObject("EM.Dep.Restore");
        var newEM = newEMGO.AddComponent<EventManager>();
        var newClockGO = new GameObject("Clock.Dep.Restore");
        var newClock = newClockGO.AddComponent<SimpleClock>();
        var newLocGO = new GameObject("Loc.Dep.Restore");
        var newLoc = newLocGO.AddComponent<SimpleLocationResolver>();
        var newInputGO = new GameObject("Input.Dep.Restore");
        var newInput = newInputGO.AddComponent<TestInputProxy>();

        var settings = ScriptableObject.CreateInstance<Game.Config.GlobalSettings>();
        settings.dayLengthSeconds = 1440f;
        Game.Tests.TestHelpers.Inject(newEM, newClock, newLoc, newInput, settings);

        // --- 同じ EventData で初期化 → 状態をインポート
        newEM.InitializeForTest(new[] { e1, e2 });
        newEM.ImportStateForTest(snapshot);

        var why = newEM.ExplainWhyLockedForTest("E2");
        Debug.Log($"[WHY] id={why.id} now={why.now} appearSec={why.appearSec} " +
                  $"depsOK={why.depsOK} deps=[{string.Join(",", why.deps ?? new string[0])}] depsStates={why.depsStates} " +
                  $"appearOK={why.appearOK} calendarOK={why.calendarOK}");

        // ★ 診断: 復元直後の E1/E2 情報を確認
        {
            var e2rt = Game.Tests.TestHelpers.GetRuntime(newEM, "E2");
            var e1rt = Game.Tests.TestHelpers.GetRuntime(newEM, "E1");

            // 1) E1 は Completed になっているか？
            Game.Tests.TestHelpers.AssertState(newEM, "E1", EventState.Completed);

            // 2) 現在時刻（秒）と E2 の時間窓
            var now = (newClock as Game.Runtime.IClock)?.NowGameSeconds ?? -1f;
            Debug.Log($"[DBG] now={now}, E2.appear='{e2.appearAt}', start='{e2.startDeadline}', end='{e2.endDeadline}'");

            // 3) 依存リストと実体
            Assert.IsNotNull(e2rt.Data.dependencies, "E2.dependencies が null");
            Debug.Log($"[DBG] E2.deps=[{string.Join(",", e2rt.Data.dependencies)}]");
            CollectionAssert.Contains(e2rt.Data.dependencies, "E1", "E2.dependencies に 'E1' が含まれていません");

            // 4) 依存先 E1 の実状態
            Debug.Log($"[DBG] E1.state={e1rt.State}, E1.failed={e1rt.FailedReason}, E1.progress={e1rt.Progress}");
        }

        // ★ ここで時間は『先に進める』だけにする（過去へ戻さない）
        //   snapshot の now は E1 完了時刻（00:20）のはず。追加で 1分だけ先へ。
        Game.Tests.TestHelpers.AdvanceTo(newEM, newClockGO, "00:21");

        // ★ E2 の場所（Scheduledには不要だが Available 用にセット）
        newLoc.SetArea("B");

        // ★ 状態遷移を待つ（猶予を少し広く）
        Game.Tests.TestHelpers.TickUntil(newEM, "E2", EventState.Scheduled, 8);
        Game.Tests.TestHelpers.TickUntil(newEM, "E2", EventState.Available, 8);
        newInput.PressOnce();
        Game.Tests.TestHelpers.Tick(newEM, 1);
        Game.Tests.TestHelpers.AssertState(newEM, "E2", EventState.InProgress);

    }

}
