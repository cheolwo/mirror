using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ssalddel.Contracts.Common.Dispatch;

/// <summary>판단 항목의 공통 의미입니다. 개인별 값·점수·주소나 실제 알고리즘 버전을 담지 않습니다.</summary>
public sealed record CargoDispatchBasisDefinition(string Code, string Label, string Kind, string Explanation);

/// <summary>공통 근거 설명의 불변 판본입니다. 알고리즘·AI 모델 버전 또는 판단 품질 점수가 아닙니다.</summary>
public sealed class CargoDispatchBasisCatalogVersion
{
    internal CargoDispatchBasisCatalogVersion(string version, IEnumerable<CargoDispatchBasisDefinition> definitions)
    {
        Version = version;
        Definitions = Array.AsReadOnly(definitions.ToArray());
        // 직렬화 속성/정의 순서와 기본 JSON 규칙도 이 설명 판본의 내용에 포함합니다.
        var canonical = JsonSerializer.Serialize(new { Version, Definitions });
        ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public string Version { get; }
    public string ContentHash { get; }
    public IReadOnlyList<CargoDispatchBasisDefinition> Definitions { get; }
}

/// <summary>과거 판본을 현재 설명으로 대체하지 않는 공통 근거 설명 대장입니다.</summary>
public static class CargoDispatchBasisCatalog
{
    // r1의 의미·문구·순서를 변경하지 않습니다. 변경이 필요하면 독립 판본을 추가합니다.
    private static readonly CargoDispatchBasisCatalogVersion R1 = new("cargo-dispatch-basis.r1",
    [
        new("intake.weight", "당시 접수 중량", "intake",
            "추천 기록 시 접수 원장에서 확인한 화물 중량입니다. 최초 접수 시점의 원본이나 실제 계측·검수 결과를 뜻하지 않습니다."),
        new("intake.mode", "당시 운송 방식", "intake",
            "추천 기록 시 접수된 독차·혼적 요청 조건입니다. 기사 수락이나 최종 운송 계약을 뜻하지 않습니다."),
        new("intake.temperature", "당시 온도 조건", "intake",
            "추천 기록 시 접수된 온도 조건입니다. 실제 설비의 온도 유지나 검증 결과를 뜻하지 않습니다."),
        new("intake.pickup-deadline", "당시 상차 마감", "intake",
            "추천 기록 시 접수된 상차 마감 시각입니다. 실제 도착·상차 완료나 시간 준수를 보장하지 않습니다."),
        new("intake.dropoff-deadline", "당시 하차 마감", "intake",
            "추천 기록 시 접수된 하차 마감 시각입니다. 실제 도착·하차 완료나 시간 준수를 보장하지 않습니다."),
        new("decision.plan", "당시 수행계획", "decision",
            "추천 저장 시 전달받아 기록한 수행계획 검증 결과입니다. 미확인과 누락을 구분하며 실제 운송 완료를 뜻하지 않습니다."),
        new("decision.loading", "당시 구간 적재", "decision",
            "추천 저장 시 전달받아 기록한 구간별 적재 검증 결과입니다. 미확인이나 누락을 실제 적재 가능 확인으로 바꾸지 않습니다."),
        new("decision.pickup-order", "이번 상차 순서", "decision",
            "기록된 계획에서 이번 운송의 상차 정차 순서입니다. 실제 운행 순서의 확정이나 전체 후보 비교 결과를 뜻하지 않습니다."),
        new("decision.dropoff-order", "이번 하차 순서", "decision",
            "기록된 계획에서 이번 운송의 하차 정차 순서입니다. 실제 운행 순서의 확정이나 전체 후보 비교 결과를 뜻하지 않습니다.")
    ]);

    private static readonly IReadOnlyDictionary<string, CargoDispatchBasisCatalogVersion> Versions =
        new ReadOnlyDictionary<string, CargoDispatchBasisCatalogVersion>(
            new Dictionary<string, CargoDispatchBasisCatalogVersion>(StringComparer.Ordinal) { [R1.Version] = R1 });

    public static CargoDispatchBasisCatalogVersion Current => R1;

    /// <summary>일치하는 설명 판본만 조회합니다. 알 수 없는 판본을 현재 판본으로 보충하지 않습니다.</summary>
    public static bool TryGet(string version, out CargoDispatchBasisCatalogVersion catalog)
    {
        if (version is not null && Versions.TryGetValue(version, out var found))
        {
            catalog = found;
            return true;
        }
        catalog = null!;
        return false;
    }
}
