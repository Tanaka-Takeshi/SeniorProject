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

        // 内部タイマー（凍結中は評価しない）
        private bool _timerFrozen = false;

        public EventRuntime(Game.Data.EventData data) { Data = data; }

        public void SetProgress(float value)
        {
            Progress = Mathf.Clamp01(value);
            EventSignals.RaiseProgress(Data.eventId, Progress);
        }

        public void FreezeTimers(bool frozen)
        {
            if (_timerFrozen == frozen) return;
            _timerFrozen = frozen;
            if (frozen) EventSignals.RaiseTimerFrozen(Data.eventId);
            else EventSignals.RaiseTimerResumed(Data.eventId);
        }

        public void Evaluate(IEvalContext ctx)
        {
            if (_timerFrozen || ctx.IsGloballyPaused) return;

            switch (State)
            {
                case EventState.Locked:
                    {
                        // 依存OK + 時刻到達 + カレンダーOK で Scheduled
                        if (ctx.DependenciesSatisfied(Data.dependencies) &&
                            ctx.NowReached(Data.appearAt) &&
                            ctx.CalendarAllowed(Data.weekdayRule))
                        {
                            State = EventState.Scheduled;
                            EventSignals.RaiseScheduled(Data.eventId);
                        }
                        break;
                    }

                case EventState.Scheduled:
                    {
                        // 開始期限超過を最優先
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

                        // ★新仕様：時間だけで Available（場所/入力の可否は見ない）
                        State = EventState.Available;
                        EventSignals.RaiseAvailable(Data.eventId);
                        break;
                    }

                case EventState.Available:
                    {
                        // ★新仕様：開始条件は「場所到達」または「インタラクト」
                        //   - 場所到達は Data.autoStartOnLocation が true のときのみ許可
                        //   - インタラクトは 1 フレーム消費（TryConsumeStartInput）
                        bool byLocation = Data.autoStartOnLocation && ctx.LocationSatisfied(Data.location);
                        bool byInteract = ctx.TryConsumeStartInput();

                        // （補足）requiresButtonPress は互換のため残存。
                        //  インタラクト必須のイベントにしたい場合は
                        //  autoStartOnLocation=false にして byLocation を無効化してください。
                        if (byLocation || byInteract)
                        {
                            State = EventState.InProgress;
                            EventSignals.RaiseStarted(Data.eventId);
                            break; // このフレームは開始で終える
                        }

                        // 開始しないまま開始期限を超えたら失敗（またはExpiredポリシー）
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
                        }
                        break;
                    }

                case EventState.InProgress:
                    {
                        // 終了到達時に進捗で Completed / Failed を分岐
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
                        break;
                    }

                case EventState.Completed:
                case EventState.Failed:
                case EventState.Expired:
                default:
                    // ターミナル状態
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

    // Manager が提供する評価コンテキスト（既存そのまま）
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
        bool InteractionPossible(Game.Data.EventData data); // 互換のため存置（未使用）
        bool StartInputReceived();                           // 互換のため存置（未使用）
        bool TryConsumeStartInput();
    }
}
