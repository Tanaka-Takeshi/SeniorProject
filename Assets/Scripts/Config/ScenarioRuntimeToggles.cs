using UnityEngine;

namespace Game.Config
{
    [CreateAssetMenu(menuName = "Game/Config/Scenario Runtime Toggles")]
    public class ScenarioRuntimeToggles : ScriptableObject
    {
        [Header("HUD / Tracker")]
        public bool showAvailableInHUD = true;   // AvailableもHUDで見せる
        [Min(0)] public int emptyGraceFrames = 2; // 候補ゼロがNフレ連続で隠す

        [Tooltip("Availableの並びでMainを優先して選ぶ")]
        public bool preferMainOnAvailable = true;

        [Header("Start 入力ポリシー")]
        [Tooltip("インタラクト開始に位置条件を求める（指定エリアに居る時だけE開始）")]
        public bool interactNeedsLocation = true;

        [Header("Toast (任意)")]
        public bool enableToasts = true;
        [Min(1)] public int toastQueueLimit = 8;
    }
}

