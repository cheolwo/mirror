using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Ui.Common.Areas.App.Components.Community.Exchange;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodDeliveryFacilitationPresentationTests
{
    [Fact]
    public void 전달완료와_입금미확인을_별도로_표시한다()
    {
        var value = new NeighborhoodDeliveryResponse
        {
            ProposalStateCode = NeighborhoodDeliveryProposalStates.Assigned,
            DispatchStatusCode = "인수완료",
            PaymentStatusCode = "결제대기",
            SettlementStatusCode = "현장수금예정"
        };

        Assert.Equal("전달 완료", NeighborhoodDeliveryPresentation.Stage(value));
        Assert.Contains("지급 여부", NeighborhoodDeliveryPresentation.다음행동안내(value));
        Assert.Equal("기사에게 직접 현장 지급 예정", NeighborhoodDeliveryPresentation.Collection(value));
        Assert.Equal("플랫폼 입금 미확인", NeighborhoodDeliveryPresentation.입금확인안내(value));
        Assert.Equal("현장수금예정", value.SettlementStatusCode);
    }

    [Theory]
    [InlineData("현장수금완료")]
    [InlineData("정산완료")]
    [InlineData("입금확인완료")]
    public void 실제_수금확인_기록을_미확인으로_덮지않는다(string settlement)
    {
        var value = new NeighborhoodDeliveryResponse { SettlementStatusCode = settlement };
        Assert.Equal("수금 확인 기록 있음", NeighborhoodDeliveryPresentation.Collection(value));
        Assert.Null(NeighborhoodDeliveryPresentation.입금확인안내(value));
    }

    [Theory]
    [InlineData("결제완료")]
    [InlineData("입금확인완료")]
    public void 결제확인_기록에도_입금미확인_문구를_붙이지않는다(string payment)
    {
        var value = new NeighborhoodDeliveryResponse { PaymentStatusCode = payment };
        Assert.Null(NeighborhoodDeliveryPresentation.입금확인안내(value));
    }

    [Fact]
    public void 지급조건_취소를_수금예정이나_입금미확인으로_표시하지않는다()
    {
        var value = new NeighborhoodDeliveryResponse { SettlementStatusCode = "정산취소" };
        Assert.Equal("지급 조건 취소", NeighborhoodDeliveryPresentation.Collection(value));
        Assert.Null(NeighborhoodDeliveryPresentation.입금확인안내(value));
    }

    [Fact]
    public void 추천과_대기는_기사수락이나_배차확정으로_설명하지않는다()
    {
        var value = new NeighborhoodDeliveryResponse { ProposalStateCode = NeighborhoodDeliveryProposalStates.Proposed };
        Assert.Equal("기사에게 제안 중", NeighborhoodDeliveryPresentation.Stage(value));
        Assert.Contains("기사 수락 전", NeighborhoodDeliveryPresentation.다음행동안내(value));
        value.ProposalStateCode = NeighborhoodDeliveryProposalStates.Queued;
        Assert.Contains("아직 기사가 확정되지", NeighborhoodDeliveryPresentation.다음행동안내(value));
        Assert.Contains("새로고침", NeighborhoodDeliveryPresentation.다음행동안내(value));
    }

    [Fact]
    public void 선정기사_동의가_필요한때만_정보제공_행동을_안내한다()
    {
        var value = new NeighborhoodDeliveryResponse
        {
            ProposalStateCode = NeighborhoodDeliveryProposalStates.Assigned,
            DriverDisclosure = new() { ConfirmedDriverId = "driver-1", CanRecordConsent = true }
        };
        Assert.Contains("정보 제공 동의", NeighborhoodDeliveryPresentation.다음행동안내(value));
        value.DriverDisclosure.Consented = true;
        Assert.DoesNotContain("정보 제공 동의", NeighborhoodDeliveryPresentation.다음행동안내(value));
        value.DriverDisclosure.Consented = false;
        value.DriverDisclosure.ConfirmedDriverId = null;
        Assert.DoesNotContain("정보 제공 동의", NeighborhoodDeliveryPresentation.다음행동안내(value));
    }

    [Fact]
    public void 완료나_종료상태에_새_정보제공_동의를_안내하지않는다()
    {
        var value = new NeighborhoodDeliveryResponse
        {
            ProposalStateCode = NeighborhoodDeliveryProposalStates.Assigned,
            DispatchStatusCode = "인수완료",
            DriverDisclosure = new() { ConfirmedDriverId = "driver-1", CanRecordConsent = true }
        };
        Assert.DoesNotContain("정보 제공 동의", NeighborhoodDeliveryPresentation.다음행동안내(value));
        value.DispatchStatusCode = "취소";
        Assert.Contains("종료된 배송 기록", NeighborhoodDeliveryPresentation.다음행동안내(value));
    }

    [Fact]
    public void 미확정_배차변경이_있으면_새선택보다_원선택_결과를_먼저_확인한다()
    {
        var value = new NeighborhoodDeliveryResponse { ProposalStateCode = NeighborhoodDeliveryProposalStates.Queued };
        Assert.Contains("이전에 선택한 배차 방식", NeighborhoodDeliveryPresentation.다음행동안내(value, true));
        Assert.DoesNotContain("새로고침", NeighborhoodDeliveryPresentation.다음행동안내(value, true));
    }

    [Fact]
    public void 알수없는_진행상태는_완료나_허용행동을_추정하지않는다()
    {
        var value = new NeighborhoodDeliveryResponse { ProposalStateCode = "future-state" };
        Assert.Equal("진행 상태 확인 필요", NeighborhoodDeliveryPresentation.Stage(value));
        Assert.Equal("진행을 새로고침해 현재 배송 상태를 확인해 주세요.", NeighborhoodDeliveryPresentation.다음행동안내(value));
    }

    [Fact]
    public void 공개콜_단독은_자동추천_비활성_경고를_붙이지않는다()
    {
        Assert.Null(NeighborhoodDeliveryPresentation.자동추천준비안내(NeighborhoodDispatchModes.PublicCall, false));
        Assert.Contains("공개 콜", NeighborhoodDeliveryPresentation.배차선택안내(NeighborhoodDispatchModes.PublicCall));
        Assert.Contains("기사 수락은 별도", NeighborhoodDeliveryPresentation.배차선택안내(NeighborhoodDispatchModes.PublicCall));
        Assert.Contains("공개 콜", NeighborhoodDeliveryPresentation.자동추천준비안내(NeighborhoodDispatchModes.Hybrid, false));
        Assert.Contains("준비 중", NeighborhoodDeliveryPresentation.자동추천준비안내(NeighborhoodDispatchModes.Automatic, false));
        Assert.Null(NeighborhoodDeliveryPresentation.자동추천준비안내(NeighborhoodDispatchModes.Automatic, true));
        Assert.Null(NeighborhoodDeliveryPresentation.자동추천준비안내("future-mode", false));
    }

    [Fact]
    public async Task 배차선택_응답을_못받아도_현재기록과_원선택_복구버튼을_함께_렌더한다()
    {
        var client = new DeliveryClient();
        var services = new ServiceCollection(); services.AddLogging(); services.AddCommerceUiFixture();
        services.AddSingleton<NavigationManager, Navigation>();
        services.AddSingleton<ISsalddel현재사용자Context, User>();
        services.AddSingleton<INeighborhoodDeliveryClient>(client);
        services.AddSingleton<NeighborhoodDeliveryQueryViewModel>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var component = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<NeighborhoodDeliveryDetail>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(NeighborhoodDeliveryDetail.RequestId)] = "delivery-1" })));
        var model = provider.GetRequiredService<NeighborhoodDeliveryQueryViewModel>();
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            model.SelectedDispatchMode = NeighborhoodDispatchModes.PublicCall;
            await model.ChangeDispatchAsync();
        });
        var html = await renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(component.ToHtmlString()));

        Assert.True(model.HasPendingChoice); Assert.NotNull(model.Error);
        Assert.Contains("물품 배송 1", html);
        Assert.Contains("요청 결과를 확인하지 못했습니다", html);
        Assert.Contains("원 선택 결과 확인", html);
        Assert.Matches("<select[^>]*id=\"delivery-choice\"[^>]*disabled", html);
        Assert.DoesNotContain("배차 방식 저장", html);
        Assert.Contains("플랫폼 입금 미확인", html);
        Assert.Single(client.Choices);
        Assert.Equal(NeighborhoodDispatchModes.PublicCall, client.Choices[0].DispatchMode);
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/roles/01/", "http://localhost/roles/01/community/exchange/deliveries/delivery-1");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class User : ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot 현재사용자 => new("owner", "이웃", []);
    }

    private sealed class DeliveryClient : INeighborhoodDeliveryClient
    {
        public List<NeighborhoodDispatchChoiceRequest> Choices { get; } = [];
        public Task<NeighborhoodDeliveryResponse?> ReadAsync(string id, CancellationToken ct)
            => Task.FromResult<NeighborhoodDeliveryResponse?>(new()
            {
                RequestId = id, CargoName = "물품 배송 1", DispatchMode = NeighborhoodDispatchModes.Automatic,
                ProposalStateCode = NeighborhoodDeliveryProposalStates.Queued, CanChangeDispatchMode = true,
                FareKrw = 4000, PaymentStatusCode = "결제대기", SettlementStatusCode = "현장수금예정"
            });
        public Task<NeighborhoodDeliveryResponse?> ChangeDispatchAsync(string id, NeighborhoodDispatchChoiceRequest request, CancellationToken ct)
        {
            Choices.Add(request);
            return Task.FromException<NeighborhoodDeliveryResponse?>(new HttpRequestException("response unavailable"));
        }
        public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct) => throw new NotSupportedException();
    }
}
