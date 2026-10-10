using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Restaurants;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleOptionScopeR3Tests
{
    [Fact]
    public async Task 조회반경안내는_기존조회필터를보존하고_조회만으로주문조건을변경하지않는다()
    {
        var service = new 탐색Fixture();
        var criteria = new 음식점탐색기준ViewModel(service, service);
        Assert.True(await criteria.준비Async());
        criteria.선택배달권키 = "area-a";
        criteria.빠른반경설정(5);
        var list = new 음식점공개목록ViewModel(service) { 검색어 = "분식", 주문가능만 = true };

        var html = await RenderAsync<OrdererRestaurantSearchPanel>(new()
        {
            [nameof(OrdererRestaurantSearchPanel.Criteria)] = criteria,
            [nameof(OrdererRestaurantSearchPanel.List)] = list
        });

        Assert.Contains("조회 범위이며 주문 가능 지역이나 배달료를 변경하지 않습니다.", html);
        Assert.Contains("공개 행정권역", html);
        Assert.Contains("주문 가능한 곳만", html);
        Assert.Contains("음식점 조회", html);
        Assert.Equal(5d, criteria.반경Km);
        Assert.Equal("분식", list.검색어);
        Assert.True(list.주문가능만);
        Assert.Empty(service.ListRequests);
        Assert.True(await list.조회Async(criteria.선택배달권키, criteria.반경Km));
        var request = Assert.Single(service.ListRequests);
        Assert.Equal("area-a", request.배달권키);
        Assert.Equal(5m, request.반경Km);
        Assert.Equal("분식", request.검색어);
        Assert.True(request.주문가능만);
    }

    [Theory]
    [InlineData("restaurant", 음식배달가능행동Ids.음식점주문수락, "이번 주문 조리 예상 시간(분)")]
    [InlineData("restaurant", 음식배달가능행동Ids.음식점조리시간변경, "이번 주문 조리 예상 시간(분)")]
    [InlineData("food-driver", 음식배달가능행동Ids.기사배달중단, "이번 배달 중단 사유")]
    [InlineData("operator", OperatorRoleWorkspaceAdapter.ReviewInterruption, "이번 중단 건 책임 판정")]
    public async Task 건별옵션안내는_해당역할의허용입력에만보이고_렌더는업무를실행하지않는다(
        string role, string action, string label)
    {
        var api = new 역할Fixture(action);
        var html = await RenderAsync<FoodRoleActionForm>(new()
        {
            [nameof(FoodRoleActionForm.RoleKey)] = role,
            [nameof(FoodRoleActionForm.ItemId)] = "work-a",
            [nameof(FoodRoleActionForm.ActionKey)] = action
        }, services =>
        {
            services.AddSingleton<IRoleWorkspaceApi>(api);
            services.AddSingleton<IRoleWorkspaceAccess>(new 접근Fixture());
        });

        Assert.Contains(label, html);
        Assert.Contains("요청 내용 확인", html);
        Assert.Contains("업무로 돌아가기", html);
        Assert.Single(api.Reads);
        Assert.Equal(0, api.Writes);
        if (role == "restaurant")
        {
            Assert.Contains("이 시간은 이번 주문에 적용합니다. 가게나 기기의 기본 조리시간은 바뀌지 않습니다.", html);
            Assert.DoesNotContain("이번 배달 중단 사유", html);
            Assert.DoesNotContain("이번 중단 건 책임 판정", html);
        }
        else
        {
            Assert.DoesNotContain("이번 주문 조리 예상 시간(분)", html);
            Assert.DoesNotContain(role == "food-driver" ? "이번 중단 건 책임 판정" : "이번 배달 중단 사유", html);
        }
    }

    [Fact]
    public async Task 현재허용행동이없으면_건별조리입력과확인버튼도열리지않는다()
    {
        var api = new 역할Fixture(음식배달가능행동Ids.음식점조리시간변경, allowed: false);
        var html = await RenderAsync<FoodRoleActionForm>(new()
        {
            [nameof(FoodRoleActionForm.RoleKey)] = "restaurant",
            [nameof(FoodRoleActionForm.ItemId)] = "work-a",
            [nameof(FoodRoleActionForm.ActionKey)] = 음식배달가능행동Ids.음식점조리시간변경
        }, services =>
        {
            services.AddSingleton<IRoleWorkspaceApi>(api);
            services.AddSingleton<IRoleWorkspaceAccess>(new 접근Fixture());
        });

        Assert.Contains("현재 요청할 수 없는 행동입니다.", html);
        Assert.DoesNotContain("이번 주문 조리 예상 시간(분)", html);
        Assert.DoesNotContain("요청 내용 확인", html);
        Assert.Single(api.Reads);
        Assert.Equal(0, api.Writes);
    }

    [Theory]
    [InlineData("Components/Transport/ShipperRequestProcedureScreen.razor", "이번 의뢰의 금액·추가 작업 조건",
        "예정액은 이번 의뢰의 조건이며, 확정 기사 대가나 결제·실지급 완료를 뜻하지 않습니다.",
        "@bind-Value=\"ViewModel.결제예정금액\"", "@bind-Value=\"ViewModel.수작업비\"", "OnClick=\"EstimateFareAsync\"")]
    [InlineData("RoleWorkspace/Cargo/CargoOfferActionScreen.razor", "<dt>의뢰 결제 예정액</dt>",
        "의뢰 결제 예정액은 기사 실지급액을 뜻하지 않습니다. 참여 확인은 이번 추천 운송 한 건에 적용됩니다.",
        "CargoWorkspacePresentation.Money(source.결제예정금액)", "@bind-Value=\"Model.AcceptConfirmed\"", "Disabled=\"@(!Model.CanSubmit)\"")]
    [InlineData("RoleWorkspace/Cargo/WarehouseTaskActionScreen.razor", "<h3>이번 작업 확인</h3>",
        "아래 입력은 이번 작업에만 적용됩니다. 개인 기본 설정이나 계약 단가를 변경하지 않습니다.",
        "@bind-Value=\"Model.Quantity\"", "@bind-Value=\"Model.SaveConfirmed\"", "Disabled=\"@(!Model.CanSubmit)\"")]
    public void 화물과창고의범위안내는_기존금액수량확인바인딩을유지한다(
        string path, string heading, string notice, string valueBinding, string confirmationBinding, string guard)
    {
        var source = Read(path);
        Assert.Contains(heading, source);
        Assert.Contains(notice, source);
        Assert.Contains(valueBinding, source);
        Assert.Contains(confirmationBinding, source);
        Assert.Contains(guard, source);
        if (path.StartsWith("RoleWorkspace/", StringComparison.Ordinal))
        {
            Assert.Contains("OnClick=\"Model.SubmitAsync\"", source);
            Assert.Contains("OnClick=\"Reload\"", source);
        }
    }

    private static async Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters,
        Action<IServiceCollection>? configure = null) where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, 정적JsFixture>();
        services.AddSingleton<NavigationManager, NavigationFixture>();
        configure?.Invoke(services);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var output = await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private static string Read(string path)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
                return File.ReadAllText(Path.Combine(directory.FullName, "Ssalddel.Ui.Common", "Areas", "App", path));
        throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾지 못했습니다.");
    }

    private sealed class 탐색Fixture : I음식점탐색정책읽기Service, I음식점공개읽기Service
    {
        public List<음식점공개목록조회요청> ListRequests { get; } = [];
        public Task<RestaurantSearchPolicyDto> 조회Async(CancellationToken cancellationToken = default)
            => Task.FromResult(new RestaurantSearchPolicyDto());
        public Task<IReadOnlyList<음식점탐색권역응답>> 권역목록Async(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<음식점탐색권역응답>>([new() { 배달권키 = "area-a", 표시명 = "권역 1", 기준지명 = "공개 기준점 1" }]);
        public Task<음식점공개목록응답> 목록Async(음식점공개목록조회요청 request, CancellationToken cancellationToken = default)
        { ListRequests.Add(request); return Task.FromResult(new 음식점공개목록응답()); }
        public Task<음식점공개상세응답?> 상세Async(long restaurantId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("탐색 안내 렌더는 개별 음식점을 조회하지 않습니다.");
    }

    private sealed class 역할Fixture(string action, bool allowed = true) : IRoleWorkspaceApi
    {
        public List<string> Reads { get; } = [];
        public int Writes { get; private set; }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        {
            Reads.Add(path);
            object response = roleKey switch
            {
                "restaurant" => new 음식주문응답 { 주문번호 = "work-a", 음식점명 = "음식점 1", 음식점주소 = "픽업 장소 1", 조리예상분 = 15,
                    AvailableActions = allowed ? [new() { ActionId = action, ExpectedRevision = 3 }] : [] },
                "food-driver" => new FoodDeliveryDriverCurrentWorkDto { DriverId = "owner-a", ActiveDeliveries = [new() { OfferId = "work-a",
                    DeliveryAttemptId = "attempt-a", AvailableActions = [new() { ActionId = action, ExpectedRevision = 3 }] }] },
                "operator" => new 음식주문운영추적응답 { 주문번호 = "work-a", 배달시도목록 = [new() { 시도StableId = "attempt-a", Revision = 3,
                    상태Code = 음식배달시도상태Code.중단 }] },
                _ => throw new InvalidOperationException("지원하지 않는 표시 검증 역할입니다.")
            };
            return Task.FromResult((T)response);
        }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        { Writes++; throw new InvalidOperationException("옵션 안내 렌더는 업무 명령을 실행하지 않습니다."); }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
            => PostAsync<T>(roleKey, path, body, cancellationToken);
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class 접근Fixture : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new("owner-a", 1, true);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class 정적JsFixture : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class NavigationFixture : NavigationManager
    {
        public NavigationFixture() => Initialize("http://localhost/", "http://localhost/workspace/orderer");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
