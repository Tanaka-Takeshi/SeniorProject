// Assets/Scripts/Debug/EventDebugOverlay.cs
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Config;
using Game.Events;
using Game.Runtime;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class EventDebugOverlay : MonoBehaviour
{
    [Header("Refs (なくても自動探索)")]
    public EventManager eventManager;
    public HUDController hud;

    [Header("UI")]
    public bool showWindow = false;
    public Vector2 windowScroll;
    public Rect windowRect = new Rect(10, 10, 460, 420);

    // 表示オプション
    public bool showAvailableList = false;     // Available も表示したい時 ON
    [Range(1, 200)]
    public int maxRows = 20;                   // 行数制限（UIから変更可）

    // 追加情報（場所など）
    SimpleLocationResolver locator;

    const string kRegistryPath = "ScenarioRegistry";
    const string kTogglesPath = "ScenarioRuntimeToggles";

    void Awake()
    {
        // 参照が未設定なら軽く探す（First/Any でOK）
        if (!eventManager)
            eventManager = Object.FindFirstObjectByType<EventManager>();
        if (!hud)
            hud = Object.FindFirstObjectByType<HUDController>();
        if (!locator)
            locator = Object.FindFirstObjectByType<SimpleLocationResolver>(FindObjectsInactive.Include);
    }

    void OnGUI()
    {
        if (!showWindow) return;

        GUI.depth = 0;
        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Event Debug Overlay");
    }

    void DrawWindow(int id)
    {
        if (!eventManager)
        {
            GUILayout.Label("<color=#f66><b>EventManager not found</b></color>", Rich());
            if (GUILayout.Button("Find EventManager")) eventManager = Object.FindFirstObjectByType<EventManager>();
            GUI.DragWindow();
            return;
        }

        // ── ヘッダ（時刻・場所・HUD情報・行数UI） ───────────────────────────────
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Time: {eventManager.ClockRef?.NowGameSeconds:0.0} (min as sec)", Mini());
        GUILayout.FlexibleSpace();
        var area = locator ? (locator.CurrentAreaId ?? "<none>") : "<locator missing>";
        GUILayout.Label($"Area: <b>{area}</b>", Rich(), GUILayout.Width(150));
        GUILayout.EndHorizontal();

        string hudTitle = hud ? (hud.CurrentTitle ?? "<null>") : "<HUD missing>";
        GUILayout.Label($"HUD Title: <b>{hudTitle}</b>", Rich());

        GUILayout.BeginHorizontal();
        showAvailableList = GUILayout.Toggle(showAvailableList, "Show Available", GUILayout.Width(130));
        GUILayout.Label("Rows:", GUILayout.Width(38));
        // 行数の + / - と IntField
        if (GUILayout.Button("-", GUILayout.Width(22))) maxRows = Mathf.Max(1, maxRows - 1);
        maxRows = Mathf.Clamp(EditorIntField(maxRows, 50), 1, 200);
        if (GUILayout.Button("+", GUILayout.Width(22))) maxRows = Mathf.Min(200, maxRows + 1);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // ── トグル適用結果（Resources から直接読む） ─────────────────────────
        {
            var (allCnt, activeCnt, incCnt, excCnt, disabledAll) = SnapshotToggleSummary(eventManager);
            GUILayout.Space(4);
            GUILayout.Label(
                $"<b>Toggles</b> : all={allCnt}, active={activeCnt}, include={incCnt}, exclude={excCnt}, disableAll={disabledAll}",
                Rich());
        }

        // ── 本体リスト ────────────────────────────────────────────────────────
        var all = eventManager.AllRuntimes();
        var inprog = all.Where(r => r.State == EventState.InProgress)
                        .OrderBy(r => r.Data.type == Game.Events.EventType.Main ? 0 : 1)
                        .ThenBy(r => r.Data.eventId)
                        .ToList();

        GUILayout.Space(6);
        GUILayout.Label($"InProgress ({inprog.Count})  <size=10>(max {maxRows})</size>", Rich());

        windowScroll = GUILayout.BeginScrollView(windowScroll, GUILayout.Height(showAvailableList ? 230 : 280));
        DrawRowsClamped(inprog, maxRows, "#A3E635"); // 黄緑

        // 必要なら Available も
        if (showAvailableList)
        {
            GUILayout.Space(8);
            var available = all.Where(r => r.State == EventState.Available)
                               .OrderBy(r => r.Data.type == Game.Events.EventType.Main ? 0 : 1)
                               .ThenBy(r => r.Data.eventId)
                               .ToList();
            GUILayout.Label($"Available ({available.Count})  <size=10>(max {maxRows})</size>", Rich());
            DrawRowsClamped(available, maxRows, "#60A5FA"); // 青
        }
        GUILayout.EndScrollView();

        // ── フッタ操作 ───────────────────────────────────────────────────────
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Snap JSON", GUILayout.Width(100)))
        {
            Debug.Log("[Overlay] --- SNAPSHOT ---\n" + eventManager.ExportSnapshotJson(), eventManager);
        }
        if (GUILayout.Button("Trace Toggle", GUILayout.Width(100)))
        {
            var mi = typeof(EventManager).GetMethod("EnableRuntimeTrace",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            mi?.Invoke(eventManager, new object[] { true });
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close", GUILayout.Width(80))) showWindow = false;
        GUILayout.EndHorizontal();

        GUI.DragWindow();
    }

    // 行数を制限して描画 + 余り件数表示
    void DrawRowsClamped(List<EventRuntime> list, int limit, string colorHex)
    {
        int shown = 0;
        foreach (var rt in list)
        {
            if (shown++ >= limit) break;
            DrawRow(rt, colorHex);
        }
        int remain = Mathf.Max(0, list.Count - limit);
        if (remain > 0)
        {
            GUILayout.Label($"… and {remain} more", Mini());
        }
    }

    void DrawRow(EventRuntime rt, string colorHex)
    {
        var id = rt.Data.eventId;
        var type = rt.Data.type.ToString();
        var prg = rt.Progress.ToString("0.00");
        var appear = rt.Data.appearAt;
        var start = rt.Data.startDeadline;
        var end = rt.Data.endDeadline;

        GUILayout.Label(
            $"<color={colorHex}><b>{id}</b></color> [{type}]  " +
            $"state=<b>{rt.State}</b>  prog={prg}  time({appear}→{start}→{end})",
            Mini());
    }

    // ── Toggles の要約（Resources から直接ロード） ──────────────────────────
    (int all, int active, int include, int exclude, bool disabledAll) SnapshotToggleSummary(EventManager em)
    {
        // 全イベント数（ScenarioRegistry）
        var reg = Resources.Load<ScenarioRegistry>(kRegistryPath);
        int allCnt = reg?.events?.Count(e => e) ?? 0;

        // 現在有効になっている（EventManagerに渡された）件数
        int activeCnt = 0;
        if (em != null)
        {
            // AllRuntimes() は IEnumerable<EventRuntime>
            activeCnt = em.AllRuntimes()?.Count() ?? 0;
        }

        // トグル情報（ScenarioRuntimeToggles）
        var toggles = Resources.Load<ScenarioRuntimeToggles>(kTogglesPath);
        bool disabledAll = false;
        int incCnt = 0;
        int excCnt = 0;

        if (toggles)
        {
            var (disAll, include, exclude) = ReadToggles(toggles);
            disabledAll = disAll;
            incCnt = include?.Count ?? 0;
            excCnt = exclude?.Count ?? 0;
        }

        return (allCnt, activeCnt, incCnt, excCnt, disabledAll);
    }

    // ── ScenarioBootstrap と同じ“名前ゆれ吸収”の反射ユーティリティ ───────────
    static (bool disableAll, List<string> include, List<string> exclude) ReadToggles(ScriptableObject t)
    {
        if (!t) return (false, null, null);
        bool disableAll = GetBoolField(t, "disableAll") || GetBoolField(t, "allDisabled") || GetBoolField(t, "disable");
        var include = GetStringListField(t, "includeIds") ?? GetStringListField(t, "include") ?? GetStringListField(t, "whitelist");
        var exclude = GetStringListField(t, "excludeIds") ?? GetStringListField(t, "exclude") ?? GetStringListField(t, "blacklist");
        return (disableAll, include, exclude);
    }
    static bool GetBoolField(Object o, string name)
    {
        var f = o.GetType().GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(bool))
            return (bool)f.GetValue(o);
        return false;
    }
    static List<string> GetStringListField(Object o, string name)
    {
        var f = o.GetType().GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null && typeof(List<string>).IsAssignableFrom(f.FieldType))
            return (List<string>)f.GetValue(o);
        return null;
    }

    // GUIStyle ヘルパ
    GUIStyle Bold()
    {
        var s = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        return s;
    }
    GUIStyle Mini()
    {
        var s = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 };
        return s;
    }
    GUIStyle Rich()
    {
        var s = new GUIStyle(GUI.skin.label) { richText = true };
        return s;
    }

    // Editor/Player両用の軽量 IntField 代替
    int EditorIntField(int value, float width)
    {
        string str = GUILayout.TextField(value.ToString(), GUILayout.Width(width));
        return int.TryParse(str, out var v) ? v : value;
    }
}
#endif
