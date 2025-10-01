// Assets/Scripts/Dev/DevNpcDialogueSwitcher.cs
using UnityEngine;

public class DevNpcDialogueSwitcher : MonoBehaviour
{
    public NPCInteractable target;          // NPCInteractable 参照
    public string questId = "Quest.Town01";

    [Header("IDs")]
    public string idBeforeStart = "Talk_Offer";
    public string idWhenActive = "Talk_Report";
    public string idWhenDone = "Talk_Thanks"; // 使わないなら空でOK

    void Reset()
    {
        if (!target) target = GetComponent<NPCInteractable>();
    }

    void Update()
    {
        if (!target || QuestService.Instance == null) return;

        var qs = QuestService.Instance;
        var st = qs.GetState(questId);

        switch (st)
        {
            case QuestState.Inactive:
                target.dialogueId = idBeforeStart;
                break;
            case QuestState.Active:
                target.dialogueId = idWhenActive;
                break;
            case QuestState.Completed:
                if (!string.IsNullOrEmpty(idWhenDone))
                    target.dialogueId = idWhenDone;
                break;
        }
    }
}
