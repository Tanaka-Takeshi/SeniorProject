// Game.Events 名前空間に配置
using System;
using System.Collections.Generic;

namespace Game.Events
{
    /// <summary>イベント購読の重複を防ぐ簡易ユーティリティ。</summary>
    public static class EventSignalUtils
    {
        // シグナル毎の「登録済みリスナー」を記録して二重登録を防止
        private static readonly HashSet<Delegate> _registered = new();

        public static void SubscribeOnce<T>(ref Action<T> signal, Action<T> handler)
        {
            if (handler == null) return;
            if (_registered.Contains(handler)) return;
            signal += handler;
            _registered.Add(handler);
        }
        public static void SubscribeOnce<T1, T2>(ref Action<T1, T2> signal, Action<T1, T2> handler)
        {
            if (handler == null) return;
            if (_registered.Contains(handler)) return;
            signal += handler;
            _registered.Add(handler);
        }

        public static void Unsubscribe(ref Action signal, Action handler)
        {
            if (handler == null) return;
            signal -= handler;
            _registered.Remove(handler);
        }
        public static void Unsubscribe<T>(ref Action<T> signal, Action<T> handler)
        {
            if (handler == null) return;
            signal -= handler;
            _registered.Remove(handler);
        }
        public static void Unsubscribe<T1, T2>(ref Action<T1, T2> signal, Action<T1, T2> handler)
        {
            if (handler == null) return;
            signal -= handler;
            _registered.Remove(handler);
        }

        /// <summary>テストやシーン終了時用：全登録情報をクリア</summary>
        public static void Reset() => _registered.Clear();
    }
}
