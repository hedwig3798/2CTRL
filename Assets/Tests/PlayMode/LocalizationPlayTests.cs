using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// LocalizedText : 언어 변경 시 갱신, 비활성화 시 구독 해제
/// </summary>
public class LocalizationPlayTests
{
    private const string LANG_PREF = "Settings.Language";
    private bool hadPref;
    private int prefBackup;
    private GameObject go;

    [SetUp]
    public void SetUp()
    {
        hadPref = PlayerPrefs.HasKey(LANG_PREF);
        prefBackup = PlayerPrefs.GetInt(LANG_PREF, 0);
        PlayerPrefs.DeleteKey(LANG_PREF);
        GameSettings.Reload();
        StringTable.LoadFromText("key,ko,en\nt.a,가,A\nt.b,나,B\n");
    }

    [TearDown]
    public void TearDown()
    {
        if (go != null) { Object.Destroy(go); }
        PlayerPrefs.DeleteKey(LANG_PREF);
        if (hadPref) { PlayerPrefs.SetInt(LANG_PREF, prefBackup); }
        GameSettings.Reload();
        StringTable.Reload();
    }

    private LocalizedText Make(string _key)
    {
        go = new GameObject("LT", typeof(RectTransform));
        go.SetActive(false);
        go.AddComponent<TextMeshProUGUI>();
        var lt = go.AddComponent<LocalizedText>();
        lt.Key = _key;
        return lt;
    }

    [UnityTest]
    public IEnumerator Enable_AppliesCurrentLanguage_AndFollowsChange()
    {
        var lt = Make("t.a");
        var text = go.GetComponent<TMP_Text>();
        go.SetActive(true);
        yield return null;
        Assert.AreEqual("가", text.text);

        StringTable.CurrentLanguage = Language.English;
        Assert.AreEqual("A", text.text);

        lt.Key = "t.b";
        Assert.AreEqual("B", text.text);
    }

    [UnityTest]
    public IEnumerator Disable_UnsubscribesFromLanguageChange()
    {
        Make("t.a");
        var text = go.GetComponent<TMP_Text>();
        go.SetActive(true);
        yield return null;
        go.SetActive(false);
        StringTable.CurrentLanguage = Language.English;
        Assert.AreEqual("가", text.text);
        go.SetActive(true);
        yield return null;
        Assert.AreEqual("A", text.text);
    }

    [UnityTest]
    public IEnumerator EmptyKey_LeavesTextUntouched()
    {
        Make(null);
        var text = go.GetComponent<TMP_Text>();
        text.text = "keep";
        go.SetActive(true);
        yield return null;
        StringTable.CurrentLanguage = Language.English;
        Assert.AreEqual("keep", text.text);
    }

    [UnityTest]
    public IEnumerator Destroyed_DoesNotThrowOnLanguageChange()
    {
        Make("t.a");
        go.SetActive(true);
        yield return null;
        Object.Destroy(go);
        yield return null;
        Assert.DoesNotThrow(() => StringTable.CurrentLanguage = Language.English);
    }
}
