// Assets/Scripts/Debug/ProgressNudgeHotkeys.cs
using UnityEngine;
using Game.Runtime;
using System.Collections.Generic;
using System.Reflection;

public class ProgressNudgeHotkeys : MonoBehaviour
{
    [SerializeField] EventManager manager;
    [SerializeField] string targetEventId = "Sub.Progress";
    [SerializeField, Range(0.01f, 1f)] float step = 0.25f; // 25%ëùå∏

    Dictionary<string, EventRuntime> dict;
    void Start()
    {
        var f = typeof(EventManager).GetField("_events", BindingFlags.NonPublic | BindingFlags.Instance);
        dict = (Dictionary<string, EventRuntime>)f.GetValue(manager);
    }

    void Update()
    {
        if (dict == null || !dict.TryGetValue(targetEventId, out var rt)) return;

        if (Input.GetKeyDown(KeyCode.P))        // +25%
            rt.SetProgress(Mathf.Clamp01(rt.Progress + step));
        if (Input.GetKeyDown(KeyCode.O))        // -25%
            rt.SetProgress(Mathf.Clamp01(rt.Progress - step));
    }
}
