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
                // === 最後に到達：選択肢 or 終了 ===
                if (current != null && current.choices != null && current.choices.Length > 0)
                {
                    ShowFilteredChoices(current.choices);
                }
                else
                {
                    // ★ 1) autoRoutes があれば評価して自動遷移
                    if (TryAutoRoute(current, out var nextIdAuto))
                    {
                        var next = database?.Find(nextIdAuto);
                        if (next != null && next.lines != null && next.lines.Length > 0)
                        {
                            // 今の会話を既読にしてから次へ
                            MarkSeen(current);

                            current = next;
                            index = 0;
                            timeSinceStart = 0f;

                            cc.UpdateTitle(string.IsNullOrEmpty(current.speakerName) ? "NPC" : current.speakerName);
                            cc.ShowNextLine(current.lines[index]);
                            return;
                        }
                        else
                        {
                            Debug.LogWarning($"[DM] AutoRoute nextId '{nextIdAuto}' not found or has no lines.");
                        }
                    }

                    // ★ 2) 自動遷移できなければ既読にして終了
                    MarkSeen(current);
                    cc.EndConversation();
                }
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

        // ConversationController.ShowChoices(DialogueChoice[], Action<int>) を呼ぶ
        ConversationController.Instance.ShowChoices(
            visibleChoices.ToArray(),
            OnChoiceSelected
        );
    }

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


    // --- 条件判定（全て満たす必要あり） ---
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

    static bool PassConditions(DialogueChoice c)
    {
        if (c.conditions == null || c.conditions.Length == 0) return true;

        foreach (var cond in c.conditions)
        {
            // ★ enum 版：GameFlag.None は無視。その他は FlagService.Has で判定
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

        // 1) フラグ操作（enum 版）
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

        // 2) イベント発火（任意）
        if (!string.IsNullOrEmpty(choice.eventSignalId) &&
            choice.signalKind != ConversationSignalKind.None)
        {
            EventSignalRouter.Raise(choice.eventSignalId, choice.signalKind);
        }

        MarkSeen(current);

        // 3) 次の会話へ or 終了
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

    void MarkSeen(DialogueData data)
    {
        if (data == null) return;
        if (data.markSeenFlag == GameFlag.None) return;

        if (!FlagService.Has(data.markSeenFlag))
        {
            FlagService.Set(data.markSeenFlag);
            FlagService.Save();
            // Debug.Log($"[DM] MarkSeen: {data.id} -> {data.markSeenFlag}");
        }
    }


}
