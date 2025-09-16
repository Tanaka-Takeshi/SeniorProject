// Assets/Scripts/Bootstrap/ClockAndAreaBootstrap.cs
using UnityEngine;
using System.Reflection;
using Game.Runtime;

[DefaultExecutionOrder(-1000)]  // Åö EventManager ÇÊÇËêÊÇ…ìÆÇ≠
public class ClockAndAreaBootstrap : MonoBehaviour
{
    [SerializeField] MonoBehaviour clockBehaviour;   // SimpleClock
    [SerializeField] SimpleLocationResolver locator;
    [SerializeField] string areaId = "TestArea";

    MethodInfo jump;
    void Awake()
    {
        if (clockBehaviour != null)
        {
            jump = clockBehaviour.GetType().GetMethod("Jump", BindingFlags.Public | BindingFlags.Instance);
            jump?.Invoke(clockBehaviour, new object[] { 0f });  // Åö 00:00 Ç…å≈íË
        }
        if (locator != null) locator.SetArea(areaId);
    }
}

