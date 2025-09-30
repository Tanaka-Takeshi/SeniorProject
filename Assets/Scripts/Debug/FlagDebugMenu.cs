// Assets/Scripts/Dev/FlagDebugMenu.cs
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class FlagDebugMenu
{
#if UNITY_EDITOR
    const string Key = "GAME_FLAGS_CSV";

    [MenuItem("Tools/Flags/Dump")]
    public static void Dump()
    {
        var csv = PlayerPrefs.GetString(Key, "(none)");
        Debug.Log($"[Flags] {csv}");
    }

    [MenuItem("Tools/Flags/Clear")]
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
        Debug.Log("[Flags] Cleared");
    }
#endif
}
