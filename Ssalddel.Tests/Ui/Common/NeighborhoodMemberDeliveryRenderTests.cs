using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Ui.Common.Areas.App.Components.Community.Exchange;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodMemberDeliveryRenderTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 원의뢰전체화면에서_회원배송과_기사선택정보를_섞지않는다(bool memberDelivery)
    {
        var fixture = new NeighborhoodMemberDeliveryUiTests.Fixture();
        var sources = new SourceClient(memberDelivery);
        var services = new ServiceCollection(); services.AddLogging(); services.AddCommerceUiFixture();
        services.AddSingleton<NavigationManager, Nav>();
        services.AddSingleton<ISsalddel현재사용자Context>(fixture);
        services.AddSingleton<INeighborhoodDeliveryClient>(sources);
        services.AddSingleton<NeighborhoodDeliveryQueryViewModel>();
        services.AddTransient(_ => new NeighborhoodMemberRequestViewModel(fixture, sources, fixture, fixture.Session));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode((await renderer.RenderComponentAsync<NeighborhoodDeliveryDetail>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(NeighborhoodDeliveryDetail.RequestId)] = "source" }))).ToHtmlString()));
        Assert.Contains("물품 배송", html);
        if (memberDelivery)
        {
            Assert.Contains("생활 회원 배송", html); Assert.Contains("픽업 이동 중", html); Assert.Contains("픽업 전 배송 취소", html);
            Assert.DoesNotContain("기사를 찾는 방식", html); Assert.DoesNotContain("배차 방식 변경", html);
            Assert.DoesNotContain("기사에게 배송 정보 제공", html); Assert.DoesNotContain("기사 정보 제공에 동의하기", html);
            Assert.DoesNotContain("선정 기사", html); Assert.DoesNotContain("기존 기사", html);
        }
        else
        {
            Assert.Contains("기사를 찾는 방식", html); Assert.Contains("배차 방식 변경", html); Assert.Contains("기사에게 배송 정보 제공", html);
            Assert.DoesNotContain("픽업 전 배송 취소", html);
        }
    }
    [Fact]
    public async Task 참여설정은_좌표대신_선택상태와_기본OFF를_표시한다()
    {
        var f = new NeighborhoodMemberDeliveryUiTests.Fixture(); f.Settings.Latitude = 37.5812345m; f.Settings.Longitude = 127.0812345m;
        var html = await Render<NeighborhoodMemberParticipation>(f);
        Assert.Contains("배송 제안 받기", html); Assert.Contains("활동 기준점이 선택", html);
        Assert.DoesNotContain("37.5812345", html); Assert.DoesNotContain("127.0812345", html);
        Assert.DoesNotContain("checked", html); Assert.DoesNotContain("기사 등록", html); Assert.DoesNotContain("settings-put", f.Calls);
    }
    [Fact]
    public async Task 제안은_지역_거리_배송비만보이고_상세주소는_수락후에만_나온다()
    {
        var f = new NeighborhoodMemberDeliveryUiTests.Fixture();
        var html = await Render<NeighborhoodMemberOfferDetail>(f, new() { [nameof(NeighborhoodMemberOfferDetail.OfferId)] = "offer" });
        Assert.Contains("픽업 지역", html); Assert.Contains("전달 지역", html); Assert.Contains("4,000", html); Assert.Contains("2", html);
        Assert.Contains("플랫폼 이용료 0원", html); Assert.Contains("배차 수수료 0원", html);
        Assert.DoesNotContain("010-0000-0000", html); Assert.DoesNotContain("비공개 상세", html); Assert.Contains("현재 위치 확인하고 수락", html);
    }
    [Fact]
    public async Task 만료제안은_수락버튼대신_상태확인만_보인다()
    {
        var f = new NeighborhoodMemberDeliveryUiTests.Fixture(); f.Offer.CanAccept = false; f.Offer.StateCode = "Expired";
        var html = await Render<NeighborhoodMemberOfferDetail>(f, new() { [nameof(NeighborhoodMemberOfferDetail.OfferId)] = "offer" });
        Assert.Contains("기한 만료", html); Assert.DoesNotContain("현재 위치 확인하고 수락", html); Assert.Contains("최신 상태 확인", html);
    }
    [Theory]
    [InlineData("Assigned", "픽업 장소", "사진 보내고 픽업 완료")]
    [InlineData("PickedUp", "전달 장소", "사진 보내고 전달 완료")]
    public async Task 수행카드는_현재상태의장소와_사진완료만_주행동으로보인다(string state, string place, string primary)
    {
        var f = new NeighborhoodMemberDeliveryUiTests.Fixture(); f.CurrentWork.StateCode = state;
        f.CurrentWork.CanPickup = state == "Assigned"; f.CurrentWork.CanDeliver = state == "PickedUp";
        var html = await Render<NeighborhoodMemberWork>(f, new() { [nameof(NeighborhoodMemberWork.RequestId)] = "work" });
        Assert.Contains("member-delivery-map__canvas", html); Assert.Matches("<summary[^>]*>" + place, html);
        Assert.Contains(primary, html); Assert.Matches("<summary[^>]*>요청 사항", html); Assert.Matches("<summary[^>]*>물품·배송비", html);
        Assert.DoesNotContain("다른 배달", html); Assert.DoesNotContain("사진 보내고 " + (state == "Assigned" ? "전달" : "픽업") + " 완료", html);
    }
    [Fact]
    public async Task 전달완료는_상세주소를숨기고_수령과지급확인이_별도임을_설명한다()
    {
        var f = new NeighborhoodMemberDeliveryUiTests.Fixture(); f.CurrentWork.StateCode = "Delivered";
        f.CurrentWork.CanPickup = f.CurrentWork.CanDeliver = false; f.CurrentWork.Pickup = f.CurrentWork.Dropoff = null;
        var html = await Render<NeighborhoodMemberWork>(f, new() { [nameof(NeighborhoodMemberWork.RequestId)] = "work" });
        Assert.Contains("수령자의 확인", html); Assert.Contains("배송비 지급 확인은 전달 완료와 별도", html);
        Assert.DoesNotContain("010-0000-0000", html); Assert.DoesNotContain("사진 촬영", html);
    }
    [Fact]
    public async Task 로그인화면에는_업무정보와_주소카드를_동시에렌더하지않는다()
    {
        var f = new NeighborhoodMemberDeliveryUiTests.Fixture { UserId = null };
        var html = await Render<NeighborhoodMemberWork>(f, new() { [nameof(NeighborhoodMemberWork.RequestId)] = "work" });
        Assert.Contains("로그인이 필요", html); Assert.DoesNotContain("비공개 상세", html); Assert.DoesNotContain("책 한 상자", html); Assert.Empty(f.Calls);
    }
    private static async Task<string> Render<T>(NeighborhoodMemberDeliveryUiTests.Fixture fixture, Dictionary<string, object?>? parameters = null) where T : IComponent
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<NavigationManager, Nav>();
        services.AddSingleton<INeighborhoodMapHost, UnsupportedNeighborhoodMapHost>();
        services.AddSingleton(fixture.Participation()); services.AddSingleton(fixture.Offers()); services.AddSingleton(fixture.Work());
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode((await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters ?? []))).ToHtmlString()));
    }
    private sealed class Nav : NavigationManager
    {
        public Nav() => Initialize("http://localhost/roles/01/", "http://localhost/roles/01/community/exchange/deliveries/offers");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class SourceClient(bool memberDelivery) : INeighborhoodDeliveryClient
    {
        public Task<NeighborhoodDeliveryResponse?> ReadAsync(string id, CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryResponse?>(new()
        {
            RequestId = id, CargoName = "물품 배송", MemberDeliveryRequested = memberDelivery, MemberWorkStateCode = "Assigned", MemberWorkRevision = 1, CanCancelMemberDelivery = true,
            DispatchMode = NeighborhoodDispatchModes.PublicCall, CanChangeDispatchMode = true, FareKrw = 4000,
            DriverDisclosure = new() { ConfirmedDriverId = "legacy-driver", ConfirmedDriverName = "기존 기사", CanRecordConsent = true, Fields = ["PickupAddress"] }
        });
        public Task<Ssalddel.Contracts.Common.Privacy.신청개인정보동의증적Response?> ConsentAsync(Ssalddel.Contracts.Common.Privacy.신청개인정보동의기록Request request, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct) => throw new NotSupportedException();
    }
}
