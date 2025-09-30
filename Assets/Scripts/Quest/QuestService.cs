// Assets/Scripts/Quest/QuestService.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Data;  // あなたの QuestData の namespace

public enum QuestState { Inactive, Active, Completed, Failed }

public class QuestService : MonoBehaviour
{
    public static QuestService Instance { get; private set; }

    [Header("Registry")]
    public QuestData[] quests;

    [Header("UI (optional)")]
    public HUDController toastHud; // Panel_Toast の HUDController

    // 状態
    readonly Dictionary<string, QuestState> _states = new(); // questId -> state
    readonly Dictionary<string, int> _stepIndex = new();     // questId -> step(0..)

    // 保存キー
    const string KEY_QSTATE = "QUEST_STATES_V2";
    const string KEY_QSTEP = "QUEST_STEPS_V2";

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Load();
    }

    public QuestData Find(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return null;
        foreach (var q in quests) if (q && q.questId == questId) return q;
        return null;
    }

    public QuestState GetState(string questId)
        => _states.TryGetValue(questId, out var s) ? s : QuestState.Inactive;

    public int GetCurrentStep(string questId)
        => _stepIndex.TryGetValue(questId, out var i) ? i : 0;

    public void StartQuest(string questId)
    {
        var q = Find(questId);
        if (!q) { Debug.LogWarning($"[Quest] StartQuest: not found {questId}"); return; }

        _states[questId] = QuestState.Active;
        if (!_stepIndex.ContainsKey(questId)) _stepIndex[questId] = 0;

        Toast("クエスト開始", ReadableQuestName(q));
        Debug.Log($"[Quest] Started: {questId}");
        Save();
    }

    public void CompleteQuest(string questId)
    {
        var q = Find(questId);
        if (!q) { Debug.LogWarning($"[Quest] CompleteQuest: not found {questId}"); return; }

        _states[questId] = QuestState.Completed;

        // 報酬フラグ（string -> GameFlag へ変換できたものだけ適用）
        if (q.rewardFlags != null)
        {
            foreach (var rf in q.rewardFlags)
            {
                if (string.IsNullOrEmpty(rf)) continue;
                if (Enum.TryParse<GameFlag>(rf, out var gf) && gf != GameFlag.None)
                {
                    FlagService.Set(gf);
                }
                else
                {
                    Debug.LogWarning($"[Quest] reward flag '{rf}' is not a valid GameFlag enum. Skipped.");
                }
            }
            FlagService.Save();
        }

        Toast("クエスト完了", ReadableQuestName(q));
        Debug.Log($"[Quest] Completed: {questId}");
        Save();
    }

    /// <summary>
    /// 進行中クエストの「現在の要求 eventId」と一致したらステップを進める。
    /// （Started / Custom を進行トリガに使用）
    /// </summary>
    public void NotifyEventSignal(string eventId, ConversationSignalKind kind)
    {
        if (string.IsNullOrEmpty(eventId)) return;

        foreach (var q in quests)
        {
            if (!q) continue;
            if (GetState(q.questId) != QuestState.Active) continue;

            var step = GetCurrentStep(q.questId);
            var list = q.stepEventIds;
            if (list == null || list.Count == 0) continue;
            if (step >= list.Count) continue;

            var requireId = list[step];
            if (string.IsNullOrEmpty(requireId)) continue;

            if (eventId == requireId && (kind == ConversationSignalKind.Started || kind == ConversationSignalKind.Custom))
            {
                _stepIndex[q.questId] = step + 1;
                Debug.Log($"[Quest] Step ✓ ({q.questId}) {requireId} -> step={_stepIndex[q.questId]}/{list.Count}");
                Toast(null, $"目標達成：{ReadableEvent(requireId)}");

                if (_stepIndex[q.questId] >= list.Count)
                {
                    CompleteQuest(q.questId);
                }
                Save();
            }
        }
    }

    public void MarkEventCompleted(string eventId)
        => NotifyEventSignal(eventId, ConversationSignalKind.Custom);

    // ===== 内部 =====

    string ReadableQuestName(QuestData q)
        => string.IsNullOrEmpty(q.goalTextKey) ? q.questId : q.goalTextKey;

    string ReadableEvent(string eventId) => eventId; // 必要なら EventData からタイトル取得に拡張

    void Toast(string title, string body)
    {
        if (!toastHud) return;
        if (!string.IsNullOrEmpty(title)) toastHud.ShowToast($"{title}：{body}");
        else toastHud.ShowToast(body);
    }

    public void Save()
    {
        // states
        var parts = new List<string>();
        foreach (var q in quests)
        {
            if (!q) continue;
            parts.Add($"{q.questId}:{(int)GetState(q.questId)}");
        }
        PlayerPrefs.SetString(KEY_QSTATE, string.Join(";", parts));

        // steps
        var parts2 = new List<string>();
        foreach (var q in quests)
        {
            if (!q) continue;
            parts2.Add($"{q.questId}:{GetCurrentStep(q.questId)}");
        }
        PlayerPrefs.SetString(KEY_QSTEP, string.Join(";", parts2));
        PlayerPrefs.Save();
    }

    public void Load()
    {
        _states.Clear();
        _stepIndex.Clear();

        var s1 = PlayerPrefs.GetString(KEY_QSTATE, "");
        if (!string.IsNullOrEmpty(s1))
        {
            foreach (var p in s1.Split(';'))
            {
                var kv = p.Split(':'); if (kv.Length != 2) continue;
                if (int.TryParse(kv[1], out int si))
                    _states[kv[0]] = (QuestState)si;
            }
        }

        var s2 = PlayerPrefs.GetString(KEY_QSTEP, "");
        if (!string.IsNullOrEmpty(s2))
        {
            foreach (var p in s2.Split(';'))
            {
                var kv = p.Split(':'); if (kv.Length != 2) continue;
                if (int.TryParse(kv[1], out int idx))
                    _stepIndex[kv[0]] = idx;
            }
        }
    }
}
