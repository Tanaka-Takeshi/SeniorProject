using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] GameObject player;

    Vector3 currentPos; // 現在のカメラ位置
    Vector3 pastPos;    // 過去のカメラ位置

    Vector3 diff;       // 移動距離

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        // 最初のプレイヤー位置の取得
        pastPos = player.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        // ------カメラの移動------

        // プレイヤーの現在地の取得
        currentPos = player.transform.position;
        diff = currentPos - pastPos;
        // カメラをプレイヤーの移動差分だけ動かす
        transform.position = Vector3.Lerp(transform.position, transform.position + diff, 1.0f);
        pastPos = currentPos;

        // ------カメラの回転------

        // マウスの移動量を取得
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y");

        // X方向に一定量移動していれば横回転
        if(Mathf.Abs(mx) > 0.01f)
        {
            // 回転軸はワールド座標のY軸
            transform.RotateAround(player.transform.position, Vector3.up, mx);
        }

        // Y軸方向に一定量移動していれば縦回転
        if(Mathf.Abs(my) > 0.01f)
        {
            // 回転軸はカメラ自身のX軸
            transform.RotateAround(player.transform.position, transform.right, -my);
        }
    }
}
