using Hongdal.Contracts.Common.VehicleLoading;

namespace DriverApp.Models.Driver.Samples;

public sealed record 기사현재위치샘플(
    string 위치명,
    decimal 위도,
    decimal 경도,
    DateTime 갱신시각);

public sealed record 추천의뢰표시항목(
    DriverRequestItem 의뢰,
    decimal 상차지까지거리Km,
    int 가까운순위)
{
    public string 상차지까지거리표시 => $"{상차지까지거리Km:0.0}km";
    public string 추천점수표시 => $"{의뢰.추천점수 ?? 0m:0}점";
    public string 예상수익표시 => $"{의뢰.예상수익 ?? 0m:0}원";
    public string 운송거리표시 => $"{의뢰.운송거리Km ?? 0m:0.0}km";
}

public sealed record 기사근무샘플상태(
    string 기사명,
    string 운행상태,
    string 시작모드,
    string 시작위치,
    string? 복귀지,
    DateTime 시작시각,
    int 추천콜수,
    int 오늘예약수);

public sealed record 기사예약샘플항목(
    long Id,
    DateTime 시작시각,
    string 시작모드,
    string 시작위치,
    string? 복귀지,
    string 상태,
    string 메모);

public sealed record 기사운송샘플항목(
    long Id,
    string 의뢰Id,
    string 화물종류,
    string 픽업지,
    string 하차지,
    decimal? 픽업위도,
    decimal? 픽업경도,
    decimal? 하차위도,
    decimal? 하차경도,
    string 현재단계,
    DateTime 예정시각,
    decimal 운송거리Km,
    decimal 예상수익,
    bool 인수증필요,
    bool 인수증서명필수,
    string 결제방식,
    string 다음행동)
{
    public string 수입화물유형 { get; init; } = "일반";

    public string 선하증권번호 { get; init; } = string.Empty;

    public string 컨테이너번호 { get; init; } = string.Empty;

    public string 봉인번호 { get; init; } = string.Empty;

    public string 상차작업방식 { get; init; } = "일반 상차";

    public string 상차주의사항 { get; init; } = "상차지, 화물 수량, 외관 상태를 확인합니다.";

    public IReadOnlyList<기사상차체크항목> 상차체크목록 { get; init; } = [];

    public IReadOnlyList<기사상차대상화물> 상차대상화물목록 { get; init; } = [];

    public bool IsLcl => string.Equals(수입화물유형, "LCL", StringComparison.OrdinalIgnoreCase);

    public bool IsFcl => string.Equals(수입화물유형, "FCL", StringComparison.OrdinalIgnoreCase);

    public string 수입화물유형표시 => IsLcl
        ? "LCL 혼재화물"
        : IsFcl
            ? "FCL 컨테이너"
            : 수입화물유형;

    public bool 적재순번필요
    {
        get
        {
            if (상차대상화물목록.Count <= 1)
            {
                return false;
            }

            var 기준화물 = 상차대상화물목록[0];
            return 상차대상화물목록.Any(item =>
                !string.Equals(item.Label, 기준화물.Label, StringComparison.Ordinal)
                || !string.Equals(item.하차위치, 기준화물.하차위치, StringComparison.Ordinal)
                || item.하차순번 != 기준화물.하차순번
                || !string.Equals(item.차량적재위치, 기준화물.차량적재위치, StringComparison.Ordinal)
                || item.수량 != 기준화물.수량
                || item.중량Kg != 기준화물.중량Kg);
        }
    }

    public string 적재순번운영안내 => 적재순번필요
        ? 혼적상하차순서계획기.후방하차운영원칙
        : "동일 하차지와 동일 규격 화물은 별도 혼적 순서 없이 수량 확인 중심으로 상차합니다.";
}

public sealed record 기사상차체크항목(
    string Code,
    string Label,
    string HelpText);

public sealed record 기사상차대상화물(
    string Code,
    string Barcode,
    string Label,
    string 하차위치,
    int 하차순번,
    int 적재순번,
    string 차량적재위치,
    int 수량,
    decimal 중량Kg,
    string 작업메모)
{
    public string 적재순번표시 => $"{적재순번}번";

    public string 하차순번표시 => $"{하차순번}번째 하차";

    public string 수량중량표시 => $"{수량:N0}개 / {중량Kg:0.##}kg";

    public string 상하차동선표시 => $"{적재순번표시} 상차 → {차량적재위치} → {하차순번표시}";
}

public sealed record 기사정산샘플요약(
    int 년도,
    int 월,
    int 배차건수,
    decimal 이용료,
    decimal 월상한,
    bool 결제완료,
    IReadOnlyList<기사정산샘플상세항목> 상세항목);

public sealed record 기사정산샘플상세항목(
    string 항목명,
    string 설명,
    decimal 금액);

public sealed record 기사알림샘플항목(
    long Id,
    string 종류,
    string 제목,
    string 내용,
    DateTime 발생시각,
    bool 읽음);
