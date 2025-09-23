using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// 1イベント分の小さなカード。
    /// Title/Body/Progress と、フェード退場を担当。
    /// </summary>
    public class EventHUDItem : MonoBehaviour
    {
        [Header("Refs")]
        public TMP_Text titleTMP;
        public TMP_Text bodyTMP;
        public Image typeBadge;
        public Slider progressBar;
        public CanvasGroup group;

        [Header("Style")]
        public Color mainColor = new(0.95f, 0.35f, 0.35f);
        public Color subColor = new(0.35f, 0.55f, 0.95f);

        [HideInInspector] public string EventId;

        void Awake()
        {
            if (!group) group = GetComponent<CanvasGroup>();
            if (group) group.alpha = 1f;
            if (progressBar) progressBar.value = 0f;
        }

        public void Setup(string eventId, string title, string body, Game.Events.EventType type)
        {
            EventId = eventId;
            if (titleTMP) titleTMP.text = title;
            if (bodyTMP) bodyTMP.text = body;
            if (typeBadge) typeBadge.color = (type == Game.Events.EventType.Main) ? mainColor : subColor;
            SetProgress01(0f);
        }

        public void SetTitle(string t) { if (titleTMP) titleTMP.text = t; }
        public void SetBody(string t) { if (bodyTMP) bodyTMP.text = t; }

        public void SetProgress01(float v)
        {
            if (progressBar) progressBar.value = Mathf.Clamp01(v);
        }

        public void FadeOutAndDestroy(float sec = 0.2f)
        {
            // フェード無し → 即時破棄（テストで childCount をすぐ 0 にできる）
            if (sec <= 0f)
            {
                if (this) DestroyImmediate(gameObject);
                return;
            }

            if (!group) { DestroyImmediate(gameObject); return; } // safety
            StopAllCoroutines();
            StartCoroutine(CoFadeOut(sec));
        }

        System.Collections.IEnumerator CoFadeOut(float sec)
        {
            float t = 0f;
            float start = group.alpha;
            while (t < sec)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / sec);
                group.alpha = Mathf.Lerp(start, 0f, u);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
