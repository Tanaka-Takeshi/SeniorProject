// Assets/Tests/PlayMode/UI_Toast_Tests.cs
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class UI_Toast_Tests
{
    GameObject go;
    ToastUI toast;
    CanvasGroup cg;
    TMP_Text tmp;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        go = new GameObject("Panel_Toast");
        cg = go.AddComponent<CanvasGroup>();
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        tmp = textGO.AddComponent<TextMeshProUGUI>();
        toast = go.AddComponent<ToastUI>();

        // インスペクタ割り当て相当
        var so = new SerializedObject(toast);
        so.FindProperty("panelRoot").objectReferenceValue = go;
        so.FindProperty("canvasGroup").objectReferenceValue = cg;
        so.FindProperty("messageTMP").objectReferenceValue = tmp;
        so.ApplyModifiedPropertiesWithoutUndo();

        go.SetActive(true);
        yield return null; // Awake() 実行
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.DestroyImmediate(go);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Toast_Shows_All_In_Order()
    {
        var seen = new List<string>();
        toast.OnShown += m => seen.Add(m);

        toast.Show("A", 0.2f);
        toast.Show("B", 0.2f);
        toast.Show("C", 0.2f);

        // 全部出るまで待つ
        var timeout = Time.realtimeSinceStartup + 3f;
        while (seen.Count < 3 && Time.realtimeSinceStartup < timeout)
            yield return null;

        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, seen, "FIFOで全件出るべき");
        Assert.AreEqual(0, toast.PendingCount);

        // 完全に終了するまで待機
        timeout = Time.realtimeSinceStartup + 1f;
        while (toast.IsRunning && Time.realtimeSinceStartup < timeout)
            yield return null;

        Assert.False(toast.IsRunning, "最後に必ず止まっているべき");
    }

    [UnityTest]
    public IEnumerator Toast_Respects_Duration_Roughly()
    {
        var tList = new List<float>();
        toast.OnShown += _ => tList.Add(Time.realtimeSinceStartup);

        toast.Show("X", 0.2f);
        toast.Show("Y", 0.4f);

        // 2個終わるまで待機
        var timeout = Time.realtimeSinceStartup + 3f;
        while (tList.Count < 2 && Time.realtimeSinceStartup < timeout)
            yield return null;

        Assert.That(tList.Count, Is.EqualTo(2));
        // だいたい 0.2s 以上は間が空いている（フェード含め 0.15s くらいの許容）
        Assert.GreaterOrEqual(tList[1] - tList[0], 0.15f);
    }

    [UnityTest]
    public IEnumerator Toast_DoesNot_Deactivate_GameObject()
    {
        toast.Show("On", 0.2f);
        toast.Show("Off", 0.2f);

        var timeout = Time.realtimeSinceStartup + 3f;
        while (toast.IsRunning && Time.realtimeSinceStartup < timeout)
            yield return null;

        Assert.True(go.activeSelf, "GameObject 自体は常にアクティブのまま");
        Assert.LessOrEqual(cg.alpha, 0.001f, "見えないときは CanvasGroup alpha=0");
    }
}
