using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineCamera))]
public class ConversationZoomController : MonoBehaviour
{
    [Header("Refs")]
    public CinemachineTargetGroup targetGroup;       // Conv_TargetGroup を割り当て
    public CinemachineCamera vcam;                   // VCam_Conversation を割り当て

    [Header("Distance Mapping (meters)")]
    public Vector2 distanceRange = new Vector2(2.5f, 6f); // 近距離〜遠距離のカメラ距離
    public Vector2 sizeRange = new Vector2(0.5f, 3f); // BoundingBox の想定最小〜最大“サイズ”
    public float zoomLerpSpeed = 5f;

    void Reset()
    {
        vcam = GetComponent<CinemachineCamera>();
    }

    void LateUpdate()
    {
        if (!vcam || !targetGroup) return;

        // グループの境界（Bounds）から“画面に収めたい大きさ”を取得
        Bounds bb = targetGroup.BoundingBox; // CM3 で提供されるプロパティ
        // 横幅と高さの大きい方を採用（パースありのため概算で十分）
        float size = Mathf.Max(bb.size.x, bb.size.y);

        // sizeRange を 0..1 に正規化し、distanceRange にマップ
        float t = Mathf.InverseLerp(sizeRange.x, sizeRange.y, size);
        float desired = Mathf.Lerp(distanceRange.x, distanceRange.y, t);

        // CinemachineFollow の FollowOffset.z（距離）をスムーズに補正
        var follow = vcam.GetComponent<CinemachineFollow>();
        if (!follow) return;

        Vector3 off = follow.FollowOffset;
        float current = -off.z;                 // 手前がマイナスなので符号を揃える
        float next = Mathf.Lerp(current, desired, Time.deltaTime * zoomLerpSpeed);
        off.z = -next;
        follow.FollowOffset = off;
    }
}
