using Game.Data;
using Game.Events;
using Game.Runtime;
using Game.UI;
using Ionic.Zlib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 進行中（Available / InProgress）だけを縦に並べる軽量HUD。
    /// Terminal（Completed/Failed/Expired）は即フェード退場。
    /// </summary>
    public class EventHUDList : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private EventManager eventManager;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private EventHUDItem itemPrefab;

        [Header("Options")]
        [Min(1)] public int maxItems = 5;
        [Min(0f)] public float removeFadeSec = 0.18f;
        public bool mainOnTop = true; // Mainを上に積む

        // 表示中アイテム（EventId -> Item）
        private readonly Dictionary<string, EventHUDItem> _items = new();
        // 追加順の管理（最古→最新）
        private readonly LinkedList<string> _order = new();

        void Reset()
        {
            if (!eventManager) eventManager = FindAnyObjectByType<EventManager>();
        }

        void Awake()
        {
            if (!eventManager)
            {
                // Unity 2023以降推奨API
                eventManager = Object.FindFirstObjectByType<EventManager>();
            }
        }

        void ClearAllUI()
        {
            foreach (var kv in _items)
            {
                if (kv.Value) Destroy(kv.Value.gameObject);
            }
            _items.Clear();
            _order.Clear();
        }

        void OnEnable()
        {
            SubscribeSignals();

            ClearAllUI();

            // 後から有効化された時でも現在の進行中イベントを復元
            RebuildFromManager();

            if(contentRoot) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }

        void OnDisable()
        {
            UnsubscribeSignals();
        }

        void OnDestroy()
        {
            foreach (var kv in _items)
            {
                if (kv.Value) Destroy(kv.Value.gameObject);
            }
            _items.Clear();
            _order.Clear();
        }

        // ====== Signals ======
        void SubscribeSignals()
        {
            EventSignals.OnScheduled += HandleScheduled;
            EventSignals.OnAvailable += HandleAvailable;
            EventSignals.OnStarted += HandleStarted;
            EventSignals.OnCompleted += HandleCompleted;
            EventSignals.OnFailed += HandleFailed;
            EventSignals.OnExpired += HandleExpired;
            EventSignals.OnProgress += HandleProgress;
        }

        void UnsubscribeSignals()
        {
            EventSignals.OnScheduled -= HandleScheduled;
            EventSignals.OnAvailable -= HandleAvailable;
            EventSignals.OnStarted -= HandleStarted;
            EventSignals.OnCompleted -= HandleCompleted;
            EventSignals.OnFailed -= HandleFailed;
            EventSignals.OnExpired -= HandleExpired;
            EventSignals.OnProgress -= HandleProgress;
        }

        void HandleScheduled(string id) { /* 表示対象外 */ }

        void HandleAvailable(string id)
        {
            if (!TryGetData(id, out var data)) return;
            var item = GetOrCreate(id, data);
            item.SetBody(BuildBodyAvailable(data));
            TrimToMax();
        }

        void HandleStarted(string id)
        {
            Debug.Log($"[List] HandleStarted {id}");
            if (!TryGetData(id, out var data)) return;

            var item = GetOrCreate(id, data);
            item.SetBody(BuildBodyInProgress(data));

            // 進捗バーを 0 に（任意）
            item.SetProgress01(0f);

            TrimToMax();
            RebuildLayout();
        }

        void HandleCompleted(string id) => RemoveIfExists(id, "完了しました。");
        void HandleFailed(string id, FailedReason r) => RemoveIfExists(id, $"失敗：{r}");
        void HandleExpired(string id) => RemoveIfExists(id, "期限切れ");

        void HandleProgress(string id, float p)
        {
            if (_items.TryGetValue(id, out var it))
                it.SetProgress01(Mathf.Clamp01(p));
        }

        // ====== Core ======
        EventHUDItem GetOrCreate(string id, EventData data)
        {
            if (_items.TryGetValue(id, out var existed))
            {
                BumpOrder(id);
                return existed;
            }

            if (!itemPrefab || !contentRoot) return null;

            var go = Instantiate(itemPrefab, contentRoot);
            var item = go;
            var label = $"[{((data.type == Game.Events.EventType.Main) ? "Main" : "Sub")}] {id}";
            item.Setup(id, label, "", data.type);

            if (mainOnTop && data.type == Game.Events.EventType.Main)
                item.transform.SetAsFirstSibling();
            else
                item.transform.SetAsLastSibling();

            _items[id] = item;
            _order.AddLast(id);
            return item;
        }

        void RemoveIfExists(string id, string finalText)
        {
            if (!_items.TryGetValue(id, out var it)) return;
            it.SetBody(finalText);
            _items.Remove(id);
            _order.Remove(id);
            it.FadeOutAndDestroy(removeFadeSec);
        }

        void BumpOrder(string id)
        {
            if (!_order.Contains(id)) return;
            _order.Remove(id);
            _order.AddLast(id);
        }

        void TrimToMax()
        {
            while (_order.Count > maxItems)
            {
                var oldest = _order.First.Value;
                _order.RemoveFirst();
                if (_items.TryGetValue(oldest, out var it))
                {
                    _items.Remove(oldest);
                    it.FadeOutAndDestroy(removeFadeSec);
                }
            }
        }

        bool TryGetData(string id, out EventData data)
        {
            data = null;
            if (!eventManager) return false;
            if (!eventManager.TryGetRuntime(id, out var rt) || rt == null) return false;
            data = rt.Data;
            return true;
        }

        static string BuildBodyAvailable(EventData d)
        {
            var place = string.IsNullOrEmpty(d.location.id) ? "目的地" : d.location.id;
            if (d.autoStartOnLocation) return $"{place} に到達すると自動で開始";
            if (d.requiresButtonPress) return $"{place} で [E] で開始";
            return "開始条件を満たすと開始";
        }

        // ====== Rebuild（途中合流に対応） ======
        public void RebuildFromManager()
        {
            if (!eventManager) { Debug.Log("[List] Rebuild: em=null"); return; }

            ClearAllUI();

            int added = 0;

            foreach (var rt in EnumerateRuntimesSafe(eventManager))
            {
                if (rt == null) continue;

                var d = rt.Data;
                switch (rt.State)
                {
                    case EventState.Available:
                        {
                            var item = GetOrCreate(d.eventId, d);
                            item.SetBody(BuildBodyAvailable(d));
                            // Available は進捗バーを消す
                            item.SetProgress01(0f);
                            added++;
                            break;
                        }
                    case EventState.InProgress:
                        {
                            var item = GetOrCreate(d.eventId, d);
                            item.SetBody(BuildBodyInProgress(d));
                            // もし進捗値がとれるなら反映（なければ 0 でもOK）
                            // rt に Progress (0..1) がある想定。無ければこの行は削除。
                            item.SetProgress01(Mathf.Clamp01(rt.Progress));
                            added++;
                            break;
                        }
                        // Completed/Failed/Expired は表示しない
                }
            }

            TrimToMax();

            if (contentRoot) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
            Debug.Log($"[List] Rebuild done. added={added}");
        }

        static IEnumerable<EventRuntime> EnumerateRuntimesSafe(EventManager em)
        {
            var mi = em.GetType().GetMethod("AllRuntimes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null && typeof(System.Collections.IEnumerable).IsAssignableFrom(mi.ReturnType))
            {
                foreach (var obj in (System.Collections.IEnumerable)mi.Invoke(em, null))
                    if (obj is EventRuntime rt) yield return rt;
                yield break;
            }

            var fi = em.GetType().GetField("_runtimes", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi != null)
            {
                var dictObj = fi.GetValue(em);
                if (dictObj is System.Collections.IDictionary dict)
                {
                    foreach (System.Collections.DictionaryEntry de in dict)
                        if (de.Value is EventRuntime rt) yield return rt;
                }
            }
        }

        public RectTransform ContentRoot
        {
            get => contentRoot;
            set => contentRoot = value;
        }

        public EventHUDItem ItemPrefab
        {
            get => itemPrefab;
            set => itemPrefab = value;
        }

        public EventManager Manager
        {
            get => eventManager;
            set => eventManager = value;
        }

        public void ForceRefreshNow()
        {
            StopAllCoroutines();
            RebuildFromManager();
            if (contentRoot)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
            }
        }

        public IEnumerator CoRefreshNextFrame()
        {
            yield return null;
            ForceRefreshNow();
        }

        static string BuildBodyInProgress(EventData d)
        {
            var place = string.IsNullOrEmpty(d.location.id) ? "目的地" : d.location.id;
            return $"進行中：{place}";
        }

        void RebuildLayout()
        {
            if (!contentRoot) return;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
            Canvas.ForceUpdateCanvases();
        }
    }
}
