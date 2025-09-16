using System;
using System.Collections.Generic;
using UnityEngine;

// 共通定義
namespace Game.Events
{
    public enum EventState
    {
        Locked,
        Scheduled,
        Available,
        InProgress,
        Completed,
        Failed,
        Expired
    }

    public enum FailedReason
    {
        None = 0,
        MissedStart,
        MissedEndLowProgress,
        Interrupted
    }

    public enum EventType
    {
        Main,
        Sub,
        World,
        Tutorial
    }

    public enum LocationKind
    {
        AreaId,         // 論理IDでエリアを参照
        WaypointId,     // マーカー等
        WorldPos        // OW向けに絶対座標
    }

    [Serializable]
    public struct LocationRef
    {
        public LocationKind kind;
        public string id;           // AreaIDまたはWaypointID
        public Vector3 worldPos;
    }

    [Serializable]
    public class WeekdayRule
    {
        // 0 = Sun ... 6 = Sat
        public List<int> allowedWeekdays = new();   // 空なら制限なし
        public bool hasRequireHolidayFlag = false;
        public bool requireHoliday = false;         // true = 休日のみ, false = 平日のみ
    }
}