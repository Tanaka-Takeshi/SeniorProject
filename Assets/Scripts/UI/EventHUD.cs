// Assets/Scripts/UI/EventHUD.cs
using UnityEngine;
using Game.Events;  // ← EventSignals がある名前空間に合わせてください

/// <summary>
/// EventSignals を購読して、HUDController にタイトル・本文を流す最小ブリッジ。
/// - Available/Started で Show
/// - Completed/Failed/Expired で Hide
/// </summary>
public class EventHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private HUDController hud;     // Panel_HUD 上の HUDController
    [SerializeField] private string mainTag = "Main"; // タイトルラベル用（任意）

    // “単純モード”：最後に来たIDだけ覚える（EventManager に依存しない）
    private string _lastAvailableId;
    private string _lastStartedId;

    private void OnEnable()
    {
        EventSignals.OnAvailable += OnAvailable;
        EventSignals.OnStarted += OnStarted;
        EventSignals.OnCompleted += OnCompleted;
        EventSignals.OnFailed += OnFailed;
        EventSignals.OnExpired += OnExpired;

        // 既に HUD が割当済みであれば、起動時は消しておく
        if (hud != null) hud.Hide(instant: true);
    }

    private void OnDisable()
    {
        EventSignals.OnAvailable -= OnAvailable;
        EventSignals.OnStarted -= OnStarted;
        EventSignals.OnCompleted -= OnCompleted;
        EventSignals.OnFailed -= OnFailed;
        EventSignals.OnExpired -= OnExpired;
    }

    private void OnAvailable(string id)
    {
        _lastAvailableId = id;
        if (hud == null) return;

        var title = $"[{mainTag}] {id}";
        var body = "Available：Eキーで開始できます。";
        hud.Show(title, body); // まずは出して存在を知らせる
    }

    private void OnStarted(string id)
    {
        _lastStartedId = id;
        if (hud == null) return;

        var title = $"[{mainTag}] {id}";
        var body = "InProgress：進行中。";
        hud.Show(title, body); // 進行開始で内容更新
    }

    private void OnCompleted(string id)
    {
        if (hud == null) return;
        // 進行中のものが終わったら閉じる（最小ポリシー）
        if (id == _lastStartedId) hud.Hide();
    }

    private void OnFailed(string id, FailedReason reason)
    {
        if (hud == null) return;
        if (id == _lastStartedId)
        {
            hud.Show($"[{mainTag}] {id}", $"Failed：{reason}", instant: true);
            hud.Hide(); // 最小実装では失敗表示→すぐ閉じる（好みに応じて保持も可）
        }
    }

    private void OnExpired(string id)
    {
        if (hud == null) return;
        if (id == _lastStartedId || id == _lastAvailableId)
            hud.Hide();
    }
}
