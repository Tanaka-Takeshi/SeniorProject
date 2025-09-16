using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Data;
using Game.Events;

namespace Game.Runtime
{
    /// <summary>
    /// クエスト提示・履歴・報酬（フラグ）・通知制御を担う最小実装。
    /// ・イベント信号を購読して履歴を残す
    /// ・クエストの全ステップ達成で報酬フラグを付与
    /// ・ブラックアウト中は通知を遅延（解除時にまとめて適用）
    /// ※UIは持たず、テスト用に内部状態を公開プロパティで確認できるようにする
    /// </summary>
    public sealed class QuestManager : MonoBehaviour
    {
        [SerializeField] private List<QuestData> quests = new();

        // ====== 検証しやすい公開プロパティ ======
        public bool NotificationsBlackout { get; private set; }

        // イベント履歴（ログ）: "Started/Completed/Failed/Expired/Available/Scheduled" など
        public readonly List<(string signal, string eventId)> EventLog = new();

        // クエスト進行状況：QuestID -> 完了した EventID の集合
        public readonly Dictionary<string, HashSet<string>> QuestProgress = new();

        // 最終的に付与された報酬フラグ（重複なし）
        public readonly HashSet<string> RewardFlags = new();

        // ブラックアウト中に溜める通知（解除時に EventLog へ反映）
        private readonly Queue<(string signal, string eventId)> _delayed = new();

        // 逆引き：EventID -> 所属クエストID（複数クエストが同一イベントを参照するケースは基本想定外だが、配列化）
        private readonly Dictionary<string, List<string>> _eventToQuests = new();

        // ====== ライフサイクル ======
        public void Awake()
        {
            BuildReverseIndex();
            EnsureQuestProgress();
        }

        public void OnEnable()
        {
            EventSignals.OnScheduled += HandleScheduled;
            EventSignals.OnAvailable += HandleAvailable;
            EventSignals.OnStarted += HandleStarted;
            EventSignals.OnCompleted += HandleCompleted;
            EventSignals.OnFailed += HandleFailed;
            EventSignals.OnExpired += HandleExpired;
            EventSignals.OnProgress += HandleProgress;
        }

        public void OnDisable()
        {
            EventSignals.OnScheduled -= HandleScheduled;
            EventSignals.OnAvailable -= HandleAvailable;
            EventSignals.OnStarted -= HandleStarted;
            EventSignals.OnCompleted -= HandleCompleted;
            EventSignals.OnFailed -= HandleFailed;
            EventSignals.OnExpired -= HandleExpired;
            EventSignals.OnProgress -= HandleProgress;
        }

        // ====== 外部操作API（テストからも使う） ======
        public void SetBlackout(bool on)
        {
            if (on == NotificationsBlackout) return;
            NotificationsBlackout = on;
            if (!on) FlushDelayed(); // 解除時に溜めた通知を反映
        }

        // ====== 内部：インデックス・初期化 ======
        private void BuildReverseIndex()
        {
            _eventToQuests.Clear();
            foreach (var q in quests)
            {
                foreach (var evId in q.stepEventIds)
                {
                    if (!_eventToQuests.TryGetValue(evId, out var list))
                    {
                        list = new List<string>();
                        _eventToQuests[evId] = list;
                    }
                    if (!list.Contains(q.questId))
                        list.Add(q.questId);
                }
            }
        }

        private void EnsureQuestProgress()
        {
            QuestProgress.Clear();
            foreach (var q in quests)
            {
                if (!QuestProgress.ContainsKey(q.questId))
                    QuestProgress[q.questId] = new HashSet<string>();
            }
        }

        private void AppendLog(string sig, string id)
        {
            if (NotificationsBlackout)
            {
                _delayed.Enqueue((sig, id));
            }
            else
            {
                EventLog.Add((sig, id));
            }
        }

        private void FlushDelayed()
        {
            while (_delayed.Count > 0)
                EventLog.Add(_delayed.Dequeue());
        }

        private void MarkEventCompleted(string eventId)
        {
            if (!_eventToQuests.TryGetValue(eventId, out var qs)) return;

            foreach (var qid in qs)
            {
                if (!QuestProgress.TryGetValue(qid, out var set))
                {
                    set = new HashSet<string>();
                    QuestProgress[qid] = set;
                }
                set.Add(eventId);

                // 全ステップ完了なら報酬フラグを付与
                var q = quests.FirstOrDefault(x => x.questId == qid);
                if (q != null && q.stepEventIds.All(e => set.Contains(e)))
                {
                    foreach (var flag in q.rewardFlags)
                        RewardFlags.Add(flag);
                }
            }
        }

        // ====== シグナルハンドラ ======
        private void HandleScheduled(string id) => AppendLog("Scheduled", id);
        private void HandleAvailable(string id) => AppendLog("Available", id);
        private void HandleStarted(string id) => AppendLog("Started", id);

        private void HandleCompleted(string id)
        {
            AppendLog("Completed", id);
            MarkEventCompleted(id);
        }

        private void HandleFailed(string id, FailedReason r)
        {
            AppendLog($"Failed:{r}", id);
        }

        private void HandleExpired(string id)
        {
            AppendLog("Expired", id);
        }

        private void HandleProgress(string id, float pct)
        {
            // 進捗はログしない or 必要なら記録（今回はログしない）
        }
    }
}


//using System.Collections.Generic;
//using UnityEngine;
//using Game.Data;

//namespace Game.Runtime
//{
//    public sealed class QuestManager : MonoBehaviour
//    {
//        [SerializeField] private List<QuestData> quests = new();

//        private void OnEnable()
//        {
//            EventSignals.OnStarted += HandleStarted;
//            EventSignals.OnCompleted += HandleCompleted;
//            EventSignals.OnFailed += HandleFailed;
//            EventSignals.OnExpired += HandleExpired;
//            EventSignals.OnProgress += HandleProgress;
//        }

//        private void OnDisable()
//        {
//            EventSignals.OnStarted -= HandleStarted;
//            EventSignals.OnCompleted -= HandleCompleted;
//            EventSignals.OnFailed -= HandleFailed;
//            EventSignals.OnExpired -= HandleExpired;
//            EventSignals.OnProgress -= HandleProgress;
//        }

//        private void HandleStarted(string id) { /* 履歴・UI更新 */ }
//        private void HandleCompleted(string id) { /* フラグ報酬・履歴 */ }
//        private void HandleFailed(string id, Game.Events.FailedReason r) { /* ログ */ }
//        private void HandleExpired(string id) { /* ログ */ }
//        private void HandleProgress(string id, float pct) { /* 表示更新 */ }
//    }
//}
