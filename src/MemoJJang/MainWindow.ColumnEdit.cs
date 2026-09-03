using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MemoJJang.Controls;
using MemoJJang.Models;
using MemoJJang.Services;

namespace MemoJJang;

/// <summary>
/// 열 단위(사각형) 편집.
///
/// WPF <see cref="TextBox"/> 에는 사각형 선택이 없으므로 선택 상태와 표시,
/// 입력 처리를 모두 직접 구현한다.
///   · Alt + 드래그          → 사각형 선택
///   · Alt+Shift+C           → 캐럿 위치에서 열 선택 시작
///   · Shift + 방향키        → 선택 영역 확장
///   · 문자 입력             → 선택된 모든 줄의 같은 열에 입력
///   · Backspace / Delete    → 선택된 모든 줄에서 삭제
///   · Ctrl+C / X / V        → 사각형 단위 복사 / 잘라내기 / 붙여넣기
///   · Esc, 일반 클릭        → 열 선택 해제
/// </summary>
public partial class MainWindow
{
    private static LineIndex GetLines(DocumentTab document)
        => document.CachedLineIndex ??= new LineIndex(document.Editor.Text);

    // ==================================================================
    //  진입 / 해제
    // ==================================================================

    private void StartColumnSelection_Click(object sender, RoutedEventArgs e)
    {
        var document = Current;
        if (document is null)
        {
            return;
        }

        EnsureWordWrapOffForColumnMode();

        var lines = GetLines(document);
        var (line, column) = lines.FromIndex(document.Editor.CaretIndex);

        document.Column = new ColumnSelection(line, column);
        document.Editor.Focus();
        UpdateColumnVisual(document);
    }

    private void ClearColumnSelection_Click(object sender, RoutedEventArgs e)
    {
        if (Current is { } document)
        {
            ClearColumnSelection(document);
        }
    }

    private void ClearColumnSelection(DocumentTab document)
    {
        document.Column = null;
        document.IsColumnDragging = false;
        document.ColumnAdorner?.Update(null, null);

        if (ReferenceEquals(document, Current))
        {
            ColumnModeText.Text = string.Empty;
        }
    }

    /// <summary>
    /// 자동 줄 바꿈이 켜져 있으면 한 논리 줄이 여러 줄로 접혀서 사각형 선택이 성립하지 않는다.
    /// 다른 편집기와 마찬가지로 열 편집에 들어갈 때 자동으로 끈다.
    /// </summary>
    private void EnsureWordWrapOffForColumnMode()
    {
        if (!Settings.WordWrap)
        {
            return;
        }

        Settings.WordWrap = false;
        ApplyEditorAppearanceToAll();
        RefreshMenuStates();
        SettingsService.Save(Settings);
    }

    // ==================================================================
    //  마우스
    // ==================================================================

    private void Editor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox { Tag: DocumentTab document } editor)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Alt) != ModifierKeys.Alt)
        {
            // Alt 없이 클릭하면 열 선택을 끝낸다.
            if (document.Column is not null)
            {
                ClearColumnSelection(document);
            }

            return;
        }

        e.Handled = true;
        EnsureWordWrapOffForColumnMode();

        var (line, column) = GetTextPosition(document, e.GetPosition(editor));

        document.Column = new ColumnSelection(line, column);
        document.IsColumnDragging = true;

        editor.Focus();
        SyncEditorCaretToColumn(document);
        editor.CaptureMouse();
        UpdateColumnVisual(document);
    }

    private void Editor_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not TextBox { Tag: DocumentTab document } editor)
        {
            return;
        }

        if (!document.IsColumnDragging || document.Column is null)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndColumnDrag(document, editor);
            return;
        }

        e.Handled = true;

        var (line, column) = GetTextPosition(document, e.GetPosition(editor));
        document.Column.MoveCaretTo(line, column);

        SyncEditorCaretToColumn(document);
        UpdateColumnVisual(document);
    }

    private void Editor_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox { Tag: DocumentTab document } editor)
        {
            return;
        }

        if (document.IsColumnDragging)
        {
            e.Handled = true;
            EndColumnDrag(document, editor);
        }
    }

    private void Editor_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (sender is TextBox { Tag: DocumentTab document } editor && document.IsColumnDragging)
        {
            EndColumnDrag(document, editor);
        }
    }

    private void EndColumnDrag(DocumentTab document, TextBox editor)
    {
        document.IsColumnDragging = false;

        if (editor.IsMouseCaptured)
        {
            editor.ReleaseMouseCapture();
        }

        UpdateColumnVisual(document);
    }

    private static (int Line, int Column) GetTextPosition(DocumentTab document, Point point)
    {
        var editor = document.Editor;
        var index = editor.GetCharacterIndexFromPoint(point, true);

        if (index < 0)
        {
            index = editor.CaretIndex;
        }

        var lines = GetLines(document);
        var (line, column) = lines.FromIndex(index);

        // GetCharacterIndexFromPoint 는 줄 끝을 넘어선 위치를 줄 끝으로 잘라 버린다.
        // 그대로 두면 짧은 줄을 지나며 드래그할 때 사각형이 그 줄 길이만큼 좁아진다.
        // 줄 끝보다 오른쪽을 가리키고 있으면 글자 폭으로 나눠 가상의 열을 더 센다.
        if (column >= lines.LengthOf(line))
        {
            var characterWidth = GetCharacterWidth(document);

            if (characterWidth > 0)
            {
                var rect = Rect.Empty;

                try
                {
                    rect = editor.GetRectFromCharacterIndex(lines.ToIndex(line, column));
                }
                catch
                {
                    // 화면 밖이면 보정하지 않는다.
                }

                if (!rect.IsEmpty && point.X > rect.X)
                {
                    column += (int)Math.Round((point.X - rect.X) / characterWidth);
                }
            }
        }

        return (line, Math.Max(0, column));
    }

    /// <summary>
    /// 글자 하나의 평균 폭. 열 편집은 사실상 고정폭 글꼴을 전제로 하므로
    /// 열 번호를 화면 좌표로 환산하는 데 이 값을 쓴다.
    /// </summary>
    private static double GetCharacterWidth(DocumentTab document)
    {
        if (document.ColumnCharWidth is { } cached)
        {
            return cached;
        }

        var editor = document.Editor;

        try
        {
            var typeface = new Typeface(editor.FontFamily, editor.FontStyle, editor.FontWeight, editor.FontStretch);

            var sample = new FormattedText(
                "0000000000",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                editor.FontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(editor).PixelsPerDip);

            var width = sample.WidthIncludingTrailingWhitespace / 10.0;
            document.ColumnCharWidth = width;
            return width;
        }
        catch
        {
            document.ColumnCharWidth = 0;
            return 0;
        }
    }

    // ==================================================================
    //  키보드 / 문자 입력
    // ==================================================================

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { Tag: DocumentTab document })
        {
            return;
        }

        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

        // Alt 가 눌린 조합에서는 실제 키가 SystemKey 에 담긴다.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Alt+Shift+C : 캐럿 위치에서 열 선택 시작
        if (alt && shift && key == Key.C)
        {
            StartColumnSelection_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (document.Column is null)
        {
            return;
        }

        switch (key)
        {
            case Key.Left when shift:
                MoveColumnCaret(document, 0, -1);
                e.Handled = true;
                return;

            case Key.Right when shift:
                MoveColumnCaret(document, 0, 1);
                e.Handled = true;
                return;

            case Key.Up when shift:
                MoveColumnCaret(document, -1, 0);
                e.Handled = true;
                return;

            case Key.Down when shift:
                MoveColumnCaret(document, 1, 0);
                e.Handled = true;
                return;

            case Key.Back:
                DeleteColumn(document, forward: false);
                e.Handled = true;
                return;

            case Key.Delete:
                DeleteColumn(document, forward: true);
                e.Handled = true;
                return;

            case Key.Tab:
                EditColumnBlock(document, "\t");
                e.Handled = true;
                return;

            case Key.C when ctrl:
                CopyColumn(document, cut: false);
                e.Handled = true;
                return;

            case Key.X when ctrl:
                CopyColumn(document, cut: true);
                e.Handled = true;
                return;

            case Key.V when ctrl:
                PasteColumn(document);
                e.Handled = true;
                return;

            // 아래 키들만 열 선택을 끝내고 평소 동작으로 넘긴다.
            case Key.A when ctrl:
            case Key.Z when ctrl:
            case Key.Y when ctrl:
            case Key.Left:
            case Key.Right:
            case Key.Up:
            case Key.Down:
            case Key.Home:
            case Key.End:
            case Key.PageUp:
            case Key.PageDown:
            case Key.Enter:
            case Key.Escape:
                ClearColumnSelection(document);
                return;
        }

        // 나머지(문자 키 등)는 여기서 건드리지 않는다.
        // 문자 입력은 Editor_PreviewTextInput 이 사각형 단위로 처리한다.
        // 예전에는 여기서 선택을 지워버려서 글자를 치는 순간 열 편집이 풀렸다.
    }

    /// <summary>
    /// 한글처럼 IME 조합을 거치는 입력은 조합 중 편집기가 직접 글자를 넣기 때문에
    /// 열 편집과 섞이면 내용이 어긋난다. 조합이 시작되면 열 선택을 풀고 평소 입력으로 넘긴다.
    /// </summary>
    private void Editor_PreviewTextInputUpdate(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox { Tag: DocumentTab document } &&
            document.Column is not null &&
            e.TextComposition.CompositionText.Length > 0)
        {
            ClearColumnSelection(document);
        }
    }

    private void Editor_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox { Tag: DocumentTab document } || document.Column is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        EditColumnBlock(document, e.Text);
        e.Handled = true;
    }

    private void MoveColumnCaret(DocumentTab document, int lineDelta, int columnDelta)
    {
        var column = document.Column;
        if (column is null)
        {
            return;
        }

        var lines = GetLines(document);

        column.MoveCaretTo(
            Math.Clamp(column.CaretLine + lineDelta, 0, lines.Count - 1),
            Math.Max(0, column.CaretColumn + columnDelta));

        SyncEditorCaretToColumn(document);
        UpdateColumnVisual(document);
    }

    private void SyncEditorCaretToColumn(DocumentTab document)
    {
        var column = document.Column;
        if (column is null)
        {
            return;
        }

        var lines = GetLines(document);
        document.Editor.Select(lines.ToIndex(column.CaretLine, column.CaretColumn), 0);
    }

    // ==================================================================
    //  편집
    // ==================================================================

    private void EditColumnBlock(DocumentTab document, string text)
    {
        var column = document.Column;
        if (column is null)
        {
            return;
        }

        EditColumn(document, column.LeftColumn, column.RightColumn, _ => text);
    }

    private void DeleteColumn(DocumentTab document, bool forward)
    {
        var column = document.Column;
        if (column is null)
        {
            return;
        }

        if (column.Width > 0)
        {
            EditColumn(document, column.LeftColumn, column.RightColumn, _ => string.Empty);
            return;
        }

        // 폭이 없는 세로 캐럿 상태에서는 모든 줄에서 한 글자씩 지운다.
        if (forward)
        {
            EditColumn(document, column.LeftColumn, column.LeftColumn + 1, _ => string.Empty);
        }
        else if (column.LeftColumn > 0)
        {
            EditColumn(document, column.LeftColumn - 1, column.LeftColumn, _ => string.Empty);
        }
    }

    /// <summary>
    /// 선택된 각 줄의 [left, right) 구간을 <paramref name="replacement"/> 결과로 바꾼다.
    /// 인자는 선택 영역 안에서의 줄 번호(0부터).
    /// </summary>
    private void EditColumn(DocumentTab document, int left, int right, Func<int, string> replacement)
    {
        var column = document.Column;
        if (column is null)
        {
            return;
        }

        var editor = document.Editor;
        var lines = GetLines(document);

        var topLine = Math.Clamp(column.TopLine, 0, lines.Count - 1);
        var bottomLine = Math.Clamp(column.BottomLine, 0, lines.Count - 1);
        var anchorLine = Math.Clamp(column.AnchorLine, 0, lines.Count - 1);
        var caretLine = Math.Clamp(column.CaretLine, 0, lines.Count - 1);

        left = Math.Max(0, left);
        right = Math.Max(left, right);

        // 아래 줄부터 고쳐야 위쪽 줄의 문자 위치가 어긋나지 않는다.
        editor.BeginChange();
        try
        {
            for (var line = bottomLine; line >= topLine; line--)
            {
                var text = replacement(line - topLine) ?? string.Empty;
                var lineLength = lines.LengthOf(line);

                if (text.Length == 0 && lineLength <= left)
                {
                    continue;
                }

                if (lineLength < left)
                {
                    // 줄이 짧으면 공백으로 채워 열을 맞춘다.
                    editor.Select(lines.ToIndex(line, lineLength), 0);
                    editor.SelectedText = new string(' ', left - lineLength) + text;
                    continue;
                }

                var start = lines.ToIndex(line, left);
                var end = lines.ToIndex(line, Math.Min(right, lineLength));

                editor.Select(start, Math.Max(0, end - start));
                editor.SelectedText = text;
            }
        }
        finally
        {
            editor.EndChange();
        }

        // 편집이 끝나면 블록은 폭 0 의 세로 캐럿으로 접힌다.
        var caretOffset = Math.Clamp(caretLine - topLine, 0, bottomLine - topLine);
        var newColumn = left + (replacement(caretOffset) ?? string.Empty).Length;

        var updated = new ColumnSelection(anchorLine, newColumn);
        updated.MoveCaretTo(caretLine, newColumn);
        document.Column = updated;

        SyncEditorCaretToColumn(document);
        UpdateColumnVisual(document);
    }

    // ==================================================================
    //  클립보드
    // ==================================================================

    private void CopyColumn(DocumentTab document, bool cut)
    {
        var column = document.Column;
        if (column is null)
        {
            return;
        }

        var text = GetColumnText(document);

        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // 다른 앱이 클립보드를 붙잡고 있으면 복사만 실패한다.
            return;
        }

        if (cut && column.Width > 0)
        {
            EditColumn(document, column.LeftColumn, column.RightColumn, _ => string.Empty);
        }
    }

    private string GetColumnText(DocumentTab document)
    {
        var column = document.Column;
        if (column is null)
        {
            return string.Empty;
        }

        var lines = GetLines(document);
        var text = document.Editor.Text;
        var builder = new StringBuilder();

        var topLine = Math.Clamp(column.TopLine, 0, lines.Count - 1);
        var bottomLine = Math.Clamp(column.BottomLine, 0, lines.Count - 1);

        for (var line = topLine; line <= bottomLine; line++)
        {
            if (line > topLine)
            {
                builder.Append("\r\n");
            }

            var lineLength = lines.LengthOf(line);
            var start = lines.ToIndex(line, Math.Min(column.LeftColumn, lineLength));
            var end = lines.ToIndex(line, Math.Min(column.RightColumn, lineLength));

            if (end > start)
            {
                builder.Append(text, start, end - start);
            }
        }

        return builder.ToString();
    }

    private void PasteColumn(DocumentTab document)
    {
        var column = document.Column;
        if (column is null)
        {
            return;
        }

        string clip;
        try
        {
            clip = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
        }
        catch
        {
            return;
        }

        if (clip.Length == 0)
        {
            return;
        }

        var pieces = clip.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (pieces.Length == column.LineCount)
        {
            // 줄 수가 맞으면 한 줄씩 대응시켜 붙여 넣는다.
            EditColumn(document, column.LeftColumn, column.RightColumn,
                offset => pieces[Math.Clamp(offset, 0, pieces.Length - 1)]);
            return;
        }

        if (pieces.Length == 1)
        {
            EditColumn(document, column.LeftColumn, column.RightColumn, _ => pieces[0]);
            return;
        }

        // 줄 수가 맞지 않는 여러 줄 붙여넣기는 일반 붙여넣기로 처리한다.
        var lines = GetLines(document);
        var caret = lines.ToIndex(column.TopLine, column.LeftColumn);

        ClearColumnSelection(document);
        document.Editor.Select(caret, 0);
        document.Editor.Paste();
    }

    // ==================================================================
    //  표시 갱신
    // ==================================================================

    private void Editor_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is TextBox { Tag: DocumentTab document, IsLoaded: true })
        {
            document.ColumnAdorner?.Update(document.Column, GetLines(document));
        }
    }

    private void Editor_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is TextBox { Tag: DocumentTab document })
        {
            document.ColumnAdorner?.Update(document.Column, GetLines(document));
        }
    }

    /// <summary>
    /// 어도너는 창 전체의 장식 계층에 붙는다. 탭을 옮기면 이전 탭의 선택 표시가
    /// 남아 보일 수 있으므로, 현재 탭이 아닌 문서의 표시는 지운다.
    /// </summary>
    private void RefreshColumnAdorners()
    {
        foreach (var document in _documents)
        {
            if (ReferenceEquals(document, Current))
            {
                UpdateColumnVisual(document);
            }
            else
            {
                document.ColumnAdorner?.Update(null, null);
            }
        }
    }

    private void UpdateColumnVisual(DocumentTab document)
    {
        if (document.Column is null)
        {
            document.ColumnAdorner?.Update(null, null);

            if (ReferenceEquals(document, Current))
            {
                ColumnModeText.Text = string.Empty;
            }

            return;
        }

        EnsureColumnAdorner(document);
        document.ColumnAdorner?.Update(document.Column, GetLines(document));

        if (ReferenceEquals(document, Current))
        {
            ColumnModeText.Text = document.Column.Width > 0
                ? $"열 선택 {document.Column.LineCount}줄 × {document.Column.Width}자"
                : $"열 선택 {document.Column.LineCount}줄";
        }
    }

    private static void EnsureColumnAdorner(DocumentTab document)
    {
        if (document.ColumnAdorner is not null)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(document.Editor);
        if (layer is null)
        {
            return;
        }

        var adorner = new ColumnSelectionAdorner(document.Editor);
        adorner.SetResourceReference(ColumnSelectionAdorner.FillBrushProperty, "App.Selection");
        adorner.SetResourceReference(ColumnSelectionAdorner.CaretBrushProperty, "App.Caret");

        layer.Add(adorner);
        document.ColumnAdorner = adorner;
    }
}
