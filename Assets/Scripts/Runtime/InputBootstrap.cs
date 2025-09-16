using UnityEngine;
using UnityEngine.InputSystem;
using Game.Config;

namespace Game.Runtime
{
    public class InputBootstrap : MonoBehaviour
    {
        [SerializeField] private GlobalSettings settings;

        private void Awake()
        {
            var action = settings?.interactAction?.action;
            if (action == null) return;
            if (!action.enabled) action.Enable();

            // 既定E（初回のみ）。既にバインドがあれば何もしない。
            if (action.bindings.Count == 0)
                action.AddBinding("<Keyboard>/e");
        }
    }
}
