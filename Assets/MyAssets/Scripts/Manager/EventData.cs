using UnityEngine;
using System;

public enum EventStatus
{
    None,
    WaitingToStart,
    Progress,
    Progress1,
    Progress2,
    Progress3,
    Pertial1,
    Pertial2, 
    Completed,
}

public enum StartType
{
    None,
    NewGame,
    Time,
    Place,
    PlaceAndTime,
    Flag,

}

[CreateAssetMenu(fileName = "EventData", menuName = "Scriptable Objects/EventData")]
public class EventData : ScriptableObject
{
    public EventEntry[] events; 
}

[Serializable]
public class EventEntry
{
    // �C�x���g�ԍ�
    public int eventNo;

    // �J�n�ꏊ
    public string triggerZoneName;
    // �J�n�I�u�W�F�N�g
    public string startObjectName;
    // �J�n����
    public string startTiming;
    // �I������
    public string endTiming;

    // �J�n�t���O
    public bool isTriggered;
    // �I���t���O
    public bool isEnded;


    // �C�x���g�X�e�[�^�X
    private EventStatus status;
    // �J�n���
    private StartType startType;
    // �J�n�I�u�W�F�N�g
    private GameObject startObject;
    // �J�n����
    private int startDay;
    private TimeManager.TimeOfDay startTimeOrDay;
    // �I������
    private int endDay;
    private TimeSpan endTime;

}
