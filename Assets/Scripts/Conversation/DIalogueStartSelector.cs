// Assets/Scripts/Conversation/DialogueStartSelector.cs
using UnityEngine;

public class DialogueStartSelector : MonoBehaviour
{
    [System.Serializable]
    public class StartRule
    {
        public FlagCondition[] conditions;
        public string id;  // この条件を満たしたらこのIDを返す
    }

    [Header("From top to bottom, first match wins")]
    public StartRule[] rules;

    [Header("Fallback when none matched")]
    public string fallbackId;  // 何も当たらなければこれ

    public string ResolveId(string suggestedIdFromNPC)
    {
        if (rules != null)
        {
            for (int i = 0; i < rules.Length; i++)
            {
                var r = rules[i];
                if (PassConditions(r.conditions))
                {
                    Debug.Log($"[Selector] rule#{i} matched -> id={r.id}");
                    return r.id;
                }
                else
                {
                    Debug.Log($"[Selector] rule#{i} not matched");
                }
            }
        }
        Debug.Log($"[Selector] fallback -> {(!string.IsNullOrEmpty(fallbackId) ? fallbackId : suggestedIdFromNPC)}");
        return !string.IsNullOrEmpty(fallbackId) ? fallbackId : suggestedIdFromNPC;
    }


    static bool PassConditions(FlagCondition[] conds)
    {
        if (conds == null || conds.Length == 0) return true;
        foreach (var c in conds)
        {
            if (c == null || c.key == GameFlag.None) continue;
            bool has = FlagService.Has(c.key);
            if (c.mustBeTrue && !has) return false;
            if (!c.mustBeTrue && has) return false;
        }
        return true;
    }
}
