using System;
using Game.Events;
using UnityEngine;

namespace Game.Runtime
{
    public class EventRuntime
    {
        public readonly Game.Data.EventData Data;
        public EventState State { get; private set; } = EventState.Locked;
        public FailedReason FailedReason { get; private set; } = FailedReason.None;
        public float Progress { get; private set; } = 0f;

        // 内部タイマー（残り時間等は必要に応じて拡張）
        private bool _timerFrozen = false;

        public EventRuntime(Game.Data.EventData data) {  Data = data; }

        public void SetProgress(float value)
        {
            Progress = UnityEngine.Mathf.Clamp01(value);
            EventSignals.RaiseProgress(Data.eventId, Progress);
        }

        public void FreezeTimers(bool frozen)
        {
            if(_timerFrozen == frozen) return;
            _timerFrozen = frozen;
            if(frozen) EventSignals.RaiseTimerFrozen(Data.eventId);
            else EventSignals.RaiseTimerResumed(Data.eventId);
        }

        public void Evaluate(IEvalContext ctx)
        {
            if (_timerFrozen || ctx.IsGloballyPaused) return;

            switch (State)
            {
                case EventState.Locked:
                    if (ctx.DependenciesSatisfied(Data.dependencies) &&
                        ctx.NowReached(Data.appearAt) &&
                        ctx.CalendarAllowed(Data.weekdayRule))
                    {
                        State = EventState.Scheduled;
                        EventSignals.RaiseScheduled(Data.eventId);
                    }
                    break;

                case EventState.Scheduled:
                    // 1) 開始期限超過を最優先で判定
                    if (ctx.StartDeadlineExceeded(Data.startDeadline))
                    {
                        if (ctx.PolicyTreatStartOverAsExpired)
                        {
                            State = EventState.Expired;
                            EventSignals.RaiseExpired(Data.eventId);
                        }
                        else
                        {
                            State = EventState.Failed;
                            FailedReason = FailedReason.MissedStart;
                            EventSignals.RaiseFailed(Data.eventId, FailedReason);
                        }
                        break;
                    }

                    // 2) 期限内であれば Available 判定
                    if (ctx.LocationSatisfied(Data.location) && ctx.InteractionPossible(Data))
                    {
                        State = EventState.Available;
                        EventSignals.RaiseAvailable(Data.eventId);
                    }
                    break;

                case EventState.Available:
                    {
                        // requiresButtonPress=false なら自動開始 / true なら“消費型”入力で開始
                        bool shouldStart = Data.requiresButtonPress
                            ? ctx.TryConsumeStartInput()
                            : true;

                        if (shouldStart)
                        {
                            State = EventState.InProgress;
                            EventSignals.RaiseStarted(Data.eventId);
                            break; // 開始を優先：このフレームはここで終了
                        }

                        // このフレームに開始しなかった場合のみ、開始期限超過をチェック
                        if (ctx.StartDeadlineExceeded(Data.startDeadline))
                        {
                            if (ctx.PolicyTreatStartOverAsExpired)
                            {
                                State = EventState.Expired;
                                EventSignals.RaiseExpired(Data.eventId);
                            }
                            else
                            {
                                State = EventState.Failed;
                                FailedReason = FailedReason.MissedStart;
                                EventSignals.RaiseFailed(Data.eventId, FailedReason);
                            }
                            break;
                        }

                        // 何も起きなければ Available 継続
                        break;
                    }

                case EventState.InProgress:
                    if (ctx.EndDeadlineReached(Data.endDeadline))
                    {
                        if (Progress >= Data.altCompleteThreshold)
                        {
                            State = EventState.Completed;
                            EventSignals.RaiseCompleted(Data.eventId);
                        }
                        else
                        {
                            State = EventState.Failed;
                            FailedReason = FailedReason.MissedEndLowProgress;
                            EventSignals.RaiseFailed(Data.eventId, FailedReason);
                        }
                    }
                    // 進捗更新は外部から SetProgress() を呼ぶ
                    break;
            }
        }

        public void ForceInterrupt()
        {
            State = EventState.Failed;
            FailedReason = FailedReason.Interrupted;
            EventSignals.RaiseFailed(Data.eventId, FailedReason);
        }



#if UNITY_EDITOR
        public void RestoreForTest(Game.Events.EventState state, Game.Events.FailedReason failed, float progress01)
        {
            State = state;
            FailedReason = failed;
            Progress = Mathf.Clamp01(progress01);
        }
#endif
    }

    // Managerが提供する評価コンテキスト（実装はプロジェクト側）
    public interface IEvalContext
    {
        bool IsGloballyPaused { get; }
        bool PolicyTreatStartOverAsExpired { get; }
        bool DependenciesSatisfied(System.Collections.Generic.List<string> ids);
        bool NowReached(string gameDateTime);
        bool StartDeadlineExceeded(string gameDateTime);
        bool EndDeadlineReached(string gameDateTime);
        bool CalendarAllowed(Game.Events.WeekdayRule rule);
        bool LocationSatisfied(Game.Events.LocationRef loc);
        bool InteractionPossible(Game.Data.EventData data);
        bool StartInputReceived();
        bool TryConsumeStartInput();
    }

}
