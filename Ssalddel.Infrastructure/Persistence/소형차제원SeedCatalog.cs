using 살뜰.도메인.차량;

namespace 살뜰.Data;

/// <summary>
/// 실차 확인을 시작할 수 있는 이름표만 제공합니다. 미확인 수치는 적재 허용값이 아니며,
/// 장비별 허용 중량이나 해외 모델 제원을 국내 소형차 공통값으로 사용하지 않습니다.
/// </summary>
public static class 소형차제원SeedCatalog
{
    public const string 오토바이코드 = "오토바이";
    public const string 다마스코드 = "다마스";
    public const string 출처카탈로그경로 = "eng/vehicle-loading/small-vehicle-spec-sources.json";

    public static IReadOnlyList<차량제원> CreateUnverifiedTemplates() =>
    [
        CreateTemplate(오토바이코드, "이륜차",
            "실차와 장착 배달통의 내부·입구 치수, 허용 적재중량 확인 대기. 확인 전 추천하지 않습니다.",
            "오토바이 공통 적재 한도는 미확인입니다. 특정 탑박스의 용량·허용중량·표시 크기를 다른 장비나 실차에 적용하지 않습니다."),
        CreateTemplate(다마스코드, "밴",
            "국내 다마스의 연식·좌석·밴 모델과 실차 적재함 치수·허용 적재중량 확인 대기. 확인 전 추천하지 않습니다.",
            "국내 모델의 적재 제원은 미확인입니다. 해외 다마스의 중량이나 승합 모델 제원을 국내 밴 공통값으로 적용하지 않습니다.")
    ];

    private static 차량제원 CreateTemplate(string code, string bodyType, string strengths, string limitations) => new()
    {
        차량코드 = code,
        차량명 = code,
        차체형태 = bodyType,
        // 기존 모델의 필수 정수 필드는 0으로 미확인을 표현합니다. 실제 허용값 0을 조사했다는 뜻이 아닙니다.
        적재함길이Mm = 0,
        적재함폭Mm = 0,
        적재함높이Mm = null,
        최대적재중량Kg = 0,
        운영권장중량Kg = null,
        권장최대CBM = null,
        팔레트적재개수 = null,
        추천사용여부 = false,
        장점메모 = strengths,
        단점메모 = $"{limitations} 출처·미확인 항목: {출처카탈로그경로}"
    };
}
