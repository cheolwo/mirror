using System.Text.Json;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class Bagua전환RuntimeViewModelTests
{
    [Fact]
    public async Task 조회기능은_원본식별자를_경로에_연결하고_서버응답을_보관한다()
    {
        var client = new RecordingApiClient();
        using var page = CreatePage(client);
        page.초기화(
            BaguaActorRoleCodes.WarehouseManager,
            BaguaTrigramKeys.Zhen,
            BaguaTrigramKeys.Dui);

        page.전환Runtime.원본선택("order-42", 7);
        page.전환Runtime.기능선택("common.order-ledgers", "view-warehouse");

        var result = await page.전환Runtime.실행Async();

        Assert.True(result);
        Assert.Equal(HttpMethod.Get, client.LastMethod);
        Assert.Equal(
            "api/v1/community/order-ledgers/order-42/views/warehouse",
            client.LastPath);
        Assert.Equal("{\"result\":\"ok\"}", page.전환Runtime.응답Json);
        Assert.Equal(7, page.전환Runtime.초안.예상Revision);
    }

    [Fact]
    public async Task 변경기능은_역할관점만으로_실행하지_않고_서버권한까지_요구한다()
    {
        var client = new RecordingApiClient();
        using var page = CreatePage(client);
        page.초기화(
            BaguaActorRoleCodes.WarehouseManager,
            BaguaTrigramKeys.Zhen,
            BaguaTrigramKeys.Dui);
        page.전환Runtime.원본선택("order-42");
        page.전환Runtime.기능선택("common.warehouse-operations", "create-inbound");
        page.전환Runtime.요청Json설정("{\"warehouseId\":\"warehouse-1\"}");

        Assert.False(await page.전환Runtime.실행Async());
        Assert.Equal("변경 작업은 서버 권한 확인이 필요합니다.", page.전환Runtime.오류메시지);
        Assert.Equal(0, client.CallCount);

        page.서버권한.권한적용(["common.warehouse-operations:create-inbound"]);

        Assert.True(await page.전환Runtime.실행Async());
        Assert.Equal(HttpMethod.Post, client.LastMethod);
        Assert.Equal("api/v1/warehouse-operations/inbounds", client.LastPath);
        Assert.Equal("{\"warehouseId\":\"warehouse-1\"}", client.LastRequestJson);
    }

    [Fact]
    public async Task 버전메타데이터가_연결된_워크플로우를_비활성으로_표시한다()
    {
        var client = new RecordingApiClient
        {
            Metadata = new VersionFeatureFlagsResponse
            {
                Workflows =
                [
                    new WorkflowFlagStateDto
                    {
                        WorkflowCode = "warehouse-flow",
                        IsEnabled = false
                    }
                ],
                ApiEndpoints =
                [
                    new WorkflowApiEndpointDto
                    {
                        Method = "POST",
                        RoutePattern = "api/v1/warehouse-operations/inbounds",
                        WorkflowCodes = ["warehouse-flow"]
                    }
                ]
            }
        };
        using var page = CreatePage(client);
        page.초기화(
            BaguaActorRoleCodes.WarehouseManager,
            BaguaTrigramKeys.Zhen,
            BaguaTrigramKeys.Dui);
        page.전환Runtime.원본선택("order-42");
        page.전환Runtime.기능선택("common.warehouse-operations", "create-inbound");
        page.서버권한.권한적용(["create-inbound"]);

        Assert.True(await page.전환Runtime.기능메타데이터조회Async());

        Assert.Equal(Bagua기능가용상태.비활성, page.전환Runtime.기능가용상태);
        Assert.False(page.전환Runtime.실행가능);
        Assert.Equal("현재 버전에서 비활성화된 업무 기능입니다.", page.전환Runtime.실행불가사유);
    }

    [Fact]
    public void 둘_이상의_경로값은_누락된_값을_실행전에_알려준다()
    {
        var client = new RecordingApiClient();
        using var page = CreatePage(client);
        page.초기화(
            BaguaActorRoleCodes.Orderer,
            BaguaTrigramKeys.Zhen,
            BaguaTrigramKeys.Zhen);
        page.전환Runtime.기능선택("common.order-ledgers", "decide-disclosure");
        page.전환Runtime.경로값설정("주문원장Id", "order-42");
        page.서버권한.권한적용(["decide-disclosure"]);

        Assert.False(page.전환Runtime.실행가능);
        Assert.Contains("요청Id", page.전환Runtime.실행불가사유);
    }

    private static BaguaRoleTransitionPageViewModel CreatePage(RecordingApiClient client)
        => new(
            new Bagua업무영역ViewModelFactory(client),
            new DefaultBaguaTargetWorkspaceResolver(),
            client);

    private sealed class RecordingApiClient : IHongdalJsonApiClient
    {
        public VersionFeatureFlagsResponse Metadata { get; init; } = new();
        public int CallCount { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastPath { get; private set; }
        public string? LastRequestJson { get; private set; }

        public Task<TResponse?> GetAsync<TResponse>(
            string path,
            string operationName,
            bool allowNotFound = true,
            CancellationToken cancellationToken = default)
        {
            Record(HttpMethod.Get, path, null);
            if (typeof(TResponse) == typeof(VersionFeatureFlagsResponse))
            {
                return Task.FromResult((TResponse?)(object)Metadata);
            }

            return JsonResult<TResponse>("{\"result\":\"ok\"}");
        }

        public Task<TResponse?> SendAsync<TResponse>(
            HttpMethod method,
            string path,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
        {
            Record(method, path, null);
            return JsonResult<TResponse>("{\"result\":\"ok\"}");
        }

        public Task<TResponse?> SendAsync<TRequest, TResponse>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
        {
            Record(method, path, JsonSerializer.Serialize(request));
            return JsonResult<TResponse>("{\"result\":\"ok\"}");
        }

        public Task SendAsync(
            HttpMethod method,
            string path,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            Record(method, path, null);
            return Task.CompletedTask;
        }

        public Task SendAsync<TRequest>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            Record(method, path, JsonSerializer.Serialize(request));
            return Task.CompletedTask;
        }

        private void Record(HttpMethod method, string path, string? requestJson)
        {
            CallCount++;
            LastMethod = method;
            LastPath = path;
            LastRequestJson = requestJson;
        }

        private static Task<TResponse?> JsonResult<TResponse>(string json)
            => Task.FromResult(JsonSerializer.Deserialize<TResponse>(json));
    }
}
