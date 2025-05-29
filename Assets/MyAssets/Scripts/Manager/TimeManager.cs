using UnityEngine;
using System;
using System.Security.Cryptography;

public class TimeManager : MonoBehaviour
{
    // 開始日時の設定
    [Header("開始日時設定")]
    [SerializeField] private int startYear  = 1;
    [SerializeField] private int startMonth = 1;
    [SerializeField] private int startDay   = 1;
    [SerializeField] private int startHour   = 0;
    [SerializeField] private int startMinute = 0;
    [SerializeField] private int startSecond = 0;

    // 1日の周期
    [SerializeField] private float dayLength = 30.0f;

    // シングルトン
    public static TimeManager Instance { get; private set; }

    // 内部カウンタ
    private float timeCounter;
    // ゲーム開始日のDate部分
    private DateTime startDate;
    // ゲーム開始日のTimeOfDay部分
    private TimeSpan startTimeOffset;

    // 現在のゲーム内日時
    public DateTime CurrentDateTime { get; private set; }
    // 現在のゲーム内時刻
    public TimeSpan CurrentTime { get; private set; }
    // 開始日からの経過ゲーム日数
    public int DayCount {  get; private set; }
    // 時間帯
    public TimeOfDay CurrentTimeOfDay { get; private set; }
    public enum TimeOfDay { EarlyMorning, Morning, Day, Evening, Night}

    private void Awake()
    {
        // シングルトン初期化
        if (Instance != null && Instance != this)
        { 
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // 開始日のDate部分とTime部分を設定
        startDate = new DateTime(startYear, startMonth, startDay);
        startTimeOffset = new TimeSpan(startHour, startMinute, startSecond);

        timeCounter = 0;
        UpdateGameTime(); // 初期値算出
    }

    // Update is called once per frame
    void Update()
    {
        // 時間カウント
        timeCounter += Time.deltaTime;
        UpdateGameTime();
    }

    private void UpdateGameTime()
    {
        // ゲーム一日を何秒に相当させるか
        float secondsPerGameDay = dayLength * 60.0f;

        // 経過ゲーム内日数（開始日のサイクル数）
        int elapsedDays = Mathf.FloorToInt(timeCounter / secondsPerGameDay);
        // 開始日 + 経過日数
        DayCount = startDate.Day + elapsedDays;

        // その日の進行割合（0～1）
        float dayProgress = (timeCounter % secondsPerGameDay) / secondsPerGameDay;

        // ゲーム内時間を秒に変換
        float gameSecondsThisDay = dayProgress * 86400.0f;

        // 開始時刻オフセットを加算
        float totalGameSeconds = (float)startTimeOffset.TotalSeconds + gameSecondsThisDay;
        // 日付へ繰り上げる追加日数
        int offsetDays = Mathf.FloorToInt(totalGameSeconds / 86400.0f);
        // 当日の時刻
        float currentDaySeconds = totalGameSeconds % 86400.0f;

        CurrentTime = TimeSpan.FromSeconds(currentDaySeconds);
        CurrentDateTime = startDate.AddDays(DayCount + offsetDays).Date + CurrentTime;

        // 時間帯判定
        float hour = (float)CurrentTime.TotalHours;
        if (hour >= 4.0f && hour < 6.0f) CurrentTimeOfDay = TimeOfDay.EarlyMorning;
        else if (hour >= 6.0f && hour < 12.0f) CurrentTimeOfDay = TimeOfDay.Morning;
        else if (hour >= 12.0f && hour < 17.0f) CurrentTimeOfDay = TimeOfDay.Day;
        else if (hour >= 17.0f && hour < 19.0f) CurrentTimeOfDay = TimeOfDay.Evening;
        else CurrentTimeOfDay = TimeOfDay.Night;

        // 動作確認
        Debug.Log($"[GameTime] CurrentDateTime : {CurrentDateTime:yyyy/MM/dd HH:mm:ss}");
        Debug.Log($"[GameTime] CurrentTime     : {CurrentTime:hh\\:mm\\:ss} (TotalHours = {CurrentTime.TotalHours:F2})");
        Debug.Log($"[GameTime] DayCount        : {DayCount}");
        Debug.Log($"[GameTime] TimeOfDay       : {CurrentTimeOfDay}");
    }
}
