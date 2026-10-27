using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Components.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 공간공동운영통합UiTests
{
    [Fact]
    public async Task 상세가없으면_표시하지않고_서비스미등록은_조회불가로_표시한다()
    {
        Assert.DoesNotContain("integrated-cooperation", await Render(null));
        var html = await Render(Detail());
        Assert.Contains("integrated-unavailable", html);
        Assert.DoesNotContain("현재 나와 관련된 약속이 없어요", html);
    }

    [Fact]
    public async Task 회차_주문_검증원천을_분리하고_미확인은_빈목록성공이아니다()
    {
        var client = new Client();
        client.Integrated.Cycles = [new() { CycleId = "old", Number = 1, StatusCode = "completed", Orders = [new()
            { ProductName = "지난 주문", RootReadStatusCode = "not-visible" }] },
            new() { CycleId = "cycle", Number = 2, StatusCode = "prepared", Orders = [new()
            { ProductName = "이번 주문", RootReadStatusCode = "verified-link", Children = [new()
                { Label = "지급 자료", KindCode = "Settlement", Verified = true, Completed = true,
                    Href = "javascript:alert(1)" }] }] }];
        client.Integrated.OrdersReadStatusCode = "source-unverified";
        client.Integrated.HasIncompleteVisibility = true;
        var html = await Render(Detail(), client);
        Assert.Contains("지난 주문", html); Assert.Contains("이번 주문", html);
        Assert.True(html.IndexOf("이번 주문", StringComparison.Ordinal) < html.IndexOf("지난 주문", StringComparison.Ordinal));
        Assert.Contains("내 권한으로 원 주문을 확인할 수 없어요", html);
        Assert.Contains("지급 준비 확인 · 실제 지급 확인 별도", html);
        Assert.Contains("중복 또는 다른 회차의 연결", html);
        Assert.DoesNotContain("javascript:", html);
        Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task 조회실패와_상위판본불일치는_변경제안을_막는다()
    {
        var failed = new Client { ReservationFailure = new IOException("비공개 오류") };
        using var first = Make(failed); await first.LoadAsync(Detail());
        Assert.Null(first.Reservations); Assert.NotNull(first.ReservationError); Assert.False(first.CanPropose);
        Assert.DoesNotContain("비공개", first.ReservationError ?? "");
        var stale = new Client(); stale.Reservations.Revision = 4;
        using var second = Make(stale); await second.LoadAsync(Detail());
        Assert.False(second.SnapshotConsistent); Assert.False(second.CanAct); Assert.NotNull(second.ReservationError);
    }

    [Fact]
    public async Task 현재서버후보와_명시조건확인으로만_한국시간을_UTC로_제안한다()
    {
        var client = new Client(); using var vm = Make(client); await vm.LoadAsync(Detail());
        SelectProposal(vm, client); await vm.ProposeAsync(); Assert.Empty(client.Commands);
        vm.Draft.ConfirmNonBinding = true; await vm.ProposeAsync();
        var command = Assert.Single(client.Commands).Command;
        Assert.Equal("propose", command.Action); Assert.True(command.ConfirmNonBinding);
        Assert.Equal(3, command.ExpectedRevision); Assert.Equal(2, command.ExpectedTermsRevision);
        Assert.Equal(2, command.Terms!.Capacity); Assert.Equal(2, command.Terms.Parties.Count);
        Assert.Equal(new DateTimeOffset(2030, 1, 2, 1, 0, 0, TimeSpan.Zero), command.Terms.StartsAtUtc);
        Assert.Equal("known-source", Assert.Single(command.Terms.References).ReferenceId);
        Assert.False(vm.HasPending); Assert.Equal(4, vm.CanonicalRevision);
        Assert.Equal(1, client.CanonicalReads); Assert.Equal(2, client.IntegratedReads);
    }

    [Fact]
    public async Task 임의참여자와_임의원천은_현재허용후보로_조용히_바꾸어전송하지않는다()
    {
        var client = new Client(); using var vm = Make(client); await vm.LoadAsync(Detail());
        SelectProposal(vm, client); vm.Draft.PartyKeys.Add("other:driver"); vm.Draft.ConfirmNonBinding = true;
        await vm.ProposeAsync(); Assert.Empty(client.Commands);
        vm.Draft.PartyKeys.Remove("other:driver"); vm.Draft.ReferenceKeys.Add("InboundPlan:forged");
        await vm.ProposeAsync(); Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task 조건변경은_확인체크를_해제하고_닫힌_같은회차_종류만_대체한다()
    {
        var client = new Client();
        var old = Client.Booking(Guid.NewGuid()); old.StatusCode = "cancelled";
        var otherCycle = Client.Booking(Guid.NewGuid()); otherCycle.StatusCode = "declined"; otherCycle.Terms.CycleId = "other";
        client.Reservations.Items = [old, otherCycle];
        using var vm = Make(client); await vm.LoadAsync(Detail()); SelectProposal(vm, client);
        vm.Draft.ConfirmNonBinding = true; vm.DraftChanged(); Assert.False(vm.Draft.ConfirmNonBinding);
        Assert.Equal(old.BookingId, Assert.Single(vm.ReplacementChoices).BookingId);
        vm.SelectReplacement(otherCycle.BookingId.ToString()); Assert.Null(vm.Draft.ReplacesBookingId);
        vm.SelectReplacement(old.BookingId.ToString()); vm.Draft.ConfirmNonBinding = true;
        await vm.ProposeAsync(); Assert.Equal(old.BookingId, Assert.Single(client.Commands).Command.Terms!.ReplacesBookingId);
    }

    [Fact]
    public async Task 다른약속의_확인체크로_수락하거나_거절하지않는다()
    {
        var client = new Client(); using var vm = Make(client); await vm.LoadAsync(Detail());
        vm.ConfirmBooking(Guid.NewGuid(), true); await vm.ExecuteAsync(client.BookingId, "accept");
        Assert.Empty(client.Commands);
        vm.ConfirmBooking(client.BookingId, true); await vm.ExecuteAsync(client.BookingId, "accept");
        var command = Assert.Single(client.Commands).Command;
        Assert.Equal("accept", command.Action); Assert.Equal(1, command.ExpectedBookingRevision);
    }

    [Fact]
    public async Task 취소영향조회는_명령이아니며_현재영향과_별도확인이_필요하다()
    {
        var client = new Client(); using var vm = Make(client); await vm.LoadAsync(Detail());
        await vm.ExecuteAsync(client.BookingId, "cancel"); Assert.Empty(client.Commands);
        await vm.PreviewCancelAsync(client.BookingId); Assert.Empty(client.Commands); Assert.Equal(1, client.ImpactReads);
        await vm.ExecuteAsync(client.BookingId, "cancel"); Assert.Empty(client.Commands);
        vm.CancelConfirmed = true; vm.CancelReason = "인계 확인 후 정리"; await vm.ExecuteAsync(client.BookingId, "cancel");
        var command = Assert.Single(client.Commands).Command;
        Assert.Equal("cancel", command.Action); Assert.Equal("impact-digest", command.ImpactDigest);
        Assert.Equal(3, command.ExpectedRevision); Assert.Equal(1, command.ExpectedBookingRevision);
        Assert.Equal("인계 확인 후 정리", command.Reason);
    }

    [Fact]
    public async Task 취소영향의_판본이바뀌면_확인으로_통과시키지않는다()
    {
        var client = new Client { ImpactRevision = 9 }; using var vm = Make(client); await vm.LoadAsync(Detail());
        await vm.PreviewCancelAsync(client.BookingId); Assert.Null(vm.Impact); Assert.NotNull(vm.ImpactError);
        vm.CancelConfirmed = true; await vm.ExecuteAsync(client.BookingId, "cancel"); Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task 취소사유가_301자면_전송하지않고_확인한영향과_목록을_보존한다()
    {
        var client = new Client(); using var vm = Make(client); await vm.LoadAsync(Detail());
        await vm.PreviewCancelAsync(client.BookingId); var impact = vm.Impact; var reservations = vm.Reservations;
        vm.CancelConfirmed = true; vm.CancelReason = new('가', 301); await vm.ExecuteAsync(client.BookingId, "cancel");
        Assert.Empty(client.Commands); Assert.False(vm.HasPending); Assert.True(vm.CancelConfirmed);
        Assert.Same(impact, vm.Impact); Assert.Same(reservations, vm.Reservations);
        Assert.Contains("300자", vm.ImpactError); Assert.True(vm.CanAct);
    }

    [Fact]
    public async Task 응답유실은_원요청을_보존하고_결과조회뒤_같은번호만_직접재전송한다()
    {
        var client = new Client { CommandFailure = new IOException("비공개 응답") };
        var session = new 공간공동운영통합Session();
        using (var vm = Make(client, session: session))
        {
            await vm.LoadAsync(Detail()); vm.ConfirmBooking(client.BookingId, true); await vm.ExecuteAsync(client.BookingId, "accept");
            Assert.True(vm.HasPending); Assert.DoesNotContain("비공개", vm.Error ?? "");
            await vm.RetryPendingAsync(); Assert.Single(client.Commands);
        }
        using var next = Make(client, session: session); await next.LoadAsync(Detail());
        Assert.True(next.HasPending); Assert.Single(client.Commands);
        await next.RecoverAsync(); Assert.Single(client.Commands); Assert.True(next.CanRetryPending);
        Assert.Equal(client.Commands[0].Command.ClientRequestId, client.LastReceipt);
        client.CommandFailure = null; await next.RetryPendingAsync();
        Assert.Equal(2, client.Commands.Count);
        Assert.Equal(JsonSerializer.Serialize(client.Commands[0]), JsonSerializer.Serialize(client.Commands[1]));
        Assert.False(next.HasPending);
    }

    [Fact]
    public async Task 다른약속응답이나_현재조회실패는_원요청을_완료로_바꾸지않는다()
    {
        var client = new Client { WrongResponse = true }; using var vm = Make(client); await vm.LoadAsync(Detail());
        vm.ConfirmBooking(client.BookingId, true); await vm.ExecuteAsync(client.BookingId, "accept");
        Assert.True(vm.HasPending); Assert.Equal(0, client.CanonicalReads);
        client.WrongResponse = false; client.Receipt = client.Latest; client.CanonicalFailure = new IOException();
        await vm.RecoverAsync(); Assert.True(vm.HasPending); Assert.Equal(1, client.CanonicalReads);
    }

    [Fact]
    public async Task CAS거절은_새조회와_재확인전_재제안을_막는다()
    {
        var client = new Client { CommandFailure = new SsalddelApiException("stale", 409, "약속", "", null) };
        using var vm = Make(client); await vm.LoadAsync(Detail()); SelectProposal(vm, client); vm.Draft.ConfirmNonBinding = true;
        await vm.ProposeAsync(); Assert.False(vm.HasPending); Assert.Null(vm.Candidates); Assert.False(vm.CanAct);
        Assert.False(vm.Draft.ConfirmNonBinding); await vm.ProposeAsync(); Assert.Single(client.Commands);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 계정또는서버가_변경되면_늦은조회결과를_다른사람화면에_표시하지않는다(bool accountChanged)
    {
        var client = new Client { ReadGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var user = new User(); var scope = new Scope(user); using var vm = Make(client, user, scope);
        var loading = vm.LoadAsync(Detail());
        if (accountChanged) user.Switch("two"); else scope.Endpoint = new('b', 32);
        client.ReadGate.SetResult(client.Integrated); await loading;
        Assert.Null(vm.Integrated); Assert.False(vm.Loaded); Assert.False(vm.CanAct); Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task 다른원장으로이동하면_진행중인응답을_섞지않고_원요청은_복구대상으로남긴다()
    {
        var client = new Client { CommandGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var vm = Make(client); await vm.LoadAsync(Detail()); vm.ConfirmBooking(client.BookingId, true);
        var sending = vm.ExecuteAsync(client.BookingId, "accept");
        await vm.LoadAsync(Detail("other"));
        client.CommandGate.SetResult(Client.Booking(client.BookingId, parent: 4)); await sending;
        Assert.Equal("other", vm.Integrated!.LedgerId); Assert.True(vm.HasPending); Assert.False(vm.PendingBelongsHere);
        Assert.EndsWith("/hub", vm.PendingReturnHref); Assert.Equal(0, client.CanonicalReads);
    }

    [Fact]
    public async Task 같은원장의초안은_보존하되_소유자서버변경과_명시조건확인은_분리한다()
    {
        var client = new Client(); var user = new User(); var scope = new Scope(user); var session = new 공간공동운영통합Session();
        using var vm = Make(client, user, scope, session); await vm.LoadAsync(Detail());
        vm.Draft.StartsAtKst = new(2030, 2, 3, 14, 0, 0); vm.Draft.ConfirmNonBinding = true;
        await vm.LoadAsync(Detail("other")); await vm.LoadAsync(Detail());
        Assert.Equal(new DateTime(2030, 2, 3, 14, 0, 0), vm.Draft.StartsAtKst); Assert.False(vm.Draft.ConfirmNonBinding);
        scope.Endpoint = new('b', 32); await vm.LoadAsync(Detail());
        Assert.NotEqual(new DateTime(2030, 2, 3, 14, 0, 0), vm.Draft.StartsAtKst);
        Assert.Empty(client.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 회차필수지정은_운영담당만_명시하며_일반제안은_필수가아니다(bool isOperator)
    {
        var client = new Client(); using var vm = Make(client); var detail = Detail(); detail.IsOperator = isOperator;
        await vm.LoadAsync(detail); SelectProposal(vm, client); Assert.False(vm.Draft.RequiredForCycle);
        vm.Draft.RequiredForCycle = true; vm.Draft.ConfirmNonBinding = true; await vm.ProposeAsync();
        if (isOperator) Assert.True(Assert.Single(client.Commands).Command.Terms!.RequiredForCycle);
        else Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task 필수약속대체는_원약속의_필수지정을_유지한다()
    {
        var client = new Client(); var old = Client.Booking(Guid.NewGuid()); old.StatusCode = "cancelled"; old.Terms.RequiredForCycle = true;
        foreach (var party in old.Parties) party.IsMe = false;
        client.Reservations.Items = [old]; using var vm = Make(client);
        var detail = Detail(); detail.IsOperator = true; await vm.LoadAsync(detail); SelectProposal(vm, client);
        vm.SelectReplacement(old.BookingId.ToString()); Assert.True(vm.Draft.RequiredForCycle);
        vm.Draft.ConfirmNonBinding = true; await vm.ProposeAsync();
        Assert.True(Assert.Single(client.Commands).Command.Terms!.RequiredForCycle);
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    public async Task 전송중받은_부모판본을_완료뒤_한번만반영하고_정규조회판본은_중복조회하지않는다(long parentRevision, int expectedReads)
    {
        var client = new Client { CommandGate = new(TaskCreationOptions.RunContinuationsAsynchronously),
            RevisionForRead = read => read == 1 ? 3 : read == 2 ? 4 : parentRevision };
        var vm = Make(client); var services = new ServiceCollection().AddLogging().AddSingleton(vm);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            공간공동운영통합Panel? panel = null; var callbacks = 0;
            await renderer.RenderComponentAsync<PanelHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(PanelHost.Capture)] = (Action<공간공동운영통합Panel>)(value => panel = value),
                [nameof(PanelHost.Reload)] = (Action)(() => callbacks++) }));
            vm.ConfirmBooking(client.BookingId, true);
            var sending = (Task)typeof(공간공동운영통합Panel).GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(panel, [client.BookingId, "accept"])!;
            Assert.True(vm.IsSending);
            var updated = Detail(); updated.Revision = parentRevision;
            await panel!.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
                { [nameof(공간공동운영통합Panel.Detail)] = updated }));
            client.Latest = Client.Booking(client.BookingId, 4); client.Reservations.Items = [client.Latest];
            client.CommandGate.SetResult(client.Latest); await sending;
            Assert.Equal(parentRevision, vm.CanonicalRevision); Assert.Equal(expectedReads, client.IntegratedReads);
            await panel.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
                { [nameof(공간공동운영통합Panel.Detail)] = updated }));
            Assert.Equal(expectedReads, client.IntegratedReads); Assert.Equal(0, callbacks); Assert.Single(client.Commands);
        });
    }

    private static 공간공동운영상세Response Detail(string id = "hub") => new()
        { LedgerId = id, Revision = 3, TermsRevision = 2, Terms = new() { Title = "동네의 공동운영" }, ExecutionMode = "Operational" };
    private static 공간공동운영통합ViewModel Make(Client client, User? user = null, Scope? scope = null, 공간공동운영통합Session? session = null)
    { user ??= new(); return new(client, user, scope ?? new(user), session ?? new(), new FixedClock()); }
    private static void SelectProposal(공간공동운영통합ViewModel vm, Client client)
    {
        vm.SelectCycle("cycle"); foreach (var party in client.Candidates.Participants) vm.SelectParty(party, true);
        vm.SelectReference(client.Candidates.Cycles[0].References[0], true);
        vm.Draft.StartsAtKst = new(2030, 1, 2, 10, 0, 0); vm.Draft.EndsAtKst = new(2030, 1, 2, 11, 0, 0);
    }
    private static async Task<string> Render(공간공동운영상세Response? detail, Client? client = null)
    {
        var services = new ServiceCollection().AddLogging();
        if (client is not null) services.AddSingleton(Make(client));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<공간공동운영통합Panel>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(공간공동운영통합Panel.Detail)] = detail }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }
    private sealed class FixedClock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero); }
    private sealed class User : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림
    {
        public 현재사용자Snapshot 현재사용자 { get; private set; } = new("one", "이웃", []);
        public event Action? Changed;
        public void Switch(string? id) { 현재사용자 = new(id, "이웃", []); Changed?.Invoke(); }
    }
    private sealed class Scope(User user) : IWorkflowRecoveryContext
    {
        public string Endpoint = new('a', 32);
        public WorkflowRecoverySnapshot Capture() => new(user.현재사용자.UserId ?? throw new InvalidOperationException(), 1, Endpoint);
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => user.현재사용자.UserId is not null && snapshot == Capture();
    }
    private sealed record Sent(Guid BookingId, 공간공동운영예약명령Request Command);
    private sealed class PanelHost : ComponentBase
    {
        [Parameter] public Action<공간공동운영통합Panel>? Capture { get; set; }
        [Parameter] public Action? Reload { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<공간공동운영통합Panel>(0);
            builder.AddAttribute(1, nameof(공간공동운영통합Panel.Detail), Detail());
            builder.AddAttribute(2, nameof(공간공동운영통합Panel.ReloadRequested), EventCallback.Factory.Create(this, () => Reload?.Invoke()));
            builder.AddComponentReferenceCapture(3, component => Capture?.Invoke((공간공동운영통합Panel)component));
            builder.CloseComponent();
        }
    }
    private sealed class Client : I공간공동운영통합Client
    {
        public readonly Guid BookingId = Guid.NewGuid();
        public 공간공동운영통합Response Integrated = new() { LedgerId = "hub", Revision = 3, ExecutionMode = "Operational", AsOfUtc = DateTimeOffset.UtcNow };
        public 공간공동운영예약목록Response Reservations;
        public 공간공동운영예약후보Response Candidates = new()
        {
            LedgerId = "hub", Revision = 3, TermsRevision = 2,
            Participants = [new() { UserId = "one", RoleCode = "space-provider", IsMe = true }, new() { UserId = "two", RoleCode = "supplier" }],
            Cycles = [new() { CycleId = "cycle", Number = 1, TermsRevision = 2, References = [new() { Label = "입고 자료", Reference = new() { KindCode = "InboundPlan", ReferenceId = "known-source", ExpectedRevision = 1 } }] }]
        };
        public 공간공동운영예약Dto Latest;
        public 공간공동운영예약Dto? Receipt;
        public readonly List<Sent> Commands = [];
        public Exception? ReservationFailure, CommandFailure, CanonicalFailure;
        public TaskCompletionSource<공간공동운영통합Response>? ReadGate;
        public TaskCompletionSource<공간공동운영예약Dto?>? CommandGate;
        public Func<int, long>? RevisionForRead;
        public bool WrongResponse;
        public int CanonicalReads, IntegratedReads, ImpactReads;
        public long ImpactRevision = 3;
        public Guid? LastReceipt;
        public Client()
        {
            Latest = Booking(BookingId);
            Reservations = new() { LedgerId = "hub", Revision = 3, TermsRevision = 2, CanPropose = true, Items = [Latest] };
        }
        public static 공간공동운영예약Dto Booking(Guid id, long parent = 3) => new()
        {
            LedgerId = "hub", ParentRevision = parent, BookingId = id, Revision = parent - 2, CooperationTermsRevision = 2,
            Terms = new() { CycleId = "cycle", KindCode = "inbound", StartsAtUtc = new(2030, 1, 2, 1, 0, 0, TimeSpan.Zero), EndsAtUtc = new(2030, 1, 2, 2, 0, 0, TimeSpan.Zero) },
            Parties = [new() { UserId = "one", RoleCode = "space-provider", IsMe = true }, new() { UserId = "two", RoleCode = "supplier" }],
            AllowedActions = ["accept", "decline", "cancel"]
        };
        public Task<공간공동운영통합Response> ReadAsync(string id, CancellationToken ct)
        {
            IntegratedReads++; if (ReadGate is not null) return ReadGate.Task;
            var value = Copy(Integrated); value.LedgerId = id;
            if (RevisionForRead is not null) value.Revision = Reservations.Revision = Candidates.Revision = RevisionForRead(IntegratedReads);
            return Task.FromResult(value);
        }
        public Task<공간공동운영예약목록Response> ReservationsAsync(string id, CancellationToken ct)
        {
            if (ReservationFailure is not null) return Task.FromException<공간공동운영예약목록Response>(ReservationFailure);
            var value = Copy(Reservations); value.LedgerId = id; return Task.FromResult(value);
        }
        public Task<공간공동운영예약후보Response> CandidatesAsync(string id, CancellationToken ct)
        { var value = Copy(Candidates); value.LedgerId = id; return Task.FromResult(value); }
        public Task<공간공동운영예약Dto?> ReservationAsync(string id, Guid booking, CancellationToken ct)
        { CanonicalReads++; return CanonicalFailure is not null ? Task.FromException<공간공동운영예약Dto?>(CanonicalFailure) : Task.FromResult<공간공동운영예약Dto?>(Latest); }
        public Task<공간공동운영예약Dto?> ReceiptAsync(string id, Guid request, CancellationToken ct)
        { LastReceipt = request; return Task.FromResult(Receipt); }
        public Task<공간공동운영예약취소영향Dto> ImpactAsync(string id, Guid booking, CancellationToken ct)
        {
            ImpactReads++; return Task.FromResult(new 공간공동운영예약취소영향Dto { LedgerId = id, BookingId = booking,
                ParentRevision = ImpactRevision, BookingRevision = 1, TermsRevision = 2, CanCancel = true, Digest = "impact-digest" });
        }
        public Task<공간공동운영예약Dto?> CommandAsync(string id, Guid booking, 공간공동운영예약명령Request request, CancellationToken ct)
        {
            Commands.Add(new(booking, Copy(request)));
            if (CommandGate is not null) return CommandGate.Task;
            if (CommandFailure is not null) return Task.FromException<공간공동운영예약Dto?>(CommandFailure);
            Latest = Booking(booking, 4); Integrated.Revision = Reservations.Revision = Candidates.Revision = 4;
            Reservations.Items = [Latest]; return Task.FromResult<공간공동운영예약Dto?>(WrongResponse ? Booking(Guid.NewGuid(), 4) : Latest);
        }
        private static T Copy<T>(T source) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source))!;
    }
}
