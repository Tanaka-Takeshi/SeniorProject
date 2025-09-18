using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class ToastUI : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI label;
    public CanvasGroup group;

    [Header("Timing")]
    [SerializeField] float showSec = 1.8f;
    [SerializeField] float fadeSec = 0.35f;

    Coroutine co;

    void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
        if (!label)
        {
            label = GetComponentInChildren<TextMeshProUGUI>();
            if (!label) Debug.LogWarning("[ToastUI] TMP label が未割り当てです。子に TextMeshProUGUI を置いて割り当ててください。", this);
        }
        // 初期は非表示
        if (group) group.alpha = 0f;
        gameObject.SetActive(true); // Canvas 内で常時有効、alphaで制御
    }

    public void Show(string message)
    {
        if (!label || !group)
        {
            Debug.LogWarning("[ToastUI] 参照が未設定です (label / group)。", this);
            return;
        }
        label.text = message;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(CoToast());
    }

    IEnumerator CoToast()
    {
        // フェードイン
        float t = 0f;
        while (t < fadeSec)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(0f, 1f, t / fadeSec);
            yield return null;
        }
        group.alpha = 1f;

        // 表示維持
        float hold = 0f;
        while (hold < showSec)
        {
            hold += Time.unscaledDeltaTime;
            yield return null;
        }

        // フェードアウト
        t = 0f;
        while (t < fadeSec)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(1f, 0f, t / fadeSec);
            yield return null;
        }
        group.alpha = 0f;
        co = null;
    }
}
