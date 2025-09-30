// Assets/Scripts/Runtime/Conversation/EventSignalRouter.cs
using UnityEngine;

public static class EventSignalRouter
{
    // あなたの EventSignals へ橋渡し
    public static void Raise(string id, ConversationSignalKind kind)
    {
        if (string.IsNullOrEmpty(id)) return;

        switch (kind)
        {
            case ConversationSignalKind.Scheduled:
                Game.Events.EventSignals.RaiseScheduled(id);
                break;
            case ConversationSignalKind.Available:
                Game.Events.EventSignals.RaiseAvailable(id);
                break;
            case ConversationSignalKind.Started:
                Game.Events.EventSignals.RaiseStarted(id);
                break;
            case ConversationSignalKind.Custom:
                // 例：プロジェクト独自の通知に振り分けたい場合にここで分岐
                // Game.Events.EventSignals.RaiseCustom(id);
                Game.Events.EventSignals.RaiseStarted(id); // 暫定的に Started に寄せるなど
                break;
            case ConversationSignalKind.None:
            default:
                break;
        }

        QuestService.Instance?.NotifyEventSignal(id, kind);
    }
}
