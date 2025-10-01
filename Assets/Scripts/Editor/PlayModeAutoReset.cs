// Assets/Scripts/Editor/PlayModeAutoReset.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayModeAutoReset
{
    const string Menu = "Tools/Dev/Clear Save On Enter PlayMode";
    const string Pref = "Dev_ClearSaveOnPlay";

    static PlayModeAutoReset()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (!EditorPrefs.HasKey(Pref)) EditorPrefs.SetBool(Pref, false);
        UnityEditor.Menu.SetChecked(Menu, EditorPrefs.GetBool(Pref)); // ★ 修正
    }

    [MenuItem(Menu)]
    static void Toggle()
    {
        bool v = !EditorPrefs.GetBool(Pref, false);
        EditorPrefs.SetBool(Pref, v);
        UnityEditor.Menu.SetChecked(Menu, v); // ★ 修正
    }

    static void OnPlayModeChanged(PlayModeStateChange s)
    {
        if (s != PlayModeStateChange.ExitingEditMode) return;
        if (!EditorPrefs.GetBool(Pref, false)) return;

        // 必要なキーだけ削除
        PlayerPrefs.DeleteKey("GAME_FLAGS_CSV");
        PlayerPrefs.DeleteKey("QUEST_STATES_V2");
        PlayerPrefs.DeleteKey("QUEST_STEPS_V2");
        PlayerPrefs.Save();
        Debug.Log("[Dev] Cleared flags & quests PlayerPrefs before Play.");
    }
}
#endif
