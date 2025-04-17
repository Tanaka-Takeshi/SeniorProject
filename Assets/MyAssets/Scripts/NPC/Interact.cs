using UnityEngine;

public class Interact : MonoBehaviour
{
    [SerializeField] GameObject enterUI;    // EnterƒL[‚ÌUI

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // ‰Šú‚Í”ñ•\¦İ’è‚É
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
