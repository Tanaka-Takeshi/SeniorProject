// Assets/Scripts/Dev/FlagWatcher.cs
using UnityEngine;

public class FlagWatcher : MonoBehaviour
{
    public bool showOverlay = true;   // 画面左上に表示
    const string Key = "GAME_FLAGS_CSV";

    void OnGUI()
    {
        if (!showOverlay) return;
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(10, 10, 600, 200), GUI.skin.box);
        GUILayout.Label("Flags CSV:");
        GUILayout.Label(PlayerPrefs.GetString(Key, "(none)"));
        GUILayout.EndArea();
    }

    void Update()
    {
        // F9でDump、F10でClear（Editor/ビルド共通）
        if (Input.GetKeyDown(KeyCode.F9))
            Debug.Log($"[Flags] {PlayerPrefs.GetString(Key, "(none)")}");
        if (Input.GetKeyDown(KeyCode.F10))
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            Debug.Log("[Flags] Cleared");
        }

        if (Input.GetKeyDown(KeyCode.F6)) { FlagService.Toggle(GameFlag.HasSpecialItem); FlagService.Save(); Debug.Log("Toggle HasSpecialItem"); }
    }
}
