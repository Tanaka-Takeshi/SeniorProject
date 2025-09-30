// Assets/Scripts/Conversation/DialogueData.cs
using UnityEngine;

[System.Serializable]
public class FlagOp
{
    public enum Op { Set, Clear, Toggle }
    public Op operation = Op.Set;
    public GameFlag key;
}

[System.Serializable]
public class FlagCondition
{
    public GameFlag key;
    public bool mustBeTrue = true;
}

public enum ConversationSignalKind
{
    None, Scheduled, Available, Started, Custom
}

[System.Serializable]
public class DialogueChoice
{
    public string text;
    public string nextId;

    [Header("On Choose: Flag Ops (optional)")]
    public FlagOp[] flagOps;

    [Header("On Choose: Signal/Event (optional)")]
    public string eventSignalId;
    public ConversationSignalKind signalKind = ConversationSignalKind.None;

    [Header("Conditions (optional)")]
    public FlagCondition[] conditions;
}

// ★ 追加：choices が無い時に自動で分岐するルール（上から優先）
[System.Serializable]
public class ConditionalRoute
{
    public FlagCondition[] conditions;   // すべて満たしたらヒット
    public string nextId;                // 遷移先ID（空なら何もしない）
}

[CreateAssetMenu(menuName = "Game/Conversation/Dialogue Data", fileName = "DialogueData_")]
public class DialogueData : ScriptableObject
{
    public string id;
    public string speakerName = "NPC";
    [TextArea(2, 5)] public string[] lines;

    [Header("Choices (optional)")]
    public DialogueChoice[] choices;

    [Header("Auto Routes (optional)")]
    public ConditionalRoute[] autoRoutes; // ★ ここに条件分岐を列挙（最初にマッチしたものへ）

    [Header("Seen Flag (optional)")]
    public GameFlag markSeenFlag = GameFlag.None;  // この会話を一度見たら立てるフラグ
}
