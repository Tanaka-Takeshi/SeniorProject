namespace Game.Runtime
{
    public interface IClock { float NowGameSeconds { get; } }
    public interface ICalendarSvc { bool IsAllowed(Game.Events.WeekdayRule rule); }
    public interface ILocationResolver { bool IsSatisfied(Game.Events.LocationRef loc); }
    public interface IInputProxy { bool StartPressedThisFrame(); }
}
