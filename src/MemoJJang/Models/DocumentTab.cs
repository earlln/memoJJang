using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

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
