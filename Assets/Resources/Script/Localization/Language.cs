/// <summary>
/// 표시 언어 (StringTable.csv 의 열 순서와 같다. 값을 추가하면 LanguageInfo 와 CSV 열도 추가)
/// </summary>
public enum Language
{
    Korean,
    English,
}

/// <summary>
/// 언어별 고정 정보
/// - Code : StringTable.csv 헤더의 열 이름
/// - NativeName : 설정 창 드롭다운에 쓰는 각 언어 고유 표기 (문자열 테이블을 거치지 않는다)
/// </summary>
public static class LanguageInfo
{
    public const Language DEFAULT = Language.Korean;

    private static readonly string[] CODES = { "ko", "en" };
    private static readonly string[] NATIVE_NAMES = { "한국어", "English" };

    public static int Count => CODES.Length;

    public static Language Clamp(int _index)
    {
        if (_index < 0 || _index >= CODES.Length)
        {
            return DEFAULT;
        }
        return (Language)_index;
    }

    public static string GetCode(Language _language)
    {
        return CODES[(int)Clamp((int)_language)];
    }

    public static string GetNativeName(Language _language)
    {
        return NATIVE_NAMES[(int)Clamp((int)_language)];
    }
}
