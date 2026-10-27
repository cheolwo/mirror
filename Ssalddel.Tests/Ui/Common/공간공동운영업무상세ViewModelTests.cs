using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 공간공동운영업무상세ViewModelTests
{
    [Fact]
    public async Task 상세로드는_현재원장만읽고_연락처조회나_전화앱을_자동실행하지않는다()
    {
        var f = new Fixture(); using var vm = f.Make(); await vm.LoadAsync("hub", "inbound", "cycle");
        Assert.NotNull(vm.Node); Assert.True(vm.CanReadContacts); Assert.Null(vm.Contacts);
        Assert.Equal(1, f.DetailReads); Assert.Equal(1, f.IntegratedReads);
        Assert.Empty(f.ContactReads); Assert.Empty(f.Dialer.Numbers); Assert.Equal(0, f.MutationCalls);
        await vm.ReadContactsAsync();
        var read = Assert.Single(f.ContactReads);
        Assert.Equal(("hub", "cycle", "inbound", 3L), (read.LedgerId, read.CycleId, read.StageKey, read.Revision));
        Assert.True(vm.CanOpen(Assert.Single(vm.Contacts!.Contacts))); Assert.Empty(f.Dialer.Numbers);
    }

    [Fact]
    public async Task 익명이나_잘못된경로는_비공개원장과_연락처를_요청하지않는다()
    {
        var f = new Fixture(); f.User.Switch(null); using var vm = f.Make();
        await vm.LoadAsync("hub", "inbound", "cycle"); Assert.NotNull(vm.Error);
        f.User.Switch("one"); await vm.LoadAsync("hub", "not-a-stage", "cycle"); Assert.NotNull(vm.Error);
        await vm.ReadContactsAsync(); await vm.OpenDialerAsync("contact");
        Assert.Equal(0, f.DetailReads); Assert.Equal(0, f.IntegratedReads); Assert.Empty(f.ContactReads); Assert.Empty(f.Dialer.Numbers);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("server")]
    [InlineData("route")]
    public async Task 상세조회중_계정서버경로가바뀌면_늦은원장을_현재화면으로_받지않는다(string change)
    {
        var f = new Fixture { DetailGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var vm = f.Make(); var loading = vm.LoadAsync("hub", "inbound", "cycle"); Assert.True(vm.IsLoading);
        await Change(f, vm, change); f.DetailGate.SetResult(Fixture.Detail("hub")); await loading;
        Assert.Equal(change == "route" ? "other" : null, vm.Detail?.LedgerId);
        Assert.Null(vm.Contacts); Assert.Empty(f.ContactReads); Assert.Empty(f.Dialer.Numbers);
        Assert.False(vm.IsLoading); Assert.Equal(change == "route" ? 1 : 0, f.IntegratedReads);
    }

    [Theory]
    [InlineData("account", false)]
    [InlineData("server", false)]
    [InlineData("route", false)]
    [InlineData("account", true)]
    [InlineData("server", true)]
    [InlineData("route", true)]
    public async Task 연락처조회또는_직전재조회중_문맥이바뀌면_늦은번호를_버리고_열지않는다(string change, bool opening)
    {
        var f = new Fixture(); using var vm = f.Make(); await vm.LoadAsync("hub", "inbound", "cycle");
        if (opening) await vm.ReadContactsAsync();
        var gate = new TaskCompletionSource<공간공동운영업무연락Response>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.ContactHandlers.Enqueue(_ => gate.Task);
        var pending = opening ? vm.OpenDialerAsync("contact") : vm.ReadContactsAsync();
        Assert.True(opening ? vm.IsOpeningDialer : vm.IsLoadingContacts);
        await Change(f, vm, change); gate.SetResult(Fixture.ContactResponse()); await pending;
        Assert.Null(vm.Contacts); Assert.Empty(f.Dialer.Numbers);
        Assert.False(vm.IsLoadingContacts); Assert.False(vm.IsOpeningDialer); Assert.Null(vm.Notice);
        Assert.Equal(change == "route" ? "other" : null, vm.Detail?.LedgerId);
    }

    [Theory]
    [InlineData("phone")]
    [InlineData("format")]
    [InlineData("source-revision")]
    [InlineData("source")]
    [InlineData("name")]
    [InlineData("label")]
    [InlineData("purpose")]
    [InlineData("href")]
    [InlineData("permission")]
    [InlineData("parent-revision")]
    [InlineData("ledger")]
    [InlineData("cycle")]
    [InlineData("stage")]
    [InlineData("missing")]
    public async Task 전화앱직전_현재조회가_번호판본권한업무변경을_보이면_열지않고_재확인을요구한다(string change)
    {
        var f = new Fixture(); using var vm = f.Make(); await vm.LoadAsync("hub", "inbound", "cycle"); await vm.ReadContactsAsync();
        var response = Fixture.ContactResponse(); var contact = Assert.Single(response.Contacts);
        switch (change)
        {
            case "phone": contact.PhoneNumber = "+12 345-6790"; break;
            case "format": contact.PhoneNumber = "+123456789"; break;
            case "source-revision": contact.SourceRevision++; break;
            case "source": contact.SourceNodeKey = "source:InboundPlan:other"; break;
            case "name": contact.DisplayName = "바뀐 업무 담당"; break;
            case "label": contact.Label = "다른 담당 연락"; break;
            case "purpose": contact.PurposeCode = "other-purpose"; break;
            case "href": contact.Href = "/workspace/warehouse/distributed/inbound?selected=other"; break;
            case "permission": response.StatusCode = "not-visible"; response.Contacts = []; break;
            case "parent-revision": response.Revision++; break;
            case "ledger": response.LedgerId = "other"; break;
            case "cycle": response.CycleId = "other-cycle"; break;
            case "stage": response.StageKey = "storage"; break;
            case "missing": response.Contacts = []; break;
        }
        f.ContactHandlers.Enqueue(_ => Task.FromResult(response)); await vm.OpenDialerAsync("contact");
        Assert.Equal(2, f.ContactReads.Count); Assert.Empty(f.Dialer.Numbers); Assert.False(vm.IsOpeningDialer);
        Assert.True(vm.Notice is not null || vm.ContactError is not null);
    }

    [Fact]
    public async Task 명시선택은_한번_재조회한후_정규화된번호만_전화앱에_전달한다()
    {
        var f = new Fixture(); using var vm = f.Make(); await vm.LoadAsync("hub", "inbound", "cycle");
        await vm.OpenDialerAsync("contact"); Assert.Empty(f.ContactReads); Assert.Empty(f.Dialer.Numbers);
        await vm.ReadContactsAsync(); await vm.OpenDialerAsync("contact");
        Assert.Equal(2, f.ContactReads.Count); Assert.Equal("+123456789", Assert.Single(f.Dialer.Numbers));
        Assert.Contains("전화 앱에 번호를 전달", vm.Notice); Assert.Contains("직접 선택", vm.Notice);
        Assert.Equal(0, f.MutationCalls);
    }

    [Fact]
    public async Task 열기처리중_중복클릭은_추가조회와_두번째열기를_만들지않는다()
    {
        var f = new Fixture(); f.Dialer.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var vm = f.Make(); await vm.LoadAsync("hub", "inbound", "cycle"); await vm.ReadContactsAsync();
        var opening = vm.OpenDialerAsync("contact"); Assert.True(vm.IsOpeningDialer);
        await vm.OpenDialerAsync("contact"); Assert.Equal(2, f.ContactReads.Count); Assert.Single(f.Dialer.Numbers);
        f.Dialer.Gate.SetResult(업무전화앱결과.Opened); await opening; Assert.False(vm.IsOpeningDialer); Assert.Single(f.Dialer.Numbers);
    }

    [Fact]
    public async Task 미지원호스트와_형식이잘못된번호는_전화앱을_열지않는다()
    {
        var f = new Fixture(); f.Dialer.Supported = false; using var vm = f.Make();
        await vm.LoadAsync("hub", "inbound", "cycle"); await vm.ReadContactsAsync();
        Assert.False(vm.CanOpen(Assert.Single(vm.Contacts!.Contacts))); await vm.OpenDialerAsync("contact");
        Assert.Single(f.ContactReads); Assert.Empty(f.Dialer.Numbers);
        f.Dialer.Supported = true; var invalid = Fixture.ContactResponse(); invalid.Contacts[0].PhoneNumber = "tel:1234567";
        f.ContactHandlers.Enqueue(_ => Task.FromResult(invalid)); await vm.ReadContactsAsync();
        Assert.False(vm.CanOpen(Assert.Single(vm.Contacts!.Contacts))); await vm.OpenDialerAsync("contact");
        Assert.Equal(2, f.ContactReads.Count); Assert.Empty(f.Dialer.Numbers);
    }

    [Fact]
    public async Task 현재목록에서_선택하지않은_연락키와객체는_전화앱진입에_쓰지않는다()
    {
        var f = new Fixture(); using var vm = f.Make(); await vm.LoadAsync("hub", "inbound", "cycle"); await vm.ReadContactsAsync();
        Assert.False(vm.CanOpen(Fixture.ContactResponse().Contacts[0]));
        await vm.OpenDialerAsync("forged-contact"); Assert.Single(f.ContactReads); Assert.Empty(f.Dialer.Numbers);
    }

    [Theory]
    [InlineData(업무전화앱결과.NotSupported, "열 수 없어요")]
    [InlineData(업무전화앱결과.InvalidNumber, "번호 형식")]
    [InlineData(업무전화앱결과.Failed, "열지 못했어요")]
    public async Task 전화앱의_실패미지원결과는_열기성공이나_실제통화로_표시하지않는다(업무전화앱결과 result, string message)
    {
        var f = new Fixture(); f.Dialer.Result = result; using var vm = f.Make();
        await vm.LoadAsync("hub", "inbound", "cycle"); await vm.ReadContactsAsync(); await vm.OpenDialerAsync("contact");
        Assert.Single(f.Dialer.Numbers); Assert.Contains(message, vm.Notice); Assert.DoesNotContain("번호를 전달했어요", vm.Notice ?? "");
        Assert.False(vm.IsOpeningDialer); Assert.Equal(0, f.MutationCalls);
    }

    [Fact]
    public async Task 조회실패는_빈연락처성공과_다르고_비공개오류를_제품문구로_복사하지않는다()
    {
        var f = new Fixture(); using var vm = f.Make(); await vm.LoadAsync("hub", "inbound", "cycle");
        f.ContactHandlers.Enqueue(_ => Task.FromException<공간공동운영업무연락Response>(new IOException("비공개 원문")));
        await vm.ReadContactsAsync(); Assert.Null(vm.Contacts); Assert.NotNull(vm.ContactError);
        Assert.DoesNotContain("비공개 원문", vm.ContactError); Assert.Empty(f.Dialer.Numbers);
    }

    [Fact]
    public async Task 전화앱예외도_실제연락성공이나_자동재시도로_바꾸지않는다()
    {
        var f = new Fixture(); f.Dialer.Failure = new IOException("비공개 전화앱 원문"); using var vm = f.Make();
        await vm.LoadAsync("hub", "inbound", "cycle"); await vm.ReadContactsAsync(); await vm.OpenDialerAsync("contact");
        Assert.Single(f.Dialer.Numbers); Assert.Null(vm.Contacts); Assert.NotNull(vm.ContactError); Assert.Null(vm.Notice);
        Assert.DoesNotContain("비공개", vm.ContactError); Assert.False(vm.IsOpeningDialer); Assert.Equal(0, f.MutationCalls);
    }

    [Fact]
    public async Task 전화앱대기중_경로변경은_토큰을취소하고_이전성공안내를_새업무에_남기지않는다()
    {
        var f = new Fixture(); f.Dialer.WaitForCancellation = true; using var vm = f.Make();
        await vm.LoadAsync("hub", "inbound", "cycle"); await vm.ReadContactsAsync();
        var opening = vm.OpenDialerAsync("contact"); Assert.Single(f.Dialer.Numbers); Assert.True(vm.IsOpeningDialer);
        await vm.LoadAsync("other", "inbound", "cycle"); await opening;
        Assert.True(f.Dialer.LastToken.IsCancellationRequested); Assert.Equal("other", vm.Detail!.LedgerId);
        Assert.Null(vm.Notice); Assert.Null(vm.Contacts); Assert.False(vm.IsOpeningDialer);
    }

    [Fact]
    public async Task 화면폐기는_대기연락처를취소하고_완료된늦은응답으로_다시채우지않는다()
    {
        var f = new Fixture(); var vm = f.Make(); await vm.LoadAsync("hub", "inbound", "cycle");
        var gate = new TaskCompletionSource<공간공동운영업무연락Response>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.ContactHandlers.Enqueue(_ => gate.Task); var reading = vm.ReadContactsAsync(); var token = Assert.Single(f.ContactReads).Token;
        vm.Dispose(); Assert.True(token.IsCancellationRequested); gate.SetResult(Fixture.ContactResponse()); await reading;
        Assert.Null(vm.Detail); Assert.Null(vm.Contacts); Assert.False(vm.CanReadContacts); Assert.Empty(f.Dialer.Numbers);
    }

    private static Task Change(Fixture fixture, 공간공동운영업무상세ViewModel vm, string change)
    {
        if (change == "route") return vm.LoadAsync("other", "inbound", "cycle");
        if (change == "account") fixture.User.Switch("two"); else fixture.Recovery.Endpoint = new('b', 32);
        return Task.CompletedTask;
    }

    private sealed class User : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림
    {
        public 현재사용자Snapshot 현재사용자 { get; private set; } = new("one", "시험 참여자", []);
        public event Action? Changed;
        public void Switch(string? id) { 현재사용자 = new(id, "시험 참여자", []); Changed?.Invoke(); }
    }
    private sealed class Scope(User user) : IWorkflowRecoveryContext
    {
        public string Endpoint = new('a', 32);
        public WorkflowRecoverySnapshot Capture() => new(user.현재사용자.UserId ?? throw new InvalidOperationException(), 1, Endpoint);
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => user.현재사용자.UserId is not null && snapshot == Capture();
    }
    private sealed class Dialer : I업무전화앱Adapter
    {
        public bool Supported = true;
        public bool IsSupported => Supported;
        public 업무전화앱결과 Result = 업무전화앱결과.Opened;
        public TaskCompletionSource<업무전화앱결과>? Gate;
        public bool WaitForCancellation;
        public Exception? Failure;
        public CancellationToken LastToken;
        public List<string> Numbers { get; } = [];
        public async Task<업무전화앱결과> OpenAsync(string number, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); Numbers.Add(number); LastToken = ct;
            if (Failure is not null) throw Failure;
            if (WaitForCancellation) await Task.Delay(Timeout.Infinite, ct);
            return Gate is null ? Result : await Gate.Task;
        }
    }
    private sealed record ContactRead(string LedgerId, string CycleId, string StageKey, long Revision, CancellationToken Token);
    private sealed class Fixture : I공간공동운영Client, I공간공동운영통합Client, I공간공동운영업무연락Client
    {
        public readonly User User = new();
        public readonly Scope Recovery;
        public readonly Dialer Dialer = new();
        public int DetailReads, IntegratedReads, MutationCalls;
        public TaskCompletionSource<공간공동운영상세Response?>? DetailGate;
        public readonly Queue<Func<ContactRead, Task<공간공동운영업무연락Response>>> ContactHandlers = new();
        public readonly List<ContactRead> ContactReads = [];
        public Fixture() { Recovery = new(User); }
        public 공간공동운영업무상세ViewModel Make() => new(this, this, this, Dialer, User, Recovery);
        public static 공간공동운영상세Response Detail(string id) => new()
        {
            LedgerId = id, Revision = 3, TermsRevision = 2, ExecutionMode = "Operational",
            Cycles = [new() { CycleId = "cycle", Number = 1, TermsRevision = 2, StatusCode = "prepared",
                References = [new() { KindCode = "InboundPlan", ReferenceId = "plan", ExpectedRevision = 1 }] }]
        };
        private static 공간공동운영통합Response Snapshot(string id) => new()
        {
            LedgerId = id, Revision = 3, ExecutionMode = "Operational", AsOfUtc = DateTimeOffset.UtcNow,
            Cycles = [new() { NodeKey = "cycle:cycle", CycleId = "cycle", Number = 1, TermsRevision = 2,
                StatusCode = "prepared", SharedSourceKeys = ["source:InboundPlan:plan"] }],
            SharedSources = [new() { NodeKey = "source:InboundPlan:plan", KindCode = "InboundPlan", ReferenceId = "plan",
                Verified = true, Completed = true, StatusCode = "Received", Label = "입고 수령 자료",
                Href = "/workspace/warehouse/distributed/inbound?selected=plan" }]
        };
        public static 공간공동운영업무연락Response ContactResponse() => new()
        {
            LedgerId = "hub", CycleId = "cycle", StageKey = "inbound", Revision = 3, AsOfUtc = DateTimeOffset.UtcNow,
            StatusCode = "available", Sources = [new() { SourceNodeKey = "source:InboundPlan:plan", KindCode = "InboundPlan", StatusCode = "available" }],
            Contacts = [new() { ContactKey = "contact", SourceNodeKey = "source:InboundPlan:plan", SourceRevision = 1,
                PurposeCode = "registered-inbound", DisplayName = "시험 업무 담당", Label = "입고 담당 연락",
                PhoneNumber = "+12 345-6789", Href = "/workspace/warehouse/distributed/inbound?selected=plan" }]
        };
        Task<공간공동운영상세Response?> I공간공동운영Client.ReadAsync(string ledgerId, CancellationToken ct)
        { DetailReads++; return DetailGate is not null && DetailReads == 1 ? DetailGate.Task : Task.FromResult<공간공동운영상세Response?>(Detail(ledgerId)); }
        Task<공간공동운영통합Response> I공간공동운영통합Client.ReadAsync(string ledgerId, CancellationToken ct)
        { IntegratedReads++; return Task.FromResult(Snapshot(ledgerId)); }
        public Task<공간공동운영업무연락Response> ReadAsync(string ledgerId, string cycleId, string stageKey, long revision, CancellationToken ct)
        {
            var read = new ContactRead(ledgerId, cycleId, stageKey, revision, ct); ContactReads.Add(read);
            return ContactHandlers.Count > 0 ? ContactHandlers.Dequeue()(read) : Task.FromResult(ContactResponse());
        }
        public Task<공간공동운영공개Response> PublicAsync(string? regionKey, int page, CancellationToken ct) => throw new InvalidOperationException("독립 상세에서 공개 목록을 읽지 않습니다.");
        public Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct) => throw new InvalidOperationException();
        public Task<공간공동운영내목록Response> MineAsync(CancellationToken ct) => throw new InvalidOperationException();
        public Task<공간공동운영상세Response?> CreateAsync(공간공동운영생성Request request, CancellationToken ct) => Mutation<공간공동운영상세Response?>();
        public Task<공간공동운영상세Response?> CommandAsync(string ledgerId, 공간공동운영명령Request request, CancellationToken ct) => Mutation<공간공동운영상세Response?>();
        public Task<공간공동운영상세Response?> ReceiptAsync(Guid requestId, string? ledgerId, CancellationToken ct) => throw new InvalidOperationException();
        public Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string ledgerId, string cycleId, CancellationToken ct) => throw new InvalidOperationException();
        public Task<공간공동운영예약목록Response> ReservationsAsync(string ledgerId, CancellationToken ct) => throw new InvalidOperationException("기존 예약 초안을 소비하지 않습니다.");
        public Task<공간공동운영예약후보Response> CandidatesAsync(string ledgerId, CancellationToken ct) => throw new InvalidOperationException();
        public Task<공간공동운영예약Dto?> ReservationAsync(string ledgerId, Guid bookingId, CancellationToken ct) => throw new InvalidOperationException();
        public Task<공간공동운영예약Dto?> ReceiptAsync(string ledgerId, Guid clientRequestId, CancellationToken ct) => throw new InvalidOperationException();
        public Task<공간공동운영예약취소영향Dto> ImpactAsync(string ledgerId, Guid bookingId, CancellationToken ct) => throw new InvalidOperationException();
        public Task<공간공동운영예약Dto?> CommandAsync(string ledgerId, Guid bookingId, 공간공동운영예약명령Request request, CancellationToken ct) => Mutation<공간공동운영예약Dto?>();
        private Task<T> Mutation<T>() { MutationCalls++; throw new InvalidOperationException("조회·전화 앱 진입에서 업무 명령을 보내면 안 됩니다."); }
    }
}
