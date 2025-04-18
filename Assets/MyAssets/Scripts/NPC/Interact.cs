using UnityEngine;

public class Interact : MonoBehaviour
{
    [SerializeField] GameObject enterUI;    // EnterキーのUI

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // 初期は非表示設定に
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
