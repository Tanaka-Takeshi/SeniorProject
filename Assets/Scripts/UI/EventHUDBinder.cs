using UnityEngine;
using Game.Events;
using Game.Runtime;   // EventManager / EventRuntime
using Game.Data;      // EventData

public class EventHUDBinder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EventManager eventManager;
    [SerializeField] private HUDController hud;   // タイトル/本文の表示先
    [SerializeField] private ToastUI toast;       // 省略可：あればトーストも出す

    [Header("Labels")]
    [SerializeField] private string mainTag = "Main";
    [SerializeField] private string subTag = "Sub";

    [Header("Options")]
    //[SerializeField] private float startedToastSec = 1.5f;   // Start時のトースト表示秒
    [SerializeField] private bool autoHideOnStart = true;   // StartしたらHUDを閉じるか

    // 直近のAvailable/Started
    private string _lastAvailable;
    private string _lastStarted;

    private void Reset()
    {
        if (!eventManager) eventManager = FindAnyObjectByType<EventManager>();
        if (!hud) hud = FindAnyObjectByType<HUDController>();
        if (!toast) toast = FindAnyObjectByType<ToastUI>();
    }

    private void OnEnable()
    {
        EventSignals.OnAvailable += HandleAvailable;
        EventSignals.OnStarted += HandleStarted;
        EventSignals.OnCompleted += HandleCompleted;
        EventSignals.OnFailed += HandleFailed;
        EventSignals.OnExpired += HandleExpired;
    }

    private void OnDisable()
    {
        EventSignals.OnAvailable -= HandleAvailable;
        EventSignals.OnStarted -= HandleStarted;
        EventSignals.OnCompleted -= HandleCompleted;
        EventSignals.OnFailed -= HandleFailed;
        EventSignals.OnExpired -= HandleExpired;
    }

    // ========= Signal Handlers =========

    private void HandleAvailable(string id)
    {
        _lastAvailable = id;
        if (!TryGetData(id, out var data, out var tag)) return;

        var body = BuildAvailableBody(data);
        hud?.Show($"[{tag}] {id}", body, instant: true);

        toast?.Show($"[{tag}] {Shorten(id)} が開始可能になりました");
    }

    private void HandleStarted(string id)
    {
        _lastStarted = id;
        if (!TryGetData(id, out var data, out var tag)) return;

        hud?.SetTitle($"[{tag}] {id}");
        hud?.SetBody("イベントを開始しました。");

        if (autoHideOnStart)
            hud?.Hide();

        if (toast)
            toast.Show($"[{tag}] {Shorten(id)} を開始");
    }

    private void HandleCompleted(string id)
    {
        if (_lastStarted == id || _lastAvailable == id)
        {
            toast?.Show($"{Shorten(id)} を完了");
            hud?.Hide();
        }
    }

    private void HandleFailed(string id, FailedReason reason)
    {
        if (_lastStarted == id || _lastAvailable == id)
        {
            toast?.Show($"{Shorten(id)} は失敗 ({reason})");
            hud?.Hide();
        }
    }

    private void HandleExpired(string id)
    {
        if (_lastStarted == id || _lastAvailable == id)
            hud?.Hide();
    }

    // ========= Helpers =========

    private bool TryGetData(string id, out EventData data, out string tag)
    {
        data = null; tag = subTag;
        if (eventManager == null) return false;
        if (!eventManager.TryGetRuntime(id, out var rt) || rt == null) return false;

        data = rt.Data;
        tag = (data.type == Game.Events.EventType.Main) ? mainTag : subTag;
        return true;
    }

    private static string BuildAvailableBody(EventData d)
    {
        // 場所の表示（LocationKind を増やすなら適宜整形）
        var place = string.IsNullOrEmpty(d.location.id) ? "目的地" : d.location.id;

        // 自動開始 or インタラクト案内
        if (d.autoStartOnLocation)
        {
            // 自動開始（必要ならボタン案内も共存可能だが、基本は到達で開始）
            return $"開始可能になりました。\n{place} に到達すると自動で開始します。";
        }
        else
        {
            // インタラクト開始（キー名はプロジェクトの入力系に合わせて文言調整）
            var press = d.requiresButtonPress ? "[E]" : "";
            return $"開始可能になりました。\n{place} で {press} インタラクトすると開始します。";
        }
    }

    private static string Shorten(string id)
        => string.IsNullOrEmpty(id) ? id : id;
}
