using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController2 : MonoBehaviour
{
    [SerializeField] float moveSpeedIn; // プレイヤーの移動速度(入力）

    Rigidbody playerRb;                 // プレイヤーのRigidbody

    Vector3 moveSpeed;                  // プレイヤーの移動速度
    Vector3 currentPos;                 // プレイヤーの現在の位置
    Vector3 pastPos;                    // プレイヤーの過去の位置

    Vector3 delta;                      // プレイヤーの移動量

    Quaternion PplayerRot;              // プレイヤーの進行方向を向くクォータニオン

    float currentAngukarVel;            // 現在の回転角速度
    [SerializeField] float maxAngularVel = Mathf.Infinity; // 最大の回転角速度[deg/s]

    [SerializeField] float smoothTime = 0.1f;   // 進行方向にかかる時間[s]

    float diffAngle;                    // 現在の向きと進行方向の角度
    float rotAngle;                     // 現在の回転する角度

    Quaternion nextRot;                 // 回転量


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerRb = GetComponent<Rigidbody>();
        pastPos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        // ------プレイヤーの移動------

        // カメラに対して正面方向を取得
        Vector3 cameraForward = Vector3.Scale(Camera.main.transform.forward, new Vector3(1, 0, 1)).normalized;
        // カメラに対して右方向を取得
        Vector3 cameraRight = Vector3.Scale(Camera.main.transform.right, new Vector3(1, 0, 1)).normalized;

        // 移動速度を0で初期化する
        moveSpeed = Vector3.zero;

        // 移動入力
        if(Input.GetKey(KeyCode.W))
        {
            moveSpeed = moveSpeedIn * cameraForward;
        }
        if (Input.GetKey(KeyCode.A))
        {
            moveSpeed = -moveSpeedIn * cameraRight;
        }
        if (Input.GetKey(KeyCode.S))
        {
            moveSpeed = -moveSpeedIn * cameraForward;
        }
        if (Input.GetKey(KeyCode.D))
        {
            moveSpeed = moveSpeedIn * cameraRight;
        }

        // 力を加え、移動する
        Move();

        // 慣性を消す
        if(Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.A)
            || Input.GetKeyUp(KeyCode.S) || Input.GetKeyUp(KeyCode.D))
        {
            //playerRb.velocity = Vector3.zero;
            //playerRb.angularVel = Vector3.zero;
        }

        // ------プレイヤーの回転------

        // 現在の位置
        currentPos = transform.position;

        // 移動量計算
        delta = currentPos - pastPos;
        delta.y = 0;

        // 過去の位置の更新
        pastPos = currentPos; ;

        if (delta == Vector3.zero) return;

        PplayerRot = Quaternion.LookRotation(delta, Vector3.up);

        diffAngle = Vector3.Angle(transform.forward, delta);

        // Vector3.SmoothDampはVector3型の値を徐々に変化させる
        // Vector3.SmoothDamp（現在地、目的地、ref 現在の速度、繊維時間、最高速度）
        rotAngle = Mathf.SmoothDampAngle(0, diffAngle, ref currentAngukarVel, smoothTime, maxAngularVel);

        nextRot = Quaternion.RotateTowards(transform.rotation, PplayerRot, rotAngle);

        transform.rotation = nextRot;
    }

    private void Move()
    {
        // playerRb.AddForce(moveSpeed, ForceMode.Force);

        playerRb.linearVelocity = moveSpeed;
    }
}