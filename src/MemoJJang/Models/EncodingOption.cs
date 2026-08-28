using System.Globalization;
using System.Text;

namespace MemoJJang.Models;

/// <summary>
/// 사용자가 선택할 수 있는 하나의 인코딩 항목.
/// 읽기용/쓰기용 <see cref="Encoding"/> 인스턴스를 분리해서 보관한다.
/// (쓰기용은 BOM 기록 여부가 반영된 인스턴스이다.)
/// </summary>
public sealed class EncodingOption
{
    public EncodingOption(string id, string displayName, string shortName,
                          Encoding readEncoding, Encoding writeEncoding, bool hasBom)
    {
        Id = id;
        DisplayName = displayName;
        ShortName = shortName;
        ReadEncoding = readEncoding;
        WriteEncoding = writeEncoding;
        HasBom = hasBom;
    }

    /// <summary>설정 파일에 저장되는 안정적인 식별자.</summary>
    public string Id { get; }

    /// <summary>메뉴에 표시되는 이름.</summary>
    public string DisplayName { get; }

    /// <summary>상태 표시줄에 표시되는 짧은 이름.</summary>
    public string ShortName { get; }

    public Encoding ReadEncoding { get; }

    public Encoding WriteEncoding { get; }

    /// <summary>저장할 때 BOM(바이트 순서 표식)을 기록하는지 여부.</summary>
    public bool HasBom { get; }

    public int CodePage => ReadEncoding.CodePage;

    public override string ToString() => DisplayName;
}

/// <summary>메모짱이 지원하는 인코딩 목록.</summary>
public static class EncodingCatalog
{
    private static readonly List<EncodingOption> _all = new();

    static EncodingCatalog()
    {
        // .NET (Core) 은 기본적으로 유니코드 계열만 제공하므로 코드 페이지 공급자를 등록한다.
        // 이 등록이 없으면 CP949(EUC-KR), CP932(Shift-JIS) 등을 사용할 수 없다.
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch
        {
            // 등록 실패 시에도 유니코드 계열은 계속 사용할 수 있다.
        }

        AnsiCodePage = ResolveAnsiCodePage();

        var ansiEncoding = TryGetEncoding(AnsiCodePage) ?? Encoding.UTF8;
        Ansi = new EncodingOption(
            "ansi",
            $"ANSI (시스템 기본 · CP{ansiEncoding.CodePage})",
            "ANSI",
            ansiEncoding,
            ansiEncoding,
            hasBom: false);

        Utf8 = new EncodingOption("utf8", "UTF-8", "UTF-8",
            new UTF8Encoding(false), new UTF8Encoding(false), hasBom: false);

        Utf8Bom = new EncodingOption("utf8bom", "UTF-8 (BOM 포함)", "UTF-8 BOM",
            new UTF8Encoding(true), new UTF8Encoding(true), hasBom: true);

        Utf16Le = new EncodingOption("utf16le", "UTF-16 LE", "UTF-16 LE",
            new UnicodeEncoding(false, true), new UnicodeEncoding(false, true), hasBom: true);

        Utf16Be = new EncodingOption("utf16be", "UTF-16 BE", "UTF-16 BE",
            new UnicodeEncoding(true, true), new UnicodeEncoding(true, true), hasBom: true);

        Utf16LeNoBom = new EncodingOption("utf16le-nobom", "UTF-16 LE (BOM 없음)", "UTF-16 LE",
            new UnicodeEncoding(false, false), new UnicodeEncoding(false, false), hasBom: false);

        Utf16BeNoBom = new EncodingOption("utf16be-nobom", "UTF-16 BE (BOM 없음)", "UTF-16 BE",
            new UnicodeEncoding(true, false), new UnicodeEncoding(true, false), hasBom: false);

        Utf32Le = new EncodingOption("utf32le", "UTF-32 LE", "UTF-32 LE",
            new UTF32Encoding(false, true), new UTF32Encoding(false, true), hasBom: true);

        Utf32Be = new EncodingOption("utf32be", "UTF-32 BE", "UTF-32 BE",
            new UTF32Encoding(true, true), new UTF32Encoding(true, true), hasBom: true);

        _all.Add(Ansi);
        _all.Add(Utf8);
        _all.Add(Utf8Bom);
        _all.Add(Utf16Le);
        _all.Add(Utf16Be);
        _all.Add(Utf32Le);

        // 지역 인코딩 (사용 가능한 것만 추가)
        AddCodePage("euckr", "한국어 (EUC-KR / CP949)", "EUC-KR", 949);
        AddCodePage("sjis", "일본어 (Shift-JIS / CP932)", "Shift-JIS", 932);
        AddCodePage("gb18030", "중국어 간체 (GB18030)", "GB18030", 54936);
        AddCodePage("big5", "중국어 번체 (Big5 / CP950)", "Big5", 950);
        AddCodePage("win1252", "서유럽 (Windows-1252)", "CP1252", 1252);
        AddCodePage("latin1", "서유럽 (ISO-8859-1)", "ISO-8859-1", 28591);

        // BOM 없는 UTF-16 은 자동 감지 결과 매핑용으로만 보관하고 메뉴에는 노출하지 않는다.
        Hidden = new[] { Utf16LeNoBom, Utf16BeNoBom, Utf32Be };
    }

    /// <summary>메뉴에 노출되는 인코딩 목록.</summary>
    public static IReadOnlyList<EncodingOption> All => _all;

    private static IReadOnlyList<EncodingOption> Hidden { get; }

    public static int AnsiCodePage { get; }

    public static EncodingOption Ansi { get; }
    public static EncodingOption Utf8 { get; }
    public static EncodingOption Utf8Bom { get; }
    public static EncodingOption Utf16Le { get; }
    public static EncodingOption Utf16Be { get; }
    public static EncodingOption Utf16LeNoBom { get; }
    public static EncodingOption Utf16BeNoBom { get; }
    public static EncodingOption Utf32Le { get; }
    public static EncodingOption Utf32Be { get; }

    /// <summary>새 문서의 기본 인코딩. (Windows 11 메모장과 동일하게 UTF-8)</summary>
    public static EncodingOption Default => Utf8;

    public static EncodingOption ById(string? id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            foreach (var option in _all)
            {
                if (string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            foreach (var option in Hidden)
            {
                if (string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }
        }

        return Default;
    }

    /// <summary>코드 페이지 + BOM 여부로 가장 알맞은 항목을 찾는다.</summary>
    public static EncodingOption Match(int codePage, bool hasBom)
    {
        foreach (var option in _all)
        {
            if (option.CodePage == codePage && option.HasBom == hasBom)
            {
                return option;
            }
        }

        foreach (var option in Hidden)
        {
            if (option.CodePage == codePage && option.HasBom == hasBom)
            {
                return option;
            }
        }

        foreach (var option in _all)
        {
            if (option.CodePage == codePage)
            {
                return option;
            }
        }

        return Ansi;
    }

    private static void AddCodePage(string id, string displayName, string shortName, int codePage)
    {
        var encoding = TryGetEncoding(codePage);
        if (encoding is null)
        {
            return;
        }

        // ANSI 항목과 코드 페이지가 겹치면 중복 노출하지 않는다.
        if (encoding.CodePage == Ansi.CodePage)
        {
            return;
        }

        _all.Add(new EncodingOption(id, displayName, shortName, encoding, encoding, hasBom: false));
    }

    private static Encoding? TryGetEncoding(int codePage)
    {
        try
        {
            return Encoding.GetEncoding(codePage);
        }
        catch
        {
            return null;
        }
    }

    private static int ResolveAnsiCodePage()
    {
        try
        {
            var cp = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
            // 일부 환경(비 Windows, 유니코드 로캘)에서는 0 또는 65001 이 반환된다.
            if (cp > 0 && cp != 65001 && TryGetEncoding(cp) is not null)
            {
                return cp;
            }
        }
        catch
        {
            // 무시하고 아래 기본값을 사용한다.
        }

        return TryGetEncoding(1252) is not null ? 1252 : 65001;
    }
}
