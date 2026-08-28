namespace MemoJJang.Models;

/// <summary>파일에 기록할 줄 바꿈 형식.</summary>
public enum LineEndingKind
{
    /// <summary>Windows (CR LF).</summary>
    CrLf,

    /// <summary>Unix / Linux / macOS (LF).</summary>
    Lf,

    /// <summary>고전 Mac (CR).</summary>
    Cr
}

public static class LineEndingKindExtensions
{
    public static string ToSequence(this LineEndingKind kind) => kind switch
    {
        LineEndingKind.Lf => "\n",
        LineEndingKind.Cr => "\r",
        _ => "\r\n"
    };

    public static string ToDisplayName(this LineEndingKind kind) => kind switch
    {
        LineEndingKind.Lf => "LF (Unix)",
        LineEndingKind.Cr => "CR (Mac)",
        _ => "CRLF (Windows)"
    };

    public static string ToShortName(this LineEndingKind kind) => kind switch
    {
        LineEndingKind.Lf => "LF",
        LineEndingKind.Cr => "CR",
        _ => "CRLF"
    };
}
