using System.Collections.Generic;
using UnityEngine;
using Game.Events;
using Game.Runtime;

/// <summary>
/// テスト用の簡易 Quest トラッカー（EventSignals を購読してクエスト進行を監視）
/// </summary>
public class TestQuestTracker : MonoBehaviour
{
    public string QuestId { get; private set; }
    public List<string> Steps { get; private set; } = new List<string>();
    public int CurrentStepIndex { get; private set; } = 0;

    public bool IsQuestCompleted { get; private set; }
    public bool IsQuestFailed { get; private set; }
    public string CurrentStepId => (CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count) ? Steps[CurrentStepIndex] : null;
    public List<string> CompletedSteps { get; private set; } = new List<string>();
    public string FailedStepId { get; private set; }

    private void OnEnable()
    {
        EventSignals.OnCompleted += OnEventCompleted;
        EventSignals.OnFailed += OnEventFailed;
    }

    private void OnDisable()
    {
        EventSignals.OnCompleted -= OnEventCompleted;
        EventSignals.OnFailed -= OnEventFailed;
    }

    /// <summary>
    /// テスト用にクエストをロード
    /// </summary>
    public void LoadQuest(string questId, IEnumerable<string> stepEventIds)
    {
        QuestId = questId;
        Steps = new List<string>(stepEventIds);
        CurrentStepIndex = 0;
        CompletedSteps.Clear();
        IsQuestCompleted = false;
        IsQuestFailed = false;
        FailedStepId = null;
    }

    private void OnEventCompleted(string eventId)
    {
        if (IsQuestCompleted || IsQuestFailed) return;

        if (CurrentStepId == null) return;

        // OR ステップ対応: "E2|E3" のような表記に対応
        bool match;
        if (CurrentStepId.Contains("|"))
        {
            var parts = CurrentStepId.Split('|');
            match = System.Array.Exists(parts, p => p.Trim() == eventId);
        }
        else
        {
            match = (CurrentStepId == eventId);
        }

        if (match)
        {
            CompletedSteps.Add(eventId);
            CurrentStepIndex++;
            if (CurrentStepIndex >= Steps.Count)
            {
                IsQuestCompleted = true;
            }
        }
    }

    private void OnEventFailed(string eventId, FailedReason reason)
    {
        if (IsQuestCompleted || IsQuestFailed) return;
        if (CurrentStepId == eventId)
        {
            IsQuestFailed = true;
            FailedStepId = eventId;
        }
    }
}
