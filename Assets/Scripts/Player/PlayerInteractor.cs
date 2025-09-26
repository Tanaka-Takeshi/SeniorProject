using UnityEngine;

[DefaultExecutionOrder(-50)]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Detection")]
    public float detectRadius = 2.5f;
    public LayerMask interactableMask = ~0;
    public bool useSphereOverlap = true;

    [Header("UI")]
    public InteractPromptUI promptUI;

    [Header("Input System Action Name (New Input)")]
    public string actionName_Interact = "Submit";

    private IInteractable current;

#if ENABLE_INPUT_SYSTEM
    void Update()
    {
        UpdateTarget();

        if (current != null && current.CanInteract())
            promptUI?.Show(current.GetPromptText());
        else
            promptUI?.Hide();

        bool pressed = false;
        var kbd = UnityEngine.InputSystem.Keyboard.current;
        if (kbd != null) pressed |= kbd.enterKey.wasPressedThisFrame;

        var gp = UnityEngine.InputSystem.Gamepad.current;
        if (gp != null) pressed |= gp.buttonSouth.wasPressedThisFrame;

        if (pressed && current != null && current.CanInteract())
            current.Interact(gameObject);
    }
#else
    void Update()
    {
        UpdateTarget();

        if (current != null && current.CanInteract())
            promptUI?.Show(current.GetPromptText());
        else
            promptUI?.Hide();

        if (current != null && current.CanInteract() &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetButtonDown("Submit")))
        {
            current.Interact(gameObject);
        }
    }
#endif

    void UpdateTarget()
    {
        IInteractable best = null;
        float bestScore = float.MaxValue;

        if (useSphereOverlap)
        {
            var hits = Physics.OverlapSphere(
                transform.position,
                detectRadius,
                interactableMask,
                QueryTriggerInteraction.Collide
            );

            foreach (var c in hits)
            {
                var it = c.GetComponentInParent<IInteractable>();
                if (it == null || !it.CanInteract()) continue;

                float d = Vector3.SqrMagnitude(it.GetTransform().position - transform.position);
                if (d < bestScore) { best = it; bestScore = d; }
            }
        }

        // ★ 視線チェックは一旦なし（まず動かす）
        current = best;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
#endif
}
