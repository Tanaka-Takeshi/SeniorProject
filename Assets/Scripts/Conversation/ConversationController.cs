using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace Game.Conversation
{
    public class ConversationController : MonoBehaviour
    {
        public static ConversationController Instance { get; private set; }

        [Header("Refs")]
        public HUDController hud;                  // 会話用HUD（Panel_ConversationHUD）
        public HUDController worldHud;             // 常時HUD
        public CameraDirector cameraDirector;      // 会話カメラ切替
        public PlayerInteractor playerInteractor;  // 会話中は無効化

        [Header("Other UI to hide during conversation")]
        public GameObject panelHUD;        // Panel_HUD
        public GameObject panelToast;      // Panel_Toast
        public GameObject panelHUDList;    // Panel_HUDList
        public GameObject interactPrompt;  // InteractPrompt

        [Header("Choice UI")]
        public ChoiceUI choiceUI;          // Panel_Choices（ChoiceUI）

        [Header("Player Input (New Input System)")]
        public PlayerInput playerInput;    // Player の PlayerInput
        public string gameplayMap = "Player";
        public string uiMap = "UI";

        [Header("State")]
        public bool IsInConversation { get; private set; }
        private Transform currentNpcTarget;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void StartConversation(Transform npcHeadOrGroup, string npcName, string firstLine, bool instant = false)
        {
            if (IsInConversation) return;
            IsInConversation = true;

            if (worldHud) worldHud.EnterToastSuppression(killExisting: true, clearQueued: false);

            var toast = panelToast ? panelToast.GetComponentInChildren<ToastUI>(true) : null;
            if (toast) { toast.EnterSuppression(clearQueued: false, hideNow: true); }

            currentNpcTarget = npcHeadOrGroup;

            if (playerInteractor) playerInteractor.enabled = false;
            if (cameraDirector) cameraDirector.PushConversationCamera(npcHeadOrGroup);

            // ActionMap を UI へ
            if (playerInput && !string.IsNullOrEmpty(uiMap))
                playerInput.SwitchCurrentActionMap(uiMap);

            // 他HUDを隠す
            if (panelHUD) panelHUD.SetActive(false);
            if (panelToast) panelToast.SetActive(false);
            if (panelHUDList) panelHUDList.SetActive(false);
            if (interactPrompt) interactPrompt.SetActive(false);

            // 会話HUD表示
            if (hud)
            {
                hud.AcquireOwner(this);
                hud.ShowFrom(this, npcName ?? "NPC", firstLine ?? "", instant);
            }

            HideChoices(); // 念のため
        }

        public void ShowNextLine(string bodyText)
        {
            if (!IsInConversation || !hud) return;
            hud.SetBodyFrom(this, bodyText ?? "");
        }

        public void UpdateTitle(string npcName)
        {
            if (!IsInConversation || !hud) return;
            hud.SetTitleFrom(this, npcName ?? "NPC");
        }

        public void EndConversation(bool instant = false)
        {
            if (!IsInConversation) return;

            if (hud)
            {
                hud.HideFrom(this, instant);
                hud.ReleaseOwner(this);
            }

            HideChoices();

            if (cameraDirector) cameraDirector.PopConversationCamera();
            if (playerInteractor) playerInteractor.enabled = true;

            // ActionMap を Gameplay に戻す
            if (playerInput && !string.IsNullOrEmpty(gameplayMap))
                playerInput.SwitchCurrentActionMap(gameplayMap);

            // 隠していたHUDを再表示
            if (panelHUD) panelHUD.SetActive(true);
            if (panelToast) panelToast.SetActive(true);
            if (panelHUDList) panelHUDList.SetActive(true);
            if (interactPrompt) interactPrompt.SetActive(true);

            if (panelHUDList)
            {
                var list = panelHUDList.GetComponentInChildren<Game.UI.EventHUDList>(true);
                if (list)
                {
                    var em = FindFirstObjectByType<Game.Runtime.EventManager>();
                    if (em != null) em.EvaluateFrame();

                    // 直ちに一度更新
                    list.ForceRefreshNow();
                    // 次のフレームでもう一度（レイアウト/状態のズレ吸収）
                    StartCoroutine(list.CoRefreshNextFrame());
                }
            }

            var toast = panelToast ? panelToast.GetComponentInChildren<ToastUI>(true) : null;
            if (toast) { toast.ExitSuppression(); }

            if (worldHud) worldHud.ExitToastSuppression(flushQueued: true);

            IsInConversation = false;
            currentNpcTarget = null;
        }

        // ===== 選択肢UI API =====
        public void ShowChoices(DialogueChoice[] choices, Action<int> onSelect)
        {
            if (choiceUI == null || choices == null || choices.Length == 0) return;

            var items = new (string, UnityEngine.Events.UnityAction)[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                int idx = i;
                items[i] = (choices[i].text, () => onSelect?.Invoke(idx));
            }
            choiceUI.Show(items);
        }

        public void HideChoices()
        {
            if (choiceUI != null) choiceUI.Hide();
        }
    }
}
