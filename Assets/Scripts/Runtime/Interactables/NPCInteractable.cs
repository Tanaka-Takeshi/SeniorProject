using UnityEngine;

public class NPCInteractable : MonoBehaviour, IInteractable
{
    public enum TalkAnimationMode
    {
        ForceIdle,   // Speed=0でIdleに固定
        TriggerAnim  // Triggerで特定モーションを再生
    }

    [Header("Conversation Settings")]
    public string dialogueId;
    public Transform headTarget;

    [Header("Animation Control")]
    public Animator animator;                  // NPCのAnimator
    public TalkAnimationMode talkAnimMode = TalkAnimationMode.ForceIdle;
    public string speedParam = "Speed";        // ForceIdle時に使うBlendTree用パラメータ
    public string talkTriggerName = "Talk";    // TriggerAnim時に使うトリガー名

    // ========== IInteractable 実装 ==========
    public Transform GetTransform()
    {
        return transform;
    }

    public string GetPromptText()
    {
        return "\nPress Enter to talk"; // プロンプトに出すテキスト（自由に変えてOK）
    }

    public bool CanInteract()
    {
        return true; // 今は常に可能。条件を付けたいならここに判定を書く
    }

    // ======================================

    public void Interact(GameObject interactor)
    {
        if (animator && animator.runtimeAnimatorController != null)
        {
            switch (talkAnimMode)
            {
                case TalkAnimationMode.ForceIdle:
                    animator.SetFloat(speedParam, 0f); // Idle固定
                    break;

                case TalkAnimationMode.TriggerAnim:
                    if (!string.IsNullOrEmpty(talkTriggerName) &&
                        HasParameter(animator, talkTriggerName, AnimatorControllerParameterType.Trigger))
                    {
                        animator.SetTrigger(talkTriggerName);
                    }
                    else
                    {
                        Debug.LogWarning($"[NPCInteractable] Trigger '{talkTriggerName}' not found on {animator.name}");
                    }
                    break;
            }
        }

        // 会話開始
        var target = headTarget ? headTarget : transform;
        DialogueManager.Instance.StartFromNpc(target, dialogueId);
    }

    private bool HasParameter(Animator anim, string paramName, AnimatorControllerParameterType type)
    {
        foreach (var p in anim.parameters)
        {
            if (p.type == type && p.name == paramName) return true;
        }
        return false;
    }
}
