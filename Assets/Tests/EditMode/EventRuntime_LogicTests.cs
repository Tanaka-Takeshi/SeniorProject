// Assets/Tests/EditMode/EventRuntime_LogicTests.cs
using NUnit.Framework;
using Game.Events;
using Game.Data;
using Game.Runtime;

// ---- フェイク評価コンテキスト（環境をフラグで制御） ----
using System.Collections.Generic;

class FakeCtx : Game.Runtime.IEvalContext
{
    public bool IsGloballyPaused { get; set; }
    public bool PolicyTreatStartOverAsExpired { get; set; }

    // ===== 既存の切替フラグ（そのまま残す）=====
    public bool deps, now, startOver, endReached, calendar = true, loc = true;

    // ===== 入力（消費型） =====
    // 以前は input=true/false だけだったが、
    // 「このフレームで押下エッジがあったか」と「消費済みか」を分ける
    public bool input;            // 押したいフレームで true にする（従来どおり）
    private bool _inputConsumed;  // そのフレームでもう使ったか

    // --- IEvalContext 実装 ---
    public bool DependenciesSatisfied(List<string> ids) => deps;
    public bool NowReached(string t) => now;
    public bool StartDeadlineExceeded(string t) => startOver;
    public bool EndDeadlineReached(string t) => endReached;
    public bool CalendarAllowed(WeekdayRule r) => calendar;
    public bool LocationSatisfied(LocationRef l) => loc;
    public bool InteractionPossible(EventData d) => true;

    // ★新：消費型。最初の1回だけ true を返し、以降は false
    public bool TryConsumeStartInput()
    {
        if (!input || _inputConsumed) return false;
        _inputConsumed = true;
        return true;
    }

    // ★互換：古い呼び出しが残っていても動くように、消費型を呼ぶ
    public bool StartInputReceived() => TryConsumeStartInput();

    // ===== テスト補助（任意）=====
    /// <summary>次の評価フレームへ進める想定で入力をリセット。</summary>
    public void NextFrame()
    {
        // フレームをまたぐと input フラグはデフォルト false に戻す想定が自然。
        // （PlayModeの EventManager.BeginEvalFrame 相当）
        input = false;
        _inputConsumed = false;
    }

    /// <summary>このフレームで押下エッジを立てる簡易API。</summary>
    public void PressOnceThisFrame()
    {
        input = true;
        _inputConsumed = false;
    }
}


// ---- テスト本体 ----
public class EventRuntime_LogicTests
{
    EventData Make(string id, string appear = "00:00", string start = "00:10", string end = "00:20",
                   string area = "A", bool requiresBtn = true, float th = 0.5f, bool autoStartOnLocation = false)
    {
        var e = UnityEngine.ScriptableObject.CreateInstance<EventData>();
        e.eventId = id;
        e.type = EventType.Sub;
        e.appearAt = appear;
        e.startDeadline = start;
        e.endDeadline = end;
        e.location = new LocationRef { kind = LocationKind.AreaId, id = area };
        e.requiresButtonPress = requiresBtn;
        e.autoStartOnLocation = autoStartOnLocation; // ★追加
        e.dependencies = new System.Collections.Generic.List<string>();
        e.altCompleteThreshold = th;
        e.weekdayRule = new WeekdayRule();
        return e;
    }

    [Test]
    public void Scheduled_After_Appear_When_Dependencies_Ok()
    {
        var ctx = new FakeCtx { deps = true, now = true }; // appear 到達
        var rt = new EventRuntime(Make("E1"));
        rt.Evaluate(ctx);
        Assert.AreEqual(EventState.Scheduled, rt.State);
    }

    [Test]
    public void Available_When_Location_Ok()
    {
        // 新仕様では「時間で Available」。ここでは now=true を維持し、loc=true も立てているが
        // Available 判定自体は時間で行われる。
        var ctx = new FakeCtx { deps = true, now = true, loc = true };
        var rt = new EventRuntime(Make("E2"));
        // Appear 到達 → Scheduled
        rt.Evaluate(ctx);
        // 時間条件で → Available
        rt.Evaluate(ctx);
        Assert.AreEqual(EventState.Available, rt.State);
    }

    [Test]
    public void Start_When_Input_Received()
    {
        // requiresButtonPress = true（既定）なので、入力で開始可能
        var ctx = new FakeCtx { deps = true, now = true, loc = true, input = true };
        var rt = new EventRuntime(Make("E3"));
        rt.Evaluate(ctx); // Scheduled
        rt.Evaluate(ctx); // Available
        rt.Evaluate(ctx); // InProgress (input=true)
        Assert.AreEqual(EventState.InProgress, rt.State);
    }

    [Test]
    public void Completed_When_Progress_Equals_Threshold_At_End()
    {
        var ctx = new FakeCtx { deps = true, now = true, loc = true, input = true, endReached = false };
        var rt = new EventRuntime(Make("E4", th: 0.6f));
        // Start
        rt.Evaluate(ctx); rt.Evaluate(ctx); rt.Evaluate(ctx);
        rt.SetProgress(0.6f); // 閾値ちょうど
        // 終了到達で判定
        ctx.endReached = true;
        rt.Evaluate(ctx);
        Assert.AreEqual(EventState.Completed, rt.State);
    }

    [Test]
    public void Failed_When_Progress_Below_Threshold_At_End()
    {
        var ctx = new FakeCtx { deps = true, now = true, loc = true, input = true };
        var rt = new EventRuntime(Make("E5", th: 0.6f));
        // Start
        rt.Evaluate(ctx); rt.Evaluate(ctx); rt.Evaluate(ctx);
        // 進捗未達のまま End 到達
        ctx.endReached = true;
        rt.Evaluate(ctx);
        Assert.AreEqual(EventState.Failed, rt.State);
        Assert.AreEqual(FailedReason.MissedEndLowProgress, rt.FailedReason);
    }

    [Test]
    public void Expired_When_Not_Started_Before_StartDeadline()
    {
        // startDeadline 超過をシミュレーション
        var ctx = new FakeCtx { deps = true, now = true, loc = true, startOver = false, PolicyTreatStartOverAsExpired = true };
        var rt = new EventRuntime(Make("E6"));
        // Appear → Scheduled
        rt.Evaluate(ctx);
        // 期限直前：Available まで行く（input=false）
        rt.Evaluate(ctx);
        // 期限超過
        ctx.startOver = true;
        rt.Evaluate(ctx);
        Assert.AreEqual(EventState.Expired, rt.State);
    }

    [Test]
    public void AutoStart_When_RequiresButtonPress_False()
    {
        // 入力不要（requiresBtn=false）かつ 到達で開始（autoStartOnLocation=true）
        // 出現時に既に目的地に居ると誤爆防止で即開始しないため、まずは loc=false → その後 true にする
        var ctx = new FakeCtx { deps = true, now = true, loc = false };
        var rt = new EventRuntime(Make("E7", requiresBtn: false, autoStartOnLocation: true));
        rt.Evaluate(ctx); // Scheduled
        rt.Evaluate(ctx); // Available（時間のみで成立）
        // 目的地へ到達
        ctx.loc = true;
        rt.Evaluate(ctx); // 到達で InProgress
        Assert.AreEqual(EventState.InProgress, rt.State);
    }

    [Test]
    public void No_Double_Transition_After_Completed()
    {
        var ctx = new FakeCtx { deps = true, now = true, loc = true, input = true, endReached = true };
        var rt = new EventRuntime(Make("E8"));
        // Start → End
        rt.Evaluate(ctx); rt.Evaluate(ctx); rt.Evaluate(ctx); // InProgress
        rt.SetProgress(1f);
        rt.Evaluate(ctx); // Completed
        var state = rt.State;
        // さらに Evaluate しても変化しない
        rt.Evaluate(ctx);
        Assert.AreEqual(state, rt.State);
    }

    [Test]
    public void Dependencies_Block_When_Not_Satisfied()
    {
        var ctx = new FakeCtx { deps = false, now = true, loc = true };
        var rt = new EventRuntime(Make("E9"));
        rt.Evaluate(ctx);
        // 依存未達 → Scheduled にならないはず（実装により Locked/Idle 扱い）
        Assert.AreNotEqual(EventState.Scheduled, rt.State, "依存未達なら発生待機に入らない想定");
    }

    [Test]
    public void Paused_Stops_Transitions()
    {
        var ctx = new FakeCtx { deps = true, now = true, loc = true };
        var rt = new EventRuntime(Make("E10"));
        // Appear 到達で Scheduled にする前にポーズ
        ctx.IsGloballyPaused = true;
        rt.Evaluate(ctx);
        Assert.AreNotEqual(EventState.Scheduled, rt.State, "ポーズ中は状態が進行しない想定");
        // ポーズ解除 → 進行
        ctx.IsGloballyPaused = false;
        rt.Evaluate(ctx);
        Assert.AreEqual(EventState.Scheduled, rt.State);
    }

    [Test]
    public void StartDeadline_Equal_Edge()
    {
        // 「== で超過扱いにするかどうか」の境界テスト
        // ここでは “startOver=true” を「超過」とみなす前提で等号で切り替える
        var ctx = new FakeCtx { deps = true, now = true, loc = true, startOver = false, PolicyTreatStartOverAsExpired = true };
        var rt = new EventRuntime(Make("E11"));
        rt.Evaluate(ctx); // Scheduled
        rt.Evaluate(ctx); // Available
        // ちょうど到達 → 超過に切り替え
        ctx.startOver = true;
        rt.Evaluate(ctx);
        Assert.AreEqual(EventState.Expired, rt.State);
    }
}
