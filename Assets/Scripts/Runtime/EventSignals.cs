using System;

namespace Game.Events
{
    public static class EventSignals
    {
        public static event Action<string> OnScheduled;
        public static event Action<string> OnAvailable;
        public static event Action<string> OnStarted;
        public static event Action<string, float> OnProgress;
        public static event Action<string> OnCompleted;
        public static event Action<string, Game.Events.FailedReason> OnFailed;
        public static event Action<string> OnExpired;
        public static event Action<string> OnTimerFrozen;
        public static event Action<string> OnTimerResumed;

        // ”­‰Î—pƒwƒ‹ƒp
        public static void RaiseScheduled(string id) => OnScheduled?.Invoke(id);
        public static void RaiseAvailable(string id) => OnAvailable?.Invoke(id);
        public static void RaiseStarted(string id) => OnStarted?.Invoke(id);
        public static void RaiseProgress(string id, float pct) => OnProgress?.Invoke(id, pct);
        public static void RaiseCompleted(string id) => OnCompleted?.Invoke(id);
        public static void RaiseFailed(string id, Game.Events.FailedReason r) => OnFailed?.Invoke(id, r);
        public static void RaiseExpired(string id) => OnExpired?.Invoke(id);
        public static void RaiseTimerFrozen(string id) => OnTimerFrozen?.Invoke(id);
        public static void RaiseTimerResumed(string id) => OnTimerResumed?.Invoke(id);
    }

}
