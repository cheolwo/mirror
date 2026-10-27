using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Shipper;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Services.Shipper;

public sealed class ShipperHomeDashboardClientTests
{
    [Fact]
    public async Task 익명사용자는_기능상태만읽고_개인원장API를호출하지않는다()
    {
        var api = new RecordingJsonApiClient();
        api.Responses["api/v1/version-feature-flags"] = Metadata(
            (ShipperHomeFeatureKeys.DomesticTransport, true),
            (ShipperHomeFeatureKeys.WarehouseFulfillment, true));
        var client = new ShipperHomeDashboardClient(
            api,
            new Test현재사용자Context(현재사용자Snapshot.익명));

        var snapshot = await client.LoadAsync();

        Assert.False(snapshot.IsAuthenticated);
        Assert.True(snapshot.FeatureMetadataAvailable);
        Assert.True(snapshot.IsFeatureEnabled(ShipperHomeFeatureKeys.DomesticTransport));
        Assert.Single(api.Paths);
        Assert.Equal("api/v1/version-feature-flags", api.Paths[0]);
    }

    [Fact]
    public async Task 비활성workflow는_로그인상태에서도_업무API를호출하지않는다()
    {
        var api = new RecordingJsonApiClient();
        api.Responses["api/v1/version-feature-flags"] = Metadata(
            (ShipperHomeFeatureKeys.DomesticTransport, false),
            (ShipperHomeFeatureKeys.WarehouseFulfillment, false));
        var client = new ShipperHomeDashboardClient(
            api,
            AuthenticatedContext());

        var snapshot = await client.LoadAsync();

        Assert.True(snapshot.IsAuthenticated);
        Assert.True(snapshot.FeatureMetadataAvailable);
        Assert.Equal(0, snapshot.ActiveRequestCount);
        Assert.Equal(0, snapshot.PendingInboundCount);
        Assert.Equal(0, snapshot.ActionableInventoryCount);
        Assert.Single(api.Paths);
    }

    [Fact]
    public async Task 활성workflow만_읽기요약을조회하고_최근원장과건수를계산한다()
    {
        var api = new RecordingJsonApiClient();
        api.Responses["api/v1/version-feature-flags"] = Metadata(
            (ShipperHomeFeatureKeys.DomesticTransport, true),
            (ShipperHomeFeatureKeys.WarehouseFulfillment, true),
            (ShipperHomeFeatureKeys.SalesChannelFulfillment, false),
            (ShipperHomeFeatureKeys.CustomsAndTradeData, false));
        api.Responses["api/v1/shipper/requests?shipperId=user-17"] =
            new 화주운송의뢰응답[]
            {
                new()
                {
                    의뢰Id = "REQ-OLD",
                    의뢰상태 = "운송중",
                    결제상태 = "결제완료",
                    배차상태 = "운송중",
                    생성일시 = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc)
                },
                new()
                {
                    의뢰Id = "REQ-LATEST",
                    의뢰상태 = "완료",
                    결제상태 = "결제완료",
                    배차상태 = "배송완료",
                    생성일시 = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc)
                }
            };
        api.Responses["api/v1/shipper/warehouse-summary"] = new ShipperWarehouseSummaryResponse
        {
            PendingInboundCount = 1,
            ActionableInventoryCount = 1
        };
        var client = new ShipperHomeDashboardClient(api, AuthenticatedContext());

        var snapshot = await client.LoadAsync();

        Assert.Equal("REQ-LATEST", snapshot.LatestRequest?.RequestId);
        Assert.Equal(1, snapshot.ActiveRequestCount);
        Assert.Equal(1, snapshot.PendingInboundCount);
        Assert.Equal(1, snapshot.ActionableInventoryCount);
        Assert.Contains("api/v1/shipper/requests?shipperId=user-17", api.Paths);
        Assert.Contains("api/v1/shipper/warehouse-summary", api.Paths);
        Assert.DoesNotContain(api.Paths, path => path.StartsWith("api/v1/warehouse-operations/", StringComparison.Ordinal));
        Assert.Empty(snapshot.Warnings);
        Assert.DoesNotContain(api.Paths, path => path.Contains("sales", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("후불승인완료", "배차확정", "", false, 정산시점.운송완료후정산, "기사 배정 완료")]
    [InlineData("후불승인완료", "배차확정", "운송중", false, 정산시점.월말정산, "운송 중")]
    [InlineData("후불승인완료", "배차확정", "하차지도착", false, 정산시점.운송완료후정산, "하차 확인 진행")]
    [InlineData("후불승인완료", "배송완료", "", false, 정산시점.운송완료후정산, "운송 완료")]
    [InlineData("후불승인완료", "취소", "", false, 정산시점.운송완료후정산, "의뢰 취소 상태")]
    [InlineData("후불승인완료", "배차확정", "", true, 정산시점.운송완료후정산, "진행 확인·처리 대기")]
    [InlineData("결제대기", "배차대기", "", false, 정산시점.선결제, "접수 준비 · 결제 대기")]
    [InlineData("후불승인완료", "배차대기", "", false, 정산시점.월말정산, "기사 배차 대기")]
    [InlineData("후불승인대기", "배차대기", "", false, 정산시점.운송완료후정산, "후불 승인 확인 필요")]
    [InlineData("후불승인완료", "미확인단계", "", false, 정산시점.운송완료후정산, "진행 상태 확인 필요")]
    public async Task 최근의뢰안내는_결제대기와운송진행을구분하고_상세화면과같은정책을사용한다(
        string settlementStatus,
        string dispatchStatus,
        string transportStatus,
        bool reviewHeld,
        정산시점 settlementTiming,
        string expectedNextAction)
    {
        var api = new RecordingJsonApiClient();
        api.Responses["api/v1/version-feature-flags"] = Metadata(
            (ShipperHomeFeatureKeys.DomesticTransport, true));
        api.Responses["api/v1/shipper/requests?shipperId=user-17"] =
            new 화주운송의뢰응답[]
            {
                new()
                {
                    의뢰Id = "REQ-UNPAID",
                    의뢰상태 = "생성됨",
                    결제상태 = "결제대기",
                    정산상태 = settlementStatus,
                    배차상태 = dispatchStatus,
                    운송상태 = transportStatus,
                    비정상운송검토보류중 = reviewHeld,
                    정산시점 = settlementTiming,
                    생성일시 = DateTime.UnixEpoch
                }
            };

        var snapshot = await new ShipperHomeDashboardClient(api, AuthenticatedContext()).LoadAsync();

        Assert.NotNull(snapshot.LatestRequest);
        Assert.Equal(expectedNextAction, snapshot.LatestRequest.NextActionTitle);
        Assert.Equal("결제대기", snapshot.LatestRequest.PaymentStatus);
        Assert.Equal(dispatchStatus, snapshot.LatestRequest.DispatchStatus);
        Assert.Empty(snapshot.Warnings);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 본인창고요약의_실패나빈응답은_정상빈상태로숨기지않는다(bool fails)
    {
        var api = new RecordingJsonApiClient { FailurePath = fails ? "api/v1/shipper/warehouse-summary" : null };
        api.Responses["api/v1/version-feature-flags"] = Metadata((ShipperHomeFeatureKeys.WarehouseFulfillment, true));

        var snapshot = await new ShipperHomeDashboardClient(api, AuthenticatedContext()).LoadAsync();

        Assert.Single(snapshot.Warnings);
        Assert.Contains("입고·재고 요약을 불러오지 못했습니다", snapshot.Warnings[0]);
        Assert.DoesNotContain(api.Paths, path => path.StartsWith("api/v1/warehouse-operations/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 기능상태조회실패는_안전한비활성과재시도안내로변환한다()
    {
        var api = new RecordingJsonApiClient { FailurePath = "api/v1/version-feature-flags" };
        var client = new ShipperHomeDashboardClient(api, AuthenticatedContext());

        var snapshot = await client.LoadAsync();

        Assert.False(snapshot.FeatureMetadataAvailable);
        Assert.False(snapshot.IsFeatureEnabled(ShipperHomeFeatureKeys.DomesticTransport));
        Assert.Contains(snapshot.Warnings, warning => warning.Contains("비활성", StringComparison.Ordinal));
        Assert.Single(api.Paths);
    }

    [Fact]
    public async Task ViewModel은_조회실패를_error상태로보존하고_로딩을해제한다()
    {
        var viewModel = new ShipperHomePageViewModel(new ThrowingDashboardClient());

        await viewModel.LoadAsync();

        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.HasLoaded);
        Assert.Contains("불러오지 못했습니다", viewModel.ErrorMessage);
    }

    private static VersionFeatureFlagsResponse Metadata(params (string Key, bool Enabled)[] flags)
        => new()
        {
            Flags = flags.ToDictionary(item => item.Key, item => item.Enabled, StringComparer.OrdinalIgnoreCase)
        };

    private static Test현재사용자Context AuthenticatedContext()
        => new(new 현재사용자Snapshot("user-17", "화주17", ["Shipper"]));

    private sealed class Test현재사용자Context(현재사용자Snapshot snapshot)
        : ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot 현재사용자 { get; } = snapshot;
    }

    private sealed class ThrowingDashboardClient : IShipperHomeDashboardClient
    {
        public Task<ShipperHomeDashboardSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("test failure");
    }

    private sealed class RecordingJsonApiClient : ISsalddelJsonApiClient
    {
        public Dictionary<string, object> Responses { get; } = new(StringComparer.Ordinal);
        public List<string> Paths { get; } = [];
        public string? FailurePath { get; init; }

        public Task<TResponse?> GetAsync<TResponse>(
            string path,
            string operationName,
            bool allowNotFound = true,
            CancellationToken cancellationToken = default)
        {
            Paths.Add(path);
            if (string.Equals(path, FailurePath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("test failure");
            }

            return Task.FromResult(
                Responses.TryGetValue(path, out var response)
                    ? (TResponse?)response
                    : default);
        }

        public Task<TResponse?> SendAsync<TResponse>(
            HttpMethod method,
            string path,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TResponse?> SendAsync<TRequest, TResponse>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SendAsync(
            HttpMethod method,
            string path,
            string operationName,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SendAsync<TRequest>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
