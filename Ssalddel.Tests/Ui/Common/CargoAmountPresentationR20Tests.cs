using System.Globalization;
using System.Net;
using DriverApp.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Transport;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoAmountPresentationR20Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 전용기사의_요약과상세는_고객총운임과_동결지급예정액을_구별한다(bool detail)
    {
        var source = Transport(detail);
        source.기사지급예정액 = 9000m;
        source.기사지급예정액근거 = "운송원장.기사지급예정액";

        var display = 기사운송표시Mapper.Map(source);

        Assert.Equal(12000m, display.고객총운임);
        Assert.Equal("12,000원", display.고객총운임표시);
        Assert.Equal(9000m, display.예상수익);
        Assert.Equal("9,000원", display.운임표시);
        source.운임 = 18000m;
        Assert.Equal(9000m, 기사운송표시Mapper.Map(source).예상수익);
        Assert.Equal(18000m, 기사운송표시Mapper.Map(source).고객총운임);
    }

    [Theory]
    [InlineData(false, null, "운송원장.기사지급예정액")]
    [InlineData(true, null, "운송원장.기사지급예정액")]
    [InlineData(false, "0", "운송원장.기사지급예정액")]
    [InlineData(true, "0", "운송원장.기사지급예정액")]
    [InlineData(false, "-1", "운송원장.기사지급예정액")]
    [InlineData(true, "-1", "운송원장.기사지급예정액")]
    [InlineData(false, "9000.5", "운송원장.기사지급예정액")]
    [InlineData(true, "9000.5", "운송원장.기사지급예정액")]
    [InlineData(false, "9000", null)]
    [InlineData(true, "9000", null)]
    [InlineData(false, "9000", "운임구성.기사지급예정운임")]
    [InlineData(true, "9000", "운임구성.기사지급예정운임")]
    public void 동결액이나근거가없거나유효하지않으면_고객총운임으로대체하지않는다(
        bool detail, string? rawAmount, string? evidence)
    {
        var source = Transport(detail);
        source.기사지급예정액 = rawAmount is null ? null : decimal.Parse(rawAmount, CultureInfo.InvariantCulture);
        source.기사지급예정액근거 = evidence;

        var display = 기사운송표시Mapper.Map(source);

        Assert.Null(display.예상수익);
        Assert.Equal("미확인", display.운임표시);
        Assert.Equal(12000m, display.고객총운임);
    }

    [Theory]
    [InlineData("미시작")]
    [InlineData("매칭중")]
    [InlineData("추천")]
    public void 수락전_화주상세는_고객총운임과_약정기사액을_구별한다(string stage)
    {
        var source = Request(stage);
        var snapshot = ShipperRequestDetailSnapshot.FromContract(source);

        Assert.Equal(12000m, snapshot.FinalFare);
        Assert.Equal(9000m, snapshot.AgreedDriverFare);
        Assert.Null(snapshot.DriverFare);
        Assert.Equal("기사 약정액 9,000원", ShipperRequestDetailPresentation.ResolveDriverPaymentSummary(snapshot));
        Assert.Equal(12000m, source.최종운임);
        Assert.Equal(9000m, source.요금옵션!.기사지급예정운임);
    }

    [Theory]
    [InlineData("배차확정")]
    [InlineData("상차완료")]
    [InlineData("인수완료")]
    [InlineData("매칭중")]
    public void 수락후_화주응답에없는_동결기사액은_고객총운임이나_이전약정으로채우지않는다(string stage)
    {
        var source = Request(stage);
        source.확정기사Id = "driver-r20";
        var snapshot = ShipperRequestDetailSnapshot.FromContract(source);

        Assert.Equal(9000m, snapshot.AgreedDriverFare);
        Assert.Null(snapshot.DriverFare);
        Assert.Equal("기사 지급 예정액 미확인", ShipperRequestDetailPresentation.ResolveDriverPaymentSummary(snapshot));
        var settlement = ShipperRequestDetailPresentation.BuildTimeline(snapshot).Single(x => x.Title == "정산");
        Assert.DoesNotContain("12,000", settlement.Detail);
        Assert.DoesNotContain("9,000", settlement.Detail);
        Assert.Contains("미확인", settlement.Detail);
    }

    [Fact]
    public void 약정액이없는_구판화주응답은_고객총액만으로_기사액을표시하지않는다()
    {
        var source = Request("미시작");
        source.요금옵션 = null;
        var snapshot = ShipperRequestDetailSnapshot.FromContract(source);

        Assert.Null(snapshot.AgreedDriverFare);
        Assert.Null(snapshot.DriverFare);
        Assert.Equal("기사 약정액 미확인", ShipperRequestDetailPresentation.ResolveDriverPaymentSummary(snapshot));
    }

    [Fact]
    public void 명시된_동결기사액은_수락전약정이나_고객총액과_별도로표시한다()
    {
        var snapshot = ShipperRequestDetailSnapshot.FromContract(Request("운송중")) with
        {
            AssignedDriverId = "driver-r20", AgreedDriverFare = 7000m, DriverFare = 9000m
        };

        Assert.Equal("기사 지급 예정액 9,000원", ShipperRequestDetailPresentation.ResolveDriverPaymentSummary(snapshot));
        Assert.Equal(12000m, snapshot.FinalFare);
        Assert.Equal(7000m, snapshot.AgreedDriverFare);
    }

    [Theory]
    [InlineData(false, "기사 약정액 9,000원")]
    [InlineData(true, "기사 지급 예정액 미확인")]
    public async Task 실제공통진행화면은_고객총액을_기사보수로렌더하지않는다(bool accepted, string expected)
    {
        var source = Request(accepted ? "상차완료" : "미시작");
        if (accepted) source.확정기사Id = "driver-r20";
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoopJs>();
        services.AddSingleton<NavigationManager, Navigation>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var page = await renderer.RenderComponentAsync<ShipperRequestTimelineScreen>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ShipperRequestTimelineScreen.State)] = new ShipperRequestDetailPageState { Request = ShipperRequestDetailSnapshot.FromContract(source) },
                [nameof(ShipperRequestTimelineScreen.Context)] = new ShipperRequestDetailNavigationContext(),
                [nameof(ShipperRequestTimelineScreen.HomeHref)] = "/shipper",
                [nameof(ShipperRequestTimelineScreen.NewRequestHref)] = "/shipper/request"
            }));
            return WebUtility.HtmlDecode(page.ToHtmlString());
        });

        Assert.Contains(expected, html);
        Assert.DoesNotContain("12,000원", html);
        Assert.DoesNotContain("기사 지급 완료", html);
    }

    private static 기사운송요약응답 Transport(bool detail)
    {
        기사운송요약응답 source = detail ? new 기사운송상세응답() : new 기사운송요약응답();
        source.Id = 20; source.운송번호 = "cargo-r20"; source.상태 = "운송중"; source.운임 = 12000m;
        return source;
    }

    private static 화주운송의뢰응답 Request(string stage) => new()
    {
        의뢰Id = "cargo-r20", 의뢰상태 = "생성됨", 배차상태 = stage, 운송상태 = stage,
        결제상태 = "결제대기", 정산상태 = "결제대기", 정산시점 = 정산시점.선결제,
        최종운임 = 12000m, 요금옵션 = new PricingDTO { 기사지급예정운임 = 9000m }
    };

    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/shipper/request/cargo-r20/timeline");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
