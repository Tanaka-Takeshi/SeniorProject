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

        // 見つからないときに手掛かりログ
#if UNITY_EDITOR
        System.Text.StringBuilder sb = new();
        sb.Append("[Quest] Find not found: ").Append(questId).Append(". Registered=[");
        bool first = true;
        foreach (var q in quests)
        {
            if (!q) continue;
            if (!first) sb.Append(", ");
            sb.Append(q.questId);
            first = false;
        }
        sb.Append("]");
        Debug.LogWarning(sb.ToString());
#endif
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
    /// 進行中クエストに対してイベント通知。
    /// 現在のステップが要求する eventId と一致し、かつ kind が Started/Custom のときだけ進行させる。
    /// それ以外は無視し、理由をログに出す（デバッグ用）。
    /// </summary>
    public void NotifyEventSignal(string eventId, ConversationSignalKind kind)
    {
        if (string.IsNullOrEmpty(eventId)) return;

        // 受け付けるシグナル種別を限定
        bool kindAcceptable = (kind == ConversationSignalKind.Started || kind == ConversationSignalKind.Custom);

        foreach (var q in quests)
        {
            if (!q) continue;

            // 対象は Active なクエストのみ
            if (GetState(q.questId) != QuestState.Active) continue;

            var step = GetCurrentStep(q.questId);
            var list = q.stepEventIds;

            // ステップ配列が空 / 範囲外ならスキップ
            if (list == null || list.Count == 0)
            {
                Debug.Log($"[Quest] IGNORED (no steps) event '{eventId}' for quest={q.questId}");
                continue;
            }
            if (step >= list.Count)
            {
                Debug.Log($"[Quest] IGNORED (already last) event '{eventId}' for quest={q.questId}");
                continue;
            }

            var requireId = list[step];
            if (string.IsNullOrEmpty(requireId))
            {
                Debug.Log($"[Quest] IGNORED (empty required id) event '{eventId}' quest={q.questId} step={step}");
                continue;
            }

            // 種別が許容外なら無視（理由を出す）
            if (!kindAcceptable)
            {
                Debug.Log($"[Quest] IGNORED (kind {kind}) event '{eventId}' quest={q.questId} expect='{requireId}' step={step}");
                continue;
            }

            // 順序チェック：いま要求しているIDと一致するときだけ進める
            if (eventId != requireId)
            {
                Debug.Log($"[Quest] IGNORED event '{eventId}' (expect '{requireId}') quest={q.questId} step={step}");
                continue;
            }

            // --- ここから進行処理 ---
            int nextStep = step + 1;
            _stepIndex[q.questId] = nextStep;

            Debug.Log($"[Quest] Step ✓ ({q.questId}) {requireId} -> step={nextStep}/{list.Count}");
            Toast(null, $"目標達成：{ReadableEvent(requireId)}");

            // 全ステップ達成で完了
            if (nextStep >= list.Count)
            {
                CompleteQuest(q.questId);
            }

            Save();
            // 同一 eventId を複数クエストが同時要求している可能性もあるので continue せずループ継続
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
