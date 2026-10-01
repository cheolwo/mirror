namespace Hongdal.Services.Community;

internal static class CommunityVoteHsCode
{
    public static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = new string(value.Where(char.IsDigit).ToArray());
        if (normalized.Length is < 2 or > 10)
        {
            throw new InvalidOperationException("HS 코드는 구분기호를 제외한 2~10자리 숫자로 입력해야 합니다.");
        }

        return normalized;
    }

    public static bool MatchesPrefix(string? storedValue, string normalizedPrefix)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return false;
        }

        var storedCode = new string(storedValue.Where(char.IsDigit).ToArray());
        return storedCode.StartsWith(normalizedPrefix, StringComparison.Ordinal);
    }

    public static string PrefixRegex(string normalizedPrefix)
        => $"^{string.Join("[^0-9]*", normalizedPrefix.Select(character => character.ToString()))}";
}
