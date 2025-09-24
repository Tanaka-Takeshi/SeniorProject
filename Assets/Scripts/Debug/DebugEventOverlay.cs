#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Runtime;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(2000)]
public sealed class DebugEventOverlay : MonoBehaviour
{
    public EventManager em;
    public KeyCode toggleKey = KeyCode.F9;
    public int showLines = 12;
    public bool visible = false;

    void Reset()
    {
#if UNITY_2023_1_OR_NEWER
        if (!em) em = Object.FindFirstObjectByType<Game.Runtime.EventManager>();
#else
        if (!em) em = Object.FindObjectOfType<Game.Runtime.EventManager>();
#endif
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) visible = !visible;
    }

    void OnGUI()
    {
        if (!visible || em == null) return;
        var tr = em.GetType().GetMethod("GetRecentTrace",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        var arr = tr?.Invoke(em, null) as string[] ?? new string[0];
        var lines = arr.Reverse().Take(showLines).Reverse();

        GUILayout.BeginArea(new Rect(10, 10, 520, 20 + 18 * (showLines + 2)), GUI.skin.box);
        GUILayout.Label($"[EventOverlay] Runtimes={em.AllRuntimes().Count()} Trace={(arr?.Length ?? 0)}");
        foreach (var l in lines) GUILayout.Label(l);
        GUILayout.EndArea();
    }
}
#endif
