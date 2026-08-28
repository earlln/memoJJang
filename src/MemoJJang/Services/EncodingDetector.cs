using MemoJJang.Models;

namespace MemoJJang.Services;

/// <summary>인코딩 자동 감지 결과.</summary>
public sealed record EncodingDetectionResult(EncodingOption Option, int PreambleLength, string Reason);

/// <summary>
/// 파일 바이트를 보고 인코딩을 추론한다.
///
/// 우선순위
///   1. BOM (UTF-32 LE/BE, UTF-8, UTF-16 LE/BE)  ← 가장 확실한 근거
///   2. BOM 없는 UTF-16 추정 (널 바이트 분포 통계)
///   3. 엄격한 UTF-8 유효성 검사
///      - 유효 + 비 ASCII 문자 포함 → UTF-8
///      - 유효 + 순수 ASCII        → ANSI (메모장과 동일한 동작)
///   4. 그 외 → 시스템 ANSI 코드 페이지
/// </summary>
public static class EncodingDetector
{
    /// <summary>감지에 사용할 최대 표본 크기(바이트).</summary>
    private const int SampleSize = 64 * 1024;

    public static EncodingDetectionResult Detect(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        // 1) BOM
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            return new EncodingDetectionResult(EncodingCatalog.Utf32Le, 4, "UTF-32 LE BOM");
        }

        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            return new EncodingDetectionResult(EncodingCatalog.Utf32Be, 4, "UTF-32 BE BOM");
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return new EncodingDetectionResult(EncodingCatalog.Utf8Bom, 3, "UTF-8 BOM");
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return new EncodingDetectionResult(EncodingCatalog.Utf16Le, 2, "UTF-16 LE BOM");
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return new EncodingDetectionResult(EncodingCatalog.Utf16Be, 2, "UTF-16 BE BOM");
        }

        if (bytes.Length == 0)
        {
            return new EncodingDetectionResult(EncodingCatalog.Default, 0, "빈 파일");
        }

        // 2) BOM 없는 UTF-16
        if (LooksLikeUtf16(bytes, out var bigEndian))
        {
            var option = bigEndian ? EncodingCatalog.Utf16BeNoBom : EncodingCatalog.Utf16LeNoBom;
            return new EncodingDetectionResult(option, 0, "BOM 없는 UTF-16 추정");
        }

        // 3) UTF-8 유효성
        var (valid, hasNonAscii) = InspectUtf8(bytes);
        if (valid)
        {
            return hasNonAscii
                ? new EncodingDetectionResult(EncodingCatalog.Utf8, 0, "UTF-8 (BOM 없음)")
                : new EncodingDetectionResult(EncodingCatalog.Ansi, 0, "순수 ASCII");
        }

        // 4) 폴백
        return new EncodingDetectionResult(EncodingCatalog.Ansi, 0, "시스템 ANSI 코드 페이지");
    }

    /// <summary>
    /// 널 바이트의 위치 분포로 BOM 없는 UTF-16 여부를 추정한다.
    /// 짝수 위치에만 널이 몰려 있으면 BE, 홀수 위치에만 몰려 있으면 LE 로 본다.
    /// </summary>
    private static bool LooksLikeUtf16(byte[] bytes, out bool bigEndian)
    {
        bigEndian = false;

        if (bytes.Length < 16 || bytes.Length % 2 != 0)
        {
            return false;
        }

        var length = Math.Min(bytes.Length, SampleSize);
        if (length % 2 != 0)
        {
            length--;
        }

        var evenZeros = 0;
        var oddZeros = 0;

        for (var i = 0; i < length; i++)
        {
            if (bytes[i] != 0)
            {
                continue;
            }

            if (i % 2 == 0)
            {
                evenZeros++;
            }
            else
            {
                oddZeros++;
            }
        }

        var pairs = length / 2;
        // 한쪽 위치에만 널이 몰려 있고, 그 비율이 전체 문자 수의 30% 이상일 때만 인정한다.
        var threshold = Math.Max(4, pairs * 3 / 10);

        if (oddZeros >= threshold && evenZeros == 0)
        {
            bigEndian = false;
            return true;
        }

        if (evenZeros >= threshold && oddZeros == 0)
        {
            bigEndian = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// UTF-8 유효성 검사. 과잉 인코딩(overlong), 서로게이트 영역, 범위 초과를 모두 거른다.
    /// </summary>
    private static (bool Valid, bool HasNonAscii) InspectUtf8(byte[] bytes)
    {
        var length = Math.Min(bytes.Length, SampleSize);
        var hasNonAscii = false;
        var i = 0;

        while (i < length)
        {
            var b = bytes[i];

            if (b < 0x80)
            {
                i++;
                continue;
            }

            hasNonAscii = true;

            int extra;
            int codePoint;

            if (b >= 0xC2 && b <= 0xDF)
            {
                extra = 1;
                codePoint = b & 0x1F;
            }
            else if (b >= 0xE0 && b <= 0xEF)
            {
                extra = 2;
                codePoint = b & 0x0F;
            }
            else if (b >= 0xF0 && b <= 0xF4)
            {
                extra = 3;
                codePoint = b & 0x07;
            }
            else
            {
                // 0x80~0xC1, 0xF5~0xFF 는 UTF-8 선두 바이트가 될 수 없다.
                return (false, hasNonAscii);
            }

            if (i + extra >= length)
            {
                // 표본이 잘린 경우에는 실패로 보지 않는다.
                return (length == bytes.Length ? false : true, hasNonAscii);
            }

            for (var k = 1; k <= extra; k++)
            {
                var cb = bytes[i + k];
                if ((cb & 0xC0) != 0x80)
                {
                    return (false, hasNonAscii);
                }

                codePoint = (codePoint << 6) | (cb & 0x3F);
            }

            if (extra == 2 && codePoint < 0x800)
            {
                return (false, hasNonAscii);
            }

            if (extra == 3 && (codePoint < 0x10000 || codePoint > 0x10FFFF))
            {
                return (false, hasNonAscii);
            }

            if (codePoint >= 0xD800 && codePoint <= 0xDFFF)
            {
                return (false, hasNonAscii);
            }

            i += extra + 1;
        }

        return (true, hasNonAscii);
    }
}
