using UnityEngine;
using Game.Runtime;

public class SetAreaOnStart : MonoBehaviour
{
    [SerializeField] private SimpleLocationResolver locator;
    [SerializeField] private string areaId = "TestArea";

    void Start()
    {
        if (locator) locator.SetArea(areaId);
    }
}