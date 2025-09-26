// Assets/Scripts/Conversation/DialogueDatabase.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Conversation/Dialogue Database", fileName = "DialogueDatabase")]
public class DialogueDatabase : ScriptableObject
{
    public List<DialogueData> entries = new List<DialogueData>();

    private Dictionary<string, DialogueData> _map;

    void OnEnable() => BuildMap();

    public void BuildMap()
    {
        _map = new Dictionary<string, DialogueData>();
        foreach (var e in entries)
        {
            if (!e || string.IsNullOrEmpty(e.id)) continue;
            _map[e.id] = e; // 後勝ち
        }
    }

    public DialogueData Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_map == null || _map.Count != entries.Count) BuildMap();
        return _map.TryGetValue(id, out var d) ? d : null;
    }
}
