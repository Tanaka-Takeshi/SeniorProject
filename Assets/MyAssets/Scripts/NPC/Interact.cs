using UnityEngine;

public class Interact : MonoBehaviour
{
    [SerializeField] GameObject enterUI;    // Enter�L�[��UI

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // �����͔�\���ݒ��
        if (enterUI != null)
            enterUI.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player") && enterUI != null)
        {
            enterUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Player") && enterUI != null)
        {
            enterUI.SetActive(false);
        }
    }
}
