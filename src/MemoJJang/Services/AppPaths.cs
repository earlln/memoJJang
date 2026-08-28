using System.IO;

namespace MemoJJang.Services;

/// <summary>사용자별 설정/세션 파일 경로.</summary>
public static class AppPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MemoJJang");

    public static string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public static string SessionDirectory => Path.Combine(RootDirectory, "session");

    public static string SessionFile => Path.Combine(SessionDirectory, "session.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(SessionDirectory);
    }
}
