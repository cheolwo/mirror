using Ssalddel.Contracts.Admin.Dispatch;

namespace 살뜰.Services.Dispatch.Coordination;

public interface IDomesticCargoDispatchAIReviewService
{
    Task<DomesticCargoDispatchAIReviewWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default);

    Task<DomesticCargoDispatchAIReviewDecisionResponse> RecordDecisionAsync(
        DomesticCargoDispatchAIReviewDecisionRequest request,
        string? adminUser,
        CancellationToken cancellationToken = default);
}

public sealed class DomesticCargoDispatchAIReviewService : IDomesticCargoDispatchAIReviewService
{
    private readonly I국내화물배차조율입력Factory _inputFactory;
    private readonly I국내화물배차조율Service _coordinationService;
    private readonly I배차AI판단사례LedgerStore _judgmentLedgerStore;

    public DomesticCargoDispatchAIReviewService(
        I국내화물배차조율입력Factory inputFactory,
        I국내화물배차조율Service coordinationService,
        I배차AI판단사례LedgerStore judgmentLedgerStore)
    {
        _inputFactory = inputFactory;
        _coordinationService = coordinationService;
        _judgmentLedgerStore = judgmentLedgerStore;
    }

    public async Task<DomesticCargoDispatchAIReviewWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var input = await _inputFactory.생성Async(
            new 국내화물배차조율입력요청
            {
                최대운송의뢰수 = 12,
                최대기사수 = 30,
                기사당최대추천건수 = 1
            },
            cancellationToken);

        if (input.운송의뢰목록.Count == 0 || input.기사후보목록.Count == 0)
        {
            return CreateSampleWorkspace();
        }

        var result = _coordinationService.조율(input);
        var assignments = result.추천배정목록
            .Select(x => new DomesticCargoDispatchAIReviewAssignmentDto
            {
                RequestId = x.의뢰Id,
                DriverId = x.기사Id,
                Order = x.순번,
                Score = x.추천점수,
                ExpectedCost = x.예상총비용,
                ExpectedFare = x.예상운임,
                ExpectedProfit = x.예상순이익,
                Reason = x.추천사유,
                Badges = x.배지.ToList()
            })
            .ToList();

        return new DomesticCargoDispatchAIReviewWorkspaceDto
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Source = "actual",
            Requests = input.운송의뢰목록.Select(ToDto).ToList(),
            Drivers = input.기사후보목록.Select(ToDto).ToList(),
            Bundles = BuildBundleDtos(input, result).ToList(),
            Assignments = assignments,
            Notes =
            [
                $"적용 알고리즘: {result.적용알고리즘}",
                $"가용 기사/의뢰 비율: {result.가용기사운송의뢰비율:0.##}",
                $"추천 {result.추천배정목록.Count}건, 보류 {result.보류목록.Count}건, 제외 {result.제외목록.Count}건"
            ]
        };
    }

    public async Task<DomesticCargoDispatchAIReviewDecisionResponse> RecordDecisionAsync(
        DomesticCargoDispatchAIReviewDecisionRequest request,
        string? adminUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestIds = request.RequestIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (requestIds.Count == 0)
        {
            throw new ArgumentException("판정할 운송 의뢰를 하나 이상 선택해야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(request.DriverId))
        {
            throw new ArgumentException("판정할 기사님을 선택해야 합니다.");
        }

        var accepted = request.Accepted ? "승인" : "보류";
        var manual = request.ManualBundle ? "수동 묶음" : "AI 제안";
        var bundleLabel = string.IsNullOrWhiteSpace(request.BundleKey) ? string.Join("+", requestIds) : request.BundleKey.Trim();
        var note = string.IsNullOrWhiteSpace(request.AdminNote)
            ? $"{manual} {bundleLabel}을 {accepted}했습니다."
            : request.AdminNote.Trim();

        var item = await _judgmentLedgerStore.CreateAsync(
            new DispatchAIJudgmentCaseCreateRequest
            {
                Title = $"{manual} 배차 판정: {bundleLabel}",
                RelatedOS = "국내 화물 운송 OS",
                Keywords =
                [
                    "국내화물운송OS",
                    "운영자판정",
                    request.ManualBundle ? "수동묶음" : "AI제안",
                    request.Accepted ? "승인" : "보류",
                    "기사위치",
                    "묶음",
                    "배차"
                ],
                SituationSummary = $"운영자가 지도형 배차 검토 화면에서 의뢰 {string.Join(", ", requestIds)}와 기사 {request.DriverId.Trim()}의 위치, 묶음, 추천 사유를 확인했습니다.",
                JudgmentSummary = note,
                UserDecision = request.DecisionType,
                BalancedDecision = $"{manual} {accepted}",
                Source = "admin-dispatch-ai-review",
                Active = true
            },
            adminUser,
            cancellationToken);

        return new DomesticCargoDispatchAIReviewDecisionResponse
        {
            CaseId = item.CaseId,
            Message = $"{item.CaseId} 판단 사례를 RAG 원장에 저장했습니다."
        };
    }

    private static IEnumerable<DomesticCargoDispatchAIReviewBundleDto> BuildBundleDtos(
        국내화물배차조율입력 input,
        국내화물배차조율결과 result)
    {
        var assignmentsByRequest = result.추천배정목록
            .GroupBy(x => x.의뢰Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        foreach (var bundle in input.수익묶음후보목록 ?? [])
        {
            var assignedDrivers = bundle.의뢰Ids
                .Where(assignmentsByRequest.ContainsKey)
                .Select(x => assignmentsByRequest[x].기사Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var suggestedDriverId = assignedDrivers.Length == 1 ? assignedDrivers[0] : null;
            var isSuggested = !string.IsNullOrWhiteSpace(suggestedDriverId)
                              && bundle.의뢰Ids.All(assignmentsByRequest.ContainsKey);

            yield return new DomesticCargoDispatchAIReviewBundleDto
            {
                BundleKey = bundle.묶음키,
                BundleType = bundle.묶음유형,
                RequestIds = bundle.의뢰Ids.ToList(),
                BundleSize = bundle.묶음크기,
                IsBundleAvailable = bundle.묶음가능여부,
                IsAISuggested = isSuggested,
                SuggestedDriverId = suggestedDriverId,
                Score = bundle.우선순위점수,
                ExpectedFare = bundle.예상운임합계,
                ExpectedCost = bundle.예상원가합계,
                ExpectedProfit = bundle.예상플랫폼순이익,
                ExpectedProfitPerRequest = bundle.예상건당플랫폼순이익,
                Badges = bundle.배지.ToList(),
                Warnings = bundle.경고.ToList(),
                ExclusionReasons = bundle.제외사유?.ToList() ?? [],
                Reason = bundle.선택근거
            };
        }
    }

    private static DomesticCargoDispatchAIReviewRequestDto ToDto(운송의뢰조율입력 item)
        => new()
        {
            QueueId = item.배차대기Id,
            RequestId = item.의뢰Id,
            SourceType = item.원본의뢰유형,
            CargoType = item.화물종류,
            PickupAddress = item.상차좌표 is null ? "상차 좌표 없음" : item.배달권명,
            PickupLatitude = item.상차좌표?.Latitude,
            PickupLongitude = item.상차좌표?.Longitude,
            DropoffAddress = item.하차좌표 is null ? "하차 좌표 없음" : item.배달권명,
            DropoffLatitude = item.하차좌표?.Latitude,
            DropoffLongitude = item.하차좌표?.Longitude,
            DeliveryScopeKey = item.배달권키,
            DeliveryScopeName = item.배달권명,
            Fare = item.최종운임,
            PickupWindowEndUtc = item.상차시간창종료Utc
        };

    private static DomesticCargoDispatchAIReviewDriverDto ToDto(기사후보조율입력 item)
        => new()
        {
            DriverId = item.기사Id,
            DriverName = item.기사Id,
            VehicleType = item.차량종류,
            DrivingStatus = item.운행상태,
            Latitude = item.현재좌표?.Latitude,
            Longitude = item.현재좌표?.Longitude,
            DeliveryScopeKey = item.배달권키,
            DeliveryScopeName = item.배달권명,
            CurrentAcceptedTransportCount = item.현재수락운송건수,
            LastLocationReceivedAtUtc = item.최근위치수신시각Utc
        };

    private static DomesticCargoDispatchAIReviewWorkspaceDto CreateSampleWorkspace()
    {
        var now = DateTimeOffset.UtcNow;
        return new DomesticCargoDispatchAIReviewWorkspaceDto
        {
            GeneratedAt = now,
            Source = "sample",
            Requests =
            [
                new()
                {
                    QueueId = 1001,
                    RequestId = "REQ-SAMPLE-1",
                    SourceType = "ShipperCargo",
                    CargoType = "상온 박스",
                    PickupAddress = "서울 마포구 공덕동",
                    PickupLatitude = 37.5446m,
                    PickupLongitude = 126.9517m,
                    DropoffAddress = "서울 서대문구 연희동",
                    DropoffLatitude = 37.5702m,
                    DropoffLongitude = 126.9358m,
                    DeliveryScopeKey = "bjd-sigungu:11440",
                    DeliveryScopeName = "서울 마포구",
                    Fare = 42000m,
                    PickupWindowEndUtc = now.UtcDateTime.AddMinutes(50)
                },
                new()
                {
                    QueueId = 1002,
                    RequestId = "REQ-SAMPLE-2",
                    SourceType = "ShipperCargo",
                    CargoType = "생활용품",
                    PickupAddress = "서울 마포구 상암동",
                    PickupLatitude = 37.5797m,
                    PickupLongitude = 126.8897m,
                    DropoffAddress = "서울 은평구 불광동",
                    DropoffLatitude = 37.6106m,
                    DropoffLongitude = 126.9293m,
                    DeliveryScopeKey = "bjd-sigungu:11440",
                    DeliveryScopeName = "서울 마포구",
                    Fare = 46000m,
                    PickupWindowEndUtc = now.UtcDateTime.AddMinutes(70)
                }
            ],
            Drivers =
            [
                new()
                {
                    DriverId = "driver-sample-a",
                    DriverName = "김기사",
                    VehicleType = "다마스",
                    DrivingStatus = "운행중",
                    Latitude = 37.5573m,
                    Longitude = 126.9368m,
                    DeliveryScopeKey = "bjd-sigungu:11440",
                    DeliveryScopeName = "서울 마포구",
                    LastLocationReceivedAtUtc = now.UtcDateTime.AddMinutes(-3)
                },
                new()
                {
                    DriverId = "driver-sample-b",
                    DriverName = "박기사",
                    VehicleType = "1톤 카고",
                    DrivingStatus = "운행중",
                    Latitude = 37.6025m,
                    Longitude = 126.9287m,
                    DeliveryScopeKey = "bjd-sigungu:11410",
                    DeliveryScopeName = "서울 서대문구",
                    CurrentAcceptedTransportCount = 1,
                    LastLocationReceivedAtUtc = now.UtcDateTime.AddMinutes(-8)
                }
            ],
            Bundles =
            [
                new()
                {
                    BundleKey = "REQ-SAMPLE-1+REQ-SAMPLE-2",
                    BundleType = "멀티묶음",
                    RequestIds = ["REQ-SAMPLE-1", "REQ-SAMPLE-2"],
                    BundleSize = 2,
                    IsBundleAvailable = true,
                    IsAISuggested = true,
                    SuggestedDriverId = "driver-sample-a",
                    Score = 94m,
                    ExpectedFare = 88000m,
                    ExpectedCost = 65000m,
                    ExpectedProfit = 23000m,
                    ExpectedProfitPerRequest = 11500m,
                    Badges = ["같은배달권", "상차지근접", "한 명의 기사에게 묶음 동시 배정", "판단근거반영"],
                    Reason = "두 의뢰의 상차지가 가깝고 같은 배달권 안에 있어 한 명의 기사에게 묶음 동시 배정할 수 있습니다."
                }
            ],
            Assignments =
            [
                new()
                {
                    RequestId = "REQ-SAMPLE-1",
                    DriverId = "driver-sample-a",
                    Order = 1,
                    Score = 91m,
                    ExpectedFare = 42000m,
                    ExpectedCost = 31000m,
                    ExpectedProfit = 11000m,
                    Reason = "기사 위치가 상차지와 가깝고 묶음의 첫 상차로 적합합니다.",
                    Badges = ["지도확인", "같은배달권"]
                },
                new()
                {
                    RequestId = "REQ-SAMPLE-2",
                    DriverId = "driver-sample-a",
                    Order = 2,
                    Score = 89m,
                    ExpectedFare = 46000m,
                    ExpectedCost = 34000m,
                    ExpectedProfit = 12000m,
                    Reason = "같은 기사에게 묶음으로 붙이면 총 이동 낭비가 줄어듭니다.",
                    Badges = ["한 명의 기사에게 묶음 동시 배정"]
                }
            ],
            Notes = ["실데이터 후보가 없어 운영자 판정 루프 검증용 샘플을 표시합니다."]
        };
    }
}
