using UnityEngine;

public class ToastDebug : MonoBehaviour
{
    public ToastUI toast;
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
            toast.Show("トーストのテスト");
    }
}
