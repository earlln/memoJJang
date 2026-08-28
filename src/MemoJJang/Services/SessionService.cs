using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MemoJJang.Models;

namespace MemoJJang.Services;

/// <summary>세션에 보관되는 문서 하나의 상태.</summary>
public sealed class SessionDocument
{
    public string? FilePath { get; set; }

    public string UntitledName { get; set; } = AppInfo.UntitledName;

    public string EncodingId { get; set; } = EncodingCatalog.Default.Id;

    public LineEndingKind LineEnding { get; set; } = LineEndingKind.CrLf;

    public bool IsModified { get; set; }

    /// <summary>저장되지 않은 내용을 담아 둔 버퍼 파일 이름.</summary>
    public string? BufferFile { get; set; }

    public int CaretIndex { get; set; }
}

public sealed class SessionState
{
    public List<SessionDocument> Documents { get; set; } = new();

    public int SelectedIndex { get; set; }
}

/// <summary>
/// 종료 시 열려 있던 탭(저장되지 않은 내용 포함)을 보존했다가 다음 실행에서 복원한다.
/// Windows 11 메모장의 세션 유지 동작에 해당한다.
/// </summary>
public static class SessionService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static SessionState? Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SessionFile))
            {
                return null;
            }

            var json = File.ReadAllText(AppPaths.SessionFile);
            return JsonSerializer.Deserialize<SessionState>(json, Options);
        }
        catch
        {
            return null;
        }
    }

    public static string? ReadBuffer(string? bufferFile)
    {
        if (string.IsNullOrEmpty(bufferFile))
        {
            return null;
        }

        try
        {
            var path = Path.Combine(AppPaths.SessionDirectory, bufferFile);
            return File.Exists(path) ? File.ReadAllText(path, new UTF8Encoding(false)) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>세션을 통째로 다시 기록한다. (이전 버퍼 파일은 정리)</summary>
    public static void Save(SessionState state, IReadOnlyList<string?> buffers)
    {
        try
        {
            AppPaths.EnsureCreated();
            ClearBuffers();

            for (var i = 0; i < state.Documents.Count && i < buffers.Count; i++)
            {
                var content = buffers[i];
                if (content is null)
                {
                    state.Documents[i].BufferFile = null;
                    continue;
                }

                var name = $"buffer-{i:D3}.txt";
                File.WriteAllText(Path.Combine(AppPaths.SessionDirectory, name), content, new UTF8Encoding(false));
                state.Documents[i].BufferFile = name;
            }

            File.WriteAllText(AppPaths.SessionFile, JsonSerializer.Serialize(state, Options));
        }
        catch
        {
            // 세션 저장 실패는 조용히 무시한다.
        }
    }

    public static void Clear()
    {
        try
        {
            ClearBuffers();
            if (File.Exists(AppPaths.SessionFile))
            {
                File.Delete(AppPaths.SessionFile);
            }
        }
        catch
        {
            // 무시
        }
    }

    private static void ClearBuffers()
    {
        if (!Directory.Exists(AppPaths.SessionDirectory))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(AppPaths.SessionDirectory, "buffer-*.txt"))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // 무시
            }
        }
    }
}
