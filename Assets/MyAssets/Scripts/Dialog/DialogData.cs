using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DialogLine
{
    [Header("�b�Җ�")]
    public string speakerName;

    [Header("�Z���t")]
    public string text;
}

[CreateAssetMenu(fileName = "DialogData", menuName = "Scriptable Objects/DialogData")]
public class DialogData : ScriptableObject
{
    [Header("�V�i���INo.")]
    public int scenarioNum;

    [Header("�Z���t���X�g")]
    public List<DialogLine> textList = new List<DialogLine>();
}
