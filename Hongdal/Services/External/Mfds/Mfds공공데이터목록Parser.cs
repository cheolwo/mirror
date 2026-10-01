using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace 홍달.Services.External.Mfds;

internal static class Mfds공공데이터목록Parser
{
    public static Mfds공공데이터목록결과<T> 파싱<T>(
        string 본문텍스트,
        string 데이터형식,
        Func<Mfds공공데이터항목, T> 항목변환)
    {
        return string.Equals(데이터형식, "json", StringComparison.OrdinalIgnoreCase)
            ? JSON파싱(본문텍스트, 항목변환)
            : XML파싱(본문텍스트, 항목변환);
    }

    private static Mfds공공데이터목록결과<T> XML파싱<T>(
        string 본문텍스트,
        Func<Mfds공공데이터항목, T> 항목변환)
    {
        var 문서 = XDocument.Parse(본문텍스트);
        var 루트 = 문서.Root;
        var 응답 = 첫자식찾기(루트, "response") ?? 루트;
        var 헤더 = 첫자식찾기(응답, "header");
        var 본문 = 첫자식찾기(응답, "body");
        var 항목목록 = 모든자손찾기(본문 ?? 응답, "item")
            .Select(항목 => 항목변환(new Mfds공공데이터항목(항목)))
            .ToArray();

        return new Mfds공공데이터목록결과<T>
        {
            결과코드 = 문자열찾기(헤더, "resultCode") ?? 문자열찾기(응답, "resultCode"),
            결과메시지 = 문자열찾기(헤더, "resultMsg") ?? 문자열찾기(응답, "resultMsg"),
            한페이지결과수 = 정수찾기(본문 ?? 응답, "numOfRows") ?? 0,
            페이지번호 = 정수찾기(본문 ?? 응답, "pageNo") ?? 0,
            전체결과수 = 정수찾기(본문 ?? 응답, "totalCount") ?? 0,
            항목목록 = 항목목록
        };
    }

    private static Mfds공공데이터목록결과<T> JSON파싱<T>(
        string 본문텍스트,
        Func<Mfds공공데이터항목, T> 항목변환)
    {
        using var 문서 = JsonDocument.Parse(본문텍스트);
        var 루트 = 문서.RootElement;
        var 응답 = 속성찾기(루트, "response") ?? 루트;
        var 헤더 = 속성찾기(응답, "header");
        var 본문 = 속성찾기(응답, "body") ?? 응답;
        var 항목목록 = JSON항목찾기(본문)
            .Select(항목 => 항목변환(new Mfds공공데이터항목(항목)))
            .ToArray();

        return new Mfds공공데이터목록결과<T>
        {
            결과코드 = JSON문자열찾기(헤더, "resultCode") ?? JSON문자열찾기(응답, "resultCode"),
            결과메시지 = JSON문자열찾기(헤더, "resultMsg") ?? JSON문자열찾기(응답, "resultMsg"),
            한페이지결과수 = JSON정수찾기(본문, "numOfRows") ?? 0,
            페이지번호 = JSON정수찾기(본문, "pageNo") ?? 0,
            전체결과수 = JSON정수찾기(본문, "totalCount") ?? 0,
            항목목록 = 항목목록
        };
    }

    private static XElement? 첫자식찾기(XElement? 요소, string 이름)
        => 요소?.Elements().FirstOrDefault(x => 이름일치(x.Name.LocalName, 이름));

    private static IEnumerable<XElement> 모든자손찾기(XElement? 요소, string 이름)
        => 요소?.Descendants().Where(x => 이름일치(x.Name.LocalName, 이름))
            ?? Enumerable.Empty<XElement>();

    private static string? 문자열찾기(XElement? 요소, string 이름)
        => 요소?.Elements().FirstOrDefault(x => 이름일치(x.Name.LocalName, 이름))?.Value
            ?? 요소?.Descendants().FirstOrDefault(x => 이름일치(x.Name.LocalName, 이름))?.Value;

    private static int? 정수찾기(XElement? 요소, string 이름)
    {
        var 값 = 문자열찾기(요소, 이름);
        return int.TryParse(값, NumberStyles.Any, CultureInfo.InvariantCulture, out var 결과)
            ? 결과
            : null;
    }

    private static JsonElement? 속성찾기(JsonElement 요소, string 이름)
    {
        if (요소.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var 속성 in 요소.EnumerateObject())
        {
            if (이름일치(속성.Name, 이름))
            {
                return 속성.Value;
            }
        }

        return null;
    }

    private static IReadOnlyList<JsonElement> JSON항목찾기(JsonElement 요소)
    {
        var 항목영역 = 속성찾기(요소, "items");
        if (!항목영역.HasValue)
        {
            항목영역 = 속성찾기(요소, "item");
        }

        if (!항목영역.HasValue || 항목영역.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        var 값 = 항목영역.Value;
        if (값.ValueKind == JsonValueKind.Object)
        {
            if (!값.EnumerateObject().Any())
            {
                return [];
            }

            var 내부항목 = 속성찾기(값, "item");
            if (내부항목.HasValue)
            {
                값 = 내부항목.Value;
            }
        }

        return 값.ValueKind switch
        {
            JsonValueKind.Array => 값.EnumerateArray().ToArray(),
            JsonValueKind.Object => [값],
            _ => []
        };
    }

    private static string? JSON문자열찾기(JsonElement 요소, string 이름)
    {
        var 속성 = 속성찾기(요소, 이름);
        return 속성.HasValue && 속성.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? 속성.Value.ToString()
            : null;
    }

    private static string? JSON문자열찾기(JsonElement? 요소, string 이름)
        => 요소.HasValue ? JSON문자열찾기(요소.Value, 이름) : null;

    private static int? JSON정수찾기(JsonElement 요소, string 이름)
    {
        var 속성 = 속성찾기(요소, 이름);
        if (!속성.HasValue)
        {
            return null;
        }

        return 속성.Value.ValueKind switch
        {
            JsonValueKind.Number when 속성.Value.TryGetInt32(out var 결과) => 결과,
            JsonValueKind.String when int.TryParse(
                속성.Value.GetString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var 결과) => 결과,
            _ => null
        };
    }

    private static bool 이름일치(string 왼쪽, string 오른쪽)
        => string.Equals(왼쪽, 오른쪽, StringComparison.OrdinalIgnoreCase);
}

internal sealed class Mfds공공데이터항목
{
    private readonly XElement? _xml;
    private readonly JsonElement? _json;

    public Mfds공공데이터항목(XElement xml)
    {
        _xml = xml;
    }

    public Mfds공공데이터항목(JsonElement json)
    {
        _json = json;
    }

    public string? 문자열(string 필드명)
    {
        if (_xml is not null)
        {
            return _xml.Elements()
                .FirstOrDefault(x => string.Equals(x.Name.LocalName, 필드명, StringComparison.OrdinalIgnoreCase))
                ?.Value;
        }

        if (!_json.HasValue || _json.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var 속성 in _json.Value.EnumerateObject())
        {
            if (string.Equals(속성.Name, 필드명, StringComparison.OrdinalIgnoreCase))
            {
                return 속성.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                    ? null
                    : 속성.Value.ToString();
            }
        }

        return null;
    }
}

internal sealed class Mfds공공데이터목록결과<T>
{
    public string? 결과코드 { get; init; }

    public string? 결과메시지 { get; init; }

    public int 한페이지결과수 { get; init; }

    public int 페이지번호 { get; init; }

    public int 전체결과수 { get; init; }

    public IReadOnlyList<T> 항목목록 { get; init; } = [];
}
