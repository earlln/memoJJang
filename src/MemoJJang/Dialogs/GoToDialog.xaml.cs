using System.Windows;
using System.Windows.Input;

namespace MemoJJang.Dialogs;

public partial class GoToDialog : Window
{
    private readonly int _maxLine;

    public GoToDialog(int currentLine, int maxLine)
    {
        InitializeComponent();

        _maxLine = Math.Max(1, maxLine);
        HintText.Text = $"1 ~ {_maxLine} 사이의 줄 번호를 입력하세요.";
        LineNumberBox.Text = currentLine.ToString();
        Loaded += (_, _) =>
        {
            LineNumberBox.SelectAll();
            LineNumberBox.Focus();
        };
    }

    /// <summary>확인을 눌렀을 때 이동할 1-기반 줄 번호.</summary>
    public int SelectedLine { get; private set; } = 1;

    private void Ok_Click(object sender, RoutedEventArgs e) => Commit();

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void LineNumberBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Commit();
        }
    }

    private void Commit()
    {
        if (!int.TryParse(LineNumberBox.Text.Trim(), out var line) || line < 1)
        {
            HintText.Text = "올바른 줄 번호를 입력하세요.";
            LineNumberBox.SelectAll();
            LineNumberBox.Focus();
            return;
        }

        SelectedLine = Math.Min(line, _maxLine);
        DialogResult = true;
    }
}
