namespace Hongdal.Contracts.Common.Localization;

public static class DisplayLanguageCodes
{
    public const string Korean = "ko-KR";
    public const string English = "en-US";

    public static IReadOnlyList<string> Supported { get; } = [Korean, English];

    public static string Normalize(string? value, string fallback = Korean)
    {
        var normalizedFallback = TryNormalize(fallback, out var fallbackCode)
            ? fallbackCode
            : Korean;

        return TryNormalize(value, out var normalizedCode)
            ? normalizedCode
            : normalizedFallback;
    }

    public static bool TryNormalize(string? value, out string normalizedCode)
    {
        normalizedCode = Korean;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (candidate.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
        {
            normalizedCode = Korean;
            return true;
        }

        if (candidate.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            normalizedCode = English;
            return true;
        }

        return false;
    }

    public static string ToNeutralCode(string? value)
        => Normalize(value) == English ? "en" : "ko";
}
