using System.IO;
using System.Text;
using MemoJJang.Models;

namespace MemoJJang.Services;

/// <summary>파일에서 읽어온 텍스트와 그 부가 정보.</summary>
public sealed class LoadedText
{
    public required string Text { get; init; }
    public required EncodingOption Encoding { get; init; }
    public required LineEndingKind LineEnding { get; init; }
    public required string DetectionReason { get; init; }
    public required long ByteLength { get; init; }
}

/// <summary>인코딩으로 표현할 수 없는 문자가 있을 때의 검사 결과.</summary>
public sealed record EncodabilityResult(bool CanEncode, char Offending, int Index);

/// <summary>
/// 텍스트 파일 입출력. 인코딩 감지/변환과 줄 바꿈 정규화를 담당한다.
/// 편집기 내부에서는 항상 "\r\n" 으로 정규화된 텍스트를 다루고,
/// 저장 시점에 문서에 지정된 줄 바꿈 형식으로 되돌린다.
/// </summary>
public static class TextFileService
{
    public static LoadedText Load(string path, EncodingOption? forcedEncoding = null)
    {
        var bytes = File.ReadAllBytes(path);

        EncodingOption option;
        int preamble;
        string reason;

        if (forcedEncoding is null)
        {
            var detection = EncodingDetector.Detect(bytes);
            option = detection.Option;
            preamble = detection.PreambleLength;
            reason = detection.Reason;
        }
        else
        {
            option = forcedEncoding;
            preamble = MatchPreambleLength(bytes, forcedEncoding);
            reason = "사용자 지정";
        }

        var raw = option.ReadEncoding.GetString(bytes, preamble, bytes.Length - preamble);

        // 디코딩 결과 앞에 U+FEFF 가 남아 있으면 제거한다.
        if (raw.Length > 0 && raw[0] == '\uFEFF')
        {
            raw = raw[1..];
        }

        var lineEnding = DetectLineEnding(raw);

        return new LoadedText
        {
            Text = NormalizeToCrLf(raw),
            Encoding = option,
            LineEnding = lineEnding,
            DetectionReason = reason,
            ByteLength = bytes.Length
        };
    }

    public static void Save(string path, string crLfText, EncodingOption option, LineEndingKind lineEnding)
    {
        var converted = ConvertLineEndings(crLfText, lineEnding);
        var encoding = option.WriteEncoding;

        var preamble = option.HasBom ? encoding.GetPreamble() : Array.Empty<byte>();
        var body = encoding.GetBytes(converted);

        // 원자적 저장: 임시 파일에 먼저 쓰고 교체한다. (쓰기 도중 오류로 원본이 날아가는 것을 방지)
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }

        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.memojjang.tmp");

        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (preamble.Length > 0)
                {
                    stream.Write(preamble, 0, preamble.Length);
                }

                stream.Write(body, 0, body.Length);
                stream.Flush(true);
            }

            if (File.Exists(path))
            {
                File.Replace(temp, path, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, path);
            }
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    /// <summary>선택한 인코딩으로 모든 문자를 손실 없이 표현할 수 있는지 검사한다.</summary>
    public static EncodabilityResult CheckEncodable(string text, EncodingOption option)
    {
        // 유니코드 계열은 항상 표현 가능하다.
        if (option.CodePage is 65001 or 1200 or 1201 or 12000 or 12001)
        {
            return new EncodabilityResult(true, '\0', -1);
        }

        try
        {
            var strict = Encoding.GetEncoding(
                option.CodePage,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ReplacementFallback);

            strict.GetByteCount(text);
            return new EncodabilityResult(true, '\0', -1);
        }
        catch (EncoderFallbackException ex)
        {
            return new EncodabilityResult(false, ex.CharUnknown, ex.Index);
        }
        catch
        {
            return new EncodabilityResult(true, '\0', -1);
        }
    }

    /// <summary>텍스트에서 가장 많이 쓰인 줄 바꿈 형식을 찾는다.</summary>
    public static LineEndingKind DetectLineEnding(string text)
    {
        var crlf = 0;
        var lf = 0;
        var cr = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlf++;
                    i++;
                }
                else
                {
                    cr++;
                }
            }
            else if (c == '\n')
            {
                lf++;
            }
        }

        if (crlf == 0 && lf == 0 && cr == 0)
        {
            return LineEndingKind.CrLf;
        }

        if (lf > crlf && lf >= cr)
        {
            return LineEndingKind.Lf;
        }

        if (cr > crlf && cr > lf)
        {
            return LineEndingKind.Cr;
        }

        return LineEndingKind.CrLf;
    }

    /// <summary>어떤 줄 바꿈이든 "\r\n" 으로 통일한다.</summary>
    public static string NormalizeToCrLf(string text)
    {
        if (text.IndexOf('\r') < 0 && text.IndexOf('\n') < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + 16);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r')
            {
                builder.Append("\r\n");
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
            }
            else if (c == '\n')
            {
                builder.Append("\r\n");
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>"\r\n" 으로 정규화된 텍스트를 지정한 줄 바꿈 형식으로 변환한다.</summary>
    public static string ConvertLineEndings(string crLfText, LineEndingKind kind)
    {
        return kind switch
        {
            LineEndingKind.Lf => crLfText.Replace("\r\n", "\n"),
            LineEndingKind.Cr => crLfText.Replace("\r\n", "\r"),
            _ => crLfText
        };
    }

    private static int MatchPreambleLength(byte[] bytes, EncodingOption option)
    {
        if (!option.HasBom)
        {
            return 0;
        }

        var preamble = option.WriteEncoding.GetPreamble();
        if (preamble.Length == 0 || bytes.Length < preamble.Length)
        {
            return 0;
        }

        for (var i = 0; i < preamble.Length; i++)
        {
            if (bytes[i] != preamble[i])
            {
                return 0;
            }
        }

        return preamble.Length;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 임시 파일 정리 실패는 무시한다.
        }
    }
}
