using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace RestaurantDeskApp.Services;

/// <summary>
/// 음식점 식재료 공급 요청 화면을 검증하기 위한 Simulation adapter입니다.
/// 실제 공급자 선정, 계약, 수입, 통관 또는 결제를 수행하지 않습니다.
/// </summary>
public sealed class RestaurantIngredientSupplySampleService : I음식점식재료공급요청Service
{
    private readonly object _gate = new();
    private readonly List<음식점식재료공급요청Snapshot> _requests;
    private int _sequence = 2;

    public RestaurantIngredientSupplySampleService()
    {
        _requests =
        [
            new(
                "ING-REQ-SAMPLE-001",
                음식점식재료공급요청상태.수요모으는중,
                "인근 음식점 수요 모으는 중",
                new 음식점식재료공급요청Draft
                {
                    공급경로 = 음식점식재료공급경로.국내산지,
                    품목명 = "대파",
                    품목분류 = "농산물",
                    규격 = "1kg 단",
                    필요수량 = 80,
                    수량단위 = "kg",
                    납품주기 = "매주",
                    희망납품일 = DateTime.Today.AddDays(5),
                    현재구매단가 = 4200,
                    희망도착단가 = 3700,
                    통화코드 = "KRW",
                    희망원산지 = "경기·충청 산지",
                    납품지역 = "서울 강서구",
                    보관방식 = 음식점식재료보관방식.냉장,
                    사용목적 = "국·볶음 고명",
                    공동수요집계동의 = true,
                    산지Lot추적필수 = true
                },
                "domestic-producer-pool",
                "경기 남부 생산자 공동출하",
                DateTimeOffset.Now.AddDays(-1),
                true)
        ];
    }

    public bool SimulationMode => true;

    public Task<IReadOnlyList<음식점식재료공급후보>> 공급후보조회Async(
        음식점식재료공급요청Draft request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<음식점식재료공급후보> candidates = request.공급경로 switch
        {
            음식점식재료공급경로.공동수입 => BuildImportCandidates(request),
            _ => BuildDomesticCandidates(request)
        };

        return Task.FromResult(candidates);
    }

    public Task<IReadOnlyList<음식점식재료공급요청Snapshot>> 요청목록조회Async(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<음식점식재료공급요청Snapshot>>(
                _requests.Select(Clone).ToArray());
        }
    }

    public Task<음식점식재료공급요청Snapshot> 초안저장Async(
        음식점식재료공급요청Draft request,
        string? selectedCandidateId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.품목명) || request.필요수량 <= 0)
        {
            throw new InvalidOperationException("품목명과 필요 수량을 확인해 주세요.");
        }

        var selectedCandidate = BuildCandidates(request)
            .FirstOrDefault(candidate => candidate.후보Id == selectedCandidateId);

        음식점식재료공급요청Snapshot saved;
        lock (_gate)
        {
            saved = new(
                $"ING-REQ-{DateTime.Today:yyyyMMdd}-{_sequence++:000}",
                음식점식재료공급요청상태.초안,
                "Simulation 초안",
                request.복사(),
                selectedCandidate?.후보Id,
                selectedCandidate?.공급주체명,
                DateTimeOffset.Now,
                true);
            _requests.Add(saved);
        }

        return Task.FromResult(Clone(saved));
    }

    private static IReadOnlyList<음식점식재료공급후보> BuildCandidates(
        음식점식재료공급요청Draft request)
        => request.공급경로 == 음식점식재료공급경로.공동수입
            ? BuildImportCandidates(request)
            : BuildDomesticCandidates(request);

    private static IReadOnlyList<음식점식재료공급후보> BuildDomesticCandidates(
        음식점식재료공급요청Draft request)
    {
        var benchmark = request.현재구매단가 > 0 ? request.현재구매단가 : 2300m;
        var firstProduct = RoundToTen(benchmark * 0.76m);
        var firstLogistics = RoundToTen(benchmark * 0.10m);
        var secondProduct = RoundToTen(benchmark * 0.81m);
        var secondLogistics = RoundToTen(benchmark * 0.10m);
        var unit = NormalizeUnit(request.수량단위);
        var requestedDate = request.희망납품일?.Date ?? DateTime.Today.AddDays(7);

        return
        [
            new(
                "domestic-producer-pool",
                음식점식재료공급경로.국내산지,
                "국내 산지 공동공급",
                "경기 남부 생산자 공동출하",
                string.IsNullOrWhiteSpace(request.희망원산지) ? "국내 산지" : request.희망원산지,
                $"{request.품목명} · {request.규격}",
                Math.Max(request.필요수량 * 4m, 300m),
                unit,
                5,
                firstProduct,
                firstLogistics,
                0,
                firstProduct + firstLogistics,
                benchmark,
                request.통화코드,
                requestedDate,
                request.보관방식.ToString(),
                "산지 출하가 + 공동 선별·운송 예상액",
                "품질 등급 변경, 긴급 재배송, 개별 소분 추가 작업",
                ["생산자·산지조직", "산지 선별·포장", "국내 운송", "납품 음식점"],
                true,
                true),
            new(
                "domestic-market-hub",
                음식점식재료공급경로.국내산지,
                "지역시장 공동입고",
                "전통시장 산지 직입고 협의",
                string.IsNullOrWhiteSpace(request.희망원산지) ? "국내 산지" : request.희망원산지,
                $"{request.품목명} · {request.규격}",
                Math.Max(request.필요수량 * 3m, 240m),
                unit,
                3,
                secondProduct,
                secondLogistics,
                0,
                secondProduct + secondLogistics,
                benchmark,
                request.통화코드,
                requestedDate.AddDays(1),
                request.보관방식.ToString(),
                "산지 출하가 + 시장 검수·입고·생활권 운송 예상액",
                "가게별 추가 소분, 영업시간 외 인계, 반품 회수",
                ["생산자", "시장 입고·검수", "생활권 운송", "납품 음식점"],
                true,
                true)
        ];
    }

    private static IReadOnlyList<음식점식재료공급후보> BuildImportCandidates(
        음식점식재료공급요청Draft request)
    {
        var benchmark = request.현재구매단가 > 0 ? request.현재구매단가 : 5600m;
        var firstProduct = RoundToTen(benchmark * 0.68m);
        var firstLogistics = RoundToTen(benchmark * 0.08m);
        var firstImportCost = RoundToTen(benchmark * 0.075m);
        var secondProduct = RoundToTen(benchmark * 0.72m);
        var secondLogistics = RoundToTen(benchmark * 0.07m);
        var secondImportCost = RoundToTen(benchmark * 0.08m);
        var unit = NormalizeUnit(request.수량단위);
        var requestedDate = request.희망납품일?.Date ?? DateTime.Today.AddDays(35);
        var origin = string.IsNullOrWhiteSpace(request.희망원산지)
            ? "원산지 협의 필요"
            : request.희망원산지;

        return
        [
            new(
                "import-shared-fcl",
                음식점식재료공급경로.공동수입,
                "공동수입 물량 집계",
                "검증 전 수입자·공장 조건 예시 A",
                origin,
                $"{request.품목명} · {request.규격}",
                Math.Max(request.필요수량 * 4m, 1200m),
                unit,
                8,
                firstProduct,
                firstLogistics,
                firstImportCost,
                firstProduct + firstLogistics + firstImportCost,
                benchmark,
                request.통화코드,
                requestedDate,
                request.보관방식.ToString(),
                "공장 출고가 + 국제·국내 물류 + 통관·검역 예상액",
                "관세율 확정 차이, 검사·보관 지연, 환율 변동, 폐기·반송",
                ["수출자", "수입자", "관세사·검역", "국제·국내 물류", "납품 음식점"],
                true,
                true),
            new(
                "import-shared-lcl",
                음식점식재료공급경로.공동수입,
                "소량 공동수입",
                "검증 전 수입자·혼재화물 조건 예시 B",
                origin,
                $"{request.품목명} · {request.규격}",
                Math.Max(request.필요수량 * 2m, 600m),
                unit,
                4,
                secondProduct,
                secondLogistics,
                secondImportCost,
                secondProduct + secondLogistics + secondImportCost,
                benchmark,
                request.통화코드,
                requestedDate.AddDays(5),
                request.보관방식.ToString(),
                "공장 출고가 + 혼재운송 + 통관·검역 예상액",
                "관세율 확정 차이, 혼재창고 작업, 검사·보관 지연, 환율 변동",
                ["수출자", "수입자", "관세사·검역", "혼재화물 물류", "납품 음식점"],
                true,
                true)
        ];
    }

    private static 음식점식재료공급요청Snapshot Clone(
        음식점식재료공급요청Snapshot source)
        => source with { 요청 = source.요청.복사() };

    private static decimal RoundToTen(decimal value)
        => Math.Round(value / 10m, MidpointRounding.AwayFromZero) * 10m;

    private static string NormalizeUnit(string? value)
        => string.IsNullOrWhiteSpace(value) ? "kg" : value.Trim();
}
