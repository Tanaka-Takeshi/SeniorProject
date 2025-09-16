using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Config
{
    [CreateAssetMenu(menuName = "Game/Config/GlobalSettings")]
    public class GlobalSettings : ScriptableObject
    {
        [Header("Time & Calendar")]
        public float dayLengthSeconds = 30f * 60f;  // 1日=30分
        public bool pauseStopsTime = true;
        public bool useWeekdaySystem = false;
        public HolidaySet holidaySet;

        [Header("Main/Sub Blackout window (Seconds)")]
        public float mainPreWindowSec = 30f;
        public float mainPostWindowSec = 30f;

        [Header("Input")]
#if ENABLE_INPUT_SYSTEM
        public InputActionReference interactAction; // 任意: Input System を使うとき
#endif
        public KeyCode fallbackInteractKey = KeyCode.E; // フォールバック（Eキー）
    }
}
