using UnityEngine;
using TMPro;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine.SocialPlatforms.GameCenter;

public class DialogManager : MonoBehaviour
{
    //[SerializeField] Camera mainCamera;     // メインカメラ情報
    //[SerializeField] Camera dialogCamera;   // 会話用カメラ情報

    public static DialogManager Instance;

    [Header("UI")]
    [SerializeField] GameObject dialogUI;           // 会話UI全体
    [SerializeField] TextMeshProUGUI nameText;      // NPCの名前
    [SerializeField] TextMeshProUGUI dialogText;    // セリフ表示

    [Header("Camera")]
    [SerializeField] Camera mainCamera;             // メインカメラ
    [SerializeField] Camera dialogCamera;           // 会話用カメラ

    [Header("Camera Offset Settings")]
    [SerializeField] Vector3 cameraOffset = new Vector3(0, 5, -5);  // 俯瞰カメラのオフセット
    [SerializeField] float lookHeight = 1.5f;    // 始点の高さ

    private Queue<string> sentences;                // セリフのキュー
    private Transform currentPlayer;                // プレイヤーの姿勢状態
    private Transform currentNPC;                   // NPCの位置状態
    private bool isDialogActive = false;            // 会話状態

    private void Awake()
    {
        // シングルトン
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        sentences = new Queue<string>();
    }
    private void Start()
    {
        Debug.Log("DialogManager Start() 呼び出し");
    }

    private void Update()
    {
        //if (isDialogActive && Input.GetKeyDown(KeyCode.Return))
        //{ 
        //    DisplayNextSentence();
        //}

        Debug.Log("Update呼び出し中");

        if (Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log("Enterキーが押された");

            if(isDialogActive)
            {
                Debug.Log("→ 会話中：DisplayNextSentence を呼びます");
                DisplayNextSentence();
            }
            else
            {
                Debug.Log("→ 会話中ではない");
            }
        }
    }

    public void StartDialog(Dialog dialog, Transform player, Transform npc)
    {
        // 引数のプレイヤー、NPC情報を代入
        currentPlayer = player;
        currentNPC = npc;

        // UIを仮死状態にし、会話中にする
        dialogUI.SetActive(true);
        isDialogActive = true;

        nameText.text = dialog.npcName;
        sentences.Clear();

        // 文字列をセリフキューに追加
        foreach(string sentence in dialog.sentences)
        {
            sentences.Enqueue(sentence);
        }

        // キャラクターを向かい合わせにする
        FaceEachOther(player, npc);

        // カメラ切り替え
        if(mainCamera != null) mainCamera.enabled = false;
        if(dialogCamera != null)
        {
            dialogCamera.enabled = true;
            PositionDialogCamera(player.position, npc.position);
        }

        // 移動ロック
        if(player.TryGetComponent(out ThirdPersonController move))
        {
            move.canMove = false;
        }

        // 文字列を表示
        DisplayNextSentence();
    }

    public void DisplayNextSentence()
    {

        Debug.Log("DisplayNextSentenceが呼び出されました");

        Debug.Log("セリフキュー : " + sentences.Count);

        if (sentences.Count == 0)        // セリフキューが0のとき
        {

            Debug.Log("→ セリフキューが0");

            // 会話終了
            EndDialog();
            return;
        }

        string sentence = sentences.Dequeue();
        dialogText.text = sentence;
    }

    public void EndDialog()
    {
        // 会話用UIを非表示に、非会話中に
        dialogUI.SetActive(false);
        isDialogActive = false;

        // プレイヤーの移動ロックを解除
        if(currentPlayer != null && currentPlayer.TryGetComponent(out ThirdPersonController move))
        {
            move.canMove = true;
        }

        // カメラ切り替え
        if (mainCamera != null) mainCamera.enabled = true;
        if (dialogCamera != null) dialogCamera.enabled = false;

    }

    private void FaceEachOther(Transform player, Transform npc)
    {
        // プレイヤーがNPCを向く処理
        Vector3 lookAtNPC = new Vector3(npc.position.x, player.position.y, npc.position.z);
        player.LookAt(lookAtNPC);

        // NPCがプレイヤーを向く処理
        Vector3 lookAtPlayer = new Vector3(player.position.x, npc.position.y, player.position.z);
        npc.LookAt(lookAtPlayer);
    }

    private void PositionDialogCamera(Vector3 playerPos, Vector3 npcPos)
    {
        Vector3 center = (playerPos + npcPos) / 2f;
        Vector3 offsetPos = center + cameraOffset;

        dialogCamera.transform.position = offsetPos;

        // 2人の中間点を見下ろすようにする
        Vector3 lookTarget = center + Vector3.up * lookHeight;
        dialogCamera.transform.LookAt(lookTarget);
    }
}
