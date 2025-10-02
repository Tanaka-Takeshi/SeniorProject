// Assets/Scripts/Conversation/DialogueStartSelector.cs
using UnityEngine;

[DisallowMultipleComponent]
public class DialogueStartSelector : MonoBehaviour
{
    [System.Serializable]
    public class Rule
    {
        public string eventId;

        [Header("Pick when...")]
        public string whenInactive;   // 例: Talk_Offer
        public string whenActive;     // 例: Talk_Report
        public string whenCompleted;  // 例: (空) …次段へバトン

        public void Sanitize()
        {
            eventId = (eventId ?? "").Trim();
            whenInactive = (whenInactive ?? "").Trim();
            whenActive = (whenActive ?? "").Trim();
            whenCompleted = (whenCompleted ?? "").Trim();
        }
    }

    [Header("Top -> Bottom = prerequisite chain (E1 -> E2 -> E3)")]
    public Rule[] rules;

    void OnValidate()
    {
        if (rules == null) return;
        foreach (var r in rules) r?.Sanitize();
    }

    static string Safe(string s) => string.IsNullOrEmpty(s) ? "" : s.Trim();

    /// <summary>
    /// ルールを上から順に“前提チェーン”として評価。
    /// - ある段（E1 など）が Inactive/Active なら、そこで決着し**下の段は見ない**。
    /// - その段が Completed のときだけ**次の段**を評価に進む（バトン渡し）。
    /// </summary>
    public string ResolveId(string requestedId)
    {
        if (rules == null || rules.Length == 0)
        {
            Debug.Log($"[Selector] (NO RULE) -> pick='{requestedId}'");
            return requestedId;
        }

        string lastCompletedPick = null; // Completed 行用の候補（任意・ログ用）

        for (int i = 0; i < rules.Length; i++)
        {
            var r = rules[i];
            if (r == null) continue;
            r.Sanitize();

            if (string.IsNullOrEmpty(r.eventId))
            {
                Debug.LogWarning($"[Selector] Rule[{i}] skipped: eventId is empty.");
                continue;
            }

            var st = EventProgressService.Instance
                       ? EventProgressService.Instance.GetState(r.eventId)
                       : EventRunState.Inactive;

            string cand = st switch
            {
                EventRunState.Inactive => Safe(r.whenInactive),
                EventRunState.Active => Safe(r.whenActive),
                EventRunState.Completed => Safe(r.whenCompleted),
                _ => ""
            };

            // ログ（詳細）
            Debug.Log($"[Selector] npc={name} rule[{i}] event={r.eventId} state={st} " +
                      $"-> cand='{cand}' (req='{requestedId}')  " +
                      $"[Inactive='{r.whenInactive}', Active='{r.whenActive}', Completed='{r.whenCompleted}']");

            if (st == EventRunState.Completed)
            {
                // 完了済み：次段へバトン。Completed に会話を入れているなら lastCompletedPick として保持（任意）。
                if (!string.IsNullOrEmpty(cand)) lastCompletedPick = cand;
                // ここでは**決めず**、次のルールへ（= E2/E3 を評価）
                continue;
            }

            // ここに来るのは Inactive or Active
            // ⇒ “この段”が現在の前提。**ここで決めて終了**（下の段は見ない）
            if (!string.IsNullOrEmpty(cand))
            {
                Debug.Log($"[Selector] FINAL pick='{cand}'  (stopped at rule[{i}] {r.eventId})");
                return cand;
            }
            else
            {
                // 候補が空でも、前提段階が未完了なので下は見ない。requestedId で終了。
                Debug.Log($"[Selector] FINAL pick='{requestedId}' (no cand at rule[{i}] {r.eventId}, chain stop)");
                return requestedId;
            }
        }

        // すべて Completed だったケース：
        // 最後の Completed 候補があればそれを使う。なければ requestedId。
        if (!string.IsNullOrEmpty(lastCompletedPick))
        {
            Debug.Log($"[Selector] FINAL pick='{lastCompletedPick}' (all completed path)");
            return lastCompletedPick;
        }

        Debug.Log($"[Selector] FINAL pick='{requestedId}' (fallback)");
        return requestedId;
    }
}
