using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using System.Diagnostics;
using System.Collections.Generic;

#pragma warning disable 0414
public class HUDController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text titleTMP;
    [SerializeField] private TMP_Text bodyTMP;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Show/Hide Options")]
    [SerializeField] private bool startHidden = false;
    [SerializeField] private float fadeSec = 0.25f;

    [Header("Guard & Debug")]
    [Tooltip("true にすると Owner 以外からの SetTitle/SetBody を無視します")]
    [SerializeField] private bool enableOwnerGuard = true;
    [Tooltip("ログを詳細表示（呼び出しスタックも）")]
    [SerializeField] private bool verboseLog = false;

    [Header("Optional UI (Progress & Toast)")]
    [SerializeField] Slider progressBar;                // 任意: 0..1
    [SerializeField] CanvasGroup toastGroup;            // 任意: フェード用
    [SerializeField] TMP_Text toastText;                // 任意: 文言

    [SerializeField] float toastFadeIn = 0.12f;
    [SerializeField] float toastShow = 1.6f;
    [SerializeField] float toastFadeOut = 0.25f;

    float toastTimer = 0f;
    bool toastActive = false;
    float toastStartRealtime = 0f;

    bool suppressToasts = false;
    Queue<string> toastQueue = new Queue<string>();

    private Coroutine _fadeCo;

    // ---- 所有権（オプション） ----
    private Object _owner;                // UnityEngine.Object を使うとインスペクタで確認しやすい
    private int _ownerId;                 // null でも衝突しないよう InstanceID を持つ

    public string CurrentTitle => titleTMP ? titleTMP.text : null;
    public string CurrentBody => bodyTMP ? bodyTMP.text : null;
    public bool IsVisible => panelRoot && panelRoot.activeInHierarchy && (!canvasGroup || canvasGroup.alpha > 0.001f);


    public void AcquireOwner(Object owner)
    {
        _owner = owner;
        _ownerId = owner ? owner.GetInstanceID() : 0;
        if (verboseLog) UnityEngine.Debug.Log($"[HUD] Owner acquired: {_owner} ({_ownerId})", this);
    }
    public void ReleaseOwner(Object owner)
    {
        if (!enableOwnerGuard) return;
        if (_ownerId != 0 && owner && _ownerId == owner.GetInstanceID())
        {
            if (verboseLog) UnityEngine.Debug.Log($"[HUD] Owner released: {_owner} ({_ownerId})", this);
            _owner = null;
            _ownerId = 0;
        }
    }
    private bool CheckOwner(Object caller)
    {
        if (!enableOwnerGuard) return true;
        // オーナー未設定なら誰でも可（初期化フェーズ）
        if (_ownerId == 0) return true;
        return caller && caller.GetInstanceID() == _ownerId;
    }

    void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = startHidden ? 0f : 1f;
            canvasGroup.interactable = !startHidden;
            canvasGroup.blocksRaycasts = !startHidden;
        }
        if (startHidden) panelRoot.SetActive(false);

        if (verboseLog)
        {
            UnityEngine.Debug.Log($"[HUD] Awake panel={panelRoot?.name} titleTMP={titleTMP?.name} bodyTMP={bodyTMP?.name}", this);
        }
    }

    // ===== API（所有権付き版を優先使用） =====
    public void ShowFrom(Object owner, string title, string body, bool instant = false)
    {
        if (!CheckOwner(owner)) { LogBlocked("Show", owner); return; }
        if (owner) AcquireOwner(owner);
        SetTitleFrom(owner, title);
        SetBodyFrom(owner, body);
        SetVisible(true, instant);
    }

    public void SetTitleFrom(Object owner, string title)
    {
        if (!CheckOwner(owner)) { LogBlocked("SetTitle", owner); return; }
        if (!titleTMP) { if (verboseLog) UnityEngine.Debug.LogWarning("[HUD] titleTMP is null", this); return; }
        titleTMP.text = title;
        if (verboseLog) UnityEngine.Debug.Log($"[HUD] SetTitleFrom({owner}) -> \"{title}\"", this);
        if (verboseLog) LogCallsiteIfSuspicious(title);
    }

    public void SetBodyFrom(Object owner, string body)
    {
        if (!CheckOwner(owner)) { LogBlocked("SetBody", owner); return; }
        if (!bodyTMP) { if (verboseLog) UnityEngine.Debug.LogWarning("[HUD] bodyTMP is null", this); return; }
        bodyTMP.text = body;
        if (verboseLog) UnityEngine.Debug.Log($"[HUD] SetBodyFrom({owner}) -> \"{body}\"", this);
    }

    public void HideFrom(Object owner, bool instant = false)
    {
        if (!CheckOwner(owner)) { LogBlocked("Hide", owner); return; }
        SetVisible(false, instant);
        ReleaseOwner(owner);
    }

    // ===== 既存互換API（可能なら使わない） =====
    public void Show(string title, string body, bool instant = false)
    {
        // 互換：オーナー未設定時のみ通す
        if (enableOwnerGuard && _ownerId != 0) { LogBlocked("Show(legacy)", null); return; }
        SetTitle(title);
        SetBody(body);
        SetVisible(true, instant);
    }
    public void Hide(bool instant = false)
    {
        if (enableOwnerGuard && _ownerId != 0) { LogBlocked("Hide(legacy)", null); return; }
        SetVisible(false, instant);
    }
    public void SetTitle(string title)
    {
        if (enableOwnerGuard && _ownerId != 0) { LogBlocked("SetTitle(legacy)", null); return; }
        if (!titleTMP) { if (verboseLog) UnityEngine.Debug.LogWarning("[HUD] titleTMP is null", this); return; }
        titleTMP.text = title;
        if (verboseLog) { UnityEngine.Debug.Log($"[HUD] SetTitle(legacy) -> \"{title}\"", this); LogCallsiteIfSuspicious(title); }
    }
    public void SetBody(string body)
    {
        if (enableOwnerGuard && _ownerId != 0) { LogBlocked("SetBody(legacy)", null); return; }
        if (!bodyTMP) { if (verboseLog) UnityEngine.Debug.LogWarning("[HUD] bodyTMP is null", this); return; }
        bodyTMP.text = body;
        if (verboseLog) UnityEngine.Debug.Log($"[HUD] SetBody(legacy) -> \"{body}\"", this);
    }

    public void AppendBody(string t, bool nl = true)
    {
        if (!bodyTMP) return;
        bodyTMP.text += nl ? ("\n" + t) : t;
        if (verboseLog) UnityEngine.Debug.Log($"[HUD] AppendBody -> now \"{bodyTMP.text}\"", this);
    }
    public void SwapTitleAndBody()
    {
        if (!titleTMP || !bodyTMP) return;
        var t = titleTMP.text; titleTMP.text = bodyTMP.text; bodyTMP.text = t;
        if (verboseLog) UnityEngine.Debug.Log($"[HUD] SwapTitleAndBody -> title:\"{titleTMP.text}\" body:\"{bodyTMP.text}\"", this);
    }

    public void SetVisible(bool visible, bool instant = false)
    {
        if (panelRoot == null) return;

        if(!visible)
        {
            CancelToast();
        }

        if (canvasGroup == null || fadeSec <= 0f || instant)
        {
            panelRoot.SetActive(visible);
            if (canvasGroup)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            if (verboseLog) UnityEngine.Debug.Log($"[HUD] SetVisible immediate -> {visible}", this);
            return;
        }

        if (_fadeCo != null) StopCoroutine(_fadeCo);
        panelRoot.SetActive(true);
        _fadeCo = StartCoroutine(CoFade(visible));
        if (verboseLog) UnityEngine.Debug.Log($"[HUD] SetVisible fade start -> {visible}", this);
    }

    private IEnumerator CoFade(bool toVisible)
    {
        float start = canvasGroup.alpha;
        float end = toVisible ? 1f : 0f;
        float t = 0f;
        while (t < fadeSec)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, end, t / fadeSec);
            yield return null;
        }
        canvasGroup.alpha = end;
        canvasGroup.interactable = toVisible;
        canvasGroup.blocksRaycasts = toVisible;
        if (!toVisible) panelRoot.SetActive(false);
        _fadeCo = null;
        if (verboseLog) UnityEngine.Debug.Log($"[HUD] Fade done -> {toVisible}", this);
    }

    private void LogBlocked(string what, Object caller)
    {
        if (!verboseLog) return;
        UnityEngine.Debug.LogWarning($"[HUD] BLOCKED {what} by {caller} (owner={_owner})", this);
        var st = new StackTrace(1, true);
        UnityEngine.Debug.Log(st.ToString());
    }

    // 「[Main]」「[Sub]」の書換えトリガを特定したいときに役立つ
    private void LogCallsiteIfSuspicious(string title)
    {
        if (!verboseLog) return;
        if (title.Contains("[Main]") || title.Contains("[Sub]"))
        {
            var st = new StackTrace(1, true);
            UnityEngine.Debug.Log($"[HUD] TitleTag write: {title}\n{st}", this);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (toastGroup && toastText)
        {
            return;
        }

        if (!titleTMP) UnityEngine.Debug.LogWarning("[HUD] titleTMP is not assigned.", this);
        if (!bodyTMP) UnityEngine.Debug.LogWarning("[HUD] bodyTMP is not assigned.", this);
        if (!panelRoot) UnityEngine.Debug.LogWarning("[HUD] panelRoot is not assigned.", this);
    }

    public void AssignForTest(TMPro.TMP_Text title, TMPro.TMP_Text body, GameObject root, CanvasGroup cg)
    {
        // 直参照で注入（PlayModeテスト用）
        titleTMP = title;
        bodyTMP = body;
        panelRoot = root ? root : gameObject;
        canvasGroup = cg;

        // Awake 相当の初期可視状態も整える（startHidden を無視して常時表示でOK）
        if (canvasGroup)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        if (panelRoot) panelRoot.SetActive(true);
    }
#endif

    /// <summary>進捗を表示。null で非表示。</summary>
    public void SetProgress(float? value)
    {
        if (!progressBar) return;
        if (value.HasValue)
        {
            if (!progressBar.gameObject.activeSelf) progressBar.gameObject.SetActive(true);
            progressBar.normalizedValue = Mathf.Clamp01(value.Value);
        }
        else
        {
            if (progressBar.gameObject.activeSelf) progressBar.gameObject.SetActive(false);
        }
    }

    /// <summary>短いトーストを表示。</summary>
    public void ShowToast(string message)
    {
        if (!toastText || !toastGroup) return;

        // 抑止中ならキューに積んで終了（表示しない）
        if (suppressToasts)
        {
            if (!string.IsNullOrEmpty(message))
                toastQueue.Enqueue(message);
            return;
        }

        // ここから通常表示（常にリスタートする）
        toastText.text = message ?? "";
        toastTimer = 0f;
        toastActive = true;
        toastStartRealtime = Time.realtimeSinceStartup; // ★リアルタイム開始時刻
        toastGroup.alpha = 0f;
        if (!toastGroup.gameObject.activeSelf) toastGroup.gameObject.SetActive(true);
    }

    void Update()
    {
        if (!toastActive || !toastGroup) return;

        float elapsed = Time.realtimeSinceStartup - toastStartRealtime; // ★UIが止まっても進む
        float a;

        if (elapsed <= toastFadeIn)
            a = Mathf.InverseLerp(0f, toastFadeIn, elapsed);
        else if (elapsed <= toastFadeIn + toastShow)
            a = 1f;
        else if (elapsed <= toastFadeIn + toastShow + toastFadeOut)
            a = 1f - Mathf.InverseLerp(toastFadeIn + toastShow, toastFadeIn + toastShow + toastFadeOut, elapsed);
        else
        {
            a = 0f;
            toastActive = false;
            toastGroup.gameObject.SetActive(false);
        }

        toastGroup.alpha = a;
    }

    public void CancelToast()
    {
        toastActive = false;
        toastTimer = 0f;
        toastStartRealtime = 0f;
        if (toastGroup)
        {
            toastGroup.alpha = 0f;
            toastGroup.gameObject.SetActive(false);
        }
    }

    public void EnterToastSuppression(bool killExisting = true, bool clearQueued = false)
    {
        if (killExisting) CancelToast();
        suppressToasts = true;
        if (clearQueued) toastQueue.Clear();
    }

    public void ExitToastSuppression(bool flushQueued = true, float flushInterval = 0.05f)
    {
        suppressToasts = false;

        if (flushQueued && toastQueue.Count > 0)
        {
            // 1つだけ即時表示（残りは簡易的に連続再生）
            var first = toastQueue.Dequeue();
            ShowToast(first);

            // 残りはコルーチン等で間隔再生しても良いが、簡易に一括破棄でもOK
            toastQueue.Clear();
        }
    }

    void OnDisable()
    {
        // パネルが非アクティブ化された瞬間に表示中を殺す
        CancelToast();
    }
}
#pragma warning restore 0414
