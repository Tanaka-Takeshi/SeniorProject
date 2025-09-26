using UnityEngine;
using Unity.Cinemachine;

public class CameraDirector : MonoBehaviour
{
    [Header("Cinemachine Cameras")]
    public CinemachineCamera vcamFree;         // 通常操作用
    public CinemachineCamera vcamConversation; // 会話用

    [Header("Priority Settings")]
    public int freePriority = 10;
    public int convoPriority = 100;

    /// <summary>
    /// 会話カメラを有効化し、必要なら LookAt/Follow を差し替え
    /// </summary>
    public void PushConversationCamera(Transform npcHeadOrGroup)
    {
        if (vcamConversation)
        {
            if (npcHeadOrGroup)
            {
                vcamConversation.Follow = npcHeadOrGroup;
                vcamConversation.LookAt = npcHeadOrGroup;
            }
            vcamConversation.Priority = convoPriority;
        }
        if (vcamFree) vcamFree.Priority = 0;
    }

    /// <summary>
    /// 通常カメラに戻す
    /// </summary>
    public void PopConversationCamera()
    {
        if (vcamConversation) vcamConversation.Priority = 0;
        if (vcamFree) vcamFree.Priority = freePriority;
    }
}
