using UnityEngine;
using TMPro;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine.SocialPlatforms.GameCenter;

public class DialogManager : MonoBehaviour
{
    //[SerializeField] Camera mainCamera;     // ���C���J�������
    //[SerializeField] Camera dialogCamera;   // ��b�p�J�������

    public static DialogManager Instance;

    [Header("UI")]
    [SerializeField] GameObject dialogUI;           // ��bUI�S��
    [SerializeField] TextMeshProUGUI nameText;      // NPC�̖��O
    [SerializeField] TextMeshProUGUI dialogText;    // �Z���t�\��

    [Header("Camera")]
    [SerializeField] Camera mainCamera;             // ���C���J����
    [SerializeField] Camera dialogCamera;           // ��b�p�J����

    [Header("Camera Offset Settings")]
    [SerializeField] Vector3 cameraOffset = new Vector3(0, 5, -5);  // ���ՃJ�����̃I�t�Z�b�g
    [SerializeField] float lookHeight = 1.5f;    // �n�_�̍���

    private Queue<string> sentences;                // �Z���t�̃L���[
    private Transform currentPlayer;                // �v���C���[�̎p�����
    private Transform currentNPC;                   // NPC�̈ʒu���
    private bool isDialogActive = false;            // ��b���

    private void Awake()
    {
        // �V���O���g��
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        sentences = new Queue<string>();
    }
    private void Start()
    {
        Debug.Log("DialogManager Start() �Ăяo��");
    }

    private void Update()
    {
        //if (isDialogActive && Input.GetKeyDown(KeyCode.Return))
        //{ 
        //    DisplayNextSentence();
        //}

        Debug.Log("Update�Ăяo����");

        if (Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log("Enter�L�[�������ꂽ");

            if(isDialogActive)
            {
                Debug.Log("�� ��b���FDisplayNextSentence ���Ăт܂�");
                DisplayNextSentence();
            }
            else
            {
                Debug.Log("�� ��b���ł͂Ȃ�");
            }
        }
    }

    public void StartDialog(Dialog dialog, Transform player, Transform npc)
    {
        // �����̃v���C���[�ANPC������
        currentPlayer = player;
        currentNPC = npc;

        // UI��������Ԃɂ��A��b���ɂ���
        dialogUI.SetActive(true);
        isDialogActive = true;

        nameText.text = dialog.npcName;
        sentences.Clear();

        // ��������Z���t�L���[�ɒǉ�
        foreach(string sentence in dialog.sentences)
        {
            sentences.Enqueue(sentence);
        }

        // �L�����N�^�[�����������킹�ɂ���
        FaceEachOther(player, npc);

        // �J�����؂�ւ�
        if(mainCamera != null) mainCamera.enabled = false;
        if(dialogCamera != null)
        {
            dialogCamera.enabled = true;
            PositionDialogCamera(player.position, npc.position);
        }

        // �ړ����b�N
        if(player.TryGetComponent(out ThirdPersonController move))
        {
            move.canMove = false;
        }

        // �������\��
        DisplayNextSentence();
    }

    public void DisplayNextSentence()
    {

        Debug.Log("DisplayNextSentence���Ăяo����܂���");

        Debug.Log("�Z���t�L���[ : " + sentences.Count);

        if (sentences.Count == 0)        // �Z���t�L���[��0�̂Ƃ�
        {

            Debug.Log("�� �Z���t�L���[��0");

            // ��b�I��
            EndDialog();
            return;
        }

        string sentence = sentences.Dequeue();
        dialogText.text = sentence;
    }

    public void EndDialog()
    {
        // ��b�pUI���\���ɁA���b����
        dialogUI.SetActive(false);
        isDialogActive = false;

        // �v���C���[�̈ړ����b�N������
        if(currentPlayer != null && currentPlayer.TryGetComponent(out ThirdPersonController move))
        {
            move.canMove = true;
        }

        // �J�����؂�ւ�
        if (mainCamera != null) mainCamera.enabled = true;
        if (dialogCamera != null) dialogCamera.enabled = false;

    }

    private void FaceEachOther(Transform player, Transform npc)
    {
        // �v���C���[��NPC����������
        Vector3 lookAtNPC = new Vector3(npc.position.x, player.position.y, npc.position.z);
        player.LookAt(lookAtNPC);

        // NPC���v���C���[����������
        Vector3 lookAtPlayer = new Vector3(player.position.x, npc.position.y, player.position.z);
        npc.LookAt(lookAtPlayer);
    }

    private void PositionDialogCamera(Vector3 playerPos, Vector3 npcPos)
    {
        Vector3 center = (playerPos + npcPos) / 2f;
        Vector3 offsetPos = center + cameraOffset;

        dialogCamera.transform.position = offsetPos;

        // 2�l�̒��ԓ_�������낷�悤�ɂ���
        Vector3 lookTarget = center + Vector3.up * lookHeight;
        dialogCamera.transform.LookAt(lookTarget);
    }
}
