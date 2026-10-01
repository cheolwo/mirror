using System.Text;

namespace 홍달.Services.External.Mfds;

internal static class Mfds공공데이터요청Builder
{
    public static string 데이터형식정리(string? 값, string 기본값)
    {
        var 후보 = string.IsNullOrWhiteSpace(값) ? 기본값 : 값;
        return string.Equals(후보?.Trim(), "json", StringComparison.OrdinalIgnoreCase)
            ? "json"
            : "xml";
    }

    public static string 요청주소생성(
        string 경로,
        IReadOnlyDictionary<string, string?> 매개변수목록)
    {
        var 빌더 = new StringBuilder(경로.TrimStart('/'));
        var 첫매개변수인지여부 = true;

        foreach (var 항목 in 매개변수목록)
        {
            if (string.IsNullOrWhiteSpace(항목.Value))
            {
                continue;
            }

            빌더.Append(첫매개변수인지여부 ? '?' : '&');
            빌더.Append(항목.Key);
            빌더.Append('=');
            빌더.Append(Uri.EscapeDataString(항목.Value));
            첫매개변수인지여부 = false;
        }

        return 빌더.ToString();
    }
}
