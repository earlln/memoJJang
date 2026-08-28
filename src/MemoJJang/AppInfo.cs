using System.Reflection;

namespace MemoJJang;

/// <summary>
/// 애플리케이션 식별/버전 정보. 버전 값의 원천은 Directory.Build.props 이며
/// 빌드 시 어셈블리 메타데이터로 주입된다.
/// </summary>
public static class AppInfo
{
    /// <summary>제품명.</summary>
    public const string ProductName = "메모짱";

    /// <summary>제작자/브랜드.</summary>
    public const string Vendor = "Earlln.com";

    /// <summary>홈페이지.</summary>
    public const string Homepage = "https://earlln.com";

    /// <summary>창 제목에 표시되는 짧은 버전 (예: "1.0").</summary>
    public static string DisplayVersion { get; } = ReadDisplayVersion();

    /// <summary>전체 시맨틱 버전 (예: "1.0.0").</summary>
    public static string FullVersion { get; } =
        typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>창 제목 접미사: "메모짱 ver1.0 by Earlln.com".</summary>
    public static string TitleSuffix => $"{ProductName} ver{DisplayVersion} by {Vendor}";

    /// <summary>파일이 없는 새 문서의 기본 이름.</summary>
    public const string UntitledName = "제목 없음";

    private static string ReadDisplayVersion()
    {
        try
        {
            foreach (var attr in typeof(AppInfo).Assembly
                         .GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                if (string.Equals(attr.Key, "DisplayVersion", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(attr.Value))
                {
                    return attr.Value!;
                }
            }
        }
        catch
        {
            // 메타데이터를 읽지 못해도 앱은 계속 동작해야 한다.
        }

        var v = typeof(AppInfo).Assembly.GetName().Version;
        return v is null ? "1.0" : $"{v.Major}.{v.Minor}";
    }
}
