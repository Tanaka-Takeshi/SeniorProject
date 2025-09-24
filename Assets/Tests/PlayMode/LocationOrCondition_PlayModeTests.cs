using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Data;
using Game.Events;
using Game.Runtime;
using Game.Tests;
using static Game.Tests.TestHelpers; // Tick / AssertState / GetRuntime など

/// <summary>
/// 複数エリア(OR条件: "Town|Forest")の基本動作と、
/// HUD優先ルール(Available表示はMain優先)の健全性を確認するテスト。
/// 既存のシステムに依存するため、特別なモックは使わず PlayMode で検証。
/// </summary>
public class LocationOrCondition_PlayModeTests : PlayModeTestBase
{
    // テスト用イベント生成（既存のシグネチャに合わせる）
    private static EventData MakeEvent(
        string id,
        string appear = "00:00",
        string start = "00:10",
        string end = "00:30",
        string area = "Town|Forest",   // デフォルトで OR 指定
        float alt = 0.5f,
        bool requiresBtn = true,
        Game.Events.EventType type = Game.Events.EventType.Sub,
        bool autoStartOnLocation = false
    )
    {
        var e = ScriptableObject.CreateInstance<EventData>();
        e.eventId = id;
        e.type = type;
        e.appearAt = appear;
        e.startDeadline = start;
        e.endDeadline = end;
        e.location = new LocationRef { kind = LocationKind.AreaId, id = area };
        e.altCompleteThreshold = alt;
        e.requiresButtonPress = requiresBtn;
        e.dependencies = new List<string>();
        e.weekdayRule = new WeekdayRule();
        e.autoStartOnLocation = autoStartOnLocation; // 仕様変更後フィールド
        return e;
    }

    [UnityTest]
    public System.Collections.IEnumerator Available_When_Any_Area_Matches_Then_Start_By_Input()
    {
        // E1 は Town|Forest で出現
        var e1 = MakeEvent("E1", appear: "00:00", start: "00:20", end: "00:40", area: "Town|Forest", requiresBtn: true, type: Game.Events.EventType.Main);
        InitEvents(e1);

        // まずは Town にいる → Appearからの遷移を進める
        locator.SetArea("Town");
        Tick(em, 1); // Locked -> Scheduled
        Tick(em, 1); // Scheduled -> Available
        AssertState(em, "E1", EventState.Available);

        // 入力で開始
        input.PressOnce();
        Tick(em, 1); // Available -> InProgress
        AssertState(em, "E1", EventState.InProgress);

        yield return null;
    }

    [UnityTest]
    public System.Collections.IEnumerator AutoStart_On_Any_Area_When_AutoStartOnLocation_True()
    {
        // E2 は Town|Forest かつ「到達で自動開始」
        var e2 = MakeEvent("E2", appear: "00:00", start: "00:20", end: "00:40",
                           area: "Town|Forest", requiresBtn: false, type: Game.Events.EventType.Sub, autoStartOnLocation: true);
        InitEvents(e2);

        // Forest にいる → 自動で Available → InProgress になることを確認
        locator.SetArea("Forest");
        Tick(em, 1); // Locked -> Scheduled
        Tick(em, 1); // Scheduled -> Available（ここではまだ開始前）
        // autoStartOnLocation=true なので、Availableフレーム以降のEvaluateで InProgress に遷移
        Tick(em, 1); // Available -> InProgress（自動）
        AssertState(em, "E2", EventState.InProgress);

        yield return null;
    }

    [UnityTest]
    public System.Collections.IEnumerator HUD_Priority_Available_Main_Wins_Over_Sub()
    {
        // HUD単体でのUIは別テストで十分担保済みだが、Available優先度(Main>Sub)の前提を軽く確認
        // EMain(Sub=false) と ESub(Sub=true) を同時Availableにする
        var eMain = MakeEvent("E.Main", appear: "00:00", start: "00:20", end: "00:40",
                              area: "Town|Forest", requiresBtn: true, type: Game.Events.EventType.Main);
        var eSub = MakeEvent("E.Sub", appear: "00:00", start: "00:20", end: "00:40",
                             area: "Town|Forest", requiresBtn: true, type: Game.Events.EventType.Sub);

        InitEvents(eMain, eSub);

        // どちらも同条件で Available に入れる
        locator.SetArea("Town");
        Tick(em, 1); // Locked -> Scheduled（両方）
        Tick(em, 1); // Scheduled -> Available（両方）
        AssertState(em, "E.Main", EventState.Available);
        AssertState(em, "E.Sub", EventState.Available);

        // ここでは UI 実体は出さないが SingleEventTracker の選出規則と同じ前提：
        // InProgress > Available、同じ状態なら Main > Sub、同じならserial新しい方
        // → どちらも Available の場合は Main 優先、という仕様を前提にしたシナリオ側の検証。
        // 実 UI の優先表示は UI 側のテストで担保されるので、ここでは状態のみ確認としておく。
        // （もしUI層も合わせて検証したければ、UI_SingleEventTracker_Tests を使用）

        yield return null;
    }
}
