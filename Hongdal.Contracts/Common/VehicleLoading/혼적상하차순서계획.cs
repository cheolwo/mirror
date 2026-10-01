namespace Hongdal.Contracts.Common.VehicleLoading;

public sealed record 혼적화물순서요청항목(
    string 화물코드,
    string 화물명,
    string 하차지,
    int 하차순번,
    decimal 중량Kg,
    bool 적층가능여부 = true);

public sealed record 혼적화물순서계획항목(
    string 화물코드,
    string 화물명,
    string 하차지,
    int 상차순번,
    int 하차순번,
    string 차량적재위치,
    string 작업안내);

public sealed record 혼적상하차순서계획(
    IReadOnlyList<혼적화물순서계획항목> 상차순서,
    IReadOnlyList<혼적화물순서계획항목> 하차순서,
    string 운영원칙);

/// <summary>
/// 후방 출입문을 사용하는 일반 화물차를 기준으로 혼적 상·하차 순서를 계산합니다.
/// 나중에 내릴 화물을 먼저 안쪽에 싣고, 먼저 내릴 화물을 마지막에 출입문 가까이에 둡니다.
/// </summary>
public static class 혼적상하차순서계획기
{
    public const string 후방하차운영원칙 =
        "후방 출입문 하차 기준: 나중 하차 물량을 먼저 차량 안쪽에 싣고, 첫 하차 물량은 마지막에 출입문 가까이에 배치합니다.";

    public static 혼적상하차순서계획 계획(IReadOnlyList<혼적화물순서요청항목> 화물목록)
    {
        ArgumentNullException.ThrowIfNull(화물목록);

        if (화물목록.Count == 0)
        {
            return new 혼적상하차순서계획([], [], 후방하차운영원칙);
        }

        Validate(화물목록);

        var 하차순번목록 = 화물목록
            .Select(item => item.하차순번)
            .Distinct()
            .OrderBy(sequence => sequence)
            .ToArray();

        var 상차대상 = 화물목록
            .OrderByDescending(item => item.하차순번)
            .ThenBy(item => item.적층가능여부 ? 1 : 0)
            .ThenByDescending(item => item.중량Kg)
            .ThenBy(item => item.화물코드, StringComparer.Ordinal)
            .ToArray();

        var 계획항목 = 상차대상
            .Select((item, index) => new 혼적화물순서계획항목(
                item.화물코드.Trim(),
                item.화물명.Trim(),
                item.하차지.Trim(),
                index + 1,
                item.하차순번,
                ResolveVehicleZone(item.하차순번, 하차순번목록),
                BuildWorkGuide(item, 하차순번목록)))
            .ToArray();

        var 하차계획 = 계획항목
            .OrderBy(item => item.하차순번)
            .ThenByDescending(item => item.상차순번)
            .ToArray();

        return new 혼적상하차순서계획(계획항목, 하차계획, 후방하차운영원칙);
    }

    private static void Validate(IReadOnlyList<혼적화물순서요청항목> items)
    {
        if (items.Any(item => string.IsNullOrWhiteSpace(item.화물코드)))
        {
            throw new ArgumentException("화물코드는 비어 있을 수 없습니다.", nameof(items));
        }

        var duplicateCode = items
            .GroupBy(item => item.화물코드.Trim(), StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateCode is not null)
        {
            throw new ArgumentException($"중복된 화물코드입니다: {duplicateCode}", nameof(items));
        }

        if (items.Any(item => item.하차순번 <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(items), "하차순번은 1 이상이어야 합니다.");
        }

        if (items.Any(item => item.중량Kg < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(items), "화물 중량은 음수일 수 없습니다.");
        }
    }

    private static string ResolveVehicleZone(int dropoffSequence, IReadOnlyList<int> dropoffSequences)
    {
        if (dropoffSequences.Count == 1)
        {
            return "동일 하차지 적재 구역";
        }

        var dropoffIndex = Array.IndexOf(dropoffSequences.ToArray(), dropoffSequence);
        if (dropoffIndex == 0)
        {
            return "후방 출입문 가까이";
        }

        if (dropoffIndex == dropoffSequences.Count - 1)
        {
            return "차량 전방 안쪽";
        }

        return $"차량 중앙 {dropoffIndex}구역";
    }

    private static string BuildWorkGuide(
        혼적화물순서요청항목 item,
        IReadOnlyList<int> dropoffSequences)
    {
        var handlingGuide = !item.적층가능여부
            ? " 적층 불가 화물이므로 바닥 자리를 확보합니다."
            : item.중량Kg > 0
                ? " 같은 하차 묶음에서는 무거운 화물을 먼저 실어 아래쪽에 둡니다."
                : string.Empty;

        if (dropoffSequences.Count == 1)
        {
            return $"같은 하차지 물량끼리 묶어 적재합니다.{handlingGuide}".Trim();
        }

        if (item.하차순번 == dropoffSequences[0])
        {
            return $"첫 하차 물량이므로 마지막에 싣고 출입문 가까이에 둡니다.{handlingGuide}".Trim();
        }

        if (item.하차순번 == dropoffSequences[^1])
        {
            return $"마지막 하차 물량이므로 먼저 싣고 차량 안쪽에 둡니다.{handlingGuide}".Trim();
        }

        return $"{item.하차순번}번째 하차 순서에 맞춰 중간 구역에 적재합니다.{handlingGuide}".Trim();
    }
}
