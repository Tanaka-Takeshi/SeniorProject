namespace Game.Runtime
{
    public class SimpleCalendar : UnityEngine.MonoBehaviour, ICalendarSvc
    {
        public bool useWeekday = false;         // テストではfalse既定
        public bool IsAllowed(Game.Events.WeekdayRule rule)
        {
            if (!useWeekday || rule == null) return true;
            // 必要なら曜日/休日ロジックを実装
            return true;
        }
    }
}