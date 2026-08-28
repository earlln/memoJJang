using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MemoJJang.Dialogs;
using MemoJJang.Models;

namespace MemoJJang;

/// <summary>찾기 / 바꾸기 / 줄 이동 기능.</summary>
public partial class MainWindow
{
    // ==================================================================
    //  찾기 막대 표시
    // ==================================================================

    private void Find_Click(object sender, RoutedEventArgs e) => ShowFindBar(withReplace: false);

    private void Replace_Click(object sender, RoutedEventArgs e) => ShowFindBar(withReplace: true);

    private void ShowFindBar(bool withReplace)
    {
        FindBar.Visibility = Visibility.Visible;
        ReplaceRow.Visibility = withReplace ? Visibility.Visible : Visibility.Collapsed;

        var editor = Current?.Editor;
        if (editor is not null && editor.SelectionLength > 0)
        {
            var selected = editor.SelectedText;
            if (!selected.Contains('\n') && !selected.Contains('\r'))
            {
                FindTextBox.Text = selected;
            }
        }

        FindTextBox.Focus();
        FindTextBox.SelectAll();
        UpdateFindResult();
    }

    private void CloseFindBar_Click(object sender, RoutedEventArgs e) => CloseFindBar();

    private void CloseFindBar()
    {
        FindBar.Visibility = Visibility.Collapsed;
        ReplaceRow.Visibility = Visibility.Collapsed;
        FocusEditor();
    }

    private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateFindResult();
    }

    private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            FindNext(forward: false);
        }
        else
        {
            FindNext(forward: true);
        }
    }

    private void ReplaceTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ReplaceOne_Click(sender, e);
        }
    }

    private void SearchOption_Changed(object sender, RoutedEventArgs e) => UpdateFindResult();

    // ==================================================================
    //  찾기
    // ==================================================================

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        if (FindBar.Visibility != Visibility.Visible && string.IsNullOrEmpty(FindTextBox.Text))
        {
            ShowFindBar(withReplace: false);
            return;
        }

        FindNext(forward: true);
    }

    private void FindPrevious_Click(object sender, RoutedEventArgs e)
    {
        if (FindBar.Visibility != Visibility.Visible && string.IsNullOrEmpty(FindTextBox.Text))
        {
            ShowFindBar(withReplace: false);
            return;
        }

        FindNext(forward: false);
    }

    private bool FindNext(bool forward)
    {
        var editor = Current?.Editor;
        var term = FindTextBox.Text;

        if (editor is null || string.IsNullOrEmpty(term))
        {
            return false;
        }

        var text = editor.Text;
        var comparison = MatchCaseToggle.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var wholeWord = WholeWordToggle.IsChecked == true;
        var wrap = WrapAroundToggle.IsChecked == true;

        var start = forward
            ? editor.SelectionStart + (editor.SelectionLength > 0 ? 1 : 0)
            : editor.SelectionStart - 1;

        var index = FindIndex(text, term, start, forward, comparison, wholeWord, wrap);

        if (index < 0)
        {
            FindResultText.Text = "결과 없음";
            return false;
        }

        SelectAndReveal(editor, index, term.Length);
        UpdateFindResult();
        return true;
    }

    private static int FindIndex(string text, string term, int start, bool forward,
                                 StringComparison comparison, bool wholeWord, bool wrap)
    {
        if (term.Length == 0 || text.Length < term.Length)
        {
            return -1;
        }

        if (forward)
        {
            var from = Math.Clamp(start, 0, text.Length);
            var index = FindForward(text, term, from, comparison, wholeWord);

            if (index < 0 && wrap)
            {
                index = FindForward(text, term, 0, comparison, wholeWord);
            }

            return index;
        }

        var backFrom = Math.Min(start, text.Length - term.Length);
        var result = backFrom < 0 ? -1 : FindBackward(text, term, backFrom, comparison, wholeWord);

        if (result < 0 && wrap)
        {
            result = FindBackward(text, term, text.Length - term.Length, comparison, wholeWord);
        }

        return result;
    }

    private static int FindForward(string text, string term, int from,
                                   StringComparison comparison, bool wholeWord)
    {
        var i = from;

        while (i <= text.Length - term.Length)
        {
            var index = text.IndexOf(term, i, comparison);
            if (index < 0)
            {
                return -1;
            }

            if (!wholeWord || IsWholeWord(text, index, term.Length))
            {
                return index;
            }

            i = index + 1;
        }

        return -1;
    }

    private static int FindBackward(string text, string term, int from,
                                    StringComparison comparison, bool wholeWord)
    {
        var i = Math.Min(from, text.Length - term.Length);

        while (i >= 0)
        {
            if (string.Compare(text, i, term, 0, term.Length, comparison) == 0 &&
                (!wholeWord || IsWholeWord(text, i, term.Length)))
            {
                return i;
            }

            i--;
        }

        return -1;
    }

    private static bool IsWholeWord(string text, int index, int length)
    {
        if (index > 0 && IsWordChar(text[index - 1]))
        {
            return false;
        }

        var end = index + length;
        return end >= text.Length || !IsWordChar(text[end]);
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static void SelectAndReveal(TextBox editor, int index, int length)
    {
        editor.Select(index, length);

        try
        {
            editor.ScrollToLine(editor.GetLineIndexFromCharacterIndex(index));
        }
        catch
        {
            // 레이아웃이 아직 준비되지 않았으면 스크롤은 건너뛴다.
        }
    }

    private void UpdateFindResult()
    {
        var editor = Current?.Editor;
        var term = FindTextBox.Text;

        if (editor is null || string.IsNullOrEmpty(term))
        {
            FindResultText.Text = string.Empty;
            return;
        }

        var text = editor.Text;
        var comparison = MatchCaseToggle.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var wholeWord = WholeWordToggle.IsChecked == true;

        var total = 0;
        var current = 0;
        var cursor = 0;

        while (cursor <= text.Length - term.Length)
        {
            var index = FindForward(text, term, cursor, comparison, wholeWord);
            if (index < 0)
            {
                break;
            }

            total++;

            if (index == editor.SelectionStart && editor.SelectionLength == term.Length)
            {
                current = total;
            }

            cursor = index + Math.Max(1, term.Length);
        }

        FindResultText.Text = total == 0
            ? "결과 없음"
            : current > 0 ? $"{current} / {total}" : $"{total}개 찾음";
    }

    // ==================================================================
    //  바꾸기
    // ==================================================================

    private void ReplaceOne_Click(object sender, RoutedEventArgs e)
    {
        var editor = Current?.Editor;
        var term = FindTextBox.Text;

        if (editor is null || string.IsNullOrEmpty(term))
        {
            return;
        }

        var comparison = MatchCaseToggle.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        // 이미 검색어가 선택되어 있으면 그 자리를 바꾸고, 아니면 먼저 찾는다.
        if (editor.SelectionLength == term.Length &&
            string.Equals(editor.SelectedText, term, comparison))
        {
            var start = editor.SelectionStart;
            var replacement = ReplaceTextBox.Text;

            editor.SelectedText = replacement;
            editor.Select(start + replacement.Length, 0);
        }

        FindNext(forward: true);
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        var document = Current;
        var term = FindTextBox.Text;

        if (document is null || string.IsNullOrEmpty(term))
        {
            return;
        }

        var editor = document.Editor;
        var text = editor.Text;
        var replacement = ReplaceTextBox.Text;

        var comparison = MatchCaseToggle.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var wholeWord = WholeWordToggle.IsChecked == true;

        var builder = new System.Text.StringBuilder(text.Length);
        var cursor = 0;
        var count = 0;

        while (cursor <= text.Length - term.Length)
        {
            var index = FindForward(text, term, cursor, comparison, wholeWord);
            if (index < 0)
            {
                break;
            }

            builder.Append(text, cursor, index - cursor);
            builder.Append(replacement);
            cursor = index + term.Length;
            count++;
        }

        if (count == 0)
        {
            FindResultText.Text = "결과 없음";
            return;
        }

        builder.Append(text, cursor, text.Length - cursor);

        var caret = Math.Min(editor.CaretIndex, builder.Length);
        editor.SelectAll();
        editor.SelectedText = builder.ToString();
        editor.Select(Math.Min(caret, editor.Text.Length), 0);

        FindResultText.Text = $"{count}개 바꿈";
        UpdateTitle();
        UpdateStatusBar();
    }

    // ==================================================================
    //  줄 이동
    // ==================================================================

    private void GoTo_Click(object sender, RoutedEventArgs e)
    {
        var editor = Current?.Editor;
        if (editor is null)
        {
            return;
        }

        var text = editor.Text;
        var totalLines = 1;

        foreach (var c in text)
        {
            if (c == '\n')
            {
                totalLines++;
            }
        }

        var caret = Math.Clamp(editor.CaretIndex, 0, text.Length);
        var currentLine = 1;

        for (var i = 0; i < caret; i++)
        {
            if (text[i] == '\n')
            {
                currentLine++;
            }
        }

        var dialog = new GoToDialog(currentLine, totalLines) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var target = dialog.SelectedLine;
        var index = 0;
        var line = 1;

        for (var i = 0; i < text.Length && line < target; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                index = i + 1;
            }
        }

        if (line < target)
        {
            index = text.Length;
        }

        SelectAndReveal(editor, index, 0);
        FocusEditor();
    }
}
