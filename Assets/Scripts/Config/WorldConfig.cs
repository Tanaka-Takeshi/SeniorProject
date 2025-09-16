using UnityEngine;

namespace Game.Config
{
    public enum WorldMode { OpenWorld, SceneSwitching }

    [CreateAssetMenu(menuName = "Game/Config/WorldConfig")]
    public class WorldConfig : ScriptableObject
    {
        public WorldMode worldMode = WorldMode.SceneSwitching;
        public bool timeRunsDuringSceneLoad = false;
        public float sceneSettleGraceSec = 0.25f;
    }
}

