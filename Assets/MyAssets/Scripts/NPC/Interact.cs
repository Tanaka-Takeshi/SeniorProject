using UnityEngine;
using UnityEngine.UI;

public class Interact : MonoBehaviour
{
    [SerializeField] GameObject enterUI;        // Enter�L�[��UI
    public Dialog dialogData;         // �Z���t�f�[�^

    private bool isPlayerInRange = false;       // Player�ڐG���

    private Transform playerTransform;          // �v���C���[�̎p�����

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        /*
         * ?.���Z�q : �����o��Null�̏ꍇ���삵�Ȃ�
         * for(�����o != null){}�Ɠ�������
         */
        enterUI?.SetActive(false);          // �����͔�\���ݒ��
        //dialogCacnvas?.SetActive(false);    // �����͔�\���ݒ��
        //dialogCamera.enabled = false;       // ��b�p�J�����͏�����Ԃ�OFF�ݒ��
        //mainCamera.enabled = true;          // ���C���J�����͏�����Ԃ�ON�ݒ��
    }

    private void Update()
    {
        // Player�ڐG���ŉ�b��Ԃ�True��Enter�L�[�������ꂽ�Ƃ�
        if(isPlayerInRange && Input.GetKeyDown(KeyCode.Return))
        {
            if(playerTransform != null)
            {
                DialogManager.Instance.StartDialog(dialogData, playerTransform, this.transform);  // ��b���J�n����
                enterUI?.SetActive(false);
            }

        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // �ڐG�����̂�Player�Ȃ�
        if(other.CompareTag("Player"))
        {
            playerTransform = other.transform;
            isPlayerInRange = true;     // Player�ڐG�����true��
            enterUI?.SetActive(true);   // EnterUI������Ԃ�
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // �ڐG���Ȃ��Ȃ����̂�Player�Ȃ�
        if(other.CompareTag("Player"))
        {
            isPlayerInRange = false;   // Player�ڐG�����false��
            enterUI?.SetActive(false); // enterUI��s����Ԃ�
        }
    }
}
