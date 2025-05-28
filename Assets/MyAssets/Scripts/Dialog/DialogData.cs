using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DialogLine
{
    [Header("話者名")]
    public string speakerName;

    [Header("セリフ")]
    public string text;
}

[CreateAssetMenu(fileName = "DialogData", menuName = "Scriptable Objects/DialogData")]
public class DialogData : ScriptableObject
{
    [Header("シナリオNo.")]
    public int scenarioNum;

    [Header("セリフリスト")]
    public List<DialogLine> textList = new List<DialogLine>();
}
