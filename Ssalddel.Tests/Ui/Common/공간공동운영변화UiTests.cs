using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Components.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 공간공동운영변화UiTests
{
    [Fact]
    public async Task 비공개상세가_없으면_변화카드를_출력하지_않는다()
    {
        var html = await Render(null);
        Assert.DoesNotContain("space-shared-outcomes", html);
        Assert.DoesNotContain("이번 회차에서 함께 만든 변화", html);
    }

    [Fact]
    public async Task 변화조회가_없으면_확인불가를_표시하고_0명이나_0원으로_채우지_않는다()
    {
        var html = await Render(new() { LedgerId = "hub", ExecutionMode = "Operational" });
        Assert.Contains("이번 회차에서 함께 만든 변화", html);
        Assert.Contains("현재 변화 정보를 확인하지 못했어요", html);
        Assert.Contains("outcomes-unavailable", html);
        Assert.DoesNotContain("current-participation", html);
        Assert.DoesNotContain("0명", html);
        Assert.DoesNotContain("0원", html);
    }

    [Fact]
    public async Task 현재_고유참여인원은_역할수나_과거실제근무인원으로_표시하지_않는다()
    {
        var detail = Detail();
        detail.SharedOutcomes!.InterestedParticipantCount = 1;
        detail.SharedOutcomes.AcceptedParticipantCount = 2;
        detail.SharedOutcomes.AcceptedRoleCodes = [공간공동운영역할Codes.SpaceProvider,
            공간공동운영역할Codes.Supplier, 공간공동운영역할Codes.Picker];
        detail.SharedOutcomes.CurrentTermsAgreedRoleCodes = [공간공동운영역할Codes.SpaceProvider];
        detail.SharedOutcomes.PendingTermsAgreementRoleCodes = [공간공동운영역할Codes.Supplier];
        detail.SharedOutcomes.MissingRoleCodes = [공간공동운영역할Codes.Driver];
        detail.Participants = Enumerable.Range(0, 9).Select(index => new 공간공동운영참여Dto
        { UserId = "private-actor-" + index, RoleCode = 공간공동운영역할Codes.Picker, ParticipationStateCode = "accepted" }).ToArray();

        var html = await Render(detail);
        var text = PlainText(html);
        Assert.Matches(@"관심 참여\s+1\s*명", text);
        Assert.Matches(@"역할 수락\s+2\s*명", text);
        Assert.Contains("현재 참여 인원이며, 과거 실제 근무 인원이 아니에요.", text);
        Assert.Contains("공간 제공", text); Assert.Contains("상품 공급", text); Assert.Contains("배송", text);
        Assert.DoesNotMatch(@"(?:9|3)\s*명", text);
        Assert.DoesNotContain("private-actor", html);
    }

    [Fact]
    public async Task 연결이없음과_원천을확인할수없음은_다르게_표시한다()
    {
        var empty = await Render(Detail(Cycle("empty", 1)));
        var unknown = await Render(Detail(Cycle("unknown", 1,
            Source("InboundPlan", "not-visible", verified: false, completed: false))));

        Assert.Contains("아직 연결한 업무가 없어요", empty);
        Assert.DoesNotContain("현재 상태 확인 필요", empty);
        Assert.Contains("현재 상태 확인 필요", unknown);
        Assert.DoesNotContain("아직 연결한 업무가 없어요", unknown);
        Assert.DoesNotContain("원 업무에서 완료 확인", unknown);
    }

    [Theory]
    [InlineData(false, false, false, "현재 상태 확인 필요")]
    [InlineData(false, true, false, "현재 상태 확인 필요")]
    [InlineData(true, false, false, "진행 중")]
    [InlineData(true, true, false, "원 업무에서 완료 확인")]
    [InlineData(true, true, true, "문제 확인 필요")]
    public async Task 원천의_확인권한과_문제상태가_완료표시보다_우선한다(
        bool verified, bool completed, bool attention, string expected)
    {
        var source = Source("InboundPlan", "Received", verified, completed);
        source.RequiresAttention = attention;
        var html = await Render(Detail(Cycle("cycle", 1, source)));
        Assert.Equal(expected, Assert.Single(TestIdTexts(html, "source-status")));
    }

    [Fact]
    public async Task 정산완료는_지급준비확인으로_표시하고_입금실적으로_바꾸지_않는다()
    {
        var html = await Render(Detail(Cycle("cycle", 1,
            Source("Settlement", "Confirmed", verified: true, completed: true))));
        Assert.Equal("지급 준비 확인", Assert.Single(TestIdTexts(html, "source-status")));
        Assert.DoesNotContain("원 업무에서 완료 확인", TestIdTexts(html, "source-status"));
        Assert.Contains("실제 지급이나 입금 확인과는 달라요.", html);
        Assert.DoesNotContain("0원", html);
    }

    [Fact]
    public async Task 시험회차는_실제운영실적과_구별해_표시한다()
    {
        var detail = Detail(Cycle("trial", 1, Source("DistributedOrder", "Delivered", true, true)));
        detail.ExecutionMode = detail.SharedOutcomes!.ExecutionMode = "Simulation";
        var text = PlainText(await Render(detail));
        Assert.Contains("시험", text);
        Assert.Contains("실제", text);
    }

    [Fact]
    public async Task 보수와시간은_조건이나메모의수치로_추정하지_않는다()
    {
        var detail = Detail(Cycle("cycle", 1, Source("Settlement", "Confirmed", true, true)));
        detail.Terms.CostAndCompensation = "999999원 이익과 777시간 절감";
        detail.Terms.OperatingHours = "777시간";
        detail.Cycles = [new() { CycleId = "cycle", Number = 1, ResultNote = "999999원 수령 완료" }];
        var html = await Render(detail);
        var text = PlainText(html);
        var evidence = Assert.Single(TestIdTexts(html, "evidence-unconnected"));
        Assert.Equal(3, Regex.Matches(evidence, "확인 근거 없음").Count);
        Assert.Contains("실수령액", evidence); Assert.Contains("이동·대기 시간", evidence); Assert.Contains("비용 절감", evidence);
        Assert.DoesNotContain("999999", evidence); Assert.DoesNotContain("777", evidence);
        Assert.Contains("함께 남긴 개선 메모", text);
        Assert.Contains("999999원 수령 완료", text);
        Assert.DoesNotContain("0원", text); Assert.DoesNotContain("0시간", text);
    }

    [Fact]
    public async Task 회차마감기록과_현재원천상태는_구분하고_메모를_다른회차로_옮기지_않는다()
    {
        var oldSource = Source("InboundPlan", "not-visible", false, false);
        oldSource.Label = "지난 회차 입고";
        var old = Cycle("old", 1, oldSource);
        old.StatusCode = "completed"; old.SourceCompletionRecorded = true;
        var newSource = Source("DistributedOrder", "Pending", true, false);
        newSource.Label = "이번 회차 주문";
        var current = Cycle("current", 2, newSource);
        current.StatusCode = "prepared";
        var detail = Detail(old, current);
        detail.Cycles = [new() { CycleId = "old", Number = 1, ResultNote = "지난 회차 개선 메모" },
            new() { CycleId = "current", Number = 2, ResultNote = "이번 회차 개선 메모" }];

        var html = await Render(detail);
        var blocks = TestIdTexts(html, "cycle-outcomes");
        Assert.Equal(2, blocks.Length);
        var previousText = Assert.Single(blocks, block => block.Contains("1번째 회차", StringComparison.Ordinal));
        var currentText = Assert.Single(blocks, block => block.Contains("2번째 회차", StringComparison.Ordinal));
        Assert.Contains("회차 마침", previousText);
        Assert.Contains("회차 결과 확인 기록이 있어요", previousText);
        Assert.Contains("현재 상태 확인 필요", previousText);
        Assert.Contains("지난 회차 입고", previousText); Assert.Contains("지난 회차 개선 메모", previousText);
        Assert.DoesNotContain("이번 회차 주문", previousText); Assert.DoesNotContain("이번 회차 개선 메모", previousText);
        Assert.Contains("회차 준비", currentText); Assert.Contains("진행 중", currentText);
        Assert.Contains("이번 회차 주문", currentText); Assert.Contains("이번 회차 개선 메모", currentText);
        Assert.DoesNotContain("지난 회차 입고", currentText); Assert.DoesNotContain("지난 회차 개선 메모", currentText);
        Assert.True(html.IndexOf("data-cycle-id=\"current\"", StringComparison.Ordinal)
            < html.IndexOf("data-cycle-id=\"old\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/workspace/warehouse/distributed/inbound?selected=one", true)]
    [InlineData("/activity-settlements/one", true)]
    [InlineData("https://unsafe.example/work", false)]
    [InlineData("//unsafe.example/work", false)]
    [InlineData("/\\unsafe.example/work", false)]
    [InlineData("javascript:alert(1)", false)]
    public async Task 원천업무는_안전한_같은앱경로만_링크한다(string href, bool allowed)
    {
        var source = Source("InboundPlan", "Received", true, false);
        source.Href = href; source.NextActionLabel = "원 업무 확인";
        var html = await Render(Detail(Cycle("cycle", 1, source)));
        Assert.Equal(allowed, html.Contains("href=\"" + href + "\"", StringComparison.Ordinal));
        if (!allowed)
        {
            Assert.DoesNotContain("unsafe.example", html);
            Assert.DoesNotContain("javascript:", html);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("outside-user")]
    public async Task 익명과_비참여자는_공개제안을_보더라도_비공개변화카드를_받지_않는다(string? userId)
    {
        var client = new PublicOnlyClient();
        var user = new User(userId);
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<NavigationManager, Navigation>();
        services.AddSingleton(new 공간공동운영PageViewModel(client, user, new(), new Recovery(user)));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<공간공동운영Workspace>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(공간공동운영Workspace.LedgerId)] = "hub" }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });

        Assert.Contains("공개 제안", html);
        Assert.DoesNotContain("space-shared-outcomes", html);
        Assert.DoesNotContain("이번 회차에서 함께 만든 변화", html);
        Assert.Equal(0, client.DetailReads);
        Assert.Equal(userId is null ? 0 : 1, client.MineReads);
    }

    private static 공간공동운영상세Response Detail(params 공간공동운영회차변화Dto[] cycles) => new()
    {
        LedgerId = "hub", Revision = 3, TermsRevision = 2, ExecutionMode = "Operational",
        SharedOutcomes = new()
        {
            AsOfUtc = new(2026, 10, 24, 5, 0, 0, TimeSpan.Zero), ExecutionMode = "Operational",
            MatchesCurrentExecutionMode = true, TermsRevision = 2, Cycles = cycles
        }
    };

    private static 공간공동운영회차변화Dto Cycle(string id, int number, params 공간공동운영원천상태Dto[] sources)
        => new() { CycleId = id, Number = number, TermsRevision = 2, StatusCode = "running",
            LinkedSourceCount = sources.Length, Sources = sources,
            UnverifiedSourceCount = sources.Count(source => !source.Verified),
            AttentionSourceCount = sources.Count(source => source.RequiresAttention) };

    private static 공간공동운영원천상태Dto Source(string kind, string status, bool verified, bool completed)
        => new() { KindCode = kind, ReferenceId = "private-source", Label = "원천 업무", StatusCode = status,
            Verified = verified, Completed = completed, VerifiedRevision = verified ? 1 : null };

    private static async Task<string> Render(공간공동운영상세Response? detail)
    {
        await using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<공간공동운영변화Panel>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(공간공동운영변화Panel.Detail)] = detail }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private static string PlainText(string html)
        => Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), @"\s+", " ").Trim();

    private static string[] TestIdTexts(string html, string testId)
        => Regex.Matches(html, "<(?<tag>[A-Za-z][A-Za-z0-9]*)\\b[^>]*\\bdata-testid=\"" + Regex.Escape(testId)
            + "\"[^>]*>(?<body>[\\s\\S]*?)</\\k<tag>>", RegexOptions.CultureInvariant)
            .Select(match => PlainText(match.Groups["body"].Value)).ToArray();

    private sealed class User(string? userId) : ISsalddel현재사용자Context
    { public 현재사용자Snapshot 현재사용자 { get; } = new(userId, "이웃", []); }

    private sealed class Recovery(User user) : IWorkflowRecoveryContext
    {
        public WorkflowRecoverySnapshot Capture() => user.현재사용자.UserId is { } owner
            ? new(owner, 1, new('a', 32)) : throw new InvalidOperationException();
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot)
            => user.현재사용자.UserId is not null && snapshot == Capture();
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/community/space-cooperations/hub");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class PublicOnlyClient : I공간공동운영Client
    {
        public int DetailReads, MineReads;
        public Task<공간공동운영공개Response> PublicAsync(string? regionKey, int page, CancellationToken ct)
            => Task.FromResult(new 공간공동운영공개Response
            {
                ExecutionMode = "Operational", AsOfUtc = DateTimeOffset.UtcNow,
                Items = [new() { LedgerId = "hub", Revision = 3, TermsRevision = 2, Title = "공개 제안", ExecutionMode = "Operational" }]
            });
        public Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct)
            => Task.FromResult(new NeighborhoodExchangeRegionListResponse());
        public Task<공간공동운영내목록Response> MineAsync(CancellationToken ct)
        { MineReads++; return Task.FromResult(new 공간공동운영내목록Response()); }
        public Task<공간공동운영상세Response?> ReadAsync(string ledgerId, CancellationToken ct)
        { DetailReads++; throw new InvalidOperationException("공개 조회에서 비공개 상세를 요청하면 안 됩니다."); }
        public Task<공간공동운영상세Response?> CreateAsync(공간공동운영생성Request request, CancellationToken ct) => throw new NotSupportedException();
        public Task<공간공동운영상세Response?> CommandAsync(string ledgerId, 공간공동운영명령Request request, CancellationToken ct) => throw new NotSupportedException();
        public Task<공간공동운영상세Response?> ReceiptAsync(Guid requestId, string? ledgerId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string ledgerId, string cycleId, CancellationToken ct) => throw new NotSupportedException();
    }
}
