using UnityEngine;

public interface IInteractable
{
    Transform GetTransform();           // フォーカス用
    string GetPromptText();             // UIに出す文言
    bool CanInteract();                 // 有効/無効
    void Interact(GameObject interactor); // 実行（プレイヤーを渡す）
}
