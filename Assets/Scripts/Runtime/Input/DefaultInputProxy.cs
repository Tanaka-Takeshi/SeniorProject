using UnityEngine;
using UnityEngine.InputSystem; // 新InputSystem
using Game.Config;

namespace Game.Runtime
{
    /// <summary>
    /// Interact入力を取得する既定の実装。
    /// GlobalSettings.interactAction を優先し、未設定なら E キー（または defaultIntreractKey）で代替。
    /// </summary>
    public sealed class DefaultInputProxy : MonoBehaviour, IInputProxy
    {
        [SerializeField] private GlobalSettings settings;

        private InputAction _action; // settings.interactAction から取得

        private void OnEnable()
        {
            // InputActionReference があれば有効化
            if (settings != null && settings.interactAction != null)
            {
                _action = settings.interactAction.action;
                if (_action != null) _action.Enable();
            }
        }

        private void OnDisable()
        {
            if (_action != null) _action.Disable();
        }

        public bool StartPressedThisFrame()
        {
            // 1) InputAction があれば優先
            if (_action != null && _action.WasPressedThisFrame())
                return true;

            // 2) フォールバック: 新InputSystemのKeyboard
            if (Keyboard.current != null)
            {
                // GlobalSettings にキーがあればそれを優先
                var key = settings != null ? settings.fallbackInteractKey : KeyCode.E;
                // Keyboard.current での任意キー対応：Eだけ見る（KeyCode を Keyboardへマップするのは複雑なのでE固定）
                if (key == KeyCode.E && Keyboard.current.eKey.wasPressedThisFrame) return true;
            }

            // 3) さらにフォールバック: 旧 Input.GetKeyDown（両対応プロジェクト向け）
            var fallbackKey = settings != null ? settings.fallbackInteractKey : KeyCode.E;
            if (Input.GetKeyDown(fallbackKey)) return true;

            return false;
        }
    }
}
