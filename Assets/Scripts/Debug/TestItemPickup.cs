// Assets/Scripts/Debug/TestItemPickup.cs
using UnityEngine;

public class TestItemPickup : MonoBehaviour
{
    public enum Mode { SetFlag, AddProgress }

    [System.Serializable]
    public class Entry
    {
        [Header("Key & What to do")]
        public KeyCode key = KeyCode.G;
        public Mode mode = Mode.SetFlag;

        [Header("When mode = SetFlag")]
        public GameFlag flag = GameFlag.None;   // 例: Item_E1Apple / Item_E2Apple
        [Tooltip("SetFlag 実行後に進捗も加算する場合は ON")]
        public bool alsoAddProgress = false;
        public string alsoEventId = "Event.E1";
        public float alsoDelta = 0.5f;

        [Header("When mode = AddProgress (dev cheat)")]
        public string eventId = "Event.E1";
        public float delta = 0.5f;

        [Header("Toast/Label (任意)")]
        public string label = "";               // トーストに出す表示名
    }

    [Header("Entries (複数キーを並べられます)")]
    public Entry[] entries;

    [Header("UI/Debug")]
    public bool showToast = true;
    public bool logToConsole = true;

    void Update()
    {
        if (entries == null) return;

        foreach (var e in entries)
        {
            if (e == null) continue;
            if (Input.GetKeyDown(e.key))
            {
                switch (e.mode)
                {
                    case Mode.SetFlag:
                        DoSetFlag(e);
                        break;

                    case Mode.AddProgress:
                        DoAddProgress(e.eventId, e.delta);
                        break;
                }
            }
        }
    }

    void DoSetFlag(Entry e)
    {
        if (e.flag == GameFlag.None)
        {
            if (logToConsole) Debug.LogWarning("[TestItemPickup] SetFlag mode but flag is None.");
            return;
        }

        FlagService.Set(e.flag);
        FlagService.Save();

        if (showToast)
            QuestService.Instance?.toastHud?.ShowToast(
                string.IsNullOrEmpty(e.label) ? $"Flag ON: {e.flag}" : $"{e.label} 入手");

        if (logToConsole) Debug.Log($"[TestItemPickup] Flag ON -> {e.flag}");

        if (e.alsoAddProgress)
            DoAddProgress(e.alsoEventId, e.alsoDelta);
    }

    void DoAddProgress(string eventId, float delta)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            if (logToConsole) Debug.LogWarning("[TestItemPickup] AddProgress: eventId is empty.");
            return;
        }

        EventProgressService.Instance?.AddProgress(eventId, delta);

        if (showToast)
            QuestService.Instance?.toastHud?.ShowToast($"進捗 +{delta} → {eventId}");

        if (logToConsole) Debug.Log($"[TestItemPickup] AddProgress +{delta} to {eventId}");
    }
}
