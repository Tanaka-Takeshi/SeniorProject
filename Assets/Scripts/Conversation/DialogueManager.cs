// Assets/Scripts/Conversation/DialogueManager.cs
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Game.Conversation;
using System.Collections.Generic;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Data")]
    public DialogueDatabase database;

    [Header("Input")]
    [Tooltip("会話開始直後の入力ブロック時間(秒)")]
    public float inputBlockSec = 0.2f;

    [Header("Routing (IDs)")]
    [Tooltip("完了後に出す会話ID（例: Talk_Done）")]
    public string doneId = "Talk_Done";

    private DialogueData current;
    private int index = 0;
    private float timeSinceStart = 0f;
    private Transform currentNpc;

    // 可視化した選択肢リスト（インデックスマッピング用）
    private readonly List<DialogueChoice> visibleChoices = new();

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartFromNpc(Transform npc, string id)
    {
        currentNpc = npc;

        // ★ NPC側のセレクタで開始IDを解決
        var selector = npc ? npc.GetComponentInParent<DialogueStartSelector>() : null;
        var startId = selector ? selector.ResolveId(id) : id;

        Debug.Log($"[DM] RequestedId={id}  ResolvedId={startId}  Selector={(selector ? "YES" : "NO")}  NPC={npc?.name}");

        current = database ? database.Find(startId) : null;
        Debug.Log($"[DM] Database.Find(\"{startId}\") -> {(current ? "HIT" : "MISS")}");

        index = 0;
        timeSinceStart = 0f;

        if (current == null || current.lines == null || current.lines.Length == 0)
        {
            ConversationController.Instance.StartConversation(npc, "NPC", "……");
            return;
        }

        ConversationController.Instance.StartConversation(
            npc,
            string.IsNullOrEmpty(current.speakerName) ? "NPC" : current.speakerName,
            current.lines[index]
        );
    }

    void Update()
    {
        var cc = ConversationController.Instance;
        if (cc == null || !cc.IsInConversation) return;

        // 選択肢表示中はセリフ送りを無効化
        if (cc.choiceUI != null && cc.choiceUI.IsOpen) return;

        timeSinceStart += Time.deltaTime;

        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null) pressed = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
#else
        pressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif
        if (timeSinceStart < inputBlockSec) return;

        if (pressed)
        {
            index++;
            if (current != null && current.lines != null && index < current.lines.Length)
            {
                cc.ShowNextLine(current.lines[index]);
            }
            else
            {
                // === 最後に到達：自動ルート or 選択肢 or 終了 ===
                if (current != null)
                {
                    // AutoRoute（任意）: 条件を満たす first nextId へ自動遷移
                    if (TryAutoRoute(current, out var nextId) && !string.IsNullOrEmpty(nextId))
                    {
                        var next = database ? database.Find(nextId) : null;
                        if (next != null && next.lines != null && next.lines.Length > 0)
                        {
                            current = next;
                            index = 0;
                            timeSinceStart = 0f;
                            cc.UpdateTitle(string.IsNullOrEmpty(current.speakerName) ? "NPC" : current.speakerName);
                            cc.ShowNextLine(current.lines[index]);
                            return;
                        }
                    }

                    // 選択肢
                    if (current.choices != null && current.choices.Length > 0)
                    {
                        ShowFilteredChoices(current.choices);
                        return;
                    }
                }
                cc.EndConversation();
            }
        }
    }

    // --- 選択肢を条件付きで表示 ---
    void ShowFilteredChoices(DialogueChoice[] allChoices)
    {
        visibleChoices.Clear();
        foreach (var c in allChoices)
        {
            if (PassConditions(c)) visibleChoices.Add(c);
        }

        if (visibleChoices.Count == 0)
        {
            ConversationController.Instance.EndConversation();
            return;
        }

        timeSinceStart = 0f;

        ConversationController.Instance.ShowChoices(
            visibleChoices.ToArray(),
            OnChoiceSelected
        );
    }

    // --- AutoRoute（DialogueData 側の自動分岐） ---
    static bool TryAutoRoute(DialogueData data, out string nextId)
    {
        nextId = null;
        if (data == null || data.autoRoutes == null || data.autoRoutes.Length == 0)
            return false;

        foreach (var r in data.autoRoutes)
        {
            if (r == null || string.IsNullOrEmpty(r.nextId)) continue;
            if (PassConditions(r.conditions)) { nextId = r.nextId; return true; }
        }
        return false;
    }

    // --- 条件判定（配列版） ---
    static bool PassConditions(FlagCondition[] conds)
    {
        if (conds == null || conds.Length == 0) return true;
        foreach (var cond in conds)
        {
            if (cond == null || cond.key == GameFlag.None) continue;
            bool has = FlagService.Has(cond.key);
            if (cond.mustBeTrue && !has) return false;
            if (!cond.mustBeTrue && has) return false;
        }
        return true;
    }

    // --- 条件判定（選択肢1件） ---
    static bool PassConditions(DialogueChoice c)
    {
        if (c.conditions == null || c.conditions.Length == 0) return true;

        foreach (var cond in c.conditions)
        {
            if (cond == null || cond.key == GameFlag.None) continue;

            bool has = FlagService.Has(cond.key);
            if (cond.mustBeTrue && !has) return false;
            if (!cond.mustBeTrue && has) return false;
        }
        return true;
    }

    // --- 選択肢が押されたとき ---
    void OnChoiceSelected(int visibleIndex)
    {
        var cc = ConversationController.Instance;
        cc.HideChoices();

        if (visibleIndex < 0 || visibleIndex >= visibleChoices.Count)
        {
            cc.EndConversation();
            return;
        }

        var choice = visibleChoices[visibleIndex];

        // 1) フラグ操作
        if (choice.flagOps != null)
        {
            foreach (var op in choice.flagOps)
            {
                if (op == null || op.key == GameFlag.None) continue;

                switch (op.operation)
                {
                    case FlagOp.Op.Set: FlagService.Set(op.key); break;
                    case FlagOp.Op.Clear: FlagService.Clear(op.key); break;
                    case FlagOp.Op.Toggle: FlagService.Toggle(op.key); break;
                }
            }
            FlagService.Save();
        }

        // 2) イベント発火（任意） + Hotfix（Start直接呼び）
        if (!string.IsNullOrEmpty(choice.eventSignalId))
        {
            EventSignalRouter.Raise(choice.eventSignalId, choice.signalKind);

            // --- 取りこぼし保険：Event.Start:<id> は StartEvent を直呼び ---
            if (TryParseEventIdFromSignal(choice.eventSignalId, "Event.Start:", out var startEid))
            {
                EventProgressService.Instance?.StartEvent(startEid);
                var stNow = EventProgressService.Instance ? EventProgressService.Instance.GetState(startEid) : EventRunState.Inactive;
                Debug.Log($"[Probe] (DirectStart) {startEid} state now = {stNow}");
            }
        }

        // 2.5) ★Progress直後に「完了ならその場で Talk_Done へ即切替」
        if (!string.IsNullOrEmpty(choice.eventSignalId)
            && TryParseEventIdFromSignal(choice.eventSignalId, "Event.Progress:", out var progEid))
        {
            var eps = EventProgressService.Instance;
            if (eps != null)
            {
                var st = eps.GetState(progEid);
                var pr = eps.GetProgress(progEid);

                // Completed または progress>=1.0 なら、この会話中に即 Done に切り替え
                if (st == EventRunState.Completed || (st == EventRunState.Active && pr >= 1f))
                {
                    if (!string.IsNullOrEmpty(doneId) && database != null)
                    {
                        var done = database.Find(doneId);
                        if (done != null && done.lines != null && done.lines.Length > 0)
                        {
                            current = done;
                            index = 0;
                            timeSinceStart = 0f;

                            cc.UpdateTitle(string.IsNullOrEmpty(current.speakerName) ? "NPC" : current.speakerName);
                            cc.ShowNextLine(current.lines[index]);
                            return; // ★ ここで会話継続（終了しない）
                        }
                    }
                }
            }
        }

        // 3) Seen マーク（任意）
        MarkSeen(current);

        // 4) 通常の nextId ルート（完了していない場合はこちらに来る）
        if (database != null && !string.IsNullOrEmpty(choice.nextId))
        {
            var next = database.Find(choice.nextId);
            if (next != null && next.lines != null && next.lines.Length > 0)
            {
                current = next;
                index = 0;
                timeSinceStart = 0f;

                cc.UpdateTitle(string.IsNullOrEmpty(current.speakerName) ? "NPC" : current.speakerName);
                cc.ShowNextLine(current.lines[index]);
                return;
            }
        }

        cc.EndConversation();
    }

    // "Event.Start:<id>" / "Event.Progress:<id>:<delta>" から <id> を抜く
    static bool TryParseEventIdFromSignal(string signal, string prefix, out string eventId)
    {
        eventId = null;
        if (string.IsNullOrEmpty(signal) || string.IsNullOrEmpty(prefix)) return false;
        if (!signal.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return false;

        var tail = signal.Substring(prefix.Length).Trim();
        if (tail.Length > 0 && tail[0] == ':') tail = tail.Substring(1).Trim();

        // Progress は "Event.Progress:Event.E1:+0.5" のように : 区切りが続く
        var parts = tail.Split(':');
        if (parts.Length >= 1 && !string.IsNullOrEmpty(parts[0]))
        {
            eventId = parts[0].Trim();
            return true;
        }
        return false;
    }

    void MarkSeen(DialogueData data)
    {
        if (data == null) return;
        if (data.markSeenFlag == GameFlag.None) return;

        if (!FlagService.Has(data.markSeenFlag))
        {
            FlagService.Set(data.markSeenFlag);
            FlagService.Save();
        }
    }
}
