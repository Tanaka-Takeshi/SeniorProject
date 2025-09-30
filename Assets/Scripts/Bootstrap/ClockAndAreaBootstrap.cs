// Assets/Scripts/Bootstrap/ClockAndAreaBootstrap.cs
using UnityEngine;
using System.Reflection;
using Game.Runtime;

[DefaultExecutionOrder(-1000)]  // ★ EventManager より先に動く
public class ClockAndAreaBootstrap : MonoBehaviour
{
    [SerializeField] MonoBehaviour clockBehaviour;   // SimpleClock
    [SerializeField] SimpleLocationResolver locator;
    [SerializeField] string areaId = "Town";

    MethodInfo jump;
    void Awake()
    {
        FlagService.Load();

        if (clockBehaviour != null)
        {
            jump = clockBehaviour.GetType().GetMethod("Jump", BindingFlags.Public | BindingFlags.Instance);
            jump?.Invoke(clockBehaviour, new object[] { 0f });  // ★ 00:00 に固定
        }
        if (locator != null) locator.SetArea(areaId);
    }
}

