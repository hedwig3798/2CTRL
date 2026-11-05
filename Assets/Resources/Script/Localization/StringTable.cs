using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// 플레이어에게 보이는 문자열 테이블 (한국어 / 영어)
/// - 데이터 : Assets/Resources/Localization/StringTable.csv (UTF-8, 헤더 key,ko,en)
///   - 쉼표 · 따옴표 · 줄바꿈이 든 칸은 큰따옴표로 감싸고, 칸 안의 큰따옴표는 "" 로 쓴다
///   - 빈 줄과 key 가 # 으로 시작하는 줄은 무시
/// - 처음 접근할 때 Resources.Load 로 1회 읽어 Dictionary 에 캐시한다
/// - 현재 언어는 GameSettings.Language 에 저장된다 (CurrentLanguage 는 그것을 그대로 읽고 쓴다)
/// - 누락 키 : Get 은 key 를 그대로 반환하고 에디터에서 키마다 1회 경고. 번역이 빈 칸이면 한국어로 대체
/// - 키 이름 규칙 : 영역.항목 (예: ui.settings.title, gameover.title, stat.damage, reward.hp.explain1)
///
/// 사용 예)
///   label.text = StringTable.Get("ui.pause.title");
///   label.text = StringTable.Format("gameover.kill", kills);
///   StringTable.OnLanguageChanged += HandleLanguageChanged; // OnEnable 에서 구독, OnDisable 에서 해제
/// </summary>
public static class StringTable
{
    public const string RESOURCE_PATH = "Localization/StringTable";
    private const string KEY_COLUMN = "key";
    private const char COMMENT_PREFIX = '#';

    // key → 언어별 문자열 (Language 순서)
    private static Dictionary<string, string[]> table;
#if UNITY_EDITOR
    private static readonly HashSet<string> warnedKeys = new HashSet<string>();
#endif

    /// <summary>
    /// 현재 언어 (GameSettings 에 저장. 값이 바뀔 때만 OnLanguageChanged 호출)
    /// </summary>
    public static Language CurrentLanguage
    {
        get => GameSettings.Language;
        set => GameSettings.Language = value;
    }

    /// <summary>
    /// 언어가 바뀔 때 (새 언어). GameSettings.OnLanguageChanged 와 같은 이벤트
    /// </summary>
    public static event Action<Language> OnLanguageChanged
    {
        add => GameSettings.OnLanguageChanged += value;
        remove => GameSettings.OnLanguageChanged -= value;
    }

    /// <summary>
    /// 읽어 둔 키 개수
    /// </summary>
    public static int Count
    {
        get
        {
            EnsureLoaded();
            return table.Count;
        }
    }

    #region Public API

    /// <summary>
    /// 현재 언어 문자열 (누락 키면 key 그대로)
    /// </summary>
    public static string Get(string _key)
    {
        return Get(_key, CurrentLanguage);
    }

    /// <summary>
    /// 지정 언어 문자열 (누락 키면 key 그대로)
    /// </summary>
    public static string Get(string _key, Language _language)
    {
        if (true == TryGet(_key, _language, out string value))
        {
            return value;
        }

        WarnMissing(_key);
        return null != _key ? _key : string.Empty;
    }

    /// <summary>
    /// 현재 언어 문자열을 string.Format 한다 (포멧이 잘못되었으면 포멧 문자열 그대로)
    /// </summary>
    public static string Format(string _key, params object[] _args)
    {
        return FormatText(Get(_key), _args);
    }

    /// <summary>
    /// 키가 있으면 true 와 현재 언어 문자열 (경고 없음)
    /// </summary>
    public static bool TryGet(string _key, out string _value)
    {
        return TryGet(_key, CurrentLanguage, out _value);
    }

    /// <summary>
    /// 키가 있으면 true 와 지정 언어 문자열 (번역이 빈 칸이면 한국어. 경고 없음)
    /// </summary>
    public static bool TryGet(string _key, Language _language, out string _value)
    {
        _value = null;
        if (true == string.IsNullOrEmpty(_key))
        {
            return false;
        }

        EnsureLoaded();
        if (false == table.TryGetValue(_key, out string[] values))
        {
            return false;
        }

        int index = (int)LanguageInfo.Clamp((int)_language);
        string value = index < values.Length ? values[index] : null;
        if (true == string.IsNullOrEmpty(value))
        {
            value = values[(int)LanguageInfo.DEFAULT];
        }
        _value = null != value ? value : string.Empty;
        return true;
    }

    /// <summary>
    /// 키면 번역, 아니면 받은 문자열 그대로 (키 / 원문이 섞여 들어오는 데이터 필드용. 경고 없음)
    /// </summary>
    public static string Resolve(string _keyOrText)
    {
        if (true == string.IsNullOrEmpty(_keyOrText))
        {
            return string.Empty;
        }
        return true == TryGet(_keyOrText, out string value) ? value : _keyOrText;
    }

    public static bool HasKey(string _key)
    {
        if (true == string.IsNullOrEmpty(_key))
        {
            return false;
        }
        EnsureLoaded();
        return table.ContainsKey(_key);
    }

    /// <summary>
    /// 이미 번역된 포멧 문자열을 string.Format 한다 (잘못된 포멧이면 경고 후 원본)
    /// </summary>
    public static string FormatText(string _format, params object[] _args)
    {
        if (true == string.IsNullOrEmpty(_format))
        {
            return string.Empty;
        }
        if (null == _args || 0 == _args.Length)
        {
            return _format;
        }

        try
        {
            return string.Format(CultureInfo.InvariantCulture, _format, _args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[StringTable] 포멧 문자열이 잘못되었습니다 : {_format}");
            return _format;
        }
    }

    /// <summary>
    /// CSV 를 다시 읽는다 (에디터에서 CSV 를 고친 뒤)
    /// </summary>
    public static void Reload()
    {
        table = null;
#if UNITY_EDITOR
        warnedKeys.Clear();
#endif
        EnsureLoaded();
    }

    /// <summary>
    /// 리소스 대신 CSV 텍스트로 테이블을 채운다 (테스트용)
    /// </summary>
    public static void LoadFromText(string _csv)
    {
        table = Parse(_csv);
#if UNITY_EDITOR
        warnedKeys.Clear();
#endif
    }

    /// <summary>
    /// CSV 텍스트 → key 별 언어 문자열 (헤더의 열 이름으로 언어 열을 찾는다. 같은 키가 또 나오면 뒤의 것을 쓴다)
    /// </summary>
    public static Dictionary<string, string[]> Parse(string _csv)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        List<List<string>> rows = ParseCsv(_csv);
        if (0 == rows.Count)
        {
            return result;
        }

        // 헤더 → 열 인덱스
        List<string> header = rows[0];
        int keyColumn = -1;
        var languageColumns = new int[LanguageInfo.Count];
        for (int i = 0; i < languageColumns.Length; ++i)
        {
            languageColumns[i] = -1;
        }
        for (int c = 0; c < header.Count; ++c)
        {
            string name = header[c].Trim();
            if (true == string.Equals(name, KEY_COLUMN, StringComparison.OrdinalIgnoreCase))
            {
                keyColumn = c;
                continue;
            }
            for (int l = 0; l < LanguageInfo.Count; ++l)
            {
                if (true == string.Equals(name, LanguageInfo.GetCode((Language)l), StringComparison.OrdinalIgnoreCase))
                {
                    languageColumns[l] = c;
                }
            }
        }

        if (keyColumn < 0)
        {
            Debug.LogWarning("[StringTable] CSV 헤더에 key 열이 없습니다.");
            return result;
        }

        for (int r = 1; r < rows.Count; ++r)
        {
            List<string> row = rows[r];
            if (keyColumn >= row.Count)
            {
                continue;
            }

            string key = row[keyColumn].Trim();
            if (0 == key.Length || COMMENT_PREFIX == key[0])
            {
                continue;
            }

            var values = new string[LanguageInfo.Count];
            for (int l = 0; l < values.Length; ++l)
            {
                int c = languageColumns[l];
                values[l] = 0 <= c && c < row.Count ? row[c] : string.Empty;
            }
            result[key] = values;
        }

        return result;
    }

    /// <summary>
    /// CSV 텍스트 → 줄(칸 목록) 목록. 큰따옴표 칸 안의 쉼표 · 줄바꿈 · "" 이스케이프 지원, BOM 무시, 빈 줄 건너뜀
    /// </summary>
    public static List<List<string>> ParseCsv(string _csv)
    {
        var rows = new List<List<string>>();
        if (true == string.IsNullOrEmpty(_csv))
        {
            return rows;
        }

        var cell = new StringBuilder();
        var row = new List<string>();
        bool inQuotes = false;
        bool rowHasContent = false;
        int line = 1;
        int quoteStartLine = 0;
        int start = 0 < _csv.Length && '\uFEFF' == _csv[0] ? 1 : 0;

        for (int i = start; i < _csv.Length; ++i)
        {
            char ch = _csv[i];
            if ('\n' == ch)
            {
                ++line;
            }

            if (true == inQuotes)
            {
                // 칸 안의 줄바꿈은 \n 으로 통일 (CRLF / CR → LF)
                if ('\r' == ch)
                {
                    if (i + 1 >= _csv.Length || '\n' != _csv[i + 1])
                    {
                        cell.Append('\n');
                    }
                }
                else if ('"' == ch)
                {
                    if (i + 1 < _csv.Length && '"' == _csv[i + 1])
                    {
                        cell.Append('"');
                        ++i;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(ch);
                }
                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    quoteStartLine = line;
                    rowHasContent = true;
                    break;
                case ',':
                    row.Add(cell.ToString());
                    cell.Clear();
                    rowHasContent = true;
                    break;
                case '\r':
                    break;
                case '\n':
                    EndRow(rows, ref row, cell, ref rowHasContent);
                    break;
                default:
                    cell.Append(ch);
                    rowHasContent = true;
                    break;
            }
        }

        if (true == inQuotes)
        {
            Debug.LogWarning($"[StringTable] CSV {quoteStartLine} 번째 줄에서 시작한 큰따옴표가 닫히지 않았습니다. (파일 끝까지 한 칸으로 읽음)");
        }

        EndRow(rows, ref row, cell, ref rowHasContent);
        return rows;
    }

    #endregion

    private static void EndRow(List<List<string>> _rows, ref List<string> _row, StringBuilder _cell, ref bool _rowHasContent)
    {
        if (true == _rowHasContent)
        {
            _row.Add(_cell.ToString());
            _rows.Add(_row);
            _row = new List<string>();
        }
        _cell.Clear();
        _rowHasContent = false;
    }

    private static void EnsureLoaded()
    {
        if (null != table)
        {
            return;
        }

        TextAsset asset = Resources.Load<TextAsset>(RESOURCE_PATH);
        if (null == asset)
        {
            Debug.LogWarning($"[StringTable] Resources/{RESOURCE_PATH}.csv 를 찾지 못했습니다.");
            table = new Dictionary<string, string[]>(StringComparer.Ordinal);
            return;
        }

        table = Parse(asset.text);
        Resources.UnloadAsset(asset);
    }

    private static void WarnMissing(string _key)
    {
#if UNITY_EDITOR
        string key = null != _key ? _key : string.Empty;
        if (true == warnedKeys.Add(key))
        {
            Debug.LogWarning($"[StringTable] 키가 없습니다 : \"{key}\"");
        }
#endif
    }

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우를 위해 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        table = null;
#if UNITY_EDITOR
        warnedKeys.Clear();
#endif
    }
}
