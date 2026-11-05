using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 문자열 테이블(ko/en) 테스트 : CSV 파싱, 실제 CSV 무결성, 언어 저장, 누락 키 처리
/// </summary>
public class LocalizationEditTests
{
    private const string LANG_PREF = "Settings.Language";
    private const string CSV_PATH = "Assets/Resources/Localization/StringTable.csv";

    private bool hadPref;
    private int prefBackup;

    [SetUp]
    public void SetUp()
    {
        hadPref = PlayerPrefs.HasKey(LANG_PREF);
        prefBackup = PlayerPrefs.GetInt(LANG_PREF, 0);
        PlayerPrefs.DeleteKey(LANG_PREF);
        GameSettings.Reload();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(LANG_PREF);
        if (hadPref)
        {
            PlayerPrefs.SetInt(LANG_PREF, prefBackup);
        }
        GameSettings.Reload();
        StringTable.Reload();
    }

    private static string ProjectPath(string _relative)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", _relative));
    }

    // ---------- CSV 파싱 ----------

    [Test]
    public void ParseCsv_QuotesCommasAndNewlines()
    {
        var rows = StringTable.ParseCsv("key,ko,en\na,\"하나, 둘\",\"say \"\"hi\"\"\"\nb,\"줄1\n줄2\",x\n");
        Assert.AreEqual(3, rows.Count);
        Assert.AreEqual("하나, 둘", rows[1][1]);
        Assert.AreEqual("say \"hi\"", rows[1][2]);
        Assert.AreEqual("줄1\n줄2", rows[2][1]);
    }

    [Test]
    public void ParseCsv_CrlfAndBom()
    {
        var rows = StringTable.ParseCsv("﻿key,ko,en\r\na,가,A\r\nb,나,B\r\n");
        Assert.AreEqual(3, rows.Count);
        Assert.AreEqual("key", rows[0][0]);
        Assert.AreEqual("A", rows[1][2]);
        Assert.AreEqual("B", rows[2][2]);
    }

    [Test]
    public void ParseCsv_EmptyOrNull_ReturnsNoRows()
    {
        Assert.AreEqual(0, StringTable.ParseCsv(null).Count);
        Assert.AreEqual(0, StringTable.ParseCsv("").Count);
        Assert.AreEqual(0, StringTable.ParseCsv("\n\n").Count);
    }

    [Test]
    public void Parse_SkipsCommentsBlankLinesAndLastDuplicateWins()
    {
        var t = StringTable.Parse("key,ko,en\n# comment,x,y\n\na,1,one\na,2,two\n");
        Assert.AreEqual(1, t.Count);
        Assert.AreEqual("2", t["a"][0]);
        Assert.AreEqual("two", t["a"][1]);
    }

    [Test]
    public void Parse_NoKeyColumn_ReturnsEmpty()
    {
        LogAssert.Expect(LogType.Warning, new Regex("key"));
        Assert.AreEqual(0, StringTable.Parse("name,ko,en\na,1,2\n").Count);
    }

    [Test]
    public void Parse_ColumnOrderFromHeader()
    {
        var t = StringTable.Parse("en,key,ko\nE,a,K\n");
        Assert.AreEqual("K", t["a"][(int)Language.Korean]);
        Assert.AreEqual("E", t["a"][(int)Language.English]);
    }

    // ---------- Get / Format / 누락 ----------

    [Test]
    public void Get_ReturnsPerLanguage()
    {
        StringTable.LoadFromText("key,ko,en\nhello,안녕,Hello\n");
        Assert.AreEqual("안녕", StringTable.Get("hello", Language.Korean));
        Assert.AreEqual("Hello", StringTable.Get("hello", Language.English));
        StringTable.CurrentLanguage = Language.English;
        Assert.AreEqual("Hello", StringTable.Get("hello"));
    }

    [Test]
    public void Get_MissingKey_ReturnsKeyAndWarnsOnce()
    {
        StringTable.LoadFromText("key,ko,en\na,1,2\n");
        LogAssert.Expect(LogType.Warning, new Regex("no.such.key"));
        Assert.AreEqual("no.such.key", StringTable.Get("no.such.key"));
        Assert.AreEqual("no.such.key", StringTable.Get("no.such.key")); // 두 번째는 경고 없음
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Get_NullOrEmptyKey()
    {
        StringTable.LoadFromText("key,ko,en\na,1,2\n");
        LogAssert.ignoreFailingMessages = true;
        try
        {
            Assert.AreEqual(string.Empty, StringTable.Get(null));
            Assert.AreEqual(string.Empty, StringTable.Get(string.Empty));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }
        Assert.IsFalse(StringTable.HasKey(null));
        Assert.IsFalse(StringTable.HasKey(""));
    }

    [Test]
    public void Get_EmptyTranslation_FallsBackToKorean()
    {
        StringTable.LoadFromText("key,ko,en\na,한국어,\n");
        Assert.AreEqual("한국어", StringTable.Get("a", Language.English));
    }

    [Test]
    public void TryGet_MissingReturnsFalseWithoutWarning()
    {
        StringTable.LoadFromText("key,ko,en\na,1,2\n");
        Assert.IsFalse(StringTable.TryGet("zzz", out string v));
        Assert.IsNull(v);
        Assert.IsTrue(StringTable.TryGet("a", Language.English, out v));
        Assert.AreEqual("2", v);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Resolve_KeyOrRawText()
    {
        StringTable.LoadFromText("key,ko,en\na,가,A\n");
        StringTable.CurrentLanguage = Language.English;
        Assert.AreEqual("A", StringTable.Resolve("a"));
        Assert.AreEqual("raw text", StringTable.Resolve("raw text"));
        Assert.AreEqual(string.Empty, StringTable.Resolve(null));
        Assert.AreEqual(string.Empty, StringTable.Resolve(""));
    }

    [Test]
    public void Format_AppliesArgs_AndBadFormatReturnsFormat()
    {
        StringTable.LoadFromText("key,ko,en\nk,처치 {0}/{1},Kills {0}/{1}\nbad,{0} {1},{0} {1}\n");
        StringTable.CurrentLanguage = Language.English;
        Assert.AreEqual("Kills 3/10", StringTable.Format("k", 3, 10));
        LogAssert.Expect(LogType.Warning, new Regex("포멧"));
        Assert.AreEqual("{0} {1}", StringTable.Format("bad", 1)); // 인자 부족
    }

    [Test]
    public void Format_NoArgs_ReturnsRawString()
    {
        StringTable.LoadFromText("key,ko,en\nk,{0} 그대로,x\n");
        Assert.AreEqual("{0} 그대로", StringTable.Format("k"));
    }

    [Test]
    public void LoadFromText_ThenReload_RestoresRealCsv()
    {
        StringTable.LoadFromText("key,ko,en\nonly,1,2\n");
        Assert.AreEqual(1, StringTable.Count);
        StringTable.Reload();
        Assert.Greater(StringTable.Count, 50);
        Assert.IsFalse(StringTable.HasKey("only"));
    }

    // ---------- 언어 저장 ----------

    [Test]
    public void Language_DefaultIsKorean()
    {
        Assert.AreEqual(Language.Korean, StringTable.CurrentLanguage);
        Assert.AreEqual(Language.Korean, GameSettings.Language);
    }

    [Test]
    public void Language_PersistsAcrossReload_AndFiresEventOnlyOnChange()
    {
        var received = new List<Language>();
        void H(Language l) { received.Add(l); }
        StringTable.OnLanguageChanged += H;
        try
        {
            StringTable.CurrentLanguage = Language.Korean; // 변화 없음
            Assert.AreEqual(0, received.Count);
            StringTable.CurrentLanguage = Language.English;
            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(Language.English, received[0]);
            Assert.AreEqual((int)Language.English, PlayerPrefs.GetInt(LANG_PREF, -1));
            GameSettings.Reload();
            Assert.AreEqual(Language.English, GameSettings.Language);
        }
        finally
        {
            StringTable.OnLanguageChanged -= H;
        }
    }

    [Test]
    public void Language_OutOfRangeSavedValue_ClampsToKorean()
    {
        PlayerPrefs.SetInt(LANG_PREF, 99);
        GameSettings.Reload();
        Assert.AreEqual(Language.Korean, GameSettings.Language);
        PlayerPrefs.SetInt(LANG_PREF, -1);
        GameSettings.Reload();
        Assert.AreEqual(Language.Korean, GameSettings.Language);
    }

    [Test]
    public void LanguageInfo_NativeNamesAndCodes()
    {
        Assert.AreEqual("한국어", LanguageInfo.GetNativeName(Language.Korean));
        Assert.AreEqual("English", LanguageInfo.GetNativeName(Language.English));
        Assert.AreEqual("ko", LanguageInfo.GetCode(Language.Korean));
        Assert.AreEqual("en", LanguageInfo.GetCode(Language.English));
        Assert.AreEqual(Language.Korean, LanguageInfo.Clamp(7));
        Assert.AreEqual(Language.English, LanguageInfo.Clamp(1));
        Assert.AreEqual(2, LanguageInfo.Count);
    }

    // ---------- 실제 CSV 무결성 ----------

    private static List<List<string>> RealRows()
    {
        string text = File.ReadAllText(ProjectPath(CSV_PATH), System.Text.Encoding.UTF8);
        return StringTable.ParseCsv(text);
    }

    private static IEnumerable<List<string>> DataRows()
    {
        var rows = RealRows();
        for (int i = 1; i < rows.Count; ++i)
        {
            string key = rows[i][0].Trim();
            if (key.Length == 0 || key[0] == '#') { continue; }
            yield return rows[i];
        }
    }

    [Test]
    public void RealCsv_HeaderIsKeyKoEn()
    {
        var rows = RealRows();
        Assert.GreaterOrEqual(rows[0].Count, 3);
        Assert.AreEqual("key", rows[0][0].Trim());
        Assert.AreEqual("ko", rows[0][1].Trim());
        Assert.AreEqual("en", rows[0][2].Trim());
    }

    [Test]
    public void RealCsv_AllKeysHaveNonEmptyKoAndEn()
    {
        var bad = new List<string>();
        foreach (var r in DataRows())
        {
            if (r.Count < 3 || string.IsNullOrWhiteSpace(r[1]) || string.IsNullOrWhiteSpace(r[2]))
            {
                bad.Add(r[0]);
            }
        }
        Assert.IsEmpty(bad, "ko/en 비어있는 키 : " + string.Join(", ", bad));
    }

    [Test]
    public void RealCsv_NoDuplicateKeys()
    {
        var seen = new HashSet<string>();
        var dup = new List<string>();
        foreach (var r in DataRows())
        {
            if (!seen.Add(r[0].Trim())) { dup.Add(r[0].Trim()); }
        }
        Assert.IsEmpty(dup, "중복 키 : " + string.Join(", ", dup));
    }

    [Test]
    public void RealCsv_PlaceholdersMatchBetweenKoAndEn()
    {
        var bad = new List<string>();
        foreach (var r in DataRows())
        {
            if (r.Count < 3) { continue; }
            if (!Placeholders(r[1]).SetEquals(Placeholders(r[2])))
            {
                bad.Add(r[0] + " ko={" + string.Join(",", Placeholders(r[1])) + "} en={" + string.Join(",", Placeholders(r[2])) + "}");
            }
        }
        Assert.IsEmpty(bad, "플레이스홀더 불일치 : " + string.Join(" | ", bad));
    }

    [Test]
    public void RealCsv_FormatsAreValid()
    {
        var bad = new List<string>();
        foreach (var r in DataRows())
        {
            for (int c = 1; c <= 2 && c < r.Count; ++c)
            {
                try { string.Format(r[c], "a", "b", "c", "d", "e", "f"); }
                catch (System.FormatException) { bad.Add(r[0] + "[" + c + "]"); }
            }
        }
        Assert.IsEmpty(bad, "잘못된 포멧 : " + string.Join(", ", bad));
    }

    private static HashSet<string> Placeholders(string _s)
    {
        var set = new HashSet<string>();
        string s = _s.Replace("{{", "").Replace("}}", "");
        foreach (Match m in Regex.Matches(s, @"\{(\d+)(?:[:,][^}]*)?\}"))
        {
            set.Add(m.Groups[1].Value);
        }
        return set;
    }

    [Test]
    public void RealCsv_LoadedByResources_MatchesFile()
    {
        StringTable.Reload();
        int expected = 0;
        var seen = new HashSet<string>();
        foreach (var r in DataRows()) { if (seen.Add(r[0].Trim())) { ++expected; } }
        Assert.AreEqual(expected, StringTable.Count);
    }

    // ---------- 코드 리터럴 키 존재 검사 ----------

    [Test]
    public void CodeStringLiteralKeys_ExistInCsv()
    {
        var keys = new HashSet<string>();
        var prefixes = new HashSet<string>();
        foreach (var r in DataRows())
        {
            string k = r[0].Trim();
            keys.Add(k);
            int dot = k.IndexOf('.');
            if (dot > 0) { prefixes.Add(k.Substring(0, dot)); }
        }

        var literal = new Regex("\"([a-z][a-z0-9_]*(?:\\.[a-z0-9_]+)+)\"");
        var missing = new SortedSet<string>();
        foreach (string dir in new[] { "Assets/Resources/Script", "Assets/Editor" })
        {
            foreach (string file in Directory.GetFiles(ProjectPath(dir), "*.cs", SearchOption.AllDirectories))
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    string t = line.TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("*")) { continue; }
                    foreach (Match m in literal.Matches(line))
                    {
                        string k = m.Groups[1].Value;
                        string head = k.Substring(0, k.IndexOf('.'));
                        if (prefixes.Contains(head) && !keys.Contains(k))
                        {
                            missing.Add(k + "  (" + Path.GetFileName(file) + ")");
                        }
                    }
                }
            }
        }
        Assert.IsEmpty(missing, "CSV 에 없는 키 : " + string.Join(" | ", missing));
    }
}
