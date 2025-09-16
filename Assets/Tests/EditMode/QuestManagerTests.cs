using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Runtime;

namespace Tests.EditMode
{
    [TestFixture]
    public class QuestManagerTests
    {
        private GameObject _go;
        private QuestManager _qm;
        private List<QuestData> _createdQ = new();
        private List<EventData> _createdE = new();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("QM");
            _qm = _go.AddComponent<QuestManager>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _createdQ) Object.DestroyImmediate(so);
            foreach (var so in _createdE) Object.DestroyImmediate(so);
            Object.DestroyImmediate(_go);
        }

        private QuestData MkQuest(string id, List<string> steps, List<string> rewardFlags = null)
        {
            var q = ScriptableObject.CreateInstance<QuestData>();
            q.questId = id;
            q.priority = 0;
            q.stepEventIds = new List<string>(steps);
            q.rewardFlags = rewardFlags ?? new List<string>();
            _createdQ.Add(q);
            return q;
        }

        private EventData MkEvent(string id)
        {
            var e = ScriptableObject.CreateInstance<EventData>();
            e.eventId = id;
            e.type = Game.Events.EventType.Sub;
            e.appearAt = "0001-00:00";
            e.startDeadline = "0001-00:00";
            e.endDeadline = "0001-00:00";
            e.location = new LocationRef { kind = LocationKind.AreaId, id = "Town/Plaza" };
            e.requiresButtonPress = true;
            e.dependencies = new List<string>();
            e.altCompleteThreshold = 0.5f;
            e.weekdayRule = new WeekdayRule();
            _createdE.Add(e);
            return e;
        }

        /// <summary>
        /// 1) シグナルを受けたら履歴に記録される（ブラックアウトなし）
        /// </summary>
        [Test]
        public void Logs_History_On_Signals()
        {
            // Arrange: QuestManager を起動（Awake/OnEnable）
            _qm.Awake();
            _qm.OnEnable();

            try
            {
                // Act: いくつかの信号を流す
                EventSignals.RaiseScheduled("E1");
                EventSignals.RaiseAvailable("E1");
                EventSignals.RaiseStarted("E1");
                EventSignals.RaiseCompleted("E1");

                // Assert: 履歴に順番で記録
                Assert.AreEqual(4, _qm.EventLog.Count);
                Assert.AreEqual(("Scheduled", "E1"), _qm.EventLog[0]);
                Assert.AreEqual(("Available", "E1"), _qm.EventLog[1]);
                Assert.AreEqual(("Started", "E1"), _qm.EventLog[2]);
                Assert.AreEqual(("Completed", "E1"), _qm.EventLog[3]);
            }
            finally
            {
                _qm.OnDisable();
            }
        }

        /// <summary>
        /// 2) 全ステップ完了で報酬フラグが付与される
        /// </summary>
        [Test]
        public void Rewards_Are_Applied_When_All_Steps_Completed()
        {
            // Arrange: 2ステップのクエスト
            var q = MkQuest("Q_Main", new List<string> { "E1", "E2" }, new List<string> { "FLAG_MAIN_DONE" });
            // QuestManager に直接アサイン（Awake 前に）
            typeof(QuestManager).GetField("quests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_qm, new List<QuestData> { q });

            _qm.Awake();
            _qm.OnEnable();

            try
            {
                // Act: ステップ1完了（まだ報酬は付与されない）
                EventSignals.RaiseCompleted("E1");
                Assert.IsFalse(_qm.RewardFlags.Contains("FLAG_MAIN_DONE"), "E1だけでは報酬が付かないはず");

                // ステップ2完了（全ステップ達成で付与）
                EventSignals.RaiseCompleted("E2");
                Assert.IsTrue(_qm.RewardFlags.Contains("FLAG_MAIN_DONE"));
            }
            finally
            {
                _qm.OnDisable();
            }
        }

        /// <summary>
        /// 3) ブラックアウト中は通知が遅延し、解除時にまとめて反映される
        /// </summary>
        [Test]
        public void Notifications_Are_Delayed_During_Blackout()
        {
            _qm.Awake();
            _qm.OnEnable();

            try
            {
                // ブラックアウトON
                _qm.SetBlackout(true);
                Assert.IsTrue(_qm.NotificationsBlackout);

                // シグナルを流しても EventLog に記録されない（遅延キューに溜まる）
                EventSignals.RaiseScheduled("E1");
                EventSignals.RaiseCompleted("E1");
                Assert.AreEqual(0, _qm.EventLog.Count, "ブラックアウト中は履歴に出ないはず");

                // ブラックアウトOFF → ここでフラッシュされる
                _qm.SetBlackout(false);
                Assert.IsFalse(_qm.NotificationsBlackout);

                // 遅延分が EventLog に順序で反映される
                Assert.AreEqual(2, _qm.EventLog.Count);
                Assert.AreEqual(("Scheduled", "E1"), _qm.EventLog[0]);
                Assert.AreEqual(("Completed", "E1"), _qm.EventLog[1]);
            }
            finally
            {
                _qm.OnDisable();
            }
        }
    }
}
