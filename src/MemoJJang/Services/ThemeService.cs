using System.Windows;
using Microsoft.Win32;

namespace MemoJJang.Services;

/// <summary>테마(라이트/다크/시스템) 적용을 담당한다.</summary>
public static class ThemeService
{
    private const int PaletteDictionaryIndex = 0;

    /// <summary>현재 실제로 적용된 테마(시스템 설정이 해석된 결과).</summary>
    public static bool IsDarkApplied { get; private set; }

    public static void Apply(AppTheme theme)
    {
        var dark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsSystemDark()
        };

        var uri = new Uri(
            dark
                ? "pack://application:,,,/MemoJJang;component/Themes/Dark.xaml"
                : "pack://application:,,,/MemoJJang;component/Themes/Light.xaml",
            UriKind.Absolute);
        var dictionary = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count > PaletteDictionaryIndex)
        {
            merged[PaletteDictionaryIndex] = dictionary;
        }
        else
        {
            merged.Insert(PaletteDictionaryIndex, dictionary);
        }

        IsDarkApplied = dark;
    }

    /// <summary>Windows 앱 테마가 다크인지 확인한다.</summary>
    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value == 0;
            }
        }
        catch
        {
            // 레지스트리를 읽지 못하면 라이트 테마로 간주한다.
        }

        return false;
    }
}
