using Hongdal.Contracts.Common.Localization;

namespace Hongdal.Services.Community;

public static class CommunityPostLanguageResolver
{
    public static string Resolve(string? requestedLanguageCode, string? title, string? body)
    {
        if (DisplayLanguageCodes.TryNormalize(requestedLanguageCode, out var normalized))
        {
            return normalized;
        }

        var text = string.Concat(title, "\n", body);
        var hangulCount = 0;
        var latinCount = 0;

        foreach (var character in text)
        {
            if (character is >= '\uAC00' and <= '\uD7A3'
                or >= '\u1100' and <= '\u11FF'
                or >= '\u3130' and <= '\u318F')
            {
                hangulCount++;
            }
            else if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                latinCount++;
            }
        }

        if (hangulCount > 0 && hangulCount * 4 >= latinCount)
        {
            return DisplayLanguageCodes.Korean;
        }

        return latinCount > 0
            ? DisplayLanguageCodes.English
            : DisplayLanguageCodes.Korean;
    }
}
