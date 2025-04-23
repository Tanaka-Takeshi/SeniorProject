using UnityEngine;
using UnityEngine.UI;

public class Interact : MonoBehaviour
{
    #region 変数
    [SerializeField] GameObject enterUI;        // EnterキーのUI
                public Dialog dialogData;       // セリフデータ

    private bool isPlayerInRange = false;       // Player接触状態
    private bool hasInteracted = false;         // 会話の完了状態

    private Transform playerTransform;          // プレイヤーの姿勢情報
    #endregion

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        /*
         * ?.演算子 : メンバがNullの場合動作しない
         * for(メンバ != null){}と同じ動作
         */
        enterUI?.SetActive(false);          // 初期は非表示設定に
    }

    private void Update()
    {
        // 会話中なら更新処理を行わない
        if (DialogManager.Instance != null && DialogManager.Instance.IsDialogActive())
            return;

        // 一度会話したら処理しない
        if (hasInteracted)
            return;
        
        // Player接触中で会話状態がTrueでEnterキーが押されたとき
        if(isPlayerInRange && Input.GetKeyDown(KeyCode.Return))
        {
            if(playerTransform != null)
            {
                DialogManager.Instance.StartDialog(dialogData, playerTransform, this.transform);  // 会話を開始する
                enterUI?.SetActive(false);
                hasInteracted = true;
            }

        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 接触したのがPlayerなら
        if(other.CompareTag("Player"))
        {
            playerTransform = other.transform;
            isPlayerInRange = true;     // Player接触判定をtrueに
            enterUI?.SetActive(true);   // EnterUIを可視状態に
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 接触しなくなったのがPlayerなら
        if(other.CompareTag("Player"))
        {
            isPlayerInRange = false;   // Player接触判定をfalseに
            enterUI?.SetActive(false); // enterUIを不可視状態に

            // 領域から出たら再度会話可能にリセット
            hasInteracted = false;
        }
    }
}
