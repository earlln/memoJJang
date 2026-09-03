using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MemoJJang.Models;

namespace MemoJJang.Services;

public enum AppTheme
{
    Light,
    Dark,
    System
}

/// <summary>디스크에 저장되는 사용자 설정.</summary>
public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;

    public bool WordWrap { get; set; } = true;

    public bool ShowStatusBar { get; set; } = true;

    public string FontFamily { get; set; } = "Consolas, Malgun Gothic";

    /// <summary>글꼴 크기(포인트).</summary>
    public double FontSize { get; set; } = 11;

    public bool FontBold { get; set; }

    public bool FontItalic { get; set; }

    public int ZoomPercent { get; set; } = 100;

    /// <summary>새 문서에 사용할 기본 인코딩 식별자.</summary>
    public string DefaultEncodingId { get; set; } = EncodingCatalog.Default.Id;

    public LineEndingKind DefaultLineEnding { get; set; } = LineEndingKind.CrLf;

    /// <summary>종료 시 열려 있던 탭을 다음 실행에서 복원할지 여부.</summary>
    public bool RestoreSession { get; set; } = true;

    /// <summary>Markdown 파일을 열 때 미리 보기를 자동으로 함께 열지 여부.</summary>
    public bool MarkdownPreviewAutoOpen { get; set; } = true;

    public double WindowLeft { get; set; } = double.NaN;

    public double WindowTop { get; set; } = double.NaN;

    public double WindowWidth { get; set; } = 1000;

    public double WindowHeight { get; set; } = 680;

    public bool WindowMaximized { get; set; }

    public List<string> RecentFiles { get; set; } = new();

    [JsonIgnore]
    public EncodingOption DefaultEncoding => EncodingCatalog.ById(DefaultEncodingId);

    public const int MaxRecentFiles = 10;

    public void PushRecentFile(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);

        while (RecentFiles.Count > MaxRecentFiles)
        {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }
    }
}

public static class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // 설정이 손상되었더라도 기본값으로 계속 실행한다.
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            AppPaths.EnsureCreated();
            var json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(AppPaths.SettingsFile, json);
        }
        catch
        {
            // 설정 저장 실패로 종료가 막히면 안 된다.
        }
    }
}
