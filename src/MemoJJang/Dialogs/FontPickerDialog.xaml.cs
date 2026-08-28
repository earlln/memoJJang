using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MemoJJang.Dialogs;

/// <summary>테마가 적용된 글꼴 선택 대화 상자.</summary>
public partial class FontPickerDialog : Window
{
    private static readonly double[] SizePresets =
    {
        8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 28, 32, 36, 48, 72
    };

    private const string StyleRegular = "보통";
    private const string StyleBold = "굵게";
    private const string StyleItalic = "기울임꼴";
    private const string StyleBoldItalic = "굵은 기울임꼴";

    private readonly List<string> _allFamilies;
    private bool _initialized;

    public FontPickerDialog(string familyName, double sizeInPoints, bool bold, bool italic)
    {
        InitializeComponent();

        _allFamilies = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        FamilyList.ItemsSource = _allFamilies;

        StyleList.ItemsSource = new[] { StyleRegular, StyleBold, StyleItalic, StyleBoldItalic };
        SizeList.ItemsSource = SizePresets.Select(s => s.ToString(CultureInfo.InvariantCulture)).ToList();

        SelectedFamily = familyName;
        SelectedSize = sizeInPoints;
        SelectedBold = bold;
        SelectedItalic = italic;

        // 지정된 글꼴 이름은 "Consolas, Malgun Gothic" 처럼 대체 목록일 수 있으므로 첫 항목만 사용한다.
        var primary = familyName.Split(',')[0].Trim();
        FamilyList.SelectedItem = _allFamilies.FirstOrDefault(
            f => string.Equals(f, primary, StringComparison.OrdinalIgnoreCase)) ?? _allFamilies.FirstOrDefault();

        StyleList.SelectedItem = (bold, italic) switch
        {
            (true, true) => StyleBoldItalic,
            (true, false) => StyleBold,
            (false, true) => StyleItalic,
            _ => StyleRegular
        };

        SizeList.SelectedItem = SizeList.Items
            .Cast<string>()
            .FirstOrDefault(s => Math.Abs(double.Parse(s, CultureInfo.InvariantCulture) - sizeInPoints) < 0.01);

        _initialized = true;
        UpdatePreview();

        Loaded += (_, _) =>
        {
            if (FamilyList.SelectedItem is not null)
            {
                FamilyList.ScrollIntoView(FamilyList.SelectedItem);
            }
        };
    }

    public string SelectedFamily { get; private set; }

    public double SelectedSize { get; private set; }

    public bool SelectedBold { get; private set; }

    public bool SelectedItalic { get; private set; }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var keyword = FilterBox.Text.Trim();
        FamilyList.ItemsSource = string.IsNullOrEmpty(keyword)
            ? _allFamilies
            : _allFamilies.Where(f => f.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    private void Selection_Changed(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (!_initialized)
        {
            return;
        }

        if (FamilyList.SelectedItem is string family)
        {
            PreviewText.FontFamily = new FontFamily($"{family}, Malgun Gothic");
        }

        if (SizeList.SelectedItem is string sizeText &&
            double.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
        {
            // 포인트 → DIP (96dpi 기준)
            PreviewText.FontSize = Math.Clamp(size * 96.0 / 72.0, 6, 200);
        }

        var style = StyleList.SelectedItem as string ?? StyleRegular;
        PreviewText.FontWeight = style is StyleBold or StyleBoldItalic ? FontWeights.Bold : FontWeights.Normal;
        PreviewText.FontStyle = style is StyleItalic or StyleBoldItalic ? FontStyles.Italic : FontStyles.Normal;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (FamilyList.SelectedItem is string family)
        {
            SelectedFamily = family;
        }

        if (SizeList.SelectedItem is string sizeText &&
            double.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
        {
            SelectedSize = size;
        }

        var style = StyleList.SelectedItem as string ?? StyleRegular;
        SelectedBold = style is StyleBold or StyleBoldItalic;
        SelectedItalic = style is StyleItalic or StyleBoldItalic;

        DialogResult = true;
    }
}
