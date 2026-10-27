using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Components.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 공간공동운영HomeRenderTests
{
    [Fact]
    public async Task 등록이없는_소비호스트에서도_홈진입을_오류로_깨뜨리지_않는다()
    {
        var html = await Render();
        Assert.Contains("공개 참여 현황을 확인하지 못했습니다", html);
        Assert.Contains("동네 공동운영 보기", html);
        Assert.DoesNotContain("0명", html);
    }

    [Fact]
    public async Task 조회실패를_0명이나_예시모집으로_바꾸지_않는다()
    {
        var html = await Render(new Client(() => Task.FromException<공간공동운영공개Response>(new IOException("비공개 진단"))));
        Assert.Contains("공개 참여 현황을 확인하지 못했습니다", html);
        Assert.DoesNotContain("0명", html); Assert.DoesNotContain("비공개 진단", html);
    }

    [Fact]
    public async Task 실제_공개0건과_시험자료의_집계제외를_구별한다()
    {
        var actual = await Render(new Client(() => Task.FromResult(new 공간공동운영공개Response { ExecutionMode = "Operational", AsOfUtc = DateTimeOffset.UtcNow })));
        var simulation = await Render(new Client(() => Task.FromResult(new 공간공동운영공개Response { ExecutionMode = "Simulation", StatusCode = "simulation-excluded", AsOfUtc = DateTimeOffset.UtcNow })));
        Assert.Contains("0명", actual); Assert.Contains("아직 공개된", actual);
        Assert.Contains("시험 제안 · 실제 실적 집계 제외", simulation); Assert.DoesNotContain("0명", simulation);
    }

    [Fact]
    public async Task 공개동의한소개와_부족한역할을_표시하되_역할수를_인원으로_합산하지_않는다()
    {
        var html = await Render(new Client(() => Task.FromResult(new 공간공동운영공개Response
        {
            ExecutionMode = "Operational", AsOfUtc = DateTimeOffset.UtcNow, PublicParticipantCount = 1, SpaceCount = 1, RecruitmentCount = 1,
            CountsArePartial = true,
            Items = [new() { LedgerId = "hub", Title = "상온 보관 거점", NeighborhoodLabel = "동네", MissingRoleCodes = [공간공동운영역할Codes.InboundInspector, 공간공동운영역할Codes.Driver], Introductions = [new() { DisplayName = "이웃", RoleCode = 공간공동운영역할Codes.SpaceProvider }, new() { DisplayName = "이웃", RoleCode = 공간공동운영역할Codes.Supplier }] }]
        })));
        Assert.Contains("1명", html); Assert.DoesNotContain("2명", html);
        Assert.Contains("입고 검수 · 배송", html); Assert.Contains("이웃(공간 제공)", html);
        Assert.Contains("일부 공개 자료", html); Assert.Contains("/community/space-cooperations/hub", html);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 종료모집은_저장된공개선택과무관하게_비공개이며_다시공개버튼을_비활성화한다(bool published)
    {
        var html = await RenderVisibility(new() { StatusCode = "closed", PublishProposal = published });
        Assert.Contains("모집이 종료되어 공개 목록에 표시하지 않습니다.", html);
        Assert.Contains("disabled", html); Assert.Contains("다시 공개할 수 없음", html);
        Assert.DoesNotContain("공개 모집에 표시 중", html); Assert.DoesNotContain("동네 모집에 공개", html);
    }

    [Fact]
    public async Task 진행중모집의_공개철회는_개인소개공개와_별도행동으로_표시한다()
    {
        var html = await RenderVisibility(new() { StatusCode = "gathering", PublishProposal = true });
        Assert.Contains("공개 모집 철회", html); Assert.Contains("개인 소개 공개 동의와는 별도", html);
        Assert.DoesNotContain("disabled", html);
    }

    private static async Task<string> RenderVisibility(공간공동운영상세Response detail)
    {
        await using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<공간공동운영공개설정>(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(공간공동운영공개설정.Detail)] = detail }));
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });
    }

    private static async Task<string> Render(I공간공동운영Client? client = null)
    {
        var services = new ServiceCollection().AddLogging();
        if (client is not null) services.AddSingleton(client);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<공간공동운영HomeOverview>();
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });
    }
    private sealed class Client(Func<Task<공간공동운영공개Response>> result) : I공간공동운영Client
    {
        public Task<공간공동운영공개Response> PublicAsync(string? regionKey, int page, CancellationToken ct) => result();
        public Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<공간공동운영내목록Response> MineAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<공간공동운영상세Response?> ReadAsync(string ledgerId, CancellationToken ct) => throw new NotSupportedException();
        public Task<공간공동운영상세Response?> CreateAsync(공간공동운영생성Request request, CancellationToken ct) => throw new NotSupportedException();
        public Task<공간공동운영상세Response?> CommandAsync(string ledgerId, 공간공동운영명령Request request, CancellationToken ct) => throw new NotSupportedException();
        public Task<공간공동운영상세Response?> ReceiptAsync(Guid requestId, string? ledgerId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string ledgerId, string cycleId, CancellationToken ct) => throw new NotSupportedException();
    }
}
