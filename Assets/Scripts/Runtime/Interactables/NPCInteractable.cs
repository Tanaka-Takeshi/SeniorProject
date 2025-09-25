using UnityEngine;
using TMPro; // 使わないなら削除可

namespace Game.Runtime
{
    /// <summary>
    /// NPCに近づいたら「Eで話す」を出すシンプルなインタラクト提示。
    /// 開始トリガー自体は EventManager 側（時間でAvailable＋エリア一致＋E押下）に任せる。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NPCInteractable : MonoBehaviour
    {
        [Header("Link to Scenario")]
        [Tooltip("このNPCが紐づくイベントID（表示用メモ）")]
        public string eventId = ""; // ※開始可否は EventManager 側が判定するので、ここでは表示のみ

        [Header("Prompt UI (optional)")]
        [Tooltip("ワールド空間のCanvasなど。なければGizmosのみで確認")]
        public Canvas worldCanvas;          // ワールド空間Canvas
        public TMP_Text promptText;         // “E: 話す” など
        [TextArea] public string prompt = "E : 話す";

        [Header("Detection")]
        [Tooltip("プレイヤーのタグ名")]
        public string playerTag = "Player";
        [Tooltip("プレイヤーが範囲内にいる間だけプロンプトを表示")]
        public bool showPromptOnlyWhenNear = true;

        // 内部状態
        bool _playerInside = false;

        void Reset()
        {
            // Trigger にして衝突判定で近接を検出
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        void Awake()
        {
            ApplyPromptVisibility(false);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInside = true;
            ApplyPromptVisibility(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInside = false;
            ApplyPromptVisibility(false);
        }

        void Update()
        {
            // “常時表示”を選ぶ場合の維持
            if (!showPromptOnlyWhenNear)
                ApplyPromptVisibility(true);

            // ここで開始はしない（EventManagerがE入力＋エリア一致で開始判定）
            // NPC側は“近くにいるよ”のUIを出すだけに留める
        }

        void ApplyPromptVisibility(bool on)
        {
            if (worldCanvas)
                worldCanvas.enabled = on || !showPromptOnlyWhenNear;

            if (promptText)
            {
                promptText.text = prompt;
                // フォントが足りない時の豆腐対策は別途（LiberationSans SDF → 日本語SDFへ）
                // promptText.enableWordWrapping は obsolete なので触らない
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            // Triggerのボリューム可視化
            var col = GetComponent<Collider>();
            if (!col) return;

            Gizmos.color = _playerInside ? new Color(0.2f, 1f, 0.2f, 0.35f) : new Color(0.2f, 0.6f, 1f, 0.25f);

            if (col is SphereCollider sc)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawSphere(sc.center, sc.radius);
            }
            else if (col is BoxCollider bc)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(bc.center, bc.size);
            }
            // 必要なら他のColliderにも対応
        }
#endif
    }
}
