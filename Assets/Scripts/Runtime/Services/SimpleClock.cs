// Assets/Scripts/Runtime/SimpleClock.cs
using UnityEngine;
using Game.Config;

namespace Game.Runtime
{
    [DefaultExecutionOrder(-500)] // ★ 早めに初期化（任意だがおすすめ）
    public class SimpleClock : MonoBehaviour, IClock
    {
        [SerializeField] GlobalSettings settings;

        // 分=秒 換算の“ゲーム内秒”
        public float NowGameSeconds { get; private set; } = 0f;

        void Awake()
        {
            // ★ 明示的に 0 始動
            NowGameSeconds = 0f;
        }

        void Update()
        {
            if (settings == null) return;

            // 実装ポリシー：1フレームで1ゲーム秒進める（= 分=秒）
            // ※実時間と連動させたいなら Time.deltaTime を係数にする
            NowGameSeconds += Time.deltaTime;

            // 1日をループ
            if (NowGameSeconds >= settings.dayLengthSeconds)
                NowGameSeconds = 0f;
            else if (NowGameSeconds < 0f)
                NowGameSeconds = 0f;
        }

        /// <summary>
        /// 時刻を“絶対値”で設定（テスト/デバッグ用）
        /// 例: Jump(HH*60+MM)
        /// </summary>
        public void Jump(float absoluteSeconds)
        {
            NowGameSeconds = Mathf.Max(0f, absoluteSeconds);
            if (settings != null && NowGameSeconds >= settings.dayLengthSeconds)
                NowGameSeconds %= settings.dayLengthSeconds;
        }
    }
}
