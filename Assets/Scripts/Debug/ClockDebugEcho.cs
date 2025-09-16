// Assets/Scripts/Debug/ClockDebugEcho.cs
using UnityEngine;
using System.Reflection;

public class ClockDebugEcho : MonoBehaviour
{
    [SerializeField] MonoBehaviour clockBehaviour; // SimpleClock ‚ðŠ„“–
    MethodInfo getNow;
    void Start()
    {
        getNow = clockBehaviour?.GetType().GetProperty("NowGameSeconds")?.GetGetMethod();
        if (getNow == null) Debug.LogWarning("ClockDebugEcho: NowGameSeconds ‚ªŒ©‚Â‚©‚è‚Ü‚¹‚ñ");
    }
    void Update()
    {
        if (getNow == null) return;
        float now = (float)getNow.Invoke(clockBehaviour, null);
        if (Time.frameCount < 5 || Time.frameCount % 30 == 0)
            Debug.Log($"[Clock] now={now:F1}s");
    }
}
