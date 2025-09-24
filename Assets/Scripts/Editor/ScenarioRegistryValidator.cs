// Assets/Scripts/Editor/ScenarioRegistryValidator.cs
#if UNITY_EDITOR
using Game.Data;
using Game.Events;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ScenarioRegistryValidator
{
    [MenuItem("Tools/Scenario/Validate Registry")]
    public static void Validate()
    {
        var reg = Selection.activeObject as ScenarioRegistry;
        if (!reg)
        {
            Debug.LogWarning("選択中の ScenarioRegistry を検証します。まず Project ウィンドウで Registry を選択してください。");
            return;
        }

        var ok = true;
        var ids = new HashSet<string>();

        foreach (var e in reg.events)
        {
            if (!e) { Debug.LogError("Null の EventData 参照があります。", reg); ok = false; continue; }

            // ID重複
            if (!ids.Add(e.eventId))
            {
                Debug.LogError($"ID重複: {e.eventId}", e);
                ok = false;
            }

            // 必須項目
            if (string.IsNullOrEmpty(e.appearAt) || string.IsNullOrEmpty(e.endDeadline))
            {
                Debug.LogError($"時間が未設定: {e.eventId}", e);
                ok = false;
            }

            // 位置ID（任意の既知リストでチェック）
            if (reg.knownAreaIds != null && reg.knownAreaIds.Count > 0)
            {
                if (e.location.kind == LocationKind.AreaId && !string.IsNullOrEmpty(e.location.id))
                {
                    if (!reg.knownAreaIds.Contains(e.location.id))
                    {
                        Debug.LogWarning($"未登録のAreaId参照: {e.eventId} -> {e.location.id}", e);
                    }
                }
            }
        }

        if (ok) Debug.Log($"[Validate] OK: {reg.events.Count} 件。");
        else Debug.LogWarning("[Validate] 問題があります。ログを確認してください。");
    }
}
#endif
