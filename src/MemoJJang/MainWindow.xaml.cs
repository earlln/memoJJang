using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MemoJJang.Dialogs;
using MemoJJang.Models;
using MemoJJang.Services;
using Microsoft.Win32;

namespace MemoJJang;

public partial class MainWindow : Window
{
    private const string FileFilter =
        "텍스트 문서 (*.txt)|*.txt|" +
        "마크다운 (*.md)|*.md|" +
        "로그 파일 (*.log)|*.log|" +
        "모든 파일 (*.*)|*.*";

    private static readonly int[] ZoomPresets = { 50, 75, 100, 125, 150, 200, 300, 500 };

    private readonly List<DocumentTab> _documents = new();
    private readonly DispatcherTimer _statusTimer;
    private readonly string[] _startupArgs;

    private int _untitledCounter;
    private bool _suppressMenuEvents;

    private static AppSettings Settings => App.Settings;

    public MainWindow() : this(Array.Empty<string>())
    {
    }

    public MainWindow(string[] args)
    {
        InitializeComponent();

        _startupArgs = args;
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            UpdateStatusBar();
        };
    }

    /// <summary>현재 선택된 문서.</summary>
    private DocumentTab? Current => (Tabs.SelectedItem as TabItem)?.Tag as DocumentTab;

    // ==================================================================
    //  창 수명 주기
    // ==================================================================

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var isFirstWindow = Application.Current.Windows.OfType<MainWindow>().Count() == 1;

        if (isFirstWindow)
        {
            RestoreWindowBounds();
        }

        BuildEncodingMenus();
        BuildLineEndingMenus();
        BuildZoomMenu();
        RefreshMenuStates();
        RefreshRecentFilesMenu();

        var opened = false;

        if (isFirstWindow && Settings.RestoreSession)
        {
            opened |= RestoreSession();
        }

        foreach (var arg in _startupArgs)
        {
            if (!string.IsNullOrWhiteSpace(arg) && File.Exists(arg))
            {
                opened |= OpenFile(arg, null);
            }
        }

        if (!opened && _documents.Count == 0)
        {
            AddDocument();
        }

        UpdateTitle();
        UpdateStatusBar();
        FocusEditor();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        var isLastWindow = Application.Current.Windows.OfType<MainWindow>().Count() <= 1;

        if (Settings.RestoreSession && isLastWindow)
        {
            SaveSession();
        }
        else
        {
            foreach (var document in _documents.ToList())
            {
                if (!ConfirmClose(document))
                {
                    e.Cancel = true;
                    return;
                }
            }

            if (isLastWindow)
            {
                SessionService.Clear();
            }
        }

        if (isLastWindow)
        {
            StoreWindowBounds();
            SettingsService.Save(Settings);
        }
    }

    private void RestoreWindowBounds()
    {
        if (!double.IsNaN(Settings.WindowWidth) && Settings.WindowWidth >= MinWidth)
        {
            Width = Settings.WindowWidth;
        }

        if (!double.IsNaN(Settings.WindowHeight) && Settings.WindowHeight >= MinHeight)
        {
            Height = Settings.WindowHeight;
        }

        if (!double.IsNaN(Settings.WindowLeft) && !double.IsNaN(Settings.WindowTop))
        {
            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            // 이전에 사용하던 모니터가 사라진 경우를 대비해 화면 안쪽인지 확인한다.
            if (Settings.WindowLeft >= virtualLeft - 50 && Settings.WindowLeft <= virtualRight - 100 &&
                Settings.WindowTop >= virtualTop - 10 && Settings.WindowTop <= virtualBottom - 100)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = Settings.WindowLeft;
                Top = Settings.WindowTop;
            }
        }

        if (Settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void StoreWindowBounds()
    {
        Settings.WindowMaximized = WindowState == WindowState.Maximized;

        if (WindowState == WindowState.Normal)
        {
            Settings.WindowLeft = Left;
            Settings.WindowTop = Top;
            Settings.WindowWidth = Width;
            Settings.WindowHeight = Height;
        }
        else
        {
            Settings.WindowLeft = RestoreBounds.Left;
            Settings.WindowTop = RestoreBounds.Top;
            Settings.WindowWidth = RestoreBounds.Width;
            Settings.WindowHeight = RestoreBounds.Height;
        }
    }

    // ==================================================================
    //  탭 / 문서 관리
    // ==================================================================

    private DocumentTab AddDocument(string? filePath = null,
                                    string? text = null,
                                    EncodingOption? encoding = null,
                                    LineEndingKind? lineEnding = null,
                                    string detectionReason = "")
    {
        var editor = CreateEditor();

        var item = new TabItem
        {
            Style = (Style)FindResource("App.TabItem"),
            HeaderTemplate = (DataTemplate)FindResource("App.TabHeaderTemplate"),
            Content = editor
        };

        var document = new DocumentTab(
            item,
            editor,
            NextUntitledName(),
            encoding ?? Settings.DefaultEncoding,
            lineEnding ?? Settings.DefaultLineEnding)
        {
            FilePath = filePath,
            DetectionReason = detectionReason
        };

        item.Header = document;
        item.Tag = document;

        editor.Tag = document;

        if (!string.IsNullOrEmpty(text))
        {
            LoadTextIntoEditor(document, text);
        }

        _documents.Add(document);
        Tabs.Items.Add(item);
        Tabs.SelectedItem = item;

        return document;
    }

    private string NextUntitledName()
    {
        _untitledCounter++;
        return _untitledCounter == 1
            ? AppInfo.UntitledName
            : $"{AppInfo.UntitledName} {_untitledCounter}";
    }

    private TextBox CreateEditor()
    {
        var editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            IsUndoEnabled = true,
            UndoLimit = 512,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 10, 12, 10),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalContentAlignment = VerticalAlignment.Top,
            AutoWordSelection = false
        };

        editor.SetResourceReference(BackgroundProperty, "App.Editor.Background");
        editor.SetResourceReference(ForegroundProperty, "App.Editor.Foreground");
        editor.SetResourceReference(TextBoxBase.SelectionBrushProperty, "App.Selection");
        editor.SetResourceReference(TextBoxBase.CaretBrushProperty, "App.Caret");

        editor.TextChanged += Editor_TextChanged;
        editor.SelectionChanged += Editor_SelectionChanged;

        ApplyEditorAppearance(editor);
        return editor;
    }

    private void ApplyEditorAppearance(TextBox editor)
    {
        try
        {
            editor.FontFamily = new FontFamily(Settings.FontFamily);
        }
        catch
        {
            editor.FontFamily = new FontFamily("Consolas, Malgun Gothic");
        }

        var pixels = Settings.FontSize * 96.0 / 72.0 * Settings.ZoomPercent / 100.0;
        editor.FontSize = Math.Clamp(pixels, 5, 400);
        editor.FontWeight = Settings.FontBold ? FontWeights.Bold : FontWeights.Normal;
        editor.FontStyle = Settings.FontItalic ? FontStyles.Italic : FontStyles.Normal;
        editor.TextWrapping = Settings.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        editor.HorizontalScrollBarVisibility = Settings.WordWrap
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
    }

    private void ApplyEditorAppearanceToAll()
    {
        foreach (var document in _documents)
        {
            ApplyEditorAppearance(document.Editor);
        }
    }

    /// <summary>
    /// 편집기에 텍스트를 채워 넣는다. 실행 취소 기록을 잠시 꺼서
    /// "파일을 연 직후 Ctrl+Z 를 누르면 내용이 사라지는" 문제를 막는다.
    /// </summary>
    private static void LoadTextIntoEditor(DocumentTab document, string text)
    {
        var editor = document.Editor;

        document.IsLoading = true;
        editor.IsUndoEnabled = false;
        editor.Text = text;
        editor.IsUndoEnabled = true;
        editor.CaretIndex = 0;
        document.IsLoading = false;
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox { Tag: DocumentTab document })
        {
            return;
        }

        if (!document.IsLoading)
        {
            document.IsModified = true;
        }

        if (ReferenceEquals(document, Current))
        {
            UpdateTitle();
            ScheduleStatusUpdate();
        }
    }

    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { Tag: DocumentTab document } && ReferenceEquals(document, Current))
        {
            ScheduleStatusUpdate();
        }
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not TabControl)
        {
            return;
        }

        UpdateTitle();
        UpdateStatusBar();
        RefreshEncodingChecks();
        RefreshLineEndingChecks();
        FocusEditor();
    }

    private void FocusEditor()
    {
        var editor = Current?.Editor;
        if (editor is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            editor.Focus();
            Keyboard.Focus(editor);
        }), DispatcherPriority.Input);
    }

    private void TabCloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DocumentTab document })
        {
            CloseDocument(document);
        }
    }

    /// <summary>탭을 닫는다. 저장 여부를 물어 취소되면 false 를 돌려준다.</summary>
    private bool CloseDocument(DocumentTab document)
    {
        if (!ConfirmClose(document))
        {
            return false;
        }

        _documents.Remove(document);
        Tabs.Items.Remove(document.Item);

        if (_documents.Count == 0)
        {
            _untitledCounter = 0;
            AddDocument();
        }

        UpdateTitle();
        UpdateStatusBar();
        return true;
    }

    /// <summary>변경 내용 저장 여부를 확인한다. 취소를 누르면 false.</summary>
    private bool ConfirmClose(DocumentTab document)
    {
        if (!document.IsModified)
        {
            return true;
        }

        Tabs.SelectedItem = document.Item;

        var answer = MessageBox.Show(
            this,
            $"'{document.DisplayName}'의 변경 내용을 저장하시겠습니까?",
            AppInfo.TitleSuffix,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        return answer switch
        {
            MessageBoxResult.Yes => SaveDocument(document, saveAs: false),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    // ==================================================================
    //  파일 입출력
    // ==================================================================

    private bool OpenFile(string path, EncodingOption? forcedEncoding)
    {
        try
        {
            var full = Path.GetFullPath(path);

            if (forcedEncoding is null)
            {
                var existing = _documents.FirstOrDefault(d =>
                    d.FilePath is not null &&
                    string.Equals(d.FilePath, full, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    Tabs.SelectedItem = existing.Item;
                    return true;
                }
            }

            var loaded = TextFileService.Load(full, forcedEncoding);

            // 비어 있는 새 탭이 열려 있으면 그 자리에 그대로 연다.
            var target = Current;
            if (target is not null && target.IsPristineUntitled)
            {
                LoadTextIntoEditor(target, loaded.Text);
                target.FilePath = full;
                target.Encoding = loaded.Encoding;
                target.LineEnding = loaded.LineEnding;
                target.DetectionReason = loaded.DetectionReason;
                target.IsModified = false;
            }
            else
            {
                target = AddDocument(full, loaded.Text, loaded.Encoding, loaded.LineEnding, loaded.DetectionReason);
                target.IsModified = false;
            }

            Settings.PushRecentFile(full);
            RefreshRecentFilesMenu();
            RefreshEncodingChecks();
            RefreshLineEndingChecks();
            UpdateTitle();
            UpdateStatusBar();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"파일을 열 수 없습니다.\n\n{path}\n\n{ex.Message}",
                AppInfo.TitleSuffix,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>문서를 저장한다. 저장에 성공하면 true.</summary>
    private bool SaveDocument(DocumentTab document, bool saveAs)
    {
        var path = document.FilePath;
        var encoding = document.Encoding;
        var lineEnding = document.LineEnding;

        if (saveAs || string.IsNullOrEmpty(path))
        {
            var dialog = new SaveFileDialog
            {
                Filter = FileFilter,
                DefaultExt = ".txt",
                AddExtension = true,
                FileName = string.IsNullOrEmpty(document.FilePath)
                    ? document.UntitledName
                    : Path.GetFileName(document.FilePath),
                Title = "다른 이름으로 저장"
            };

            if (!string.IsNullOrEmpty(document.FilePath))
            {
                var directory = Path.GetDirectoryName(document.FilePath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    dialog.InitialDirectory = directory;
                }
            }

            if (dialog.ShowDialog(this) != true)
            {
                return false;
            }

            path = dialog.FileName;

            // 저장 인코딩과 줄 바꿈 형식을 확인한다.
            var picker = new EncodingPickerDialog(
                "저장 옵션",
                $"'{Path.GetFileName(path)}' 을(를) 저장할 인코딩과 줄 바꿈 형식을 선택하세요.",
                encoding,
                lineEnding,
                "저장")
            {
                Owner = this
            };

            if (picker.ShowDialog() != true)
            {
                return false;
            }

            encoding = picker.SelectedEncoding;
            lineEnding = picker.SelectedLineEnding;
        }

        if (!ConfirmEncodable(document.Text, ref encoding))
        {
            return false;
        }

        try
        {
            TextFileService.Save(path!, document.Text, encoding, lineEnding);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"파일을 저장할 수 없습니다.\n\n{path}\n\n{ex.Message}",
                AppInfo.TitleSuffix,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        document.FilePath = path;
        document.Encoding = encoding;
        document.LineEnding = lineEnding;
        document.DetectionReason = "사용자 저장";
        document.IsModified = false;

        Settings.PushRecentFile(path!);
        RefreshRecentFilesMenu();
        RefreshEncodingChecks();
        RefreshLineEndingChecks();
        UpdateTitle();
        UpdateStatusBar();
        return true;
    }

    /// <summary>
    /// 선택한 인코딩으로 표현할 수 없는 문자가 있으면 사용자에게 알리고
    /// UTF-8 로 저장할지 물어본다.
    /// </summary>
    private bool ConfirmEncodable(string text, ref EncodingOption encoding)
    {
        var check = TextFileService.CheckEncodable(text, encoding);
        if (check.CanEncode)
        {
            return true;
        }

        var answer = MessageBox.Show(
            this,
            $"이 문서에는 '{encoding.DisplayName}' 인코딩으로 저장할 수 없는 문자가 있습니다.\n" +
            $"(문제 문자: '{check.Offending}', 위치: {check.Index})\n\n" +
            "그대로 저장하면 해당 문자가 '?' 로 바뀝니다.\n" +
            "UTF-8 로 저장할까요?\n\n" +
            "  예    - UTF-8 로 저장\n" +
            "  아니요 - 그대로 저장 (일부 문자 손실)\n" +
            "  취소   - 저장하지 않음",
            AppInfo.TitleSuffix,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        switch (answer)
        {
            case MessageBoxResult.Yes:
                encoding = EncodingCatalog.Utf8;
                return true;
            case MessageBoxResult.No:
                return true;
            default:
                return false;
        }
    }

    // ==================================================================
    //  세션 유지
    // ==================================================================

    private bool RestoreSession()
    {
        var state = SessionService.Load();
        if (state is null || state.Documents.Count == 0)
        {
            return false;
        }

        var restored = false;

        foreach (var entry in state.Documents)
        {
            try
            {
                var buffer = SessionService.ReadBuffer(entry.BufferFile);
                var encoding = EncodingCatalog.ById(entry.EncodingId);

                string? text = buffer;
                var modified = entry.IsModified;

                if (text is null && !string.IsNullOrEmpty(entry.FilePath) && File.Exists(entry.FilePath))
                {
                    var loaded = TextFileService.Load(entry.FilePath, null);
                    text = loaded.Text;
                    encoding = loaded.Encoding;
                    modified = false;
                }

                if (text is null && string.IsNullOrEmpty(entry.FilePath))
                {
                    continue;
                }

                var document = AddDocument(
                    string.IsNullOrEmpty(entry.FilePath) ? null : entry.FilePath,
                    text ?? string.Empty,
                    encoding,
                    entry.LineEnding,
                    "세션 복원");

                if (!string.IsNullOrEmpty(entry.UntitledName))
                {
                    document.UntitledName = entry.UntitledName;
                }

                document.IsModified = modified;
                document.Editor.CaretIndex = Math.Clamp(entry.CaretIndex, 0, document.Editor.Text.Length);
                restored = true;
            }
            catch
            {
                // 복원할 수 없는 항목은 건너뛴다.
            }
        }

        if (restored && state.SelectedIndex >= 0 && state.SelectedIndex < Tabs.Items.Count)
        {
            Tabs.SelectedIndex = state.SelectedIndex;
        }

        return restored;
    }

    private void SaveSession()
    {
        var state = new SessionState { SelectedIndex = Math.Max(0, Tabs.SelectedIndex) };
        var buffers = new List<string?>();

        foreach (var document in _documents)
        {
            state.Documents.Add(new SessionDocument
            {
                FilePath = document.FilePath,
                UntitledName = document.UntitledName,
                EncodingId = document.Encoding.Id,
                LineEnding = document.LineEnding,
                IsModified = document.IsModified,
                CaretIndex = document.Editor.CaretIndex
            });

            // 저장되지 않은 내용이 있거나 파일이 없는 문서만 버퍼로 남긴다.
            buffers.Add(document.IsModified || document.FilePath is null ? document.Text : null);
        }

        SessionService.Save(state, buffers);
    }

    // ==================================================================
    //  상태 표시줄 / 제목
    // ==================================================================

    private void ScheduleStatusUpdate()
    {
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private void UpdateTitle()
    {
        var document = Current;
        if (document is null)
        {
            Title = AppInfo.TitleSuffix;
            return;
        }

        var marker = document.IsModified ? "*" : string.Empty;
        Title = $"{marker}{document.DisplayName} - {AppInfo.TitleSuffix}";
    }

    private void UpdateStatusBar()
    {
        var document = Current;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        var text = editor.Text;
        var caret = Math.Clamp(editor.CaretIndex, 0, text.Length);

        var line = 1;
        var lastBreak = -1;
        var characters = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                if (i < caret)
                {
                    line++;
                    lastBreak = i;
                }
            }

            if (text[i] != '\r')
            {
                characters++;
            }
        }

        var column = caret - lastBreak;
        var words = CountWords(text);

        CaretStatusText.Text = $"줄 {line}, 열 {column}";

        var selection = editor.SelectionLength;
        CountStatusText.Text = selection > 0
            ? $"{characters:N0}자 · {words:N0}단어 · 선택 {selection:N0}자"
            : $"{characters:N0}자 · {words:N0}단어";

        EncodingHintText.Text = string.IsNullOrEmpty(document.DetectionReason)
            ? string.Empty
            : $"({document.DetectionReason})";

        EncodingStatusMenu.Header = document.Encoding.ShortName;
        LineEndingStatusMenu.Header = document.LineEnding.ToShortName();
        ZoomStatusMenu.Header = $"{Settings.ZoomPercent}%";
    }

    private static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }

    // ==================================================================
    //  동적 메뉴 구성
    // ==================================================================

    private void BuildEncodingMenus()
    {
        BuildEncodingMenu(EncodingMenuItem);
        BuildEncodingMenu(EncodingStatusMenu);
    }

    private void BuildEncodingMenu(MenuItem parent)
    {
        parent.Items.Clear();

        foreach (var option in EncodingCatalog.All)
        {
            var item = new MenuItem
            {
                Header = option.DisplayName,
                IsCheckable = true,
                Tag = option
            };

            item.Click += EncodingOption_Click;
            parent.Items.Add(item);
        }

        parent.Items.Add(new Separator());

        var reopen = new MenuItem { Header = "다른 인코딩으로 다시 열기..." };
        reopen.Click += ReopenWithEncoding_Click;
        parent.Items.Add(reopen);

        var makeDefault = new MenuItem { Header = "현재 인코딩을 새 문서 기본값으로" };
        makeDefault.Click += MakeEncodingDefault_Click;
        parent.Items.Add(makeDefault);
    }

    private void BuildLineEndingMenus()
    {
        BuildLineEndingMenu(LineEndingMenuItem);
        BuildLineEndingMenu(LineEndingStatusMenu);
    }

    private void BuildLineEndingMenu(MenuItem parent)
    {
        parent.Items.Clear();

        foreach (var kind in new[] { LineEndingKind.CrLf, LineEndingKind.Lf, LineEndingKind.Cr })
        {
            var item = new MenuItem
            {
                Header = kind.ToDisplayName(),
                IsCheckable = true,
                Tag = kind
            };

            item.Click += LineEndingOption_Click;
            parent.Items.Add(item);
        }
    }

    private void BuildZoomMenu()
    {
        ZoomStatusMenu.Items.Clear();

        var zoomIn = new MenuItem { Header = "확대", InputGestureText = "Ctrl++" };
        zoomIn.Click += ZoomIn_Click;
        ZoomStatusMenu.Items.Add(zoomIn);

        var zoomOut = new MenuItem { Header = "축소", InputGestureText = "Ctrl+-" };
        zoomOut.Click += ZoomOut_Click;
        ZoomStatusMenu.Items.Add(zoomOut);

        ZoomStatusMenu.Items.Add(new Separator());

        foreach (var preset in ZoomPresets)
        {
            var item = new MenuItem { Header = $"{preset}%", Tag = preset };
            item.Click += ZoomPreset_Click;
            ZoomStatusMenu.Items.Add(item);
        }
    }

    private void RefreshRecentFilesMenu()
    {
        RecentFilesMenu.Items.Clear();

        if (Settings.RecentFiles.Count == 0)
        {
            RecentFilesMenu.Items.Add(new MenuItem { Header = "(없음)", IsEnabled = false });
            return;
        }

        var index = 1;
        foreach (var path in Settings.RecentFiles.ToList())
        {
            var item = new MenuItem
            {
                Header = $"_{index} {Path.GetFileName(path)}",
                ToolTip = path,
                Tag = path
            };

            item.Click += RecentFile_Click;
            RecentFilesMenu.Items.Add(item);
            index++;
        }

        RecentFilesMenu.Items.Add(new Separator());

        var clear = new MenuItem { Header = "목록 지우기" };
        clear.Click += ClearRecentFiles_Click;
        RecentFilesMenu.Items.Add(clear);
    }

    private void RefreshEncodingChecks()
    {
        var current = Current?.Encoding;

        foreach (var parent in new[] { EncodingMenuItem, EncodingStatusMenu })
        {
            foreach (var child in parent.Items.OfType<MenuItem>())
            {
                if (child.Tag is EncodingOption option)
                {
                    child.IsChecked = current is not null && ReferenceEquals(option, current);
                }
            }
        }

        if (current is not null)
        {
            EncodingStatusMenu.Header = current.ShortName;
        }
    }

    private void RefreshLineEndingChecks()
    {
        var current = Current?.LineEnding;

        foreach (var parent in new[] { LineEndingMenuItem, LineEndingStatusMenu })
        {
            foreach (var child in parent.Items.OfType<MenuItem>())
            {
                if (child.Tag is LineEndingKind kind)
                {
                    child.IsChecked = current is not null && kind == current.Value;
                }
            }
        }

        if (current is not null)
        {
            LineEndingStatusMenu.Header = current.Value.ToShortName();
        }
    }

    private void RefreshMenuStates()
    {
        _suppressMenuEvents = true;

        WordWrapMenuItem.IsChecked = Settings.WordWrap;
        StatusBarMenuItem.IsChecked = Settings.ShowStatusBar;
        RestoreSessionMenuItem.IsChecked = Settings.RestoreSession;
        ThemeLightMenuItem.IsChecked = Settings.Theme == AppTheme.Light;
        ThemeDarkMenuItem.IsChecked = Settings.Theme == AppTheme.Dark;
        ThemeSystemMenuItem.IsChecked = Settings.Theme == AppTheme.System;

        StatusBarRoot.Visibility = Settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
        ThemeToggleGlyph.Text = ThemeService.IsDarkApplied ? "☀" : "\U0001F319";

        _suppressMenuEvents = false;
    }

    // ==================================================================
    //  메뉴 명령 - 파일
    // ==================================================================

    private void NewTab_Click(object sender, RoutedEventArgs e)
    {
        AddDocument();
        UpdateTitle();
        UpdateStatusBar();
        RefreshEncodingChecks();
        RefreshLineEndingChecks();
        FocusEditor();
    }

    private void NewWindow_Click(object sender, RoutedEventArgs e)
    {
        new MainWindow().Show();
    }

    private void Open_Click(object sender, RoutedEventArgs e) => ShowOpenDialog(null);

    private void OpenWithEncoding_Click(object sender, RoutedEventArgs e)
    {
        var picker = new EncodingPickerDialog(
            "인코딩 지정하여 열기",
            "파일을 읽을 때 사용할 인코딩을 직접 지정합니다. 자동 감지 결과가 깨져 보일 때 사용하세요.",
            Current?.Encoding ?? Settings.DefaultEncoding,
            null,
            "선택")
        {
            Owner = this
        };

        if (picker.ShowDialog() == true)
        {
            ShowOpenDialog(picker.SelectedEncoding);
        }
    }

    private void ShowOpenDialog(EncodingOption? encoding)
    {
        var dialog = new OpenFileDialog
        {
            Filter = FileFilter,
            Multiselect = true,
            Title = encoding is null ? "열기" : $"열기 ({encoding.DisplayName})"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (var file in dialog.FileNames)
        {
            OpenFile(file, encoding);
        }

        FocusEditor();
    }

    /// <summary>현재 문서를 다른 인코딩으로 다시 읽어들인다.</summary>
    private void ReopenWithEncoding_Click(object sender, RoutedEventArgs e)
    {
        var document = Current;
        if (document is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(document.FilePath))
        {
            MessageBox.Show(
                this,
                "저장된 파일만 다른 인코딩으로 다시 열 수 있습니다.",
                AppInfo.TitleSuffix,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (document.IsModified)
        {
            var answer = MessageBox.Show(
                this,
                "저장하지 않은 변경 내용이 사라집니다. 계속할까요?",
                AppInfo.TitleSuffix,
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.OK)
            {
                return;
            }
        }

        var picker = new EncodingPickerDialog(
            "다른 인코딩으로 다시 열기",
            $"'{document.DisplayName}' 을(를) 읽을 인코딩을 선택하세요.",
            document.Encoding,
            null,
            "다시 열기")
        {
            Owner = this
        };

        if (picker.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var loaded = TextFileService.Load(document.FilePath!, picker.SelectedEncoding);
            var caret = document.Editor.CaretIndex;

            LoadTextIntoEditor(document, loaded.Text);
            document.Editor.CaretIndex = Math.Clamp(caret, 0, loaded.Text.Length);
            document.Encoding = picker.SelectedEncoding;
            document.LineEnding = loaded.LineEnding;
            document.DetectionReason = "사용자 지정";
            document.IsModified = false;

            RefreshEncodingChecks();
            RefreshLineEndingChecks();
            UpdateTitle();
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"다시 열 수 없습니다.\n\n{ex.Message}",
                AppInfo.TitleSuffix,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void MakeEncodingDefault_Click(object sender, RoutedEventArgs e)
    {
        var document = Current;
        if (document is null)
        {
            return;
        }

        Settings.DefaultEncodingId = document.Encoding.Id;
        Settings.DefaultLineEnding = document.LineEnding;
        SettingsService.Save(Settings);

        MessageBox.Show(
            this,
            $"새 문서의 기본값이 '{document.Encoding.DisplayName} / {document.LineEnding.ToDisplayName()}' 로 설정되었습니다.",
            AppInfo.TitleSuffix,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void RecentFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path })
        {
            return;
        }

        if (!File.Exists(path))
        {
            MessageBox.Show(
                this,
                $"파일을 찾을 수 없습니다.\n\n{path}",
                AppInfo.TitleSuffix,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Settings.RecentFiles.Remove(path);
            RefreshRecentFilesMenu();
            return;
        }

        OpenFile(path, null);
        FocusEditor();
    }

    private void ClearRecentFiles_Click(object sender, RoutedEventArgs e)
    {
        Settings.RecentFiles.Clear();
        RefreshRecentFilesMenu();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (Current is { } document)
        {
            SaveDocument(document, saveAs: false);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (Current is { } document)
        {
            SaveDocument(document, saveAs: true);
        }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var document = Current;
        if (document is null)
        {
            return;
        }

        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var paragraph = new Paragraph(new Run(document.Text));
        var flow = new FlowDocument(paragraph)
        {
            FontFamily = document.Editor.FontFamily,
            FontSize = Settings.FontSize * 96.0 / 72.0,
            PagePadding = new Thickness(60),
            ColumnWidth = double.PositiveInfinity,
            PageWidth = dialog.PrintableAreaWidth,
            PageHeight = dialog.PrintableAreaHeight
        };

        IDocumentPaginatorSource source = flow;
        dialog.PrintDocument(source.DocumentPaginator, document.DisplayName);
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (Current is { } document)
        {
            CloseDocument(document);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    // ==================================================================
    //  메뉴 명령 - 편집
    // ==================================================================

    private void Undo_Click(object sender, RoutedEventArgs e) => Current?.Editor.Undo();

    private void Redo_Click(object sender, RoutedEventArgs e) => Current?.Editor.Redo();

    private void Cut_Click(object sender, RoutedEventArgs e) => Current?.Editor.Cut();

    private void Copy_Click(object sender, RoutedEventArgs e) => Current?.Editor.Copy();

    private void Paste_Click(object sender, RoutedEventArgs e) => Current?.Editor.Paste();

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var editor = Current?.Editor;
        if (editor is null)
        {
            return;
        }

        if (editor.SelectionLength > 0)
        {
            editor.SelectedText = string.Empty;
        }
        else if (editor.CaretIndex < editor.Text.Length)
        {
            var index = editor.CaretIndex;
            editor.Text = editor.Text.Remove(index, 1);
            editor.CaretIndex = index;
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => Current?.Editor.SelectAll();

    private void TimeDate_Click(object sender, RoutedEventArgs e)
    {
        var editor = Current?.Editor;
        if (editor is null)
        {
            return;
        }

        var stamp = DateTime.Now.ToString("tt h:mm yyyy-MM-dd", CultureInfo.CurrentCulture);
        var index = editor.SelectionStart;
        editor.SelectedText = stamp;
        editor.CaretIndex = index + stamp.Length;
        editor.Focus();
    }

    // ==================================================================
    //  메뉴 명령 - 서식 / 보기
    // ==================================================================

    private void WordWrap_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressMenuEvents)
        {
            return;
        }

        Settings.WordWrap = WordWrapMenuItem.IsChecked;
        ApplyEditorAppearanceToAll();
        SettingsService.Save(Settings);
    }

    private void Font_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new FontPickerDialog(
            Settings.FontFamily,
            Settings.FontSize,
            Settings.FontBold,
            Settings.FontItalic)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        Settings.FontFamily = $"{dialog.SelectedFamily}, Malgun Gothic";
        Settings.FontSize = dialog.SelectedSize;
        Settings.FontBold = dialog.SelectedBold;
        Settings.FontItalic = dialog.SelectedItalic;

        ApplyEditorAppearanceToAll();
        SettingsService.Save(Settings);
    }

    private void EncodingOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: EncodingOption option })
        {
            return;
        }

        var document = Current;
        if (document is null)
        {
            return;
        }

        if (!ReferenceEquals(document.Encoding, option))
        {
            document.Encoding = option;
            document.DetectionReason = "사용자 지정";
            document.IsModified = true;
        }

        RefreshEncodingChecks();
        UpdateTitle();
        UpdateStatusBar();
    }

    private void LineEndingOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: LineEndingKind kind })
        {
            return;
        }

        var document = Current;
        if (document is null)
        {
            return;
        }

        if (document.LineEnding != kind)
        {
            document.LineEnding = kind;
            document.IsModified = true;
        }

        RefreshLineEndingChecks();
        UpdateTitle();
        UpdateStatusBar();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ApplyZoom(Settings.ZoomPercent + 10);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ApplyZoom(Settings.ZoomPercent - 10);

    private void ZoomReset_Click(object sender, RoutedEventArgs e) => ApplyZoom(100);

    private void ZoomPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: int percent })
        {
            ApplyZoom(percent);
        }
    }

    private void ApplyZoom(int percent)
    {
        Settings.ZoomPercent = Math.Clamp(percent, 20, 500);
        ApplyEditorAppearanceToAll();
        UpdateStatusBar();
        SettingsService.Save(Settings);
    }

    private void StatusBar_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressMenuEvents)
        {
            return;
        }

        Settings.ShowStatusBar = StatusBarMenuItem.IsChecked;
        StatusBarRoot.Visibility = Settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
        SettingsService.Save(Settings);
    }

    private void RestoreSession_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressMenuEvents)
        {
            return;
        }

        Settings.RestoreSession = RestoreSessionMenuItem.IsChecked;

        if (!Settings.RestoreSession)
        {
            SessionService.Clear();
        }

        SettingsService.Save(Settings);
    }

    private void ThemeLight_Click(object sender, RoutedEventArgs e) => SetTheme(AppTheme.Light);

    private void ThemeDark_Click(object sender, RoutedEventArgs e) => SetTheme(AppTheme.Dark);

    private void ThemeSystem_Click(object sender, RoutedEventArgs e) => SetTheme(AppTheme.System);

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        => SetTheme(ThemeService.IsDarkApplied ? AppTheme.Light : AppTheme.Dark);

    private void SetTheme(AppTheme theme)
    {
        if (_suppressMenuEvents)
        {
            return;
        }

        Settings.Theme = theme;
        ThemeService.Apply(theme);
        SettingsService.Save(Settings);

        foreach (var window in Application.Current.Windows.OfType<MainWindow>())
        {
            window.RefreshMenuStates();
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutDialog { Owner = this }.ShowDialog();
    }

    // ==================================================================
    //  드래그 앤 드롭
    // ==================================================================

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        foreach (var file in files.Where(File.Exists))
        {
            OpenFile(file, null);
        }

        Activate();
        FocusEditor();
    }

    // ==================================================================
    //  단축키
    // ==================================================================

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        if (e.Key == Key.Escape && FindBar.Visibility == Visibility.Visible)
        {
            CloseFindBar();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5 && !ctrl && !shift)
        {
            TimeDate_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F3)
        {
            if (shift)
            {
                FindPrevious_Click(sender, e);
            }
            else
            {
                FindNext_Click(sender, e);
            }

            e.Handled = true;
            return;
        }

        if (!ctrl)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.T when !shift:
                NewTab_Click(sender, e);
                break;
            case Key.N when shift:
                NewWindow_Click(sender, e);
                break;
            case Key.N when !shift:
                NewTab_Click(sender, e);
                break;
            case Key.O:
                Open_Click(sender, e);
                break;
            case Key.S when shift:
                SaveAs_Click(sender, e);
                break;
            case Key.S:
                Save_Click(sender, e);
                break;
            case Key.W:
                CloseTab_Click(sender, e);
                break;
            case Key.F:
                Find_Click(sender, e);
                break;
            case Key.H:
                Replace_Click(sender, e);
                break;
            case Key.G:
                GoTo_Click(sender, e);
                break;
            case Key.P:
                Print_Click(sender, e);
                break;
            case Key.OemPlus:
            case Key.Add:
                ZoomIn_Click(sender, e);
                break;
            case Key.OemMinus:
            case Key.Subtract:
                ZoomOut_Click(sender, e);
                break;
            case Key.D0:
            case Key.NumPad0:
                ZoomReset_Click(sender, e);
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
