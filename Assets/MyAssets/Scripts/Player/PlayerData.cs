using TMPro.Examples;
using UnityEngine;

public class PlayerData : MonoBehaviour
{
    public string CurrentZone { get; private set; } = "None";
    private string lastLoggedZone = "";

    private void OnTriggerEnter(Collider other)
    {
        var zone = other.GetComponent<AreaZone>();
        if (zone != null)
            CurrentZone = zone.zoneName;

        // Debug.Log($"[AreaTracker] Entered Zone: {CurrentZone}");
    }

    private void OnTriggerExit(Collider other)
    {
        var zone = other.GetComponent<AreaZone>();
        if (zone != null && zone.zoneName == CurrentZone)
        {
            CurrentZone = "None";

            // Debug.Log($"[AreaTracker] Entered Zone: {CurrentZone}");
        }
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // ƒ][ƒ“‚ª•Ï‚í‚Á‚½‚Æ‚«‚¾‚¯ƒƒO‚ğo—Í
        if (CurrentZone != lastLoggedZone)
        {
            Debug.Log($"[AreaTracker] Current Zone: {CurrentZone}");
            lastLoggedZone = CurrentZone;
        }
    }
}
