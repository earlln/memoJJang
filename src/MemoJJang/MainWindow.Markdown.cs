using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using MemoJJang.Models;
using MemoJJang.Services;

namespace MemoJJang;

/// <summary>Markdown 미리 보기 패널.</summary>
public partial class MainWindow
{
    /// <summary>미리 보기 본문의 기준 글꼴 크기(DIP). 확대 비율이 곱해진다.</summary>
    private const double PreviewBaseFontSize = 14.5;

    /// <summary>편집기와 미리 보기 사이 분할선의 두께.</summary>
    private const double SplitterWidth = 6;

    // ==================================================================
    //  패널 구성
    // ==================================================================

    /// <summary>미리 보기 패널을 아직 만들지 않았다면 만든다.</summary>
    private void EnsurePreview(DocumentTab document)
    {
        if (document.Preview is not null || document.Root is null)
        {
            return;
        }

        var splitter = new GridSplitter
        {
            Width = SplitterWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            ResizeDirection = GridResizeDirection.Columns,
            Focusable = false
        };

        splitter.SetResourceReference(BackgroundProperty, "App.Border");
        Grid.SetColumn(splitter, 1);

        var viewer = new FlowDocumentScrollViewer
        {
            IsToolBarVisible = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Focusable = false
        };

        viewer.SetResourceReference(BackgroundProperty, "App.Editor.Background");
        viewer.SetResourceReference(ForegroundProperty, "App.Editor.Foreground");
        viewer.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(Preview_RequestNavigate));
        Grid.SetColumn(viewer, 2);

        document.Root.Children.Add(splitter);
        document.Root.Children.Add(viewer);

        document.Splitter = splitter;
        document.Preview = viewer;
    }

    private void SetPreviewVisible(DocumentTab document, bool visible)
    {
        if (visible)
        {
            EnsurePreview(document);
        }

        if (document.Root is null)
        {
            return;
        }

        document.IsPreviewVisible = visible && document.Preview is not null;

        if (document.Splitter is not null)
        {
            document.Splitter.Visibility = document.IsPreviewVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        if (document.Preview is not null)
        {
            document.Preview.Visibility = document.IsPreviewVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        document.Root.ColumnDefinitions[2].Width = document.IsPreviewVisible
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);

        if (document.IsPreviewVisible)
        {
            RenderPreview(document);
        }

        RefreshMarkdownMenu();
    }

    private void ToggleMarkdownPreview()
    {
        if (Current is { } document)
        {
            SetPreviewVisible(document, !document.IsPreviewVisible);
        }
    }

    // ==================================================================
    //  렌더링
    // ==================================================================

    private void SchedulePreviewUpdate()
    {
        if (Current is not { IsPreviewVisible: true })
        {
            return;
        }

        _previewTimer ??= CreatePreviewTimer();
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private DispatcherTimer CreatePreviewTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };

        timer.Tick += (_, _) =>
        {
            timer.Stop();

            if (Current is { IsPreviewVisible: true } document)
            {
                RenderPreview(document);
            }
        };

        return timer;
    }

    private void RenderPreview(DocumentTab document)
    {
        var viewer = document.Preview;
        if (viewer is null || !document.IsPreviewVisible)
        {
            return;
        }

        // 다시 그릴 때 읽던 위치가 맨 위로 튀지 않도록 스크롤 위치를 보존한다.
        var offset = FindDescendant<ScrollViewer>(viewer)?.VerticalOffset ?? 0;
        var fontSize = Math.Clamp(PreviewBaseFontSize * Settings.ZoomPercent / 100.0, 7, 80);

        viewer.Document = MarkdownRenderer.Render(document.Text, document.FilePath, fontSize);

        if (offset > 0)
        {
            Dispatcher.BeginInvoke(
                new Action(() => FindDescendant<ScrollViewer>(viewer)?.ScrollToVerticalOffset(offset)),
                DispatcherPriority.Loaded);
        }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void Preview_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;

        // 렌더러가 http / https / mailto 만 NavigateUri 로 넘긴다.
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"링크를 열 수 없습니다.\n\n{e.Uri}\n\n{ex.Message}",
                AppInfo.TitleSuffix,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    // ==================================================================
    //  메뉴
    // ==================================================================

    private void MarkdownPreview_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressMenuEvents)
        {
            return;
        }

        var document = Current;
        if (document is null)
        {
            RefreshMarkdownMenu();
            return;
        }

        SetPreviewVisible(document, MarkdownPreviewMenuItem.IsChecked);
    }

    private void MarkdownAutoPreview_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressMenuEvents)
        {
            return;
        }

        Settings.MarkdownPreviewAutoOpen = MarkdownAutoPreviewMenuItem.IsChecked;
        SettingsService.Save(Settings);
    }

    private void RefreshMarkdownMenu()
    {
        var previous = _suppressMenuEvents;
        _suppressMenuEvents = true;

        MarkdownPreviewMenuItem.IsChecked = Current?.IsPreviewVisible == true;
        MarkdownAutoPreviewMenuItem.IsChecked = Settings.MarkdownPreviewAutoOpen;

        _suppressMenuEvents = previous;
    }
}
