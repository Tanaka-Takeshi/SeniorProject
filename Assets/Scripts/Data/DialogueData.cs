using UnityEngine;

[CreateAssetMenu(menuName = "Game/Conversation/Dialogue Data", fileName = "DialogueData_")]
public class DialogueData : ScriptableObject
{
    [Header("Key")]
    public string id;

    [Header("Display")]
    public string speakerName = "NPC";

    [TextArea(2, 5)]
    public string[] lines;

    [Header("Choices (optional)")]
    public DialogueChoice[] choices;
}

[System.Serializable]
public class DialogueChoice
{
    public string text;
    public string nextId;
}
