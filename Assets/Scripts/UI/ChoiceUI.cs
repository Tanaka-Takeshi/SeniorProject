using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ChoiceUI : MonoBehaviour
{
    [SerializeField] Transform container;     // ボタンを並べる親 (VerticalLayoutGroup推奨)
    [SerializeField] GameObject buttonPrefab; // 選択肢用の Button プレハブ (TextMeshPro付き)

    readonly List<Button> _buttons = new();
    int _selected = 0;
    public bool IsOpen { get; private set; } = false;

    public void Show((string label, UnityAction onClick)[] items)
    {
        Clear();
        gameObject.SetActive(true);
        IsOpen = true;

        Button first = null;

        foreach (var it in items)
        {
            var go = Instantiate(buttonPrefab, container);
            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt) txt.text = it.label;

            var btn = go.GetComponent<Button>();
            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(it.onClick);
                _buttons.Add(btn);
                if (first == null) first = btn;
            }
        }

        // 初期選択（最初のボタンをハイライト）
        _selected = 0;
        if (first) first.Select();
    }

    public void Hide()
    {
        IsOpen = false;
        gameObject.SetActive(false);
        Clear();
    }

    void Clear()
    {
        _buttons.Clear();
        if (!container) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    void Update()
    {
        if (!IsOpen || _buttons.Count == 0) return;

        // --- 移動入力（↑/↓、W/S、D-Pad、スティック） ---
        bool moveUp = false, moveDown = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            moveUp |= kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            moveDown |= kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
        }
        var gp = Gamepad.current;
        if (gp != null)
        {
            moveUp |= gp.dpad.up.wasPressedThisFrame || gp.leftStick.up.wasPressedThisFrame;
            moveDown |= gp.dpad.down.wasPressedThisFrame || gp.leftStick.down.wasPressedThisFrame;
        }
#else
        moveUp   = Input.GetKeyDown(KeyCode.UpArrow)   || Input.GetKeyDown(KeyCode.W);
        moveDown = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
#endif

        if (moveUp)
        {
            _selected = (_selected - 1 + _buttons.Count) % _buttons.Count;
            _buttons[_selected].Select(); // ★ ボタンを選択状態にしてハイライトさせる
        }
        if (moveDown)
        {
            _selected = (_selected + 1) % _buttons.Count;
            _buttons[_selected].Select(); // ★ ボタンを選択状態にしてハイライトさせる
        }

        // --- 数字キーでダイレクト選択（1..9） ---
        int numberHit = -1;
#if ENABLE_INPUT_SYSTEM
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) numberHit = 0;
            else if (kb.digit2Key.wasPressedThisFrame) numberHit = 1;
            else if (kb.digit3Key.wasPressedThisFrame) numberHit = 2;
            else if (kb.digit4Key.wasPressedThisFrame) numberHit = 3;
            else if (kb.digit5Key.wasPressedThisFrame) numberHit = 4;
            else if (kb.digit6Key.wasPressedThisFrame) numberHit = 5;
            else if (kb.digit7Key.wasPressedThisFrame) numberHit = 6;
            else if (kb.digit8Key.wasPressedThisFrame) numberHit = 7;
            else if (kb.digit9Key.wasPressedThisFrame) numberHit = 8;
        }
#else
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { numberHit = i; break; }
        }
#endif

        if (numberHit >= 0 && numberHit < _buttons.Count)
        {
            _selected = numberHit;
            _buttons[_selected].Select();
            _buttons[_selected].onClick.Invoke();
            return;
        }

        // --- 決定（Enter / Space / Pad South） ---
        bool decide = false;
#if ENABLE_INPUT_SYSTEM
        if (kb != null) decide |= kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        if (gp != null) decide |= gp.buttonSouth.wasPressedThisFrame;
#else
        decide = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif

        if (decide)
        {
            _buttons[_selected].onClick.Invoke();
        }

        if (IsOpen)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null)
            {
                Debug.Log("現在選択中のボタン: " + selected.name);
            }
            else
            {
                Debug.Log("何も選択されていません");
            }
        }
    }

}
