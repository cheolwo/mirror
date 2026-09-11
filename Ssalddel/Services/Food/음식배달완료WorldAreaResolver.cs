using System.Text.RegularExpressions;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Services.Food;

public interface I음식배달완료WorldAreaResolver
{
    string Resolve(음식주문 order, 운송원장? transport);
}

public interface I운영WorldAreaResolver
{
    string ResolveAddresses(params string?[] addresses);
}

public static class 음식배달완료WorldAreaStableIds
{
    public const string Myeonmok = "region:kr:bjd:1126010100";
    public const string Junghwa = "region:kr:bjd:1126010300";
    public const string Unclassified = "region:kr:bjd:unclassified";
}

/// <summary>
/// 정확한 공간 결속 자료가 없는 주문을 임의 좌표로 분류하지 않습니다.
/// 현재 수직 조각에서는 운영 주소에 법정동명이 명시된 두 승인 지역만 보수적으로 분류합니다.
/// </summary>
public sealed class 음식배달완료WorldAreaResolver : I음식배달완료WorldAreaResolver, I운영WorldAreaResolver
{
    private static readonly Regex MyeonmokPattern = new(
        @"(?:^|[\s(])면목동(?:[\s,)]|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex JunghwaPattern = new(
        @"(?:^|[\s(])중화동(?:[\s,)]|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Resolve(음식주문 order, 운송원장? transport)
    {
        ArgumentNullException.ThrowIfNull(order);

        return ResolveAddresses(
            transport?.픽업_도로명주소,
            order.음식점주소,
            transport?.하차_도로명주소,
            order.수령지주소);
    }

    public string ResolveAddresses(params string?[] addresses)
    {
        addresses ??= Array.Empty<string?>();

        if (addresses.Any(IsMyeonmok)) return 음식배달완료WorldAreaStableIds.Myeonmok;
        if (addresses.Any(IsJunghwa)) return 음식배달완료WorldAreaStableIds.Junghwa;
        return 음식배달완료WorldAreaStableIds.Unclassified;
    }

    private static bool IsMyeonmok(string? value)
        => !string.IsNullOrWhiteSpace(value) && MyeonmokPattern.IsMatch(value);

    private static bool IsJunghwa(string? value)
        => !string.IsNullOrWhiteSpace(value) && JunghwaPattern.IsMatch(value);
}
