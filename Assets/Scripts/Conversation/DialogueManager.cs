// Assets/Scripts/Conversation/DialogueManager.cs
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Game.Conversation;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Data")]
    public DialogueDatabase database;      // ← Inspectorで割り当て

    [Header("Input")]
    [Tooltip("会話開始直後の入力ブロック時間(秒)")]
    public float inputBlockSec = 0.2f;

    private DialogueData current;
    private int index = 0;
    private float timeSinceStart = 0f;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartFromNpc(Transform npc, string id)
    {
        // DBから検索（無ければフォールバック）
        current = database ? database.Find(id) : null;

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
                // ★ ここから：選択肢があれば表示、なければ終了
                if (current != null && current.choices != null && current.choices.Length > 0)
                {
                    // 進行入力を一旦無効化（誤爆防止）
                    timeSinceStart = 0f;

                    cc.ShowChoices(current.choices, OnChoiceSelected);
                }
                else
                {
                    cc.EndConversation();
                }
            }
        }
    }

    void OnChoiceSelected(int idx)
    {
        var cc = ConversationController.Instance;
        cc.HideChoices();

        var choice = current.choices[idx];
        if (database == null || string.IsNullOrEmpty(choice.nextId))
        {
            cc.EndConversation();
            return;
        }

        var next = database.Find(choice.nextId);
        if (next == null || next.lines == null || next.lines.Length == 0)
        {
            cc.EndConversation();
            return;
        }

        // 次会話へ遷移
        current = next;
        index = 0;
        timeSinceStart = 0f;
        cc.UpdateTitle(string.IsNullOrEmpty(current.speakerName) ? "NPC" : current.speakerName);
        cc.ShowNextLine(current.lines[index]);
    }
}
