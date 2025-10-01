// Assets/Scripts/Dev/TestItemPickup.cs
using UnityEngine;

public class TestItemPickup : MonoBehaviour
{
    [Header("Test Item Pickup")]
    [Tooltip("送信する eventId (QuestData.stepEventIds と一致させる)")]
    public string eventId = "Town01.GetApple";

    [Tooltip("押したら入手扱いにするキー")]
    public KeyCode key = KeyCode.G;

    void Update()
    {
        if (Input.GetKeyDown(key))
        {
            // ① 入手フラグをON（インベントリ代替）
            FlagService.Set(GameFlag.HasApple);
            FlagService.Save();

            // ② 見た目の確認用トースト（任意）
            QuestService.Instance?.toastHud?.ShowToast($"アイテム入手：{Readable(eventId)}");

            // ③ クエスト進行へ通知（現在の要求ステップと一致していれば進む）
            EventSignalRouter.Raise(eventId, ConversationSignalKind.Started);

            Debug.Log($"[TestItemPickup] Sent eventId='{eventId}' (HasApple=true)");
        }
    }

    string Readable(string id)
    {
        // "Town01.GetApple" → "GetApple"
        var i = id.LastIndexOf('.');
        return i >= 0 && i < id.Length - 1 ? id.Substring(i + 1) : id;
    }
}
