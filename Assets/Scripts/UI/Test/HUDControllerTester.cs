using UnityEngine;

public class HUDControllerTester : MonoBehaviour
{
    [SerializeField] private HUDController hud;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            hud.Show("クエスト開始", "村長の家に向かえ。");
        if (Input.GetKeyDown(KeyCode.Alpha2))
            hud.SetBody("狼を3体討伐せよ。");
        if (Input.GetKeyDown(KeyCode.Alpha3))
            hud.SwapTitleAndBody();
        if (Input.GetKeyDown(KeyCode.Alpha0))
            hud.Hide();
    }
}
