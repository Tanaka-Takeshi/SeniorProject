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
    [HideInInspector] public bool isTriggered;
    // 終了フラグ
    [HideInInspector] public bool isEnded;


    // イベントステータス
    [HideInInspector] public EventStatus status;
    // 開始種別
    [HideInInspector] public StartType startType;
    // 開始オブジェクト
    [HideInInspector] public GameObject startObject;
    // 開始時間
    [HideInInspector] public int startDay;
    [HideInInspector] public TimeManager.TimeOfDay startTimeOfDay;
    // 終了時間
    [HideInInspector] public int endDay;
    [HideInInspector] public TimeSpan endTime;

    public void InitializeEvent()
    {
        // 開始オブジェクト設定
        {
            if(string.IsNullOrEmpty(startObjectName))
            {
                Debug.LogWarning($"[{nameof(EventEntry)}] startObjectName が空です。");
            }
            else
            {
                // シーン上から名前で検索（最初耳使ったものを返す）
                startObject = GameObject.Find(startObjectName);

                if(startObject == null)
                {
                    Debug.LogError($"[{nameof(EventEntry)}] シーンに '{startObjectName}' という名前の GameObject が見つかりません。");
                }
            }
        }

        // 開始時間設定
        {
            // 入力チェック
            if (string.IsNullOrEmpty(startTiming))
            {
                Debug.LogWarning($"[{nameof(EventEntry)}] startTimingが空です。");
                return;
            }

            // “:” で分割
            var parts = startTiming.Split(':');
            if (parts.Length != 2)
            {
                Debug.LogError($"[{nameof(EventEntry)} startTimingの書式エラー: {startTiming}");
                return;
            }

            // 日数部分をintに変換
            if (!int.TryParse(parts[0], out startDay))
            {
                Debug.LogError($"[{nameof(EventEntry)}] startDay に変換失敗: {parts[0]}");
            }

            // 時間帯部分をenumに変換
            if (!Enum.TryParse<TimeManager.TimeOfDay>(parts[1], out startTimeOfDay))
            {
                Debug.LogError($"[{nameof(EventEntry)}] startTimeOrDay に変換失敗: {parts[1]}");
            }
        }

        // 終了時間設定
        {
            if(string.IsNullOrEmpty(endTiming))
            {
                Debug.LogWarning($"[{nameof(EventEntry)}] endTiming が空です。");
            }
            else
            {
                // 最初の “:”で分割
                int idx = endTiming.IndexOf(':');
                if (idx > 0)
                {
                    // 日付と時間
                    string dayPart = endTiming.Substring(0, idx);
                    string timePart = endTiming.Substring(idx + 1);

                    // dayPartをendDayに
                    if(!int.TryParse(dayPart, out endDay))
                    {
                        Debug.LogError($"[{nameof(EventEntry)}] endDay 変換失敗: {dayPart}");
                    }

                    // timePartをendTimeに
                    if(!TimeSpan.TryParse(timePart, out endTime))
                    {
                        Debug.LogError($"[{nameof(EventEntry)}] endTime 変換失敗: {timePart}");
                    }
                }
                else
                {
                    Debug.LogError($"[{nameof(EventEntry)}] endTiming の書式エラー: {endTiming}");
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
