using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using MemoJJang.Models;

namespace MemoJJang.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();

        TitleText.Text = $"{AppInfo.ProductName} ver{AppInfo.DisplayVersion}";
        VersionText.Text = $"버전 {AppInfo.FullVersion} · by {AppInfo.Vendor}";
        RuntimeText.Text =
            $".NET {Environment.Version} · 시스템 ANSI 코드 페이지 CP{EncodingCatalog.AnsiCodePage}";
    }

    private void HomeLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // 브라우저를 열 수 없어도 무시한다.
        }

        e.Handled = true;
    }
}
