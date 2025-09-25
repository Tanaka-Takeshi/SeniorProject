using Game.Events;
using UnityEngine;

namespace Game.Runtime
{
    public class SimpleLocationResolver : MonoBehaviour, ILocationResolver
    {
        [SerializeField] private string currentAreaId = "";

        public void SetArea(string areaId) => currentAreaId = areaId;

        // ★追加：外部から現在のエリアIDを参照できる
        public string CurrentAreaId => currentAreaId;

        public bool IsSatisfied(LocationRef loc)
        {
            if (loc.kind == LocationKind.AreaId) return loc.id == currentAreaId;
            if (loc.kind == LocationKind.WorldPos) return true;
            if (loc.kind == LocationKind.WaypointId) return true;
            return false;
        }
    }
}
