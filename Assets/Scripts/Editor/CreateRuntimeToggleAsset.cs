#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Config; // ← ScenarioRuntimeToggles がある名前空間

/// <summary>
/// Tools ▶ Scenario ▶ Create RuntimeToggles (Resources)
/// で Assets/Resources/ScenarioRuntimeToggles.asset を作成。
/// </summary>
public static class ScenarioRuntimeTogglesCreator
{
    private const string AssetPath = "Assets/Resources/ScenarioRuntimeToggles.asset";

    [MenuItem("Tools/Scenario/Create RuntimeToggles (Resources)")]
    public static void Create()
    {
        // 既にあるなら選択して終了
        var existing = AssetDatabase.LoadAssetAtPath<ScenarioRuntimeToggles>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorUtility.DisplayDialog("Scenario RuntimeToggles",
                $"既に存在します:\n{AssetPath}", "OK");
            return;
        }

        // Resources フォルダを(なければ)作成
        System.IO.Directory.CreateDirectory("Assets/Resources");

        // 新規作成
        var inst = ScriptableObject.CreateInstance<ScenarioRuntimeToggles>();
        AssetDatabase.CreateAsset(inst, AssetPath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = inst;
        EditorGUIUtility.PingObject(inst);
        EditorUtility.DisplayDialog("Scenario RuntimeToggles",
            $"作成しました:\n{AssetPath}", "OK");
    }

    [MenuItem("Tools/Scenario/Ping RuntimeToggles (Resources)")]
    public static void Ping()
    {
        var obj = AssetDatabase.LoadAssetAtPath<ScenarioRuntimeToggles>(AssetPath);
        if (obj != null)
        {
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }
        else
        {
            EditorUtility.DisplayDialog("Scenario RuntimeToggles",
                "Resources に ScenarioRuntimeToggles.asset が見つかりませんでした。",
                "OK");
        }
    }
}
#endif
