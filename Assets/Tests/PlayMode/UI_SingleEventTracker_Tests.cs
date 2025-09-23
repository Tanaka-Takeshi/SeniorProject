// Assets/Tests/PlayMode/UI_SingleEventTracker_Tests.cs
using Game.Data;
using Game.Events;
using Game.Runtime;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Tests;
using static Game.Tests.TestHelpers;

[TestFixture]
public class UI_SingleEventTracker_Tests : PlayModeTestBase
{
    private HUDController hud;
    private SingleEventTracker tracker;

    [UnitySetUp]
    public IEnumerator SetupHud()
    {
        // ★ 最重要：まず BaseSetup() で em / clock / locator / input を用意
        BaseSetup();

        // --- Canvas（土台） ---
        var canvasRoot = new GameObject("Canvas");
        var canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasRoot.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasRoot.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // --- HUD パネル + HUDController ---
        var hudGO = new GameObject("Panel_HUD");
        hudGO.transform.SetParent(canvasRoot.transform, false);
        var cg = hudGO.AddComponent<CanvasGroup>();
        hud = hudGO.AddComponent<HUDController>();

        // Title / Body
        var title = new GameObject("Title").AddComponent<TextMeshProUGUI>();
        title.rectTransform.SetParent(hudGO.transform, false);
        var body = new GameObject("Body").AddComponent<TextMeshProUGUI>();
        body.rectTransform.SetParent(hudGO.transform, false);

        // HUDController の private フィールドを直接代入
        var bf = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(HUDController).GetField("titleTMP", bf)?.SetValue(hud, title);
        typeof(HUDController).GetField("bodyTMP", bf)?.SetValue(hud, body);
        typeof(HUDController).GetField("panelRoot", bf)?.SetValue(hud, hudGO);
        typeof(HUDController).GetField("canvasGroup", bf)?.SetValue(hud, cg);

        // 可視状態
        if (cg)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        hudGO.SetActive(true);

        // --- Tracker を無効で追加 → 参照注入 → 有効化 ---
        tracker = hudGO.AddComponent<SingleEventTracker>();
        tracker.enabled = false;

        var tf = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(SingleEventTracker).GetField("eventManager", tf)?.SetValue(tracker, em);
        typeof(SingleEventTracker).GetField("hud", tf)?.SetValue(tracker, hud);

        tracker.showAvailable = true;    // 切り分けのため true
        tracker.emptyGraceFrames = 0;
        tracker.debugLogSelection = true;

        // Check-1: 注入直後の参照確認（em が null でも落ちないようにガード）
        var emInTracker = (EventManager)typeof(SingleEventTracker).GetField("eventManager", tf)?.GetValue(tracker);
        int emId = em ? em.GetInstanceID() : 0;
        int trId = emInTracker ? emInTracker.GetInstanceID() : 0;
        Debug.Log($"[Check-1] em.id={emId} / tracker.em.id={trId}");

        tracker.enabled = true;

        // Awake/OnEnable 完了
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDownHud()
    {
        // 共通片付け
        BaseTearDown();
        yield return null;
    }

    private static EventData MakeEvt(string id, string appear, string start, string end, string area,
                                     float alt, bool requiresBtn, Game.Events.EventType type)
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
        e.dependencies = new System.Collections.Generic.List<string>();
        e.weekdayRule = new WeekdayRule();
        return e;
    }

    [UnityTest]
    public IEnumerator Latest_InProgress_Wins()
    {
        // --- Tracker の表示方針 ---
        tracker.showAvailable = false;     // 最初は InProgress だけ表示させる（Main が先に出るように）
        tracker.mainHasPriority = true;    // Available を見せる時用の設定だが true で問題なし

        // --- イベント2つ（同条件） ---
        var e1 = MakeEvt("E1", "00:00", "00:05", "00:40", "Town", 0.5f, true, Game.Events.EventType.Main);
        var e2 = MakeEvt("E2", "00:00", "00:05", "00:40", "Forest", 0.5f, true, Game.Events.EventType.Sub);
        InitEvents(e1, e2);

        // 念のため tracker と em の参照ずれを解消（Rebind）
        {
            var tf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(SingleEventTracker).GetField("eventManager", tf)?.SetValue(tracker, em);
        }

        // ===== E1 を InProgress に =====
        locator.SetArea("Town");
        Tick(em, 1);                 // Locked → Scheduled (E1)
        Tick(em, 1);                 // Scheduled → Available (E1)
        input.PressOnce();
        Tick(em, 1);                 // Available → InProgress (E1)

        AssertState(em, "E1", EventState.InProgress);

        // 反映（Update→LateUpdate）待ち
        yield return null;
        yield return null;

        // 最初は Main(E1) が表示されるべき
        StringAssert.StartsWith("[Main]", hud.CurrentTitle, "最初は Main(E1) が表示されるはず");

        // ===== E2 を InProgress に（“最新優先”で E2 が勝つ）=====
        locator.SetArea("Forest");
        Tick(em, 1);                 // Locked → Scheduled (E2)
        Tick(em, 1);                 // Scheduled → Available (E2)
        input.PressOnce();
        Tick(em, 1);                 // Available → InProgress (E2)

        // 念のため、実際に進んでいるか確認
        AssertState(em, "E2", EventState.InProgress);

        // 反映（Update→LateUpdate）待ち
        yield return null;
        yield return null;

        // 最新で進行中になった Sub(E2) が優先される
        StringAssert.StartsWith("[Sub]", hud.CurrentTitle);
        StringAssert.Contains("E2", hud.CurrentTitle);
    }
}
