// Assets/Scripts/Runtime/ScenarioBootstrap.cs
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Game.Config;
using Game.Data;
using Game.Events;
using Game.Runtime;

[DefaultExecutionOrder(-900)]
public sealed class ScenarioBootstrap : MonoBehaviour
{
    [Header("References (Scene or Resources)")]
    [SerializeField] EventManager eventManager;
    [SerializeField] ScenarioRegistry registry;
    [SerializeField] GlobalSettings settings;

    [Tooltip("Resources/ScenarioRuntimeToggles.asset を自動読込する")]
    [SerializeField] bool useRuntimeToggles = true;

    [Header("Optional: DI (空なら自動取得)")]
    [SerializeField] MonoBehaviour clockBehaviour;     // IClock
    [SerializeField] MonoBehaviour locationBehaviour;  // ILocationResolver
    [SerializeField] MonoBehaviour inputBehaviour;     // IInputProxy

    const string kRegistryPath = "ScenarioRegistry";
    const string kTogglesPath = "ScenarioRuntimeToggles";

    void Awake()
    {
        // ---- 参照解決 ------------------------------------------------------
        if (!eventManager)
            eventManager = FindFirstObjectByType<EventManager>();

        if (!registry)
            registry = Resources.Load<ScenarioRegistry>(kRegistryPath);

        var gs = settings ? settings : (registry ? registry.overrideSettings : null);
        if (!gs)
        {
            gs = ScriptableObject.CreateInstance<GlobalSettings>();
            gs.dayLengthSeconds = 1440f;
        }

        // ---- 依存（Clock/Location/Input） 自動探索 ---------------------------
        if (!clockBehaviour) clockBehaviour = FindFirstObjectByType<SimpleClock>(FindObjectsInactive.Include);
        if (!locationBehaviour) locationBehaviour = FindFirstObjectByType<SimpleLocationResolver>(FindObjectsInactive.Include);
        if (!inputBehaviour) inputBehaviour = FindBehaviourImplementing<IInputProxy>();

        if (!eventManager)
        {
            Debug.LogError("[ScenarioBootstrap] EventManager が見つかりません。", this);
            return;
        }

        // ---- DI: インターフェイスへキャストして注入 -------------------------
        var iClock = clockBehaviour as IClock;
        var iLocator = locationBehaviour as ILocationResolver;
        var iInput = inputBehaviour as IInputProxy;

        eventManager.Inject(iClock, iLocator, iInput, gs);

        // ---- 実データの決定（トグル反映） ----------------------------------
        var picked = (registry && registry.events != null)
            ? new List<EventData>(registry.events.Where(x => x)) : new List<EventData>();

        ScenarioRuntimeToggles toggles = null;
        if (useRuntimeToggles)
            toggles = Resources.Load<ScenarioRuntimeToggles>(kTogglesPath);

        var finalList = ApplyRuntimeToggles(picked, toggles);

        // ---- EventManager に供給 --------------------------------------------
        eventManager.InitializeForTest(finalList);

        // ★ デバッグ用に「トグル適用結果」を公開（HUD下のオーバーレイ等で参照）
        {
            var allIds = picked.Select(e => e.eventId).Where(id => !string.IsNullOrEmpty(id));
            var activeIds = finalList.Select(e => e.eventId).Where(id => !string.IsNullOrEmpty(id));
            var (disabledAll, includeList, excludeList) = ReadToggles(toggles);

            Game.Runtime.ScenarioRuntimeInfo.Publish(
                allIds,
                activeIds,
                includeList,
                excludeList,
                disabledAll
            );
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var (disAll, incCnt, excCnt) = SummarizeToggles(toggles);
        Debug.Log($"[ScenarioBootstrap] Initialized with {finalList.Count} events (toggles: disableAll={disAll}, include={incCnt}, exclude={excCnt})", this);
#endif
    }

    // 任意の MonoBehaviour 群から「T を実装している最初のもの」を返す
    static MonoBehaviour FindBehaviourImplementing<T>() where T : class
    {
        var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var mb in all)
            if (mb is T) return mb;
        return null;
    }

    // ------- RuntimeToggles を“名前ゆれ吸収”で読む（Reflection） ------------
    static (bool disableAll, List<string> include, List<string> exclude) ReadToggles(ScenarioRuntimeToggles t)
    {
        if (!t) return (false, null, null);
        bool disableAll = GetBoolField(t, "disableAll") || GetBoolField(t, "allDisabled") || GetBoolField(t, "disable");
        var include = GetStringListField(t, "includeIds") ?? GetStringListField(t, "include") ?? GetStringListField(t, "whitelist");
        var exclude = GetStringListField(t, "excludeIds") ?? GetStringListField(t, "exclude") ?? GetStringListField(t, "blacklist");
        return (disableAll, include, exclude);
    }

    static bool GetBoolField(Object o, string name)
    {
        var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(bool))
            return (bool)f.GetValue(o);
        return false;
    }

    static List<string> GetStringListField(Object o, string name)
    {
        var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && typeof(List<string>).IsAssignableFrom(f.FieldType))
            return (List<string>)f.GetValue(o);
        return null;
    }

    static (bool disableAll, int includeCount, int excludeCount) SummarizeToggles(ScenarioRuntimeToggles t)
    {
        var (d, inc, exc) = ReadToggles(t);
        return (d, inc?.Count ?? 0, exc?.Count ?? 0);
    }

    static List<EventData> ApplyRuntimeToggles(List<EventData> source, ScenarioRuntimeToggles toggles)
    {
        if (source == null) return new List<EventData>();
        if (!toggles) return source;

        var (disableAll, include, exclude) = ReadToggles(toggles);

        if (disableAll) return new List<EventData>();

        IEnumerable<EventData> current = source;

        if (include != null && include.Count > 0)
        {
            var inc = new HashSet<string>(include.Where(s => !string.IsNullOrWhiteSpace(s)));
            current = current.Where(e => e && inc.Contains(e.eventId));
        }

        if (exclude != null && exclude.Count > 0)
        {
            var exc = new HashSet<string>(exclude.Where(s => !string.IsNullOrWhiteSpace(s)));
            current = current.Where(e => e && !exc.Contains(e.eventId));
        }

        return current.ToList();
    }
}
