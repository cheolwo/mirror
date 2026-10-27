using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 공간공동운영WorkflowTests
{
    [Fact]
    public async Task 익명은_공개자료만_보고_참여명령을_보내지_않는다()
    {
        var client = new Client(); var user = new User();
        using var vm = Make(client, user);
        await vm.LoadAsync("hub"); vm.ParticipationConfirmed = true;
        Assert.NotNull(vm.SelectedPublic); Assert.Null(await vm.JoinAsync(client.Public.Items[0]));
        Assert.Equal(0, client.MineCalls); Assert.Equal(0, client.ReadCalls); Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task 명령_응답을_완료로_쓰지_않고_GET정본을_다시_읽는다()
    {
        var client = new Client(); var user = new User("one");
        using var vm = Make(client, user);
        await vm.LoadAsync("hub"); vm.TermsConfirmed = true;
        client.Detail.Terms.Title = "최신 원장의 제목";
        Assert.Equal("hub", await vm.ExecuteAsync(공간공동운영명령Codes.AgreeTerms, 공간공동운영역할Codes.SpaceProvider));
        Assert.Equal(2, client.ReadCalls); Assert.Equal("최신 원장의 제목", vm.Detail?.Terms.Title);
        Assert.False(vm.HasPending); Assert.Equal("저장한 내용을 다시 확인했습니다.", vm.Notice);
    }

    [Fact]
    public async Task 서버에서_허용하지_않은_행동은_전송하지_않는다()
    {
        var client = new Client(); using var vm = Make(client, new("one"));
        await vm.LoadAsync("hub");
        Assert.Null(await vm.ExecuteAsync(공간공동운영명령Codes.StartCycle));
        Assert.Empty(client.Commands); Assert.NotNull(vm.Error);
    }

    [Fact]
    public async Task 역할별_현재조건_동의와_소개공개는_분리된다()
    {
        var client = new Client(); using var vm = Make(client, new("one"));
        await vm.LoadAsync("hub");
        Assert.Null(await vm.ExecuteAsync(공간공동운영명령Codes.AgreeTerms, 공간공동운영역할Codes.SpaceProvider));
        vm.PublicDisplayName = "이웃";
        await vm.ExecuteAsync(공간공동운영명령Codes.IntroductionConsent, 공간공동운영역할Codes.SpaceProvider, false);
        var command = Assert.Single(client.Commands);
        Assert.False(command.Consented); Assert.Null(command.PublicDisplayName);
        Assert.Equal(공간공동운영역할Codes.SpaceProvider, command.RoleCode);
    }

    [Fact]
    public async Task 전송유실_후_자동POST없이_영수증GET부터_확인한다()
    {
        var client = new Client { CommandFailure = new IOException("민감한 원문") };
        var user = new User("one"); var session = new 공간공동운영DraftSession();
        using var first = Make(client, user, session);
        await first.LoadAsync("hub"); first.TermsConfirmed = true;
        await first.ExecuteAsync(공간공동운영명령Codes.AgreeTerms, 공간공동운영역할Codes.SpaceProvider);
        var original = Assert.Single(client.Commands); Assert.True(first.HasPending);
        Assert.DoesNotContain("민감한", first.Error ?? "");
        await first.RecoverAsync(); Assert.Single(client.Commands); Assert.Equal(original.ClientRequestId, client.LastReceipt);
        Assert.True(first.CanRetryPending);
        client.CommandFailure = null;
        await first.RetryPendingAsync();
        Assert.Equal(original.ClientRequestId, client.Commands[1].ClientRequestId); Assert.False(first.HasPending);
    }

    [Fact]
    public async Task 접수응답뒤_GET정본조회가_실패하면_원요청을_보존한다()
    {
        var client = new Client(); using var vm = Make(client, new("one"));
        await vm.LoadAsync("hub"); vm.TermsConfirmed = true; client.ReadFailure = new IOException();
        Assert.Null(await vm.ExecuteAsync(공간공동운영명령Codes.AgreeTerms, 공간공동운영역할Codes.SpaceProvider));
        Assert.True(vm.HasPending); Assert.Null(vm.Notice);
    }

    [Fact]
    public async Task 다른계정으로_바뀐뒤_늦은명령응답은_표시되지_않는다()
    {
        var delayed = new TaskCompletionSource<공간공동운영상세Response?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { CommandResult = delayed.Task }; var user = new User("one");
        using var vm = Make(client, user); await vm.LoadAsync("hub"); vm.TermsConfirmed = true;
        var sending = vm.ExecuteAsync(공간공동운영명령Codes.AgreeTerms, 공간공동운영역할Codes.SpaceProvider);
        user.Change("two"); delayed.SetResult(client.Detail);
        Assert.Null(await sending); Assert.Null(vm.Detail); Assert.False(vm.HasPending); Assert.False(vm.IsSending);
    }

    [Fact]
    public async Task 로그아웃_로그인왕복은_명령을_자동재전송하지_않고_같은계정_원요청을_보존한다()
    {
        var client = new Client { CommandFailure = new IOException() }; var user = new User("one");
        using var vm = Make(client, user); await vm.LoadAsync("hub"); vm.TermsConfirmed = true;
        await vm.ExecuteAsync(공간공동운영명령Codes.AgreeTerms, 공간공동운영역할Codes.SpaceProvider);
        user.Change(null); Assert.Null(vm.Detail); user.Change("one");
        await vm.LoadAsync("hub"); Assert.Single(client.Commands); Assert.True(vm.HasPending);
    }

    [Fact]
    public async Task 서버가_바뀌면_원요청_확인과_재전송을_차단한다()
    {
        var client = new Client { CommandFailure = new IOException() }; var user = new User("one"); var scope = new Scope(user);
        using var vm = Make(client, user, recovery: scope); await vm.LoadAsync("hub"); vm.TermsConfirmed = true;
        await vm.ExecuteAsync(공간공동운영명령Codes.AgreeTerms, 공간공동운영역할Codes.SpaceProvider);
        scope.Endpoint = new('b', 32); await vm.RecoverAsync();
        Assert.Null(client.LastReceipt); Assert.Single(client.Commands); Assert.True(vm.HasPending);
    }

    [Fact]
    public async Task 연결후보의_판본과_부모참조를_변경없이_전송한다()
    {
        var client = new Client(); client.Detail.AllowedActions = [공간공동운영명령Codes.LinkReference];
        client.Detail.Cycles = [new() { CycleId = "cycle", Number = 1 }];
        client.Candidates = [new() { Label = "이번 운영의 입고", Reference = new() { KindCode = "InboundPlan", ReferenceId = "plan", ParentId = "parent", ExpectedRevision = 7 } }];
        using var vm = Make(client, new("one")); await vm.LoadAsync("hub"); vm.SelectedReferenceIndex = 0;
        await vm.ExecuteAsync(공간공동운영명령Codes.LinkReference);
        var reference = Assert.Single(client.Commands).Reference;
        Assert.NotNull(reference); Assert.Equal("parent", reference.ParentId); Assert.Equal(7, reference.ExpectedRevision);
    }

    [Fact]
    public async Task 내목록_조회실패는_빈_참여목록_성공으로_표시하지_않는다()
    {
        var client = new Client { MineFailure = new IOException() }; using var vm = Make(client, new("one"));
        await vm.LoadAsync(); Assert.False(vm.MineLoaded); Assert.NotNull(vm.Error); Assert.NotNull(vm.Public);
    }

    [Fact]
    public async Task 제안은_등록된동네와_별도안내확인이_있어야_전송한다()
    {
        var client = new Client(); using var vm = Make(client, new("one")); await vm.LoadAsync();
        vm.Form.Terms.Title = "동네 상온 보관"; vm.Form.Terms.Purpose = "함께 준비"; vm.Form.Terms.PublicNeighborhoodRegionKey = "invented";
        vm.Form.ConfirmNonBinding = true; Assert.Null(await vm.CreateAsync()); Assert.Empty(client.Created);
        vm.Form.Terms.PublicNeighborhoodRegionKey = "region"; vm.Form.ConfirmNonBinding = false;
        Assert.Null(await vm.CreateAsync()); Assert.Empty(client.Created);
        vm.Form.ConfirmNonBinding = true; Assert.Equal("hub", await vm.CreateAsync()); Assert.Single(client.Created); Assert.Equal("", vm.Form.Terms.Title);
    }

    [Theory]
    [InlineData("/workspace/warehouse/distributed/inbounds?selected=one", true)]
    [InlineData("https://example.org/work", false)]
    [InlineData("//example.org/work", false)]
    [InlineData("/\\example.org/work", false)]
    [InlineData("javascript:alert(1)", false)]
    public void 전문업무링크는_같은앱의_상대경로만_열수있다(string input, bool allowed)
        => Assert.Equal(allowed, 공간공동운영Presentation.SafeWorkHref(input) is not null);

    private static 공간공동운영PageViewModel Make(Client client, User user, 공간공동운영DraftSession? session = null, Scope? recovery = null)
        => new(client, user, session ?? new(), recovery ?? new(user));
    private sealed class User(string? id = null) : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림
    {
        public 현재사용자Snapshot 현재사용자 { get; private set; } = new(id, "이웃", []);
        public event Action? Changed;
        public void Change(string? id) { 현재사용자 = new(id, "이웃", []); Changed?.Invoke(); }
    }
    private sealed class Scope(User user) : IWorkflowRecoveryContext
    {
        public string Endpoint = new('a', 32);
        public WorkflowRecoverySnapshot Capture() => user.현재사용자.UserId is { } owner ? new(owner, 1, Endpoint) : throw new InvalidOperationException();
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) { try { return snapshot == Capture(); } catch { return false; } }
    }
    private sealed class Client : I공간공동운영Client
    {
        public 공간공동운영상세Response Detail = new() { LedgerId = "hub", Revision = 3, TermsRevision = 2, ExecutionMode = "Simulation", Terms = new() { Title = "동네 공동운영", Purpose = "함께 준비", PublicNeighborhoodRegionKey = "region" }, Participants = [new() { IsMe = true, UserId = "one", RoleCode = 공간공동운영역할Codes.SpaceProvider, ParticipationStateCode = "accepted" }], AllowedActions = [공간공동운영명령Codes.AgreeTerms, 공간공동운영명령Codes.IntroductionConsent] };
        public 공간공동운영공개Response Public = new() { ExecutionMode = "Operational", AsOfUtc = DateTimeOffset.UtcNow, Items = [new() { LedgerId = "hub", Title = "공개 제안", Revision = 3, TermsRevision = 2 }] };
        public IReadOnlyList<공간공동운영연결후보Dto> Candidates = [];
        public readonly List<공간공동운영명령Request> Commands = [];
        public readonly List<공간공동운영생성Request> Created = [];
        public int ReadCalls, MineCalls;
        public Guid? LastReceipt;
        public Exception? CommandFailure, ReadFailure, MineFailure;
        public Task<공간공동운영상세Response?>? CommandResult;
        public Task<공간공동운영공개Response> PublicAsync(string? regionKey, int page, CancellationToken ct) => Task.FromResult(Public);
        public Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct) => Task.FromResult(new NeighborhoodExchangeRegionListResponse { Items = [new() { RegionKey = "region", DisplayName = "동네" }] });
        public Task<공간공동운영내목록Response> MineAsync(CancellationToken ct) { MineCalls++; return MineFailure is null ? Task.FromResult(new 공간공동운영내목록Response { Items = [Detail] }) : Task.FromException<공간공동운영내목록Response>(MineFailure); }
        public Task<공간공동운영상세Response?> ReadAsync(string ledgerId, CancellationToken ct) { ReadCalls++; return ReadFailure is null ? Task.FromResult<공간공동운영상세Response?>(Detail) : Task.FromException<공간공동운영상세Response?>(ReadFailure); }
        public Task<공간공동운영상세Response?> CreateAsync(공간공동운영생성Request request, CancellationToken ct) { Created.Add(request); return Task.FromResult<공간공동운영상세Response?>(Detail); }
        public Task<공간공동운영상세Response?> CommandAsync(string ledgerId, 공간공동운영명령Request request, CancellationToken ct) { Commands.Add(request); return CommandFailure is not null ? Task.FromException<공간공동운영상세Response?>(CommandFailure) : CommandResult ?? Task.FromResult<공간공동운영상세Response?>(Detail); }
        public Task<공간공동운영상세Response?> ReceiptAsync(Guid requestId, string? ledgerId, CancellationToken ct) { LastReceipt = requestId; return Task.FromResult<공간공동운영상세Response?>(null); }
        public Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string ledgerId, string cycleId, CancellationToken ct) => Task.FromResult(Candidates);
    }
}
