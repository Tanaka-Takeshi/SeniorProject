using NUnit.Framework;
using System.Collections;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class UI_HUD_OwnerGuard_Tests
{
    GameObject go;
    HUDController hud;
    TMP_Text title, body;

    class DummyOwner : MonoBehaviour { }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        go = new GameObject("HUD");
        var cg = go.AddComponent<CanvasGroup>();
        hud = go.AddComponent<HUDController>();

        // Texts
        var t1 = new GameObject("Title").AddComponent<TextMeshProUGUI>();
        t1.rectTransform.SetParent(go.transform, false);
        var t2 = new GameObject("Body").AddComponent<TextMeshProUGUI>();
        t2.rectTransform.SetParent(go.transform, false);
        title = t1; body = t2;

        // インスペクタ割当
        var so = new SerializedObject(hud);
        so.FindProperty("panelRoot").objectReferenceValue = go;
        so.FindProperty("canvasGroup").objectReferenceValue = cg;
        so.FindProperty("titleTMP").objectReferenceValue = title;
        so.FindProperty("bodyTMP").objectReferenceValue = body;
        so.FindProperty("startHidden").boolValue = false;
        so.FindProperty("fadeSec").floatValue = 0f;
        so.FindProperty("enableOwnerGuard").boolValue = true; // ガード有効
        so.ApplyModifiedPropertiesWithoutUndo();

        go.SetActive(true);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.DestroyImmediate(go);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Owner_Can_Write_NonOwner_Is_Blocked()
    {
        var ownerGO = new GameObject("Owner");
        var owner = ownerGO.AddComponent<DummyOwner>();

        hud.AcquireOwner(owner);
        hud.SetTitleFrom(owner, "[Main] A");
        Assert.AreEqual("[Main] A", hud.CurrentTitle);

        // 非オーナー（null・別オブジェクト）からの書き換えは弾く
        hud.SetTitle("[Sub] B"); // legacy / 非オーナー → Block
        Assert.AreEqual("[Main] A", hud.CurrentTitle, "レガシー/非オーナーは上書きできない");

        var otherGO = new GameObject("Other");
        var other = otherGO.AddComponent<DummyOwner>();
        hud.SetTitleFrom(other, "[Sub] C");
        Assert.AreEqual("[Main] A", hud.CurrentTitle, "別オーナーも上書き不可");

        // 正しいオーナーなら上書きできる
        hud.SetTitleFrom(owner, "[Sub] D");
        Assert.AreEqual("[Sub] D", hud.CurrentTitle);

        Object.DestroyImmediate(ownerGO);
        Object.DestroyImmediate(otherGO);
        yield return null;
    }
}
