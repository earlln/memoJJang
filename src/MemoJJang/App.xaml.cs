using System.Windows;
using System.Windows.Threading;
using MemoJJang.Services;

namespace MemoJJang;

public partial class App : Application
{
    /// <summary>모든 창이 공유하는 사용자 설정.</summary>
    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        AppPaths.EnsureCreated();
        Settings = SettingsService.Load();
        ThemeService.Apply(Settings.Theme);

        var window = new MainWindow(e.Args);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SettingsService.Save(Settings);
        base.OnExit(e);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"예기치 못한 오류가 발생했습니다.\n\n{e.Exception.Message}",
            AppInfo.TitleSuffix,
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
