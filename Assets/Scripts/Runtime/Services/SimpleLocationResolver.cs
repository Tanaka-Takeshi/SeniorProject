using Game.Events;
using UnityEngine;

namespace Game.Runtime
{
    public class SimpleLocationResolver : MonoBehaviour, ILocationResolver
    {
        [SerializeField] string currentAreaId = "";
        public void SetArea(string areaId) => currentAreaId = areaId;
        public bool IsSatisfied(LocationRef loc)
        {
            if(loc.kind == LocationKind.AreaId) return loc.id == currentAreaId;
            if(loc.kind == LocationKind.WorldPos) return true;
            if(loc.kind == LocationKind.WaypointId) return true;
            return false;
        }
    }
}
