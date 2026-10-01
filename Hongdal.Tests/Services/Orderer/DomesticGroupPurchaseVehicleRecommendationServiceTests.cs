using Hongdal.Contracts.Common.Orderer;
using Hongdal.Services.LogisticsProcessing.VehicleLoading;
using Hongdal.Services.Orderer;
using 홍달.도메인.차량;

namespace Hongdal.Tests.Services.Orderer;

public sealed class DomesticGroupPurchaseVehicleRecommendationServiceTests
{
    [Fact]
    public async Task PreviewAsync_AggregatesParticipantOrdersBeforeCalculatingPackages()
    {
        var loadingService = new StubLoadingRecommendationService();
        var service = new DomesticGroupPurchaseVehicleRecommendationService(
            new StubAutoGroupStore(),
            loadingService);
        var request = CreateRequest();
        request.Orders =
        [
            Order("order-1", "person-1", 6),
            Order("order-2", "person-2", 4)
        ];

        var result = await service.PreviewAsync(request);

        Assert.Equal(DomesticGroupPurchaseQuantitySourceCodes.ExplicitOrders, result.QuantitySourceCode);
        Assert.Equal(2, result.ParticipantCount);
        Assert.Equal(2, result.OrderCount);
        Assert.Equal(1, result.TotalPackageCount);
        Assert.Equal(12m, result.ActualGrossWeightKg);
        Assert.Equal(12.6m, result.PlannedWeightWithMarginKg);
        Assert.Equal(0.06m, result.RawPackageVolumeCbm);
        Assert.Equal(0.074m, result.PlannedLoadingVolumeCbm);
        Assert.True(result.CanTransportInSingleTrip);
        Assert.Equal("test-truck", result.RecommendedVehicleType);
        Assert.NotNull(loadingService.LastRequirement);
        Assert.Equal(12.6m, loadingService.LastRequirement!.총중량Kg);
        Assert.Equal(0.074m, loadingService.LastRequirement.총부피Cbm);
    }

    [Fact]
    public async Task PreviewAsync_WhenParticipantPackagesStaySeparate_RoundsEachOrderSeparately()
    {
        var service = new DomesticGroupPurchaseVehicleRecommendationService(
            new StubAutoGroupStore(),
            new StubLoadingRecommendationService());
        var request = CreateRequest();
        request.KeepParticipantPackagesSeparate = true;
        request.Orders =
        [
            Order("order-1", "person-1", 6),
            Order("order-2", "person-2", 4)
        ];

        var result = await service.PreviewAsync(request);

        Assert.Equal(2, result.TotalPackageCount);
        Assert.Equal(24m, result.ActualGrossWeightKg);
        Assert.Contains(result.CalculationBasis, x => x.Contains("각 참여자 단위", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreviewAsync_WhenOneParticipantHasMultipleLines_CombinesTheirQuantityBeforeRounding()
    {
        var service = new DomesticGroupPurchaseVehicleRecommendationService(
            new StubAutoGroupStore(),
            new StubLoadingRecommendationService());
        var request = CreateRequest();
        request.KeepParticipantPackagesSeparate = true;
        request.Orders =
        [
            Order("order-1", "person-1", 6),
            Order("order-2", "person-1", 4)
        ];

        var result = await service.PreviewAsync(request);

        Assert.Equal(1, result.ParticipantCount);
        Assert.Equal(1, result.TotalPackageCount);
    }

    [Fact]
    public async Task PreviewAsync_ReservedMode_ExcludesInterestOnlyAutoGroupDemand()
    {
        var group = new 공동구매자동집단응답
        {
            자동집단Id = "auto-group-1",
            상품키 = "sweet-potato",
            상품명 = "고구마",
            수요목록 =
            [
                new 공동구매자동수요응답
                {
                    수요Id = "interest",
                    수요유형 = 공동구매자동수요유형코드.관심표시,
                    결제상태 = 공동구매자동결제상태코드.미결제,
                    희망수량 = 20,
                    수량단위 = "kg"
                },
                new 공동구매자동수요응답
                {
                    수요Id = "reserved",
                    수요유형 = 공동구매자동수요유형코드.예약결제,
                    결제상태 = 공동구매자동결제상태코드.예약됨,
                    희망수량 = 10,
                    수량단위 = "kg"
                }
            ]
        };
        var service = new DomesticGroupPurchaseVehicleRecommendationService(
            new StubAutoGroupStore(group),
            new StubLoadingRecommendationService());
        var request = CreateRequest();
        request.AutoGroupId = group.자동집단Id;
        request.QuantitySourceCode = DomesticGroupPurchaseQuantitySourceCodes.ReservedOrConfirmed;

        var result = await service.PreviewAsync(request);

        Assert.Equal(1, result.OrderCount);
        Assert.False(result.ContainsUnconfirmedDemand);
        Assert.Equal(1, result.TotalPackageCount);
        Assert.Equal(DomesticGroupPurchaseQuantitySourceCodes.ReservedOrConfirmed, result.QuantitySourceCode);
    }

    private static DomesticGroupPurchaseVehicleRecommendationRequest CreateRequest()
        => new()
        {
            GroupPurchaseCampaignId = Guid.NewGuid(),
            LoadingEfficiencyRate = 0.85m,
            SafetyMarginRate = 0.05m,
            ProductPackages =
            [
                new DomesticGroupPurchaseProductPackageSpecification
                {
                    ProductKey = "sweet-potato",
                    ProductName = "고구마",
                    QuantityUnit = "kg",
                    UnitsPerPackage = 10,
                    PackageLengthMm = 500,
                    PackageWidthMm = 400,
                    PackageHeightMm = 300,
                    PackageGrossWeightKg = 12,
                    TemperatureCode = "상온"
                }
            ]
        };

    private static DomesticGroupPurchaseVehicleOrderItem Order(
        string orderKey,
        string participantKey,
        decimal quantity)
        => new()
        {
            OrderKey = orderKey,
            ParticipantKey = participantKey,
            ProductKey = "sweet-potato",
            Quantity = quantity,
            QuantityUnit = "kg"
        };

    private sealed class StubLoadingRecommendationService : I차량적재추천Service
    {
        private readonly 차량적재추천Engine _engine = new();

        public 차량적재추천요구사항? LastRequirement { get; private set; }

        public Task<차량적재추천분석결과> 추천Async(
            차량적재추천요구사항 요구사항,
            CancellationToken cancellationToken = default)
        {
            LastRequirement = 요구사항;
            var vehicle = new 차량제원
            {
                차량코드 = "test-truck",
                차량명 = "test-truck",
                차체형태 = "탑차",
                적재함길이Mm = 3_000,
                적재함폭Mm = 1_700,
                적재함높이Mm = 1_700,
                최대적재중량Kg = 1_000,
                운영권장중량Kg = 900,
                권장최대CBM = 7,
                추천사용여부 = true
            };
            return Task.FromResult(_engine.분석(요구사항, [vehicle]));
        }
    }

    private sealed class StubAutoGroupStore : I공동구매자동집단화저장소
    {
        private readonly 공동구매자동집단응답? _group;

        public StubAutoGroupStore(공동구매자동집단응답? group = null)
        {
            _group = group;
        }

        public Task<공동구매자동집단응답> 수요등록Async(
            공동구매자동수요등록Command command,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<공동구매자동집단응답>> 집단목록조회Async(
            공동구매자동집단조회조건 조건,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공동구매자동집단응답>>([]);

        public Task<공동구매자동집단응답?> 집단조회Async(
            string 자동집단Id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                _group is not null && string.Equals(_group.자동집단Id, 자동집단Id, StringComparison.Ordinal)
                    ? _group
                    : null);

        public Task<공동구매자동집단응답> 개별주문원장연결Async(
            string 자동집단Id,
            string 수요Id,
            string 공동구매주문집계원장Id,
            string 개별주문원장Id,
            string 입고예정원장Id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
