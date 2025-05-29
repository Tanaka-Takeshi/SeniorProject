using UnityEngine;
using System;
using System.Security.Cryptography;

public class TimeManager : MonoBehaviour
{
    // �J�n�����̐ݒ�
    [Header("�J�n�����ݒ�")]
    [SerializeField] private int startYear  = 1;
    [SerializeField] private int startMonth = 1;
    [SerializeField] private int startDay   = 1;
    [SerializeField] private int startHour   = 0;
    [SerializeField] private int startMinute = 0;
    [SerializeField] private int startSecond = 0;

    // 1���̎���
    [SerializeField] private float dayLength = 30.0f;

    // �V���O���g��
    public static TimeManager Instance { get; private set; }

    // �����J�E���^
    private float timeCounter;
    // �Q�[���J�n����Date����
    private DateTime startDate;
    // �Q�[���J�n����TimeOfDay����
    private TimeSpan startTimeOffset;

    // ���݂̃Q�[��������
    public DateTime CurrentDateTime { get; private set; }
    // ���݂̃Q�[��������
    public TimeSpan CurrentTime { get; private set; }
    // �J�n������̌o�߃Q�[������
    public int DayCount {  get; private set; }
    // ���ԑ�
    public TimeOfDay CurrentTimeOfDay { get; private set; }
    public enum TimeOfDay { EarlyMorning, Morning, Day, Evening, Night}

    private void Awake()
    {
        // �V���O���g��������
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
        // �J�n����Date������Time������ݒ�
        startDate = new DateTime(startYear, startMonth, startDay);
        startTimeOffset = new TimeSpan(startHour, startMinute, startSecond);

        timeCounter = 0;
        UpdateGameTime(); // �����l�Z�o
    }

    // Update is called once per frame
    void Update()
    {
        // ���ԃJ�E���g
        timeCounter += Time.deltaTime;
        UpdateGameTime();
    }

    private void UpdateGameTime()
    {
        // �Q�[����������b�ɑ��������邩
        float secondsPerGameDay = dayLength * 60.0f;

        // �o�߃Q�[���������i�J�n���̃T�C�N�����j
        int elapsedDays = Mathf.FloorToInt(timeCounter / secondsPerGameDay);
        // �J�n�� + �o�ߓ���
        DayCount = startDate.Day + elapsedDays;

        // ���̓��̐i�s�����i0�`1�j
        float dayProgress = (timeCounter % secondsPerGameDay) / secondsPerGameDay;

        // �Q�[�������Ԃ�b�ɕϊ�
        float gameSecondsThisDay = dayProgress * 86400.0f;

        // �J�n�����I�t�Z�b�g�����Z
        float totalGameSeconds = (float)startTimeOffset.TotalSeconds + gameSecondsThisDay;
        // ���t�֌J��グ��ǉ�����
        int offsetDays = Mathf.FloorToInt(totalGameSeconds / 86400.0f);
        // �����̎���
        float currentDaySeconds = totalGameSeconds % 86400.0f;

        CurrentTime = TimeSpan.FromSeconds(currentDaySeconds);
        CurrentDateTime = startDate.AddDays(DayCount + offsetDays).Date + CurrentTime;

        // ���ԑє���
        float hour = (float)CurrentTime.TotalHours;
        if (hour >= 4.0f && hour < 6.0f) CurrentTimeOfDay = TimeOfDay.EarlyMorning;
        else if (hour >= 6.0f && hour < 12.0f) CurrentTimeOfDay = TimeOfDay.Morning;
        else if (hour >= 12.0f && hour < 17.0f) CurrentTimeOfDay = TimeOfDay.Day;
        else if (hour >= 17.0f && hour < 19.0f) CurrentTimeOfDay = TimeOfDay.Evening;
        else CurrentTimeOfDay = TimeOfDay.Night;

        // ����m�F
        Debug.Log($"[GameTime] CurrentDateTime : {CurrentDateTime:yyyy/MM/dd HH:mm:ss}");
        Debug.Log($"[GameTime] CurrentTime     : {CurrentTime:hh\\:mm\\:ss} (TotalHours = {CurrentTime.TotalHours:F2})");
        Debug.Log($"[GameTime] DayCount        : {DayCount}");
        Debug.Log($"[GameTime] TimeOfDay       : {CurrentTimeOfDay}");
    }
}
