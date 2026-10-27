using System.Globalization;
using System.Text;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

public interface I화주운송의뢰일괄등록파서Service
{
    Task<화주운송의뢰일괄등록파싱결과> ParseAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}

public sealed class 화주운송의뢰일괄등록파서Service : I화주운송의뢰일괄등록파서Service
{
    public async Task<화주운송의뢰일괄등록파싱결과> ParseAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return new 화주운송의뢰일괄등록파싱결과([], ["현재는 CSV 형식만 지원합니다. Excel이나 Google Sheets에서 CSV로 저장해서 올려주세요."]);
        }

        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var lines = SplitCsvLines(content);
        if (lines.Count == 0)
        {
            return new 화주운송의뢰일괄등록파싱결과([], ["파일이 비어 있습니다."]);
        }

        var headers = ParseCsvRow(lines[0]);
        var headerMap = BuildHeaderMap(headers);
        var rows = new List<화주운송의뢰일괄등록행입력>();
        var errors = new List<string>();

        for (var index = 1; index < lines.Count; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvRow(line);
            var rowNumber = TryGetInt(values, headerMap, "행번호", index) ?? index;
            var rowErrors = new List<string>();
            var item = new 화주운송의뢰일괄등록행입력
            {
                행번호 = rowNumber,
                화주Id = GetValue(values, headerMap, "화주Id"),
                화물종류 = GetValue(values, headerMap, "화물종류") ?? string.Empty,
                화물설명 = GetValue(values, headerMap, "화물설명"),
                화물수량 = TryGetInt(values, headerMap, "화물수량", rowNumber, rowErrors),
                화물길이Mm = TryGetInt(values, headerMap, "화물길이Mm", rowNumber, rowErrors),
                화물폭Mm = TryGetInt(values, headerMap, "화물폭Mm", rowNumber, rowErrors),
                화물높이Mm = TryGetInt(values, headerMap, "화물높이Mm", rowNumber, rowErrors),
                화물중량Kg = TryGetDecimal(values, headerMap, "화물중량Kg", rowNumber, rowErrors),
                화물부피Cbm = TryGetDecimal(values, headerMap, "화물부피Cbm", rowNumber, rowErrors),
                화물바닥회전가능여부 = TryGetOptionalLoadingBool(values, headerMap, "화물바닥회전가능여부", rowNumber, rowErrors),
                화물적층가능여부 = TryGetOptionalLoadingBool(values, headerMap, "화물적층가능여부", rowNumber, rowErrors),
                팔레트개수 = TryGetInt(values, headerMap, "팔레트개수", rowNumber, rowErrors),
                화물파손주의여부 = TryGetBool(values, headerMap, "화물파손주의여부") ?? false,
                화물온도조건 = GetValue(values, headerMap, "화물온도조건"),
                픽업도로명주소 = GetValue(values, headerMap, "픽업도로명주소"),
                픽업상세주소 = GetValue(values, headerMap, "픽업상세주소"),
                픽업연락처이름 = GetValue(values, headerMap, "픽업연락처이름"),
                픽업연락처전화번호 = GetValue(values, headerMap, "픽업연락처전화번호"),
                픽업시간창시작일시 = TryGetUtcTime(values, headerMap, "픽업시간창시작일시", rowNumber, rowErrors),
                픽업시간창종료일시 = TryGetUtcTime(values, headerMap, "픽업시간창종료일시", rowNumber, rowErrors),
                하차도로명주소 = GetValue(values, headerMap, "하차도로명주소"),
                하차상세주소 = GetValue(values, headerMap, "하차상세주소"),
                하차연락처이름 = GetValue(values, headerMap, "하차연락처이름"),
                하차연락처전화번호 = GetValue(values, headerMap, "하차연락처전화번호"),
                하차시간창시작일시 = TryGetUtcTime(values, headerMap, "하차시간창시작일시", rowNumber, rowErrors),
                하차시간창종료일시 = TryGetUtcTime(values, headerMap, "하차시간창종료일시", rowNumber, rowErrors),
                운송방식 = GetValue(values, headerMap, "운송방식"),
                차량종류 = GetValue(values, headerMap, "차량종류"),
                서비스레벨 = GetValue(values, headerMap, "서비스레벨"),
                요청사항 = GetValue(values, headerMap, "요청사항"),
                결제수단 = GetValue(values, headerMap, "결제수단"),
                정산시점 = GetValue(values, headerMap, "정산시점"),
                증빙방식 = GetValue(values, headerMap, "증빙방식"),
                수납주체 = GetValue(values, headerMap, "수납주체"),
                클라이언트행Id = GetValue(values, headerMap, "클라이언트행Id")
            };

            if (string.IsNullOrWhiteSpace(item.화물종류))
            {
                rowErrors.Add($"{rowNumber}행: 화물종류는 필수입니다.");
            }

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
            }

            rows.Add(item);
        }

        return new 화주운송의뢰일괄등록파싱결과(rows, errors);
    }

    private static DateTime? TryGetUtcTime(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> headerMap,
        string header, int rowNumber, List<string> errors)
    {
        var raw = GetValue(values, headerMap, header);
        if (raw is null) return null;
        // PC 시간대에 따라 같은 CSV의 약속 시각이 달라지지 않도록 UTC 또는 명시 offset만 받습니다.
        if (System.Text.RegularExpressions.Regex.IsMatch(raw, @"(?:[zZ]|[+-]\d{2}:\d{2})$")
            && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            return value.UtcDateTime;
        errors.Add($"{rowNumber}행: {header}은(는) 시간대가 있는 날짜로 입력해 주세요. 예: 2026-10-20T09:00:00+09:00");
        return null;
    }

    private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var key = NormalizeHeader(headers[i]);
            if (!map.ContainsKey(key))
            {
                map.Add(key, i);
            }
        }

        return map;
    }

    private static string? GetValue(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> headerMap, string header)
    {
        if (!headerMap.TryGetValue(NormalizeHeader(header), out var index) || index >= values.Count)
        {
            return null;
        }

        var value = values[index].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? TryGetInt(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> headerMap, string header, int rowNumber, List<string>? errors = null)
    {
        var raw = GetValue(values, headerMap, header);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || int.TryParse(raw, out value))
        {
            return value;
        }

        errors?.Add($"{rowNumber}행: {header} 값 '{raw}'을(를) 정수로 읽을 수 없습니다.");
        return null;
    }

    private static decimal? TryGetDecimal(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> headerMap, string header, int rowNumber, List<string>? errors = null)
    {
        var raw = GetValue(values, headerMap, header);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || decimal.TryParse(raw, out value))
        {
            return value;
        }

        errors?.Add($"{rowNumber}행: {header} 값 '{raw}'을(를) 숫자로 읽을 수 없습니다.");
        return null;
    }

    private static bool? TryGetOptionalLoadingBool(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> headerMap,
        string header, int rowNumber, ICollection<string> errors)
    {
        var raw = GetValue(values, headerMap, header);
        var value = TryGetBool(values, headerMap, header);
        if (!string.IsNullOrWhiteSpace(raw) && !value.HasValue)
            errors.Add($"{rowNumber}행: {header} 값은 true/false 또는 예/아니오로 입력해 주세요.");
        return value;
    }

    private static bool? TryGetBool(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> headerMap, string header)
    {
        var raw = GetValue(values, headerMap, header);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "y" or "yes" or "예" or "o" => true,
            "false" or "0" or "n" or "no" or "아니오" or "x" => false,
            _ => null
        };
    }

    private static IReadOnlyList<string> ParseCsvRow(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        values.Add(current.ToString());
        return values;
    }

    private static List<string> SplitCsvLines(string content)
    {
        var lines = new List<string>();
        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private static string NormalizeHeader(string header)
    {
        return new string(header.Trim().Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-').ToArray());
    }
}
