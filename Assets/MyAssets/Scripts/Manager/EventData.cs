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

    private void OnEnable()
    {
        if (events == null) return;

        foreach ( var entry in events)
        {
            entry.InitializeEvent();
        }
    }
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
    [HideInInspector] public bool isTriggered;
    // �I���t���O
    [HideInInspector] public bool isEnded;


    // �C�x���g�X�e�[�^�X
    [HideInInspector] public EventStatus status;
    // �J�n���
    [HideInInspector] public StartType startType;
    // �J�n�I�u�W�F�N�g
    [HideInInspector] public GameObject startObject;
    // �J�n����
    [HideInInspector] public int startDay;
    [HideInInspector] public TimeManager.TimeOfDay startTimeOfDay;
    // �I������
    [HideInInspector] public int endDay;
    [HideInInspector] public TimeSpan endTime;

    public void InitializeEvent()
    {
        // �J�n�I�u�W�F�N�g�ݒ�
        {
            if(string.IsNullOrEmpty(startObjectName))
            {
                Debug.LogWarning($"[{nameof(EventEntry)}] startObjectName ����ł��B");
            }
            else
            {
                // �V�[���ォ�疼�O�Ō����i�ŏ����g�������̂�Ԃ��j
                startObject = GameObject.Find(startObjectName);

                if(startObject == null)
                {
                    Debug.LogError($"[{nameof(EventEntry)}] �V�[���� '{startObjectName}' �Ƃ������O�� GameObject ��������܂���B");
                }
            }
        }

        // �J�n���Ԑݒ�
        {
            // ���̓`�F�b�N
            if (string.IsNullOrEmpty(startTiming))
            {
                Debug.LogWarning($"[{nameof(EventEntry)}] startTiming����ł��B");
                return;
            }

            // �g:�h �ŕ���
            var parts = startTiming.Split(':');
            if (parts.Length != 2)
            {
                Debug.LogError($"[{nameof(EventEntry)} startTiming�̏����G���[: {startTiming}");
                return;
            }

            // ����������int�ɕϊ�
            if (!int.TryParse(parts[0], out startDay))
            {
                Debug.LogError($"[{nameof(EventEntry)}] startDay �ɕϊ����s: {parts[0]}");
            }

            // ���ԑѕ�����enum�ɕϊ�
            if (!Enum.TryParse<TimeManager.TimeOfDay>(parts[1], out startTimeOfDay))
            {
                Debug.LogError($"[{nameof(EventEntry)}] startTimeOrDay �ɕϊ����s: {parts[1]}");
            }
        }

        // �I�����Ԑݒ�
        {
            if(string.IsNullOrEmpty(endTiming))
            {
                Debug.LogWarning($"[{nameof(EventEntry)}] endTiming ����ł��B");
            }
            else
            {
                // �ŏ��� �g:�h�ŕ���
                int idx = endTiming.IndexOf(':');
                if (idx > 0)
                {
                    // ���t�Ǝ���
                    string dayPart = endTiming.Substring(0, idx);
                    string timePart = endTiming.Substring(idx + 1);

                    // dayPart��endDay��
                    if(!int.TryParse(dayPart, out endDay))
                    {
                        Debug.LogError($"[{nameof(EventEntry)}] endDay �ϊ����s: {dayPart}");
                    }

                    // timePart��endTime��
                    if(!TimeSpan.TryParse(timePart, out endTime))
                    {
                        Debug.LogError($"[{nameof(EventEntry)}] endTime �ϊ����s: {timePart}");
                    }
                }
                else
                {
                    Debug.LogError($"[{nameof(EventEntry)}] endTiming �̏����G���[: {endTiming}");
                }
            }
        }

        Debug.Log(
        $"[EventEntry] No:{eventNo}  " +
        $"startObject={(startObject != null ? startObject.name : "null")}  " +
        $"startDay={startDay}  " +
        $"startTimeOfDay={startTimeOfDay}  " +
        $"endDay={endDay}  " +
        $"endTime={endTime}"
    );
    }

}
