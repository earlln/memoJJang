namespace MemoJJang.Models;

/// <summary>
/// 사각형(열 단위) 선택 영역. 시작점(anchor)과 현재점(caret)을 (줄, 열)로 들고 있으며,
/// 실제 영역은 두 점을 정규화한 사각형이다.
/// </summary>
public sealed class ColumnSelection
{
    public ColumnSelection(int anchorLine, int anchorColumn)
    {
        AnchorLine = anchorLine;
        AnchorColumn = anchorColumn;
        CaretLine = anchorLine;
        CaretColumn = anchorColumn;
    }

    public int AnchorLine { get; }

    public int AnchorColumn { get; }

    public int CaretLine { get; private set; }

    public int CaretColumn { get; private set; }

    public int TopLine => Math.Min(AnchorLine, CaretLine);

    public int BottomLine => Math.Max(AnchorLine, CaretLine);

    public int LeftColumn => Math.Min(AnchorColumn, CaretColumn);

    public int RightColumn => Math.Max(AnchorColumn, CaretColumn);

    public int LineCount => BottomLine - TopLine + 1;

    /// <summary>선택 폭(문자 수). 0이면 세로 캐럿(삽입 지점)만 있는 상태.</summary>
    public int Width => RightColumn - LeftColumn;

    public void MoveCaretTo(int line, int column)
    {
        CaretLine = Math.Max(0, line);
        CaretColumn = Math.Max(0, column);
    }
}
