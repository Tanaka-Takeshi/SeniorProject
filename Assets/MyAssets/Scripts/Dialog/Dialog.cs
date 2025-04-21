using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Dialog
{
    public string npcName;
    [TextArea(2, 5)]
    public List<string> sentences;
}
