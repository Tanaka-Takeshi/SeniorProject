// Assets/Scripts/Runtime/ScenarioBootstrap.cs
using UnityEngine;
using Game.Config;      // GlobalSettings
using Game.Runtime;     // IClock / ILocationResolver / IInputProxy / EventManager

namespace Game.Runtime
{
    /// <summary>
    /// シーン起動時に EventManager へ各依存（Clock / Locator / Input / Settings）を注入する初期化ブートストラップ。
    /// - 可能ならインスペクタで参照を設定
    /// - 未設定なら FindFirstObjectByType / FindAnyObjectByType でフォールバック
    /// - すべてインターフェイス版 Inject を使用
    /// </summary>
    public sealed class ScenarioBootstrap : MonoBehaviour
    {
        [Header("References (optional: assign in Inspector)")]
        [SerializeField] private EventManager eventManager;

        [SerializeField] private MonoBehaviour clockBehaviour;    // implements IClock
        [SerializeField] private MonoBehaviour locatorBehaviour;  // implements ILocationResolver
        [SerializeField] private MonoBehaviour inputBehaviour;    // implements IInputProxy

        [Header("Config")]
        [SerializeField] private GlobalSettings globalSettings;

        private void Awake()
        {
            // --- Resolve references if missing ---
            eventManager ??= Object.FindFirstObjectByType<EventManager>();
            clockBehaviour ??= FindClockLike();
            locatorBehaviour ??= FindLocatorLike();
            inputBehaviour ??= FindInputLike();
            globalSettings ??= Object.FindFirstObjectByType<GlobalSettings>();

            if (eventManager == null)
            {
                Debug.LogError("[ScenarioBootstrap] EventManager が見つかりません。シーンに配置してください。", this);
                enabled = false;
                return;
            }

            if (globalSettings == null)
            {
                Debug.LogWarning("[ScenarioBootstrap] GlobalSettings が未設定です。", this);
            }

            // --- Cast to interfaces & inject ---
            var clock = clockBehaviour as IClock;
            var locator = locatorBehaviour as ILocationResolver;
            var input = inputBehaviour as IInputProxy;

            if (clock == null || locator == null || input == null)
            {
                Debug.LogError("[ScenarioBootstrap] Clock/Locator/Input のいずれかが正しく設定されていません。", this);
                enabled = false;
                return;
            }

            eventManager.Inject(clock, locator, input, globalSettings);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[ScenarioBootstrap] Inject 完了", this);
#endif
        }

        // --- fallback finders ---
        private MonoBehaviour FindClockLike()
            => Object.FindFirstObjectByType<SimpleClock>();

        private MonoBehaviour FindLocatorLike()
            => Object.FindFirstObjectByType<SimpleLocationResolver>();

        private MonoBehaviour FindInputLike()
        {
            // プロジェクトに応じて TestInputProxy などを候補に
            var test = Object.FindFirstObjectByType<TestInputProxy>();
            if (test != null) return test;

            // 他の実装があればここに追加
            return null;
        }

        private void OnValidate()
        {
            eventManager ??= GetComponent<EventManager>();
            if (globalSettings == null)
            {
                var gs = Object.FindFirstObjectByType<GlobalSettings>();
                if (gs != null) globalSettings = gs;
            }
        }
    }
}
