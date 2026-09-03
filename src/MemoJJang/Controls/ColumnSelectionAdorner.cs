using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MemoJJang.Models;

namespace MemoJJang.Controls;

/// <summary>
/// 편집기 위에 사각형(열 단위) 선택 영역을 그리는 장식자.
/// TextBox 자체에는 사각형 선택이 없으므로 선택 표시를 직접 그린다.
/// </summary>
public sealed class ColumnSelectionAdorner : Adorner
{
    private readonly TextBox _editor;

    public ColumnSelectionAdorner(TextBox editor) : base(editor)
    {
        _editor = editor;
        IsHitTestVisible = false;
    }

    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush),
        typeof(Brush),
        typeof(ColumnSelectionAdorner),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CaretBrushProperty = DependencyProperty.Register(
        nameof(CaretBrush),
        typeof(Brush),
        typeof(ColumnSelectionAdorner),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush? FillBrush
    {
        get => (Brush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public Brush? CaretBrush
    {
        get => (Brush?)GetValue(CaretBrushProperty);
        set => SetValue(CaretBrushProperty, value);
    }

    /// <summary>선택 영역을 덮는 정도. 아래의 글자가 비쳐 보여야 한다.</summary>
    private const double FillOpacity = 0.38;

    private Pen? _outlinePen;
    private Brush? _outlineSource;

    /// <summary>현재 표시할 선택 영역. null 이면 아무것도 그리지 않는다.</summary>
    public ColumnSelection? Selection { get; private set; }

    /// <summary>줄 위치 계산에 쓰는 색인. 매 프레임 다시 만들지 않도록 밖에서 넘겨받는다.</summary>
    private LineIndex? _lines;

    public void Update(ColumnSelection? selection, LineIndex? lines)
    {
        Selection = selection;
        _lines = lines;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var selection = Selection;
        if (selection is null)
        {
            return;
        }

        var fill = FillBrush;
        var caret = CaretBrush;
        if (fill is null)
        {
            return;
        }

        var index = _lines ?? new LineIndex(_editor.Text);
        var viewport = new Rect(0, 0, _editor.ActualWidth, _editor.ActualHeight);

        var top = Math.Min(selection.TopLine, index.Count - 1);
        var bottom = Math.Min(selection.BottomLine, index.Count - 1);

        for (var line = top; line <= bottom; line++)
        {
            var lineLength = index.LengthOf(line);

            // 줄이 짧아 선택 영역이 걸치지 않는 경우에도 세로 캐럿은 보여야 하므로
            // 왼쪽 열은 줄 끝까지만 잘라서 사용한다.
            var startColumn = Math.Min(selection.LeftColumn, lineLength);
            var endColumn = Math.Min(selection.RightColumn, lineLength);

            var startRect = SafeRect(index.ToIndex(line, startColumn));
            if (startRect.IsEmpty)
            {
                continue;
            }

            var endRect = startColumn == endColumn ? startRect : SafeRect(index.ToIndex(line, endColumn));
            if (endRect.IsEmpty)
            {
                endRect = startRect;
            }

            var x = Math.Min(startRect.X, endRect.X);
            var width = Math.Abs(endRect.X - startRect.X);
            var rect = new Rect(x, startRect.Y, width, Math.Max(startRect.Height, endRect.Height));

            if (width <= 0.5)
            {
                // 폭이 없는 상태(세로 캐럿)는 얇은 선으로 표시한다.
                if (caret is not null)
                {
                    var caretRect = Rect.Intersect(new Rect(x, rect.Y, 1.4, rect.Height), viewport);
                    if (!caretRect.IsEmpty)
                    {
                        drawingContext.DrawRectangle(caret, null, caretRect);
                    }
                }

                continue;
            }

            var visible = Rect.Intersect(rect, viewport);
            if (visible.IsEmpty)
            {
                continue;
            }

            // 선택 브러시는 불투명하다. 그대로 칠하면 아래의 글자가 가려지므로
            // 반투명하게 덮고 테두리만 또렷하게 그린다.
            drawingContext.PushOpacity(FillOpacity);
            drawingContext.DrawRectangle(fill, null, visible);
            drawingContext.Pop();

            if (caret is not null)
            {
                drawingContext.DrawRectangle(null, GetOutlinePen(caret), visible);
            }
        }
    }

    private Pen GetOutlinePen(Brush source)
    {
        if (_outlinePen is not null && ReferenceEquals(_outlineSource, source))
        {
            return _outlinePen;
        }

        var brush = source.Clone();
        brush.Opacity = 0.55;
        brush.Freeze();

        _outlinePen = new Pen(brush, 1);
        _outlinePen.Freeze();
        _outlineSource = source;

        return _outlinePen;
    }

    private Rect SafeRect(int characterIndex)
    {
        try
        {
            return _editor.GetRectFromCharacterIndex(characterIndex);
        }
        catch
        {
            // 레이아웃이 준비되지 않았거나 화면 밖이면 그리지 않는다.
            return Rect.Empty;
        }
    }
}
