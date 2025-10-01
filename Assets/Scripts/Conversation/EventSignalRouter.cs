// Assets/Scripts/Runtime/Conversation/EventSignalRouter.cs
using UnityEngine;

public static class EventSignalRouter
{
    public static void Raise(string id, ConversationSignalKind kind)
    {
        Debug.Log($"[Router] Raise id='{id}' kind={kind}");

        if (string.IsNullOrEmpty(id)) return;

        const string QS = "Quest.Start:";
        if (id.StartsWith(QS, System.StringComparison.OrdinalIgnoreCase))
        {
            var qid = id.Substring(QS.Length).Trim(); // 前後空白除去
            if (qid.Length > 0 && qid[0] == ':') qid = qid.Substring(1).Trim(); // 誤って「::」になっていた場合の救済
            if (string.IsNullOrEmpty(qid))
            {
                Debug.LogWarning("[Router] Quest.Start:* のクエストIDが空です");
                return;
            }
            QuestService.Instance?.StartQuest(qid);
            return; // クエスト開始だけして終了
        }

        // 既存のゲーム内イベント通知（必要に応じて）
        switch (kind)
        {
            case ConversationSignalKind.Scheduled: Game.Events.EventSignals.RaiseScheduled(id); break;
            case ConversationSignalKind.Available: Game.Events.EventSignals.RaiseAvailable(id); break;
            case ConversationSignalKind.Started: Game.Events.EventSignals.RaiseStarted(id); break;
            case ConversationSignalKind.Custom: Game.Events.EventSignals.RaiseStarted(id); break;
        }

        QuestService.Instance?.NotifyEventSignal(id, kind);
    }
}
