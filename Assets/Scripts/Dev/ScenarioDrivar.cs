// Assets/Scripts/Dev/ScenarioDriver.cs
using UnityEngine;
using Game.Runtime;
using Game.Events;

[DefaultExecutionOrder(2000)]
public class ScenarioDriver : MonoBehaviour
{
    [SerializeField] private EventManager em;
    [SerializeField] private SimpleClock clock;
    [SerializeField] private SimpleLocationResolver locator;
    [SerializeField] private SingleEventTracker tracker;

    [Header("Keys")]
    public KeyCode keyTown = KeyCode.Alpha1;
    public KeyCode keyForest = KeyCode.Alpha2;
    public KeyCode keyInteract = KeyCode.E;
    public KeyCode keyPlus5Min = KeyCode.T;
    public KeyCode keyJumpMinute = KeyCode.Y;
    public KeyCode keyResetFailAll = KeyCode.R;
    public KeyCode keyDump = KeyCode.L;

    void Reset()
    {
#if UNITY_2023_1_OR_NEWER
        if (!em) em = Object.FindFirstObjectByType<EventManager>();
        if (!clock) clock = Object.FindFirstObjectByType<SimpleClock>();
        if (!locator) locator = Object.FindFirstObjectByType<SimpleLocationResolver>();
        if (!tracker) tracker = Object.FindFirstObjectByType<SingleEventTracker>();
#else
        if (!em) em = Object.FindObjectOfType<EventManager>();
        if (!clock) clock = Object.FindObjectOfType<SimpleClock>();
        if (!locator) locator = Object.FindObjectOfType<SimpleLocationResolver>();
        if (!tracker) tracker = Object.FindObjectOfType<SingleEventTracker>();
#endif
    }

    void Update()
    {
        if (!em || !clock || !locator) return;

        if (Input.GetKeyDown(keyTown))
        {
            locator.SetArea("Town");
            Debug.Log("[Driver] Moved to Town");
        }
        if (Input.GetKeyDown(keyForest))
        {
            locator.SetArea("Forest");
            Debug.Log("[Driver] Moved to Forest");
        }
        if (Input.GetKeyDown(keyInteract))
        {
            // IInputProxy 実装側がフレーム入力を見るので、ここから直接叩かない
            Debug.Log("[Driver] Press Interact (E) requested (use your input proxy)");
        }
        if (Input.GetKeyDown(keyPlus5Min))
        {
            clock.Jump(clock.NowGameSeconds + 5f);
            Debug.Log($"[Driver] +5min -> {ToHHMM(clock.NowGameSeconds)}");
        }
        if (Input.GetKeyDown(keyJumpMinute))
        {
            // 例: 00:25 へ。数字キーなどと組み合わせたい場合は適宜改造
            clock.Jump(25f);
            Debug.Log($"[Driver] Jump -> {ToHHMM(clock.NowGameSeconds)}");
        }
        if (Input.GetKeyDown(keyResetFailAll))
        {
            foreach (var rt in em.AllRuntimes())
                rt?.ForceInterrupt();
            Debug.Log("[Driver] ForceInterrupt all");
        }
        if (Input.GetKeyDown(keyDump))
        {
            DumpAll();
        }
    }

    void DumpAll()
    {
        Debug.Log("=== Dump All Runtimes ===");
        foreach (var rt in em.AllRuntimes())
            Debug.Log($"- {rt.Data.eventId} : {rt.State} ({rt.Data.type})");
        if (tracker)
        {
            var mi = typeof(SingleEventTracker).GetMethod("DebugDumpFromManager",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            mi?.Invoke(tracker, null);
        }
    }

    static string ToHHMM(float min)
    {
        int m = Mathf.FloorToInt(min);
        int hh = m / 60; int mm = m % 60;
        return $"{hh:D2}:{mm:D2}";
    }
}
