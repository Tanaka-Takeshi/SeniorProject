using UnityEngine;
using System.Collections.Generic;
using TMPro.Examples;

public class EventManager : MonoBehaviour
{
    [Header("�C�x���g�ݒ�")]
    [SerializeField] private EventData eventData;
    [SerializeField] private GameObject startIndicator;
    [SerializeField] private float indicatorOffsetY = 3f;

    // �G���g���Ɛ������C���W�P�[�^�[�̕R�t���p
    private readonly Dictionary<EventEntry, GameObject> indicators
        = new Dictionary<EventEntry, GameObject>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // EventData�̏��������Ď��s
        foreach(var entry in eventData.events)
        {
            entry.InitializeEvent();
        }
    }

    // Update is called once per frame
    void Update()
    {
        // ���݂̃Q�[���������i�J�n����̌o�ߓ����j�y�ю��ԑт��擾
        int currentDay = TimeManager.Instance.DayCount;
        TimeManager.TimeOfDay currentTod = TimeManager.Instance.CurrentTimeOfDay;

        foreach(var entry in eventData.events)
        {
            // �J�n���Ǝ��ԑт���v���Ă��邩
            bool isMatching = entry.startDay == currentDay
                && entry.startTimeOfDay == currentTod;

            if( isMatching )
            {
                // �X�e�[�^�X�X�V
                if(entry.status != EventStatus.WaitingToStart)
                    entry.status = EventStatus.WaitingToStart;

                // �C���W�P�[�^�[����
                if(!indicators.ContainsKey(entry) && entry.startObject  != null)
                {
                    Vector3 spawnPos = entry.startObject.transform.position
                        + Vector3.up * indicatorOffsetY;

                    var inst = Instantiate(startIndicator, spawnPos,
                        Quaternion.identity, entry.startObject.transform);

                    //// 1) ���[���h�ʒu�Ő���
                    //var inst = Instantiate(startIndicator, spawnPos, Quaternion.identity);
                    //// 2) ���E�ʒu���ێ����e�q�t��
                    //inst.transform.SetParent(entry.startObject.transform, true);

                    indicators.Add(entry, inst);

                    Debug.Log($"[EventManager] Indicator instantiated for Event {entry.eventNo}");

                }
            }
            else if(indicators.ContainsKey(entry))
            {
                // ���łɕ\�����Ȃ疈�t���[�����O
                Debug.Log($"[EventManager] Indicator still active for Event {entry.eventNo}");

            }
            else
            {
                // �����O�ɂȂ�����C���W�P�[�^�[��j��
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
