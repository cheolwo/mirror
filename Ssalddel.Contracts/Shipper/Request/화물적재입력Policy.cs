using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ssalddel.Contracts.Shipper.Request;

/// <summary>치수는 같은 외포장 한 개, 수량은 포장 개수, 중량과 입력 부피는 전량 기준이다.</summary>
public sealed record 화물적재입력
{
    public string? 화물종류 { get; init; }
    public string? 설명 { get; init; }
    public int? 수량 { get; init; }
    public int? 길이Mm { get; init; }
    public int? 폭Mm { get; init; }
    public int? 높이Mm { get; init; }
    public decimal? 총중량Kg { get; init; }
    public decimal? 입력부피Cbm { get; init; }
    public int? 팔레트개수 { get; init; }
    public string? 온도조건 { get; init; }
    public bool? 바닥회전가능여부 { get; init; }
    public bool? 적층가능여부 { get; init; }
    public bool 파손주의여부 { get; init; }
    public string? 운송방식 { get; init; }
    public string? 서비스레벨 { get; init; }
    public string? 요청사항 { get; init; }
}

public sealed record 화물적재입력결과
{
    public 화물적재입력 입력 { get; init; } = new();
    public decimal? 유효부피Cbm { get; init; }
    public decimal? 치수추정부피Cbm { get; init; }
    public decimal? 비적층바닥면적M2 { get; init; }
    public int 적용수량 { get; init; }
    public bool 바닥회전가능여부 { get; init; }
    public bool 적층가능여부 { get; init; }
    public string 부피근거Code { get; init; } = "Unknown";
    public string 계산판본 { get; init; } = 화물적재입력Policy.CurrentRevision;
    public string 입력지문 { get; init; } = string.Empty;
    public IReadOnlyList<string> 경고 { get; init; } = [];
    public IReadOnlyList<string> 오류 { get; init; } = [];
    public bool 유효여부 => 오류.Count == 0;
}

/// <summary>입력 계보와 계산값만 반환한다. 실측 확인·차량 선택·영속 상태 변경을 수행하지 않는다.</summary>
public static class 화물적재입력Policy
{
    public const string CurrentRevision = "cargo-loading-input-v1";

    public static 화물적재입력결과 계산(화물적재입력 input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = new List<string>();
        var warnings = new List<string>();
        ValidatePositive(input.수량, "포장 수량", errors);
        ValidatePositive(input.길이Mm, "포장 길이", errors);
        ValidatePositive(input.폭Mm, "포장 폭", errors);
        ValidatePositive(input.높이Mm, "포장 높이", errors);
        ValidatePositive(input.총중량Kg, "총중량", errors);
        ValidatePositive(input.입력부피Cbm, "입력 부피", errors);
        if (input.팔레트개수 < 0) errors.Add("팔레트 개수는 0 이상이어야 합니다.");

        var quantity = input.수량 ?? 1;
        if (!input.수량.HasValue) warnings.Add("포장 수량이 없어 기존 기준인 1개를 가정했습니다. 실제 수량을 확인해 주세요.");
        if (!input.바닥회전가능여부.HasValue) warnings.Add("바닥 회전 가능 여부가 미확인으로, 기존 회전 가능 가정을 적용했습니다.");
        if (!input.적층가능여부.HasValue) warnings.Add("적층 가능 여부가 미확인으로, 기존 적층 가능 가정을 적용했습니다.");

        decimal? dimensionsVolume = null;
        if (input.길이Mm is > 0 && input.폭Mm is > 0 && input.높이Mm is > 0 && quantity > 0)
        {
            dimensionsVolume = (input.길이Mm.Value / 1000m) * (input.폭Mm.Value / 1000m)
                * (input.높이Mm.Value / 1000m) * quantity;
        }
        else if (input.길이Mm.HasValue || input.폭Mm.HasValue || input.높이Mm.HasValue)
            warnings.Add("포장 치수 일부가 없어 치수와 수량 기준의 전체 부피를 추정하지 못했습니다.");

        var providedVolume = input.입력부피Cbm is > 0 ? input.입력부피Cbm : null;
        var effectiveVolume = providedVolume ?? dimensionsVolume;
        var source = providedVolume.HasValue ? "UserProvided" : dimensionsVolume.HasValue ? "DimensionsEstimated" : "Unknown";
        if (dimensionsVolume.HasValue && providedVolume.HasValue && providedVolume.Value < dimensionsVolume.Value)
        {
            effectiveVolume = dimensionsVolume;
            source = "ConservativeDimensionsEstimate";
            warnings.Add("입력 부피가 외포장 치수와 수량으로 추정한 부피보다 작아, 더 큰 치수 추정값으로 판정합니다.");
        }
        else if (!providedVolume.HasValue && dimensionsVolume.HasValue)
            warnings.Add("화물 부피(CBM)가 없어 외포장 치수와 수량 기준으로 추정했습니다.");
        if (!effectiveVolume.HasValue) warnings.Add("전체 부피는 미확인입니다.");

        decimal? floorArea = null;
        if (input.적층가능여부 == false)
        {
            if (input.길이Mm is > 0 && input.폭Mm is > 0 && quantity > 0)
                floorArea = (input.길이Mm.Value / 1000m) * (input.폭Mm.Value / 1000m) * quantity;
            else warnings.Add("적층 불가 화물의 길이·폭·수량을 확인해야 바닥면적을 계산할 수 있습니다.");
        }
        if (input.파손주의여부)
            warnings.Add("파손주의 화물이므로 포장 고정과 적층 조건을 추가로 확인해 주세요.");

        return new 화물적재입력결과
        {
            입력 = input,
            적용수량 = quantity,
            유효부피Cbm = effectiveVolume,
            치수추정부피Cbm = dimensionsVolume,
            비적층바닥면적M2 = floorArea,
            바닥회전가능여부 = input.바닥회전가능여부 ?? true,
            적층가능여부 = input.적층가능여부 ?? true,
            부피근거Code = source,
            입력지문 = Fingerprint(input),
            경고 = warnings.ToArray(),
            오류 = errors.ToArray()
        };
    }

    public static 화물적재입력 입력생성(차량추천요청 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new()
        {
            화물종류 = request.화물종류, 설명 = request.화물설명, 수량 = request.화물수량,
            길이Mm = request.화물길이Mm, 폭Mm = request.화물폭Mm, 높이Mm = request.화물높이Mm,
            총중량Kg = request.화물중량Kg, 입력부피Cbm = request.화물부피Cbm, 팔레트개수 = request.팔레트개수,
            온도조건 = request.화물온도조건, 바닥회전가능여부 = request.화물바닥회전가능여부,
            적층가능여부 = request.화물적층가능여부, 파손주의여부 = request.화물파손주의여부,
            운송방식 = request.운송방식, 서비스레벨 = request.서비스레벨, 요청사항 = request.요청사항
        };
    }

    public static 화물적재입력 입력생성(CargoDTO cargo, string? 운송방식 = null, string? 서비스레벨 = null, string? 요청사항 = null)
    {
        ArgumentNullException.ThrowIfNull(cargo);
        return new()
        {
            화물종류 = cargo.화물종류, 설명 = cargo.설명, 수량 = cargo.수량,
            길이Mm = cargo.길이Mm, 폭Mm = cargo.폭Mm, 높이Mm = cargo.높이Mm,
            총중량Kg = cargo.중량Kg, 입력부피Cbm = cargo.부피Cbm, 팔레트개수 = cargo.팔레트개수,
            온도조건 = cargo.온도조건, 바닥회전가능여부 = cargo.바닥회전가능여부,
            적층가능여부 = cargo.적층가능여부, 파손주의여부 = cargo.화물파손주의여부,
            운송방식 = 운송방식, 서비스레벨 = 서비스레벨, 요청사항 = 요청사항
        };
    }

    public static bool 요청문구포함(화물적재입력 input, string keyword)
        => new[] { input.운송방식, input.서비스레벨, input.요청사항, input.화물종류, input.설명 }
            .Any(value => value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>저장된 유효값을 안전 하한으로만 재사용한다. 과거 계산을 새 사용자 입력으로 승격하지 않는다.</summary>
    public static 화물적재입력결과 저장값적용(화물적재입력결과 calculated, decimal? 저장유효부피Cbm,
        string? 저장근거Code, string? 저장계산판본)
    {
        ArgumentNullException.ThrowIfNull(calculated);
        var warnings = calculated.경고.ToList();
        var effective = calculated.유효부피Cbm;
        if (저장유효부피Cbm is > 0 && (!effective.HasValue || 저장유효부피Cbm.Value > effective.Value))
        {
            effective = 저장유효부피Cbm;
            warnings.Add("저장된 전체 부피가 재계산값보다 커, 원입력으로 바꾸지 않고 보수적인 판정 하한으로 유지합니다.");
        }
        var current = string.Equals(저장계산판본, CurrentRevision, StringComparison.Ordinal);
        if (!current) warnings.Add("기존 적재 계산의 판본·입력 근거가 미확인입니다. 현재 계산만으로 과거 확인 상태를 변경하지 않습니다.");
        else if (저장유효부피Cbm != calculated.유효부피Cbm
                 || !string.Equals(저장근거Code, calculated.부피근거Code, StringComparison.Ordinal))
            warnings.Add("저장된 적재 계산 근거와 현재 입력의 재계산 결과가 다릅니다. 원입력과 변경 이력을 확인해 주세요.");
        if (effective != 저장유효부피Cbm)
            warnings.Add("현재 보수적인 부피 판정값은 저장된 부피와 다릅니다. 이 조회는 저장값을 변경하거나 과거 계산을 재확인하지 않습니다.");

        return calculated with
        {
            유효부피Cbm = effective,
            부피근거Code = 저장근거Code ?? "LegacyUnverified",
            계산판본 = 저장계산판본 ?? string.Empty,
            경고 = warnings.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private static void ValidatePositive(decimal? value, string label, ICollection<string> errors)
    {
        if (value <= 0m) errors.Add($"{label}은(는) 0보다 커야 합니다.");
    }

    private static string Fingerprint(화물적재입력 input)
    {
        // 고정 순서와 문화권 독립 숫자로 원입력 null·명시 false·문자열을 모두 구분한다.
        static string? Number(decimal? value) => value?.ToString("G29", CultureInfo.InvariantCulture);
        static string? Flag(bool? value) => value.HasValue ? value.Value ? "true" : "false" : null;
        static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        string?[] values = [CurrentRevision, Text(input.화물종류), Text(input.설명), Number(input.수량), Number(input.길이Mm),
            Number(input.폭Mm), Number(input.높이Mm), Number(input.총중량Kg), Number(input.입력부피Cbm),
            Number(input.팔레트개수), Text(input.온도조건) ?? "상온", Flag(input.바닥회전가능여부), Flag(input.적층가능여부),
            Flag(input.파손주의여부), Text(input.운송방식) ?? "혼적", Text(input.서비스레벨), Text(input.요청사항)];
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values))));
    }
}
