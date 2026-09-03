using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using MemoJJang.Controls;

namespace MemoJJang.Models;

/// <summary>
/// 열려 있는 문서 하나(= 탭 하나)의 상태.
/// 탭마다 자신의 <see cref="TextBox"/> 인스턴스를 유지하므로
/// 탭을 옮겨 다녀도 실행 취소 기록과 캐럿 위치가 보존된다.
/// </summary>
public sealed class DocumentTab : INotifyPropertyChanged
{
    private string? _filePath;
    private bool _isModified;
    private EncodingOption _encoding;
    private LineEndingKind _lineEnding;
    private string _untitledName;

    public DocumentTab(TabItem item, TextBox editor, string untitledName,
                       EncodingOption encoding, LineEndingKind lineEnding)
    {
        Item = item;
        Editor = editor;
        _untitledName = untitledName;
        _encoding = encoding;
        _lineEnding = lineEnding;
    }

    public TabItem Item { get; }

    public TextBox Editor { get; }

    /// <summary>탭의 내용 전체(편집기 + 분할선 + 미리 보기)를 담는 격자.</summary>
    public Grid? Root { get; set; }

    /// <summary>Markdown 미리 보기 패널. 미리 보기를 처음 켤 때 만들어진다.</summary>
    public FlowDocumentScrollViewer? Preview { get; set; }

    /// <summary>편집기와 미리 보기 사이의 분할선.</summary>
    public GridSplitter? Splitter { get; set; }

    /// <summary>이 문서에서 미리 보기가 열려 있는지.</summary>
    public bool IsPreviewVisible { get; set; }

    /// <summary>사각형(열 단위) 선택 상태. null 이면 열 편집 모드가 꺼진 것.</summary>
    public ColumnSelection? Column { get; set; }

    /// <summary>사각형 선택을 그리는 장식자.</summary>
    public ColumnSelectionAdorner? ColumnAdorner { get; set; }

    /// <summary>열 선택을 마우스로 끌고 있는 중인지.</summary>
    public bool IsColumnDragging { get; set; }

    /// <summary>줄 시작 위치 캐시. 텍스트가 바뀌면 무효가 된다.</summary>
    public LineIndex? CachedLineIndex { get; set; }

    /// <summary>열 편집에서 쓰는 글자 하나의 평균 폭. 글꼴이나 확대 비율이 바뀌면 무효가 된다.</summary>
    public double? ColumnCharWidth { get; set; }

    /// <summary>파일을 읽어 넣는 동안 TextChanged 를 무시하기 위한 표시.</summary>
    public bool IsLoading { get; set; }

    /// <summary>자동 감지 결과 설명 (상태 표시줄 도움말).</summary>
    public string DetectionReason { get; set; } = string.Empty;

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value)
            {
                return;
            }

            _filePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(ToolTipText));
        }
    }

    public string UntitledName
    {
        get => _untitledName;
        set
        {
            if (_untitledName == value)
            {
                return;
            }

            _untitledName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(ToolTipText));
        }
    }

    public bool IsModified
    {
        get => _isModified;
        set
        {
            if (_isModified == value)
            {
                return;
            }

            _isModified = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModifiedMarkVisibility));
        }
    }

    public EncodingOption Encoding
    {
        get => _encoding;
        set
        {
            if (ReferenceEquals(_encoding, value))
            {
                return;
            }

            _encoding = value;
            OnPropertyChanged();
        }
    }

    public LineEndingKind LineEnding
    {
        get => _lineEnding;
        set
        {
            if (_lineEnding == value)
            {
                return;
            }

            _lineEnding = value;
            OnPropertyChanged();
        }
    }

    /// <summary>탭에 표시되는 이름.</summary>
    public string DisplayName =>
        string.IsNullOrEmpty(FilePath) ? UntitledName : Path.GetFileName(FilePath);

    public string ToolTipText =>
        string.IsNullOrEmpty(FilePath) ? UntitledName : FilePath!;

    public Visibility ModifiedMarkVisibility =>
        IsModified ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>편집기 안의 텍스트 (항상 CRLF 로 정규화되어 있다).</summary>
    public string Text => Editor.Text;

    /// <summary>내용이 비어 있고 저장된 적도 없는 "깨끗한" 새 탭인지.</summary>
    public bool IsPristineUntitled =>
        FilePath is null && !IsModified && Editor.Text.Length == 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
