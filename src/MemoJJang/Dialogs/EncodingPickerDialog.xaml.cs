using System.Windows;
using System.Windows.Controls;
using MemoJJang.Models;

namespace MemoJJang.Dialogs;

/// <summary>
/// 인코딩(및 필요 시 줄 바꿈 형식)을 고르는 공용 대화 상자.
/// "인코딩 지정하여 열기" 와 "다른 이름으로 저장" 양쪽에서 사용한다.
/// </summary>
public partial class EncodingPickerDialog : Window
{
    private sealed record LineEndingEntry(LineEndingKind Kind, string Label)
    {
        public override string ToString() => Label;
    }

    public EncodingPickerDialog(string title,
                                string description,
                                EncodingOption current,
                                LineEndingKind? currentLineEnding,
                                string okText = "확인")
    {
        InitializeComponent();

        Title = title;
        DescriptionText.Text = description;
        OkButton.Content = okText;

        EncodingList.ItemsSource = EncodingCatalog.All;
        EncodingList.SelectedItem = EncodingCatalog.All.Contains(current) ? current : EncodingCatalog.Default;

        if (currentLineEnding is null)
        {
            LineEndingPanel.Visibility = Visibility.Collapsed;
            SelectedLineEnding = LineEndingKind.CrLf;
        }
        else
        {
            var entries = new[]
            {
                new LineEndingEntry(LineEndingKind.CrLf, LineEndingKind.CrLf.ToDisplayName()),
                new LineEndingEntry(LineEndingKind.Lf, LineEndingKind.Lf.ToDisplayName()),
                new LineEndingEntry(LineEndingKind.Cr, LineEndingKind.Cr.ToDisplayName())
            };

            LineEndingList.ItemsSource = entries;
            LineEndingList.SelectedItem =
                Array.Find(entries, entry => entry.Kind == currentLineEnding.Value) ?? entries[0];
            SelectedLineEnding = currentLineEnding.Value;
        }

        UpdateHint();
    }

    public EncodingOption SelectedEncoding { get; private set; } = EncodingCatalog.Default;

    public LineEndingKind SelectedLineEnding { get; private set; }

    private void EncodingList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateHint();

    private void UpdateHint()
    {
        if (EncodingList.SelectedItem is not EncodingOption option)
        {
            EncodingHintText.Text = string.Empty;
            return;
        }

        var bom = option.HasBom
            ? "BOM(바이트 순서 표식)을 파일 앞에 기록합니다."
            : "BOM 없이 저장합니다.";

        EncodingHintText.Text = $"코드 페이지 {option.CodePage} · {bom}";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (EncodingList.SelectedItem is EncodingOption option)
        {
            SelectedEncoding = option;
        }

        if (LineEndingPanel.Visibility == Visibility.Visible &&
            LineEndingList.SelectedItem is LineEndingEntry entry)
        {
            SelectedLineEnding = entry.Kind;
        }

        DialogResult = true;
    }
}
