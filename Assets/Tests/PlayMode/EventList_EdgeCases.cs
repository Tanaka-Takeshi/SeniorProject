using Game.Data;
using Game.Events;
using Game.Runtime;
using Game.Tests;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using TMPro;

/// <summary>
/// HUD と EventRuntime のエッジケース挙動確認テスト。
/// 設計上は「1フレーム1遷移」なので、最短4フレームで
/// Locked→Scheduled→Available→InProgress→Completed を踏む。
/// </summary>
public class EventList_EdgeCases : PlayModeTestBase
{
    private EventHUDList hud;
    private RectTransform content;

    [SetUp]
    public void Setup()
    {
        BaseSetup();

        // Content のダミーUIルート
        var hudRoot = new GameObject("HUDRoot", typeof(RectTransform));
        hudRoot.transform.SetParent(root.transform, false);
        content = hudRoot.GetComponent<RectTransform>();

        // HUD本体
        hud = hudRoot.AddComponent<EventHUDList>();
        hud.ContentRoot = content;
        hud.removeFadeSec = 0f; // テスト簡略化：即削除
        hud.maxItems = 5;

        // Prefab（無ければダミー）
        var prefab = Resources.Load<EventHUDItem>("Panel_HUDItem");
        if (prefab == null) prefab = MakeRuntimeDummyItemPrefab();
        hud.ItemPrefab = prefab;

        // EventManager の差し込み（基底の em を使用）
        hud.Manager = em;

        // HUD 有効化
        hud.enabled = true;
    }

    [TearDown]
    public void Teardown()
    {
        BaseTearDown();
    }

    [Test]
    public void Immediately_Completed_After_Minimal_Ticks()
    {
        // イベント定義（時間はすべて 00:00）
        var e = ScriptableObject.CreateInstance<EventData>();
        e.eventId = "Edge1";
        e.type = Game.Events.EventType.Sub;
        e.appearAt = "00:00";
        e.startDeadline = "00:00";
        e.endDeadline = "00:00";
        e.requiresButtonPress = true; // 開始は入力で行う（場所は使わない）
        e.altCompleteThreshold = 1f;
        e.location = new LocationRef { kind = LocationKind.AreaId, id = "" }; // 場所条件は空IDで実質無視

        // 初期化
        em.InitializeForTest(new[] { e });
        Assert.IsTrue(em.TryGetRuntime("Edge1", out var rt));
        Assert.NotNull(rt);

        // 完了させるために進捗を先に 100% に
        rt.SetProgress(1f);

        // ---- フレーム1：Locked → Scheduled
        em.EvaluateFrame();
        hud.RebuildFromManager();
        Assert.AreEqual(EventState.Scheduled, rt.State);

        // ---- フレーム2：Scheduled → Available
        em.EvaluateFrame();
        hud.RebuildFromManager();
        Assert.AreEqual(EventState.Available, rt.State);

        // ---- フレーム3：Available → InProgress（このフレームで入力エッジを入れる）
        // PlayModeTestBase に用意されている入力ヘルパ（なければ TestInputProxy へ直接 PressOnce）
        input.PressOnce();
        em.EvaluateFrame();
        hud.RebuildFromManager();
        Assert.AreEqual(EventState.InProgress, rt.State);

        // ---- フレーム4：InProgress → Completed（endDeadline=00:00 & progress=1）
        em.EvaluateFrame();
        hud.RebuildFromManager();
        Assert.AreEqual(EventState.Completed, rt.State);

        // Completed は HUD から即退場（removeFadeSec=0）
        Assert.AreEqual(0, content.childCount, "Completed 後は HUD に項目が残らないはず");
    }

    /// <summary>
    /// Prefabが存在しない場合に最低限の要素を持つダミーItemを生成する。
    /// </summary>
    private EventHUDItem MakeRuntimeDummyItemPrefab()
    {
        var go = new GameObject("DummyHUDItem");
        var item = go.AddComponent<EventHUDItem>();
        var cg = go.AddComponent<CanvasGroup>();

        // 子要素を最低限作成
        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        titleGO.transform.SetParent(go.transform, false);
        var bodyGO = new GameObject("Body", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        bodyGO.transform.SetParent(go.transform, false);
        var progGO = new GameObject("Progress", typeof(RectTransform), typeof(UnityEngine.UI.Slider));
        progGO.transform.SetParent(go.transform, false);
        var badgeGO = new GameObject("Badge", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        badgeGO.transform.SetParent(go.transform, false);

        // フィールド割当
        item.group = cg;
        item.titleTMP = titleGO.GetComponent<TMPro.TextMeshProUGUI>();
        item.bodyTMP = bodyGO.GetComponent<TMPro.TextMeshProUGUI>();
        item.progressBar = progGO.GetComponent<UnityEngine.UI.Slider>();
        item.typeBadge = badgeGO.GetComponent<UnityEngine.UI.Image>();

        return item;
    }
}
