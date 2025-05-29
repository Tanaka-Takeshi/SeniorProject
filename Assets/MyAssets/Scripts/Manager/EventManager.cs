using UnityEngine;
using System.Collections.Generic;
using TMPro.Examples;

public class EventManager : MonoBehaviour
{
    [Header("イベント設定")]
    [SerializeField] private EventData eventData;
    [SerializeField] private GameObject startIndicator;
    [SerializeField] private float indicatorOffsetY = 3f;

    // エントリと生成中インジケーターの紐付け用
    private readonly Dictionary<EventEntry, GameObject> indicators
        = new Dictionary<EventEntry, GameObject>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // EventDataの初期化を再実行
        foreach(var entry in eventData.events)
        {
            entry.InitializeEvent();
        }
    }

    // Update is called once per frame
    void Update()
    {
        // 現在のゲーム内日数（開始からの経過日数）及び時間帯を取得
        int currentDay = TimeManager.Instance.DayCount;
        TimeManager.TimeOfDay currentTod = TimeManager.Instance.CurrentTimeOfDay;

        foreach(var entry in eventData.events)
        {
            // 開始日と時間帯が一致しているか
            bool isMatching = entry.startDay == currentDay
                && entry.startTimeOfDay == currentTod;

            if( isMatching )
            {
                // ステータス更新
                if(entry.status != EventStatus.WaitingToStart)
                    entry.status = EventStatus.WaitingToStart;

                // インジケーター生成
                if(!indicators.ContainsKey(entry) && entry.startObject  != null)
                {
                    Vector3 spawnPos = entry.startObject.transform.position
                        + Vector3.up * indicatorOffsetY;

                    var inst = Instantiate(startIndicator, spawnPos,
                        Quaternion.identity, entry.startObject.transform);

                    //// 1) ワールド位置で生成
                    //var inst = Instantiate(startIndicator, spawnPos, Quaternion.identity);
                    //// 2) 世界位置を維持しつつ親子付け
                    //inst.transform.SetParent(entry.startObject.transform, true);

                    indicators.Add(entry, inst);

                    Debug.Log($"[EventManager] Indicator instantiated for Event {entry.eventNo}");

                }
            }
            else if(indicators.ContainsKey(entry))
            {
                // すでに表示中なら毎フレームログ
                Debug.Log($"[EventManager] Indicator still active for Event {entry.eventNo}");

            }
            else
            {
                // 条件外になったらインジケーターを破棄
                if(indicators.TryGetValue(entry, out var inst))
                {
                    Destroy(inst);
                    indicators.Remove(entry);

                    Debug.Log($"[EventManager] Indicator removed for Event {entry.eventNo}");

                }
            }
        }
    }
}
