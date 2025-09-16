namespace Game.Runtime
{
    public class TestInputProxy : UnityEngine.MonoBehaviour, IInputProxy
    {
        bool _Pressed;
        public void PressOnce() => _Pressed = true;     // ŸƒtƒŒ[ƒ€‚Åtrue‚ğ•Ô‚·
        public bool StartPressedThisFrame()
        {
            if (_Pressed) { _Pressed = false; return true; }
            return false;
        }
    }
}