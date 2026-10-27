using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoConditionCardRenderingTests
{
    [Fact]
    public void 한국시각은_날짜경계와_부분누락을_보존하고_협의로_대체하지_않는다()
    {
        Assert.Equal("2026-12-31 23:30 ~ 2027-01-01 00:30 (한국)",
            화물운송조건표시.시간창(Utc(2026, 12, 31, 14, 30), Utc(2026, 12, 31, 15, 30)));
        Assert.Equal("2026-10-24 12:00 ~ 13:00 (한국)",
            화물운송조건표시.시간창(Utc(2026, 10, 24, 3), Utc(2026, 10, 24, 4)));
        Assert.Equal("2026-10-24 12:00 ~ 종료 미확인 (한국)",
            화물운송조건표시.시간창(Utc(2026, 10, 24, 3), null));
        Assert.Equal("시작 미확인 ~ 2026-10-24 13:00 (한국)",
            화물운송조건표시.시간창(null, Utc(2026, 10, 24, 4)));
        Assert.Equal("미확인", 화물운송조건표시.시간창(default(DateTime), null));
        Assert.Equal("미확인", 화물운송조건표시.시간창(null, null));
        Assert.StartsWith("시간 범위 확인 필요", 화물운송조건표시.시간창(Utc(2026, 10, 24, 4), Utc(2026, 10, 24, 3)));
        Assert.Equal(화물운송조건표시.시간창(Utc(2026, 10, 24, 3), Utc(2026, 10, 24, 4)),
            화물운송조건표시.시간창(DateTime.SpecifyKind(Utc(2026, 10, 24, 3), DateTimeKind.Unspecified),
                DateTime.SpecifyKind(Utc(2026, 10, 24, 4), DateTimeKind.Unspecified)));
    }

    [Fact]
    public void 운송방식은_표시만_독차로_바꾸고_누락_규격을_0이나_협의로_채우지_않는다()
    {
        var source = Offer("a", "단독");
        var item = CargoDriverWorkspaceAdapter.MapOffer(source);
        Assert.Equal("단독", source.운송방식);
        Assert.Contains(item.ComparisonFields!, x => x.Label == "운송 방식" && x.Value == "독차");
        Assert.Equal("미확인", 화물운송조건표시.운송방식(null));
        Assert.Equal("협의", 화물운송조건표시.운송방식("협의"));
        Assert.Equal("미확인", 화물운송조건표시.차량종류(" "));
        Assert.Equal("미확인", 화물운송조건표시.중량(0));
        Assert.Equal("미확인", 화물운송조건표시.중량(null));
        Assert.Equal("미확인", 화물운송조건표시.수량(null));
        Assert.Equal("미확인", 화물운송조건표시.수량(-1));
    }

    [Fact]
    public async Task 추천목록은_각화물의_운송방식과_상하차시간을_선택전에도_비교한다()
    {
        var a = CargoDriverWorkspaceAdapter.MapOffer(Offer("a", "단독"));
        var b = CargoDriverWorkspaceAdapter.MapOffer(Offer("b", "혼적"));
        var html = await RenderWorkspace("cargo-driver", [a, b]);
        Assert.Equal(2, Regex.Matches(html, "cargo-conditions--compact").Count);
        Assert.Contains("독차", html);
        Assert.Contains("혼적", html);
        Assert.Contains("1톤 카고", html);
        Assert.Contains("150 kg", html);
        Assert.Contains("12개", html);
        Assert.Contains("2026-10-24 23:00 ~ 2026-10-25 00:00 (한국)", html);
        Assert.Contains("2026-10-25 00:30 ~ 02:00 (한국)", html);
        Assert.Contains("추천 조건 확인", html);
    }

    [Fact]
    public async Task 음식목록과_미인증화물에는_조건비교행을_노출하지_않는다()
    {
        var item = CargoDriverWorkspaceAdapter.MapOffer(Offer("a", "단독"));
        Assert.DoesNotContain("운송 조건 비교", await RenderWorkspace("food-driver", [item]));
        var guest = await RenderWorkspace("cargo-driver", [item], authenticated: false);
        Assert.DoesNotContain("운송 조건 비교", guest);
        Assert.DoesNotContain("1톤 카고", guest);
        Assert.Contains("로그인", guest);
    }

    [Fact]
    public void 운송정보_제공보류는_조건과_시간창도_제외한다()
    {
        var item = CargoDriverWorkspaceAdapter.Map(new 기사운송상세응답
        {
            Id = 12, 개인정보제공보류 = true, 운송방식 = "단독", 차량종류 = "1톤 카고",
            상차시간창시작일시 = Utc(2026, 10, 24, 14), 화물중량Kg = 150, 화물수량 = 12
        });
        Assert.Null(item.ComparisonFields);
        Assert.Null(item.Summary);
    }

    [Fact]
    public void 화주요약은_기존원장의_물량과_상하차시간을_그대로_이어받는다()
    {
        var source = new 화주운송의뢰응답
        {
            의뢰Id = "shipper-1", 운송방식 = "단독", 차량종류 = "1톤 카고",
            화물 = new() { 중량Kg = 150, 수량 = 12 },
            픽업 = new() { 시간창 = new() { 시작일시 = Utc(2026, 10, 24, 14), 종료일시 = Utc(2026, 10, 24, 15) } },
            하차 = new() { 시간창 = new() { 시작일시 = Utc(2026, 10, 24, 15, 30), 종료일시 = Utc(2026, 10, 24, 17) } }
        };
        var snapshot = ShipperRequestDetailSnapshot.FromContract(source);
        var item = ShipperWorkspaceAdapter.Map(source);
        Assert.Equal("단독", snapshot.TransportMethod);
        Assert.Equal(150m, snapshot.화물중량Kg);
        Assert.Equal(12, snapshot.화물수량);
        Assert.Equal(source.픽업.시간창.시작일시, snapshot.상차시간창시작일시);
        Assert.Equal(source.하차.시간창.종료일시, snapshot.하차시간창종료일시);
        Assert.Contains(item.ComparisonFields!, x => x.Label == "운송 방식" && x.Value == "독차");
    }

    [Fact]
    public async Task 참여확인화면은_동의하기전에_화물조건을_보이고_조회만으로_수락하지_않는다()
    {
        var api = new Api();
        var html = await RenderOffer(api);
        Assert.Contains("독차", html);
        Assert.Contains("2026-10-24 23:00 ~ 2026-10-25 00:00 (한국)", html);
        Assert.True(html.IndexOf("운송 조건 비교", StringComparison.Ordinal)
            < html.IndexOf("차량·화물·일정 조건과 주의사항을 확인했습니다", StringComparison.Ordinal));
        Assert.Contains("의뢰 결제 예정액", html);
        Assert.Contains("입금 완료 금액은 아닙니다", html);
        Assert.Equal(0, api.Writes);
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("window")]
    [InlineData("missing")]
    public async Task 첫조회부터_추천과의뢰조건이_다르면_동의와_수락을_막고_설명한다(string conflict)
    {
        var api = new Api { InitialConflict = conflict };
        var html = await RenderOffer(api);
        Assert.Contains("추천과 운송 의뢰의 조건이 서로 다릅니다", html);
        using var model = new CargoOfferActionViewModel(new(api), new Access(true));
        await model.LoadAsync("a");
        Assert.False(model.운송조건일치);
        model.ConditionsConfirmed = model.AcceptConfirmed = true;
        Assert.False(model.CanSubmit);
        await model.SubmitAsync();
        Assert.Equal(0, api.Writes);
    }

    [Fact]
    public async Task 양쪽_구판응답의_누락은_같은미확인으로_남기며_기존_명시동의를_보존한다()
    {
        var api = new Api { LegacyFields = true };
        using var model = new CargoOfferActionViewModel(new(api), new Access(true));
        await model.LoadAsync("a");
        Assert.True(model.운송조건일치);
        Assert.False(model.CanSubmit);
        Assert.All(CargoDriverWorkspaceAdapter.MapOffer(model.Offer!).ComparisonFields!, field => Assert.Equal("미확인", field.Value));
        model.ConditionsConfirmed = model.AcceptConfirmed = true;
        Assert.True(model.CanSubmit);
        Assert.Equal(0, api.Writes);
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("vehicle")]
    [InlineData("weight")]
    [InlineData("quantity")]
    [InlineData("pickup-start")]
    [InlineData("pickup-end")]
    [InlineData("dropoff-start")]
    [InlineData("dropoff-end")]
    [InlineData("source-window")]
    public async Task 확인후_조건이바뀌면_다시확인하기전_수락을_전송하지_않는다(string mutation)
    {
        var api = new Api();
        using var model = new CargoOfferActionViewModel(new(api), new Access(true));
        await model.LoadAsync("a");
        model.ConditionsConfirmed = model.AcceptConfirmed = true;
        Assert.True(model.운송조건일치);
        Assert.True(model.CanSubmit);
        api.Mutation = mutation;
        await model.SubmitAsync();
        Assert.True(model.RecommendationChanged);
        Assert.False(model.ConditionsConfirmed);
        Assert.False(model.AcceptConfirmed);
        Assert.Equal(0, api.Writes);
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0)
        => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static 기사배차추천항목응답 Offer(string id, string mode) => new()
    {
        의뢰Id = id, 화물종류 = "검토 물품 " + id, 운송방식 = mode, 차량종류 = "1톤 카고",
        화물중량Kg = 150, 화물수량 = 12, 차량적합여부 = true,
        상차시간창시작일시 = Utc(2026, 10, 24, 14), 상차시간창종료일시 = Utc(2026, 10, 24, 15),
        하차시간창시작일시 = Utc(2026, 10, 24, 15, 30), 하차시간창종료일시 = Utc(2026, 10, 24, 17),
        기사지급예정운임 = 6200, 기사지급예정운임근거 = "운임구성.기사지급예정운임",
        추천만료시각 = DateTime.UtcNow.AddHours(1), 픽업지 = "상차지역", 하차지 = "하차지역"
    };

    private static async Task<string> RenderOffer(Api api)
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, Js>(); services.AddSingleton<NavigationManager, Navigation>();
        services.AddSingleton<IRoleWorkspaceAccess>(new Access(true));
        services.AddSingleton(new CargoDriverWorkspaceApiClient(api));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var output = await renderer.RenderComponentAsync<CargoOfferActionScreen>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(CargoOfferActionScreen.ItemId)] = "a" }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private static async Task<string> RenderWorkspace(string role, IReadOnlyList<RoleWorkspaceItem> items, bool authenticated = true)
    {
        using var model = new RoleWorkspaceViewModel([new Adapter(role, items)], new Access(authenticated), new RoleWorkspaceState());
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices();
        services.AddSingleton(model); services.AddSingleton<NavigationManager, Navigation>();
        services.AddSingleton<INeighborhoodMapHost, MapHost>(); services.AddSingleton<IJSRuntime, Js>();
        services.AddTransient(_ => new 공개화물목록ViewModel(new EmptyPublicCargo()));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode(
            (await renderer.RenderComponentAsync<RoleMapWorkspace>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(RoleMapWorkspace.RoleKey)] = role }))).ToHtmlString()));
    }

    private sealed class Adapter(string role, IReadOnlyList<RoleWorkspaceItem> items) : IRoleWorkspaceAdapter
    {
        public string RoleKey => role;
        public Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken ct) => Task.FromResult(new RoleWorkspaceSnapshot(role, items, SelectedId: items[0].Id));
        public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken ct) => throw new InvalidOperationException();
        public void Clear() { }
    }
    private sealed class Api : IRoleWorkspaceApi
    {
        private readonly DateTime 만료시각 = DateTime.UtcNow.AddHours(1);
        public int Writes { get; private set; }
        public string? Mutation { get; set; }
        public string? InitialConflict { get; set; }
        public bool LegacyFields { get; set; }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
        {
            var offer = Offer("a", "단독");
            offer.추천만료시각 = 만료시각;
            if (LegacyFields)
            {
                offer.운송방식 = offer.차량종류 = null;
                offer.화물중량Kg = null; offer.화물수량 = null;
                offer.상차시간창시작일시 = offer.상차시간창종료일시 = offer.하차시간창시작일시 = offer.하차시간창종료일시 = null;
            }
            switch (Mutation)
            {
                case "mode": offer.운송방식 = "혼적"; break;
                case "vehicle": offer.차량종류 = "2.5톤 카고"; break;
                case "weight": offer.화물중량Kg = 300; break;
                case "quantity": offer.화물수량 = 24; break;
                case "pickup-start": offer.상차시간창시작일시 = offer.상차시간창시작일시!.Value.AddMinutes(10); break;
                case "pickup-end": offer.상차시간창종료일시 = offer.상차시간창종료일시!.Value.AddMinutes(10); break;
                case "dropoff-start": offer.하차시간창시작일시 = offer.하차시간창시작일시!.Value.AddMinutes(10); break;
                case "dropoff-end": offer.하차시간창종료일시 = offer.하차시간창종료일시!.Value.AddMinutes(10); break;
            }
            object value = path.EndsWith("/recommendations", StringComparison.Ordinal)
                ? (IReadOnlyList<기사배차추천항목응답>)[offer]
                : new 기사운송의뢰상세응답
                {
                    의뢰Id = "a", 화물종류 = "검토 물품 a", 운송방식 = LegacyFields ? null : InitialConflict == "mode" ? "혼적" : InitialConflict == "missing" ? null : "단독", 차량종류 = LegacyFields ? null : "1톤 카고", 화물중량Kg = LegacyFields ? null : 150, 화물수량 = LegacyFields ? null : 12,
                    차량적합여부 = true, 배차상태 = "대기", 결제예정금액 = 18000,
                    상차시간창시작일시 = LegacyFields ? null : Mutation == "source-window" || InitialConflict == "window" ? Utc(2026, 10, 24, 14, 10) : Utc(2026, 10, 24, 14),
                    상차시간창종료일시 = LegacyFields ? null : Utc(2026, 10, 24, 15), 하차시간창시작일시 = LegacyFields ? null : Utc(2026, 10, 24, 15, 30), 하차시간창종료일시 = LegacyFields ? null : Utc(2026, 10, 24, 17)
                };
            return Task.FromResult((T)value);
        }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken ct)
        { Writes++; throw new InvalidOperationException("표시와 재확인은 수락을 자동 전송하지 않습니다."); }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken ct) => PostAsync<T>(roleKey, path, body, ct);
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken ct) => PostAsync<T>(roleKey, path, body, ct);
    }
    private sealed class Access(bool authenticated) : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new(authenticated ? "driver-a" : null, 1, authenticated);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SignOutAsync(string roleKey, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/workspace/cargo-driver");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class Js : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class MapHost : INeighborhoodMapHost
    {
        public Task<NeighborhoodMapHostStatus> RenderAsync(string id, NeighborhoodMapRenderState state, Func<string, Task> selected, CancellationToken ct = default)
            => Task.FromResult(new NeighborhoodMapHostStatus("ready"));
        public Task HideAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class EmptyPublicCargo : I공개화물Client
    {
        public Task<IReadOnlyList<공개화물요약응답>> 조회Async(int page, int pageSize, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<공개화물요약응답>>([]);
    }
}
