using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Events;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Data/EventData")]
    public class EventData : ScriptableObject
    {
        [Header("Identity")]
        public string eventId;                  // 章.節(例：2.1)
        public Game.Events.EventType type = Game.Events.EventType.Sub;

        [Header("Time window(Game DateTime)")]
        public string appearAt;                 // 例: "0002-12:00" (暦の仕様に合わせてパース)
        public string startDeadline;            // 例: "0003-21:00"
        public string endDeadline;              // 例: "0005-12:00" 

        [Header("Location & Interaction")]
        public LocationRef location;

        [Tooltip("ボタン押下で開始可能にする（インタラクト）。trueのとき TryConsumeStartInput() を開始トリガとして見る。")]
        public bool requiresButtonPress = true;

        [Header("Start Triggers (New)")]
        [Tooltip("到達したら自動で開始してよいか（Available中に LocationSatisfied(location) で即Start）。")]
        public bool autoStartOnLocation = true;    // ★ 新仕様：場所到達でも開始できる

        [Header("Dependencies & Progress")]
        public List<string> dependencies = new();   // 依存イベントID
        [Range(0f, 1f)] public float altCompleteThreshold = 0.5f;

        [Header("Calendar Rule (optional)")]
        public WeekdayRule weekdayRule;

        [TextArea] public string notes;

        public bool interactNeedsLocation = true;
    }

    [CreateAssetMenu(menuName = "Game/Data/QuestData")]
    public class QuestData : ScriptableObject
    {
        [Header("Identity & Ordering")]
        public string questId;
        public int priority = 0;

        [Header("Steps(EventIDs) - 直列進行")]
        public List<string> stepEventIds = new();

        [Header("Presentation")]
        public string goalTextKey;                  // ローカライズキー
        public List<string> rewardFlags = new();
        public string displayOptions;               // 表示に関するメモ等
    }
}

