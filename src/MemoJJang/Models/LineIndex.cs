namespace MemoJJang.Models;

/// <summary>
/// 텍스트의 줄 시작 위치를 미리 계산해 두고 (줄, 열) ↔ 문자 인덱스 변환을 돕는다.
/// 편집기 안의 텍스트는 항상 "\r\n" 으로 정규화되어 있다는 전제를 사용한다.
/// </summary>
public sealed class LineIndex
{
    private readonly string _text;
    private readonly List<int> _starts = new();

    public LineIndex(string text)
    {
        _text = text ?? string.Empty;
        _starts.Add(0);

        for (var i = 0; i < _text.Length; i++)
        {
            if (_text[i] == '\n')
            {
                _starts.Add(i + 1);
            }
        }
    }

    /// <summary>줄 수 (1 이상).</summary>
    public int Count => _starts.Count;

    public int StartOf(int line) => _starts[Math.Clamp(line, 0, Count - 1)];

    /// <summary>줄 바꿈 문자를 제외한 줄의 끝 인덱스.</summary>
    public int EndOf(int line)
    {
        line = Math.Clamp(line, 0, Count - 1);

        var end = line < Count - 1 ? _starts[line + 1] : _text.Length;

        // 줄 바꿈 문자는 줄 내용에 포함하지 않는다.
        while (end > _starts[line] && (_text[end - 1] == '\n' || _text[end - 1] == '\r'))
        {
            end--;
        }

        return end;
    }

    public int LengthOf(int line) => EndOf(line) - StartOf(line);

    /// <summary>(줄, 열)을 문자 인덱스로. 줄 길이를 넘는 열은 줄 끝으로 잘린다.</summary>
    public int ToIndex(int line, int column)
    {
        line = Math.Clamp(line, 0, Count - 1);
        var start = StartOf(line);
        var length = EndOf(line) - start;
        return start + Math.Clamp(column, 0, length);
    }

    /// <summary>문자 인덱스를 (줄, 열)로.</summary>
    public (int Line, int Column) FromIndex(int index)
    {
        index = Math.Clamp(index, 0, _text.Length);

        // 이진 탐색으로 index 이하의 마지막 줄 시작을 찾는다.
        var low = 0;
        var high = _starts.Count - 1;

        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (_starts[mid] <= index)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        var column = Math.Min(index - _starts[low], EndOf(low) - _starts[low]);
        return (low, column);
    }
}
