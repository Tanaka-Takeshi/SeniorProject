using UnityEngine;
using Game.Runtime;
using Game.Events;

[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    [Tooltip("このオブジェクトに結びつくイベントID")]
    public string eventId;

    [Tooltip("この半径内でプレイヤーがインタラクト可能")]
    public float interactRadius = 2f;

    [Tooltip("正面を向いている必要があるか")]
    public bool requiresFacing = false;

    private Transform player;
    private IInputProxy input;
    private EventManager eventManager;

    void Start()
    {
        player = GameObject.FindWithTag("Player")?.transform;
        input = FindFirstObjectByType<TestInputProxy>(); // 実装している InputProxy を探す
        eventManager = FindFirstObjectByType<EventManager>();
    }

    void Update()
    {
        if (!player || input == null || eventManager == null) return;

        // 距離チェック
        float dist = Vector3.Distance(player.position, transform.position);
        if (dist > interactRadius) return;

        // 向きチェック（必要な場合）
        if (requiresFacing)
        {
            Vector3 toObj = (transform.position - player.position).normalized;
            float dot = Vector3.Dot(player.forward, toObj);
            if (dot < 0.5f) return; // ある程度正面でなければ不可
        }

        // イベントランタイムを取得
        if (!eventManager.TryGetRuntime(eventId, out var rt)) return;
        if (rt.State != EventState.Available) return;

        // 入力チェック
        if (input.StartPressedThisFrame())
        {
            // 開始フラグを消費
            if (eventManager.TryConsumeStartInput())
            {
                rt.Evaluate(eventManager); // Evaluateで Available→InProgress に遷移
                Debug.Log($"[Interactable] Started event {eventId}");
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
