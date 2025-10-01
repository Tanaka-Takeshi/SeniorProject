// Assets/Scripts/Editor/ScenarioRegistryValidator.cs
// Editor-only: ScenarioRegistry の内容を検証（手動／ビルド前／Play前）
// - CustomInspector / Menu / Build ガード / PlayMode ガード
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Game.Data;               // EventData

#region Inspector (手動検証UI)
[CustomEditor(typeof(ScenarioRegistry))]
public class ScenarioRegistryValidatorEditor : Editor
{
    private ValidationReport _lastReport;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var registry = (ScenarioRegistry)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Scenario Registry Validator", EditorStyles.boldLabel);

        if (GUILayout.Button("Validate Now"))
        {
            _lastReport = ScenarioRegistryValidator.Validate(registry);
            ScenarioRegistryValidator.LogReportToConsole(_lastReport);
        }

        if (_lastReport != null)
        {
            DrawReport(_lastReport);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "✔ 重複ID / 時系列(appear<=start<=end)\n" +
            "✔ 依存先の存在 / 循環依存\n" +
            "✔ エリアID存在(knownAreaIds)\n" +
            "✔ 開始手段なしの検出（autoStartOnLocation=false & requiresButtonPress=false）\n" +
            "✔ altCompleteThreshold の範囲(0..1)",
            MessageType.Info);
    }

    private void DrawReport(ValidationReport rep)
    {
        bool ok = rep.Errors.Count == 0;
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            ok ? $"No errors. Warnings: {rep.Warnings.Count}"
               : $"Errors: {rep.Errors.Count}  Warnings: {rep.Warnings.Count}",
            ok ? MessageType.Info : MessageType.Error);

        if (rep.Errors.Count > 0)
        {
            EditorGUILayout.LabelField("Errors", EditorStyles.boldLabel);
            foreach (var e in rep.Errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        }
        if (rep.Warnings.Count > 0)
        {
            EditorGUILayout.LabelField("Warnings", EditorStyles.boldLabel);
            foreach (var w in rep.Warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        }
    }
}
#endregion

#region 共通バリデータ本体 + メニュー
public static class ScenarioRegistryValidator
{
    private const string ResourcePath = "ScenarioRegistry";

    // メニュー：Resources/ScenarioRegistry を探して検証
    [MenuItem("Tools/Scenario/Validate Resources/ScenarioRegistry")]
    public static void ValidateFromMenu()
    {
        var reg = Resources.Load<ScenarioRegistry>(ResourcePath);
        if (!reg)
        {
            EditorUtility.DisplayDialog("Scenario Validator",
                $"Assets/Resources/{ResourcePath}.asset が見つかりません。先に作成してください。", "OK");
            return;
        }
        var report = Validate(reg);
        LogReportToConsole(report);
        EditorUtility.DisplayDialog("Scenario Validator",
            report.Errors.Count == 0
                ? $"OK! Errors=0, Warnings={report.Warnings.Count}"
                : $"Errors={report.Errors.Count}, Warnings={report.Warnings.Count}\nConsole を確認してください。",
            "OK");
    }

    public static ValidationReport Validate(ScenarioRegistry reg)
    {
        var rep = new ValidationReport();

        if (reg == null)
        {
            rep.Errors.Add("Registry is null.");
            return rep;
        }

        var list = reg.events ?? new List<EventData>();
        var knownAreas = new HashSet<string>(reg.knownAreaIds ?? new List<string>());

        // 重複ID／空ID
        var seen = new HashSet<string>();
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (!e)
            {
                rep.Warnings.Add($"[Index {i}] EventData is null.");
                continue;
            }
            if (string.IsNullOrEmpty(e.eventId))
            {
                rep.Errors.Add($"{Obj(e)} Empty id.");
                continue;
            }
            if (!seen.Add(e.eventId))
            {
                rep.Errors.Add($"{Obj(e)} Duplicate id: {e.eventId}");
            }
        }

        // ID -> EventData マップ
        var map = list.Where(x => x && !string.IsNullOrEmpty(x.eventId))
                      .ToDictionary(x => x.eventId, x => x);

        // 各イベントの検証
        foreach (var e in map.Values)
        {
            // appear<=start<=end
            float ap = ParseHM(e.appearAt);
            float sd = ParseHM(e.startDeadline);
            float ed = ParseHM(e.endDeadline);
            if (!(ap <= sd && sd <= ed))
            {
                rep.Errors.Add($"{Obj(e)} Time order violation: appear={e.appearAt}, start={e.startDeadline}, end={e.endDeadline}");
            }

            // alt(0..1)
            if (e.altCompleteThreshold < 0f || e.altCompleteThreshold > 1f)
            {
                rep.Errors.Add($"{Obj(e)} altCompleteThreshold out of range (0..1): {e.altCompleteThreshold}");
            }

            // 開始手段なし（両方false）
            if (!e.autoStartOnLocation && !e.requiresButtonPress)
            {
                rep.Errors.Add($"{Obj(e)} No start path: autoStartOnLocation=false AND requiresButtonPress=false");
            }

            // 依存先存在
            var deps = e.dependencies ?? new List<string>();
            foreach (var dep in deps)
            {
                if (string.IsNullOrWhiteSpace(dep)) continue;
                if (!map.ContainsKey(dep))
                {
                    rep.Errors.Add($"{Obj(e)} Missing dependency: {dep}");
                }
            }

            // Location の存在（任意: knownAreaIds に一つも無ければ Warning）
            if (e.location.kind == Game.Events.LocationKind.AreaId &&
                !string.IsNullOrEmpty(e.location.id))
            {
                var parts = e.location.id.Split('|')
                              .Select(s => s.Trim())
                              .Where(s => s.Length > 0)
                              .ToArray();
                if (parts.Length > 0)
                {
                    bool anyKnown = parts.Any(p => knownAreas.Contains(p));
                    if (!anyKnown)
                    {
                        rep.Warnings.Add($"{Obj(e)} Location id(s) not in knownAreaIds: {e.location.id}");
                    }
                }
            }
        }

        // 循環依存の検出
        DetectCycles(map, rep);
        return rep;
    }

    public static void LogReportToConsole(ValidationReport report)
    {
        var header = $"[ScenarioRegistryValidator] Errors={report.Errors.Count} Warnings={report.Warnings.Count}";
        Debug.Log(header);
        foreach (var e in report.Errors) Debug.LogError(e);
        foreach (var w in report.Warnings) Debug.LogWarning(w);
    }

    // 便利：Resources/ScenarioRegistry を読んで検証 nullならエラー報告1件
    public static ValidationReport ValidateResourcesAsset(out ScenarioRegistry reg)
    {
        reg = Resources.Load<ScenarioRegistry>(ResourcePath);
        var rep = new ValidationReport();
        if (!reg)
        {
            rep.Errors.Add($"Assets/Resources/{ResourcePath}.asset が見つかりません。");
            return rep;
        }
        return Validate(reg);
    }

    private static string Obj(UnityEngine.Object o) => o ? $"[{o.name}]" : "[<null>]";

    private static float ParseHM(string hhmm)
    {
        if (string.IsNullOrEmpty(hhmm)) return 0f;
        var sp = hhmm.Split(':');
        if (sp.Length < 2) return 0f;
        if (!int.TryParse(sp[0], out var hh)) return 0f;
        if (!int.TryParse(sp[1], out var mm)) return 0f;
        return hh * 60 + mm;
    }

    private static void DetectCycles(Dictionary<string, EventData> map, ValidationReport rep)
    {
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();

        bool Dfs(string id)
        {
            if (visited.Contains(id)) return false;
            if (!map.TryGetValue(id, out var e)) return false;

            if (!visiting.Add(id)) return true; // サイクル

            var deps = e.dependencies ?? new List<string>();
            foreach (var d in deps)
            {
                if (string.IsNullOrEmpty(d)) continue;
                if (map.ContainsKey(d) && Dfs(d)) return true;
            }
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }

        foreach (var id in map.Keys)
        {
            if (Dfs(id))
            {
                rep.Errors.Add($"[Cycle] Detected dependency cycle starting at: {id}");
            }
        }
    }
}
#endregion

#region ビルド前ガード（エラーでビルド中断）
public class ScenarioRegistryBuildGuard : IPreprocessBuildWithReport
{
    // スクリプト定義変更直後のビルドでも確実に動くプリプロセス
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var rep = ScenarioRegistryValidator.ValidateResourcesAsset(out var reg);
        ScenarioRegistryValidator.LogReportToConsole(rep);

        if (rep.Errors.Count > 0)
        {
            string msg = $"Scenario validation failed: Errors={rep.Errors.Count}  Warnings={rep.Warnings.Count}\n" +
                         $"Resources/ScenarioRegistry を修正してください。";
            throw new BuildFailedException(msg);
        }
    }
}
#endregion

#region Build メニュー経由のビルドをフック（任意／安全側）
[InitializeOnLoad]
public static class ScenarioRegistryBuildMenuHook
{
    static ScenarioRegistryBuildMenuHook()
    {
        // BuildPlayerWindow の実行を横取り → 先に検証して NG ならキャンセル
        BuildPlayerWindow.RegisterBuildPlayerHandler(BuildHandler);
    }

    private static void BuildHandler(BuildPlayerOptions options)
    {
        var rep = ScenarioRegistryValidator.ValidateResourcesAsset(out _);
        ScenarioRegistryValidator.LogReportToConsole(rep);

        if (rep.Errors.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "Scenario Validation",
                $"Errors={rep.Errors.Count}  Warnings={rep.Warnings.Count}\n" +
                "修正してから再度ビルドしてください。",
                "OK");
            return; // ビルド中断
        }

        // 問題なければ通常ビルド続行
#if UNITY_2021_2_OR_NEWER
        BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
#else
    BuildPipeline.BuildPlayer(options);
#endif
    }
}
#endregion

#region Play 開始前の注意喚起（エラー時は再生を止める）
[InitializeOnLoad]
public static class ScenarioRegistryPlayGuard
{
    private const bool CancelPlayOnError = true;   // false にすれば通知だけにできます

#if !SCENARIO_PLAY_GUARD_OFF
    static ScenarioRegistryPlayGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode) return;

        var rep = ScenarioRegistryValidator.ValidateResourcesAsset(out _);
        if (rep.Errors.Count > 0)
        {
            Debug.LogError($"[ScenarioRegistryValidator] Play aborted. Errors={rep.Errors.Count}, Warnings={rep.Warnings.Count}");

            // ダイアログは常に表示
            var message = CancelPlayOnError
                ? "シナリオ検証でエラーが見つかりました。Playを中断します。\nConsole を確認してください。"
                : "シナリオ検証でエラーが見つかりました。Console を確認してください。\n（設定によりPlayは続行します）";
            EditorUtility.DisplayDialog("Scenario Validation", message, "OK");

            // 中断フラグ時のみ再生を止める（ここだけ条件分岐）
            if (CancelPlayOnError)
            {
                // 再生直前にEditModeへ戻す
                EditorApplication.isPlaying = false;
            }
        }
    }
#endif
}
#endregion

#region 結果オブジェクト
public class ValidationReport
{
    public readonly List<string> Errors = new();
    public readonly List<string> Warnings = new();
}
#endregion
