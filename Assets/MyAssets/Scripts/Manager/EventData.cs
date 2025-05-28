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
    // イベント番号
    public int eventNo;

    // 開始場所
    public string triggerZoneName;
    // 開始オブジェクト
    public string startObjectName;
    // 開始時間
    public string startTiming;
    // 終了時間
    public string endTiming;

    // 開始フラグ
    public bool isTriggered;
    // 終了フラグ
    public bool isEnded;


    // イベントステータス
    private EventStatus status;
    // 開始種別
    private StartType startType;
    // 開始オブジェクト
    private GameObject startObject;
    // 開始時間
    private int startDay;
    private TimeManager.TimeOfDay startTimeOrDay;
    // 終了時間
    private int endDay;
    private TimeSpan endTime;

}
