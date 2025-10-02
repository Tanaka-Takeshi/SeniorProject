// Assets/Scripts/Conversation/EventSignalRouter.cs
using UnityEngine;

public enum ConversationSignalKind { None, Started, Completed, Custom }

public class EventSignalRouter : MonoBehaviour
{
    // 既存の Raise エントリポイント
    public static void Raise(string id, ConversationSignalKind kind)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning("[Router] id is null/empty");
            return;
        }

        // Normalize
        id = id.Trim();

        // ==== Progress ====
        if (id.StartsWith("Event.Progress", System.StringComparison.OrdinalIgnoreCase))
        {
            if (TryParseProgress(id, out var eventId, out var delta))
            {
                Debug.Log($"[Router] Progress -> event='{eventId}' delta={delta}");
                var svc = EventProgressService.Instance;
                if (svc == null)
                {
                    Debug.LogWarning("[Router] EventProgressService.Instance is null");
                    return;
                }
                svc.AddProgress(eventId, delta);
            }
            else
            {
                Debug.LogWarning($"[Router] Progress parse failed: '{id}'\n"
                               + "  Accepted examples:\n"
                               + "   - Event.Progress:Event.E2:+1\n"
                               + "   - Event.Progress Event.E2 +0.5\n"
                               + "   - Event.Progress, Event.E2, 1");
            }
            return;
        }

        // ==== Start ====
        if (id.StartsWith("Event.Start:", System.StringComparison.OrdinalIgnoreCase))
        {
            var eventId = id.Substring("Event.Start:".Length).Trim();
            if (eventId.StartsWith(":")) eventId = eventId.Substring(1).Trim();
            Debug.Log($"[Router] id='{id}' kind={kind}");
            EventProgressService.Instance?.StartEvent(eventId);
            return;
        }

        // ==== Complete ====
        if (id.StartsWith("Event.Complete:", System.StringComparison.OrdinalIgnoreCase))
        {
            var eventId = id.Substring("Event.Complete:".Length).Trim();
            if (eventId.StartsWith(":")) eventId = eventId.Substring(1).Trim();
            Debug.Log($"[Router] id='{id}' kind={kind}");
            EventProgressService.Instance?.CompleteEvent(eventId);
            return;
        }

        // ここに他のカスタムシグナルがあれば既存の処理を残す
        Debug.Log($"[Router] passthrough id='{id}' kind={kind}");
    }

    // ------------------------------------------------------------
    // Progress 文字列を柔軟に解析する
    // 例:
    //   Event.Progress:Event.E2:+1
    //   Event.Progress Event.E2 +0.5
    //   Event.Progress, Event.E2, 1
    //   Event.Progress:   Event.E2   :   -0.25
    // ------------------------------------------------------------
    private static bool TryParseProgress(string id, out string eventId, out float delta)
    {
        eventId = null;
        delta = 0f;

        // プレフィックス除去
        var work = id.Substring("Event.Progress".Length);

        // 区切りを統一（, と : と連続空白 → 空白1個）
        work = work.Replace(",", " ").Replace(":", " ");
        // 余分な空白を畳む
        work = System.Text.RegularExpressions.Regex.Replace(work, @"\s+", " ").Trim();

        // 先頭が空なら失敗
        if (string.IsNullOrEmpty(work)) return false;

        // 分割
        var tokens = work.Split(' ');
        if (tokens.Length < 2)
        {
            // イベントIDと数値の両方が必要
            return false;
        }

        // 最初のトークンを eventId、最後のトークンを delta とみなす
        // （中間トークンが入っても最後のものを delta として扱う）
        eventId = tokens[0].Trim();

        // delta の候補を末尾から探す（+1, 1, -0.25 など）
        for (int i = tokens.Length - 1; i >= 1; i--)
        {
            var t = tokens[i].Trim();
            if (float.TryParse(t, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out delta))
            {
                return !string.IsNullOrEmpty(eventId);
            }

            // 先頭に+/-があるが数値としてはじかれるロケール問題を吸収
            if ((t.StartsWith("+") || t.StartsWith("-")) &&
                float.TryParse(t.Substring(1), System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out var core))
            {
                delta = t.StartsWith("-") ? -core : core;
                return !string.IsNullOrEmpty(eventId);
            }
        }

        return false;
    }
}
