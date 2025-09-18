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

        // ★追加：Available入り直後、目的地に既に居たら自動開始を一旦ブロックするためのフラグ
        private bool _allowAutoStartByLocation = true;

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

                        // 時間だけで Available（場所/入力の可否は見ない）
                        State = EventState.Available;
                        EventSignals.RaiseAvailable(Data.eventId);

                        // ★Availableに入った瞬間に既に目的地に居たら、自動開始を一旦ブロック
                        //   → 一度離れて再入場した時だけ自動開始を許可する
                        _allowAutoStartByLocation = !ctx.LocationSatisfied(Data.location);
                        break;
                    }

                case EventState.Available:
                    {
                        // ① まず開始期限超過をチェック（最優先）
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

                        // ② 「その場に居たまま」だとブロック継続。離れたら許可に戻す
                        if (!_allowAutoStartByLocation && !ctx.LocationSatisfied(Data.location))
                            _allowAutoStartByLocation = true;

                        // ③ 開始条件：到達 or 入力
                        bool byLocation = Data.autoStartOnLocation
                                          && _allowAutoStartByLocation
                                          && ctx.LocationSatisfied(Data.location);

                        // 入力開始は requiresButtonPress が true のときのみ
                        bool byInteract = Data.requiresButtonPress
                                          && ctx.TryConsumeStartInput();

                        if (byLocation || byInteract)
                        {
                            State = EventState.InProgress;
                            EventSignals.RaiseStarted(Data.eventId);
                            break; // このフレームは開始で終える
                        }

                        // 何もなければ Available 継続
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

    // そのまま（インターフェース変更なし）
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
