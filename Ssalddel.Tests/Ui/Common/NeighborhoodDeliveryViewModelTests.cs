using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed partial class NeighborhoodDeliveryViewModelTests
{
    [Fact]
    public async Task 익명은_동의와_주소_견적_API를_호출하지않는다()
    {
        var client = new FakeClient(); var user = new User();
        using var vm = new NeighborhoodDeliveryAuthoringViewModel(client, user);
        vm.Initialize(null); vm.CollectionConsent = true; vm.AgeConfirmed = true;
        await vm.AcceptConsentAsync(); await vm.QuoteAsync();
        Assert.Equal(0, client.ConsentCalls); Assert.Equal(0, client.QuoteCalls);
        Assert.Null(vm.ConsentEvidenceId); Assert.NotNull(vm.Error);
    }

    [Fact]
    public async Task 입력변경은_이전견적으로_등록할수없다()
    {
        var client = new FakeClient(); using var vm = await Ready(client);
        await vm.QuoteAsync(); Assert.True(vm.HasCurrentQuote);
        vm.Draft.WeightKg = 2; vm.DispatchRequested = true; vm.DirectPaymentAgreed = true;
        Assert.False(vm.HasCurrentQuote); Assert.Null(await vm.SubmitAsync()); Assert.Empty(client.Created);
    }

    [Fact]
    public async Task 미확정접수는_동일요청번호와_입력으로만_명시재시도한다()
    {
        var client = new FakeClient { Create = _ => Task.FromException<NeighborhoodDeliveryResponse?>(new InvalidOperationException("private diagnostics")) };
        using var vm = await Ready(client); await vm.QuoteAsync();
        vm.DispatchRequested = true; vm.DirectPaymentAgreed = true;
        var id = vm.Draft.ClientRequestId;
        Assert.Null(await vm.SubmitAsync()); Assert.True(vm.NeedsSubmissionReview);
        Assert.Equal("시험 물건", vm.Draft.CargoName); Assert.DoesNotContain("private diagnostics", vm.Error);
        Assert.Single(client.Created);
        client.Create = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(new() { RequestId = "request-1" });
        Assert.Equal("request-1", await vm.SubmitAsync());
        Assert.Equal(2, client.Created.Count); Assert.All(client.Created, request => Assert.Equal(id, request.ClientRequestId));
        Assert.Equal(client.Created[0].AgreedFareKrw, client.Created[1].AgreedFareKrw);
    }

    [Fact]
    public async Task 견적변경거절은_초안을유지하고_재견적행동을복구한다()
    {
        var client = new FakeClient { Create = _ => Task.FromException<NeighborhoodDeliveryResponse?>(Problem(409, "QuoteChanged")) };
        using var vm = await Ready(client); await vm.QuoteAsync();
        vm.DispatchRequested = true; vm.DirectPaymentAgreed = true; var id = vm.Draft.ClientRequestId;
        Assert.Null(await vm.SubmitAsync()); Assert.Null(vm.Quote); Assert.False(vm.NeedsSubmissionReview);
        Assert.Equal(id, vm.Draft.ClientRequestId); Assert.Equal("시험 물건", vm.Draft.CargoName);
        await vm.QuoteAsync(); Assert.True(vm.HasCurrentQuote); Assert.False(vm.DirectPaymentAgreed);
    }

    [Fact]
    public async Task 이전계정의_동의완료가_새계정의진행표시를끄지않는다()
    {
        var old = new TaskCompletionSource<신청개인정보동의증적Response?>();
        var next = new TaskCompletionSource<신청개인정보동의증적Response?>();
        var client = new FakeClient { Consent = _ => old.Task }; var user = new User("old");
        using var vm = new NeighborhoodDeliveryAuthoringViewModel(client, user);
        vm.Initialize(null); vm.CollectionConsent = true; vm.AgeConfirmed = true;
        var first = vm.AcceptConsentAsync();
        user.Current = new("next", "next", []); vm.SynchronizeOwner();
        client.Consent = _ => next.Task; vm.CollectionConsent = true; vm.AgeConfirmed = true;
        var second = vm.AcceptConsentAsync();
        old.SetResult(Consent()); await first; Assert.True(vm.IsSending); Assert.Null(vm.ConsentEvidenceId);
        var confirmed = Consent(); next.SetResult(confirmed); await second;
        Assert.False(vm.IsSending); Assert.Equal(confirmed.증적Id, vm.ConsentEvidenceId);
    }

    [Fact]
    public async Task 늦은이전상세오류가_최신선택을덮지않고_로그아웃시개인정보를비운다()
    {
        var old = new TaskCompletionSource<NeighborhoodDeliveryResponse?>();
        var client = new FakeClient { Read = id => id == "old" ? old.Task : Task.FromResult<NeighborhoodDeliveryResponse?>(new() { RequestId = id }) };
        var user = new User("owner"); using var vm = new NeighborhoodDeliveryQueryViewModel(client, user);
        var first = vm.LoadDetailAsync("old"); await vm.LoadDetailAsync("latest");
        old.SetException(new InvalidOperationException("diagnostics")); await first;
        Assert.Equal("latest", vm.Detail!.RequestId); Assert.Null(vm.Error);
        user.Current = 현재사용자Snapshot.익명; vm.SynchronizeOwner();
        Assert.Null(vm.Detail); Assert.Empty(vm.Items); Assert.False(vm.IsAuthenticated);
    }

    [Fact]
    public async Task 동의선택철회와_종료된페이지는_추가전송하지않는다()
    {
        var client = new FakeClient(); var vm = await Ready(client);
        vm.CollectionConsent = false; await vm.QuoteAsync(); Assert.Equal(0, client.QuoteCalls);
        vm.Dispose(); vm.Dispose(); Assert.Equal("", vm.Draft.Pickup.연락처.전화번호);
        Assert.Null(vm.ConsentEvidenceId);
        await vm.AcceptConsentAsync(); await vm.QuoteAsync(); Assert.Equal(0, client.QuoteCalls);
    }

    [Fact]
    public async Task 기사정보제공은_현재선정기사와차수에만동의하고_같은의뢰를재조회한다()
    {
        var detail = Assigned(); var reads = 0;
        var client = new FakeClient
        {
            Read = _ => { reads++; return Task.FromResult<NeighborhoodDeliveryResponse?>(detail); },
            Disclosure = request => { detail.DriverDisclosure!.Consented = request.Consented; return Task.FromResult<NeighborhoodDeliveryDisclosureResponse?>(detail.DriverDisclosure); }
        };
        using var vm = new NeighborhoodDeliveryQueryViewModel(client, new User("owner"));
        await vm.LoadDetailAsync("request"); vm.DisclosureAgreement = true;
        await vm.RecordDisclosureAsync(true);
        var sent = Assert.Single(client.Disclosures);
        Assert.Equal("driver-selected", sent.ConfirmedDriverId); Assert.Equal(3, sent.ExpectedRecommendationRound);
        Assert.True(sent.Consented); Assert.True(vm.Detail!.DriverDisclosure!.Consented); Assert.Equal(2, reads);
    }

    [Fact]
    public async Task 정보제공결과미확정은_동일명령번호로만재시도한다()
    {
        var detail = Assigned(); var client = new FakeClient
        {
            Read = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(detail),
            Disclosure = _ => Task.FromException<NeighborhoodDeliveryDisclosureResponse?>(new InvalidOperationException("diagnostics"))
        };
        using var vm = new NeighborhoodDeliveryQueryViewModel(client, new User("owner"));
        await vm.LoadDetailAsync("request"); vm.DisclosureAgreement = true; await vm.RecordDisclosureAsync(true);
        var first = Assert.Single(client.Disclosures).ClientRequestId;
        vm.DisclosureAgreement = true; await vm.RecordDisclosureAsync(true);
        Assert.Equal(2, client.Disclosures.Count); Assert.Equal(first, client.Disclosures[1].ClientRequestId);
    }

    [Fact]
    public async Task 정보제공응답유실후_정본재조회로동의를확인하면_철회할수있다()
    {
        var detail = Assigned(); var client = new FakeClient
        {
            Read = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(detail),
            Disclosure = _ =>
            {
                detail.DriverDisclosure!.Consented = true;
                return Task.FromException<NeighborhoodDeliveryDisclosureResponse?>(new InvalidOperationException("response lost"));
            }
        };
        using var vm = new NeighborhoodDeliveryQueryViewModel(client, new User("owner"));
        await vm.LoadDetailAsync("request"); vm.DisclosureAgreement = true; await vm.RecordDisclosureAsync(true);
        var firstId = Assert.Single(client.Disclosures).ClientRequestId;
        await vm.LoadDetailAsync("request"); Assert.True(vm.Detail!.DriverDisclosure!.Consented);
        client.Disclosure = request =>
        {
            detail.DriverDisclosure!.Consented = request.Consented;
            return Task.FromResult<NeighborhoodDeliveryDisclosureResponse?>(detail.DriverDisclosure);
        };
        await vm.RecordDisclosureAsync(false);
        Assert.Equal(2, client.Disclosures.Count); Assert.False(client.Disclosures[1].Consented);
        Assert.NotEqual(firstId, client.Disclosures[1].ClientRequestId);
        Assert.False(vm.Detail!.DriverDisclosure!.Consented); Assert.Null(vm.Error);
    }

    private static NeighborhoodDeliveryResponse Assigned() => new()
    {
        RequestId = "request", DriverDisclosure = new()
        {
            RequestId = "request", ConfirmedDriverId = "driver-selected", ConfirmedDriverName = "시험 기사", RecommendationRound = 3, CanRecordConsent = true,
            Fields = ["PickupAddress", "DropoffAddress", "LocationCoordinate", "ContactName", "PhoneNumber", "DeliveryInstructions"]
        }
    };

    [Fact]
    public async Task 배송접수뒤_협업연결응답유실은_같은배송과명령만재시도한다()
    {
        var client = new FakeClient(); var collaboration = new CollaborationClient();
        using var vm = await Ready(client, collaboration);
        await vm.LoadCollaborationAsync("collaboration-1"); await vm.QuoteAsync();
        vm.DispatchRequested = true; vm.DirectPaymentAgreed = true;
        Assert.Null(await vm.SubmitAsync()); Assert.NotNull(vm.Submitted); Assert.Single(client.Created);
        collaboration.LoseResponse = false;
        Assert.Equal("request", await vm.RetryLinkAsync());
        Assert.Single(client.Created); Assert.Equal(2, collaboration.Commands.Count);
        Assert.Equal(collaboration.Commands[0].ClientRequestId, collaboration.Commands[1].ClientRequestId);
        Assert.All(collaboration.Commands, x => Assert.Equal("request", x.LinkedDeliveryRequestId));
    }

    [Fact]
    public async Task 합의가없거나다른계정의협업이면_배송작성을막는다()
    {
        var client = new FakeClient(); var collaboration = new CollaborationClient { Detail = null };
        using var vm = await Ready(client, collaboration);
        await vm.LoadCollaborationAsync("unowned"); await vm.QuoteAsync();
        vm.DispatchRequested = true; vm.DirectPaymentAgreed = true;
        Assert.Null(await vm.SubmitAsync()); Assert.False(vm.CanWriteCollaboration); Assert.Empty(client.Created);
    }

    private sealed class CollaborationClient : INeighborhoodCollaborationClient
    {
        public NeighborhoodCollaborationResponse? Detail { get; set; } = new()
        { StableId = "collaboration-1", Revision = 4, SourcePostId = 23, AllowedActions = [NeighborhoodCollaborationActions.LinkDelivery] };
        public bool LoseResponse { get; set; } = true;
        public List<NeighborhoodCollaborationCommandRequest> Commands { get; } = [];
        public Task<NeighborhoodCollaborationResponse?> ReadAsync(string id, CancellationToken ct) => Task.FromResult(Detail);
        public Task<NeighborhoodCollaborationResponse?> CommandAsync(string id, NeighborhoodCollaborationCommandRequest request, CancellationToken ct)
        {
            Commands.Add(request);
            if (LoseResponse) return Task.FromException<NeighborhoodCollaborationResponse?>(new HttpRequestException("lost"));
            Detail!.LinkedDeliveryRequestId = request.LinkedDeliveryRequestId; return Task.FromResult(Detail);
        }
        public Task<NeighborhoodCollaborationListResponse> MineAsync(string scope, int page, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodCollaborationResponse?> CreateAsync(NeighborhoodCollaborationCreateRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodCollaborationResponse?> ReceiptAsync(Guid requestId, string? stableId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>> OpportunitiesAsync(long postId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>> PublicHistoryAsync(long postId, CancellationToken ct) => throw new NotSupportedException();
    }

    private static async Task<NeighborhoodDeliveryAuthoringViewModel> Ready(FakeClient client, INeighborhoodCollaborationClient? collaboration = null)
    {
        var vm = new NeighborhoodDeliveryAuthoringViewModel(client, new User("owner"), collaboration);
        vm.Initialize(null); vm.CollectionConsent = true; vm.AgeConfirmed = true; await vm.AcceptConsentAsync();
        vm.Draft.CargoName = "시험 물건"; vm.Draft.WeightKg = 1;
        foreach (var location in new[] { vm.Draft.Pickup, vm.Draft.Dropoff })
        { location.주소.도로명주소 = "시험로 10"; location.연락처.이름 = "시험 이용자"; location.연락처.전화번호 = "01000000000"; }
        return vm;
    }
    private static 신청개인정보동의증적Response Consent() => new() { 증적Id = Guid.NewGuid(), 상태Code = 신청개인정보동의상태Codes.유효 };
    private static SsalddelApiException Problem(int status, string code) => new("diagnostics", status, "test", "", null, errorCode: code);
    private sealed class User(string? id = null) : ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot Current { get; set; } = id is null ? 현재사용자Snapshot.익명 : new(id, id, []);
        public 현재사용자Snapshot 현재사용자 => Current;
    }
    private sealed class FakeClient : INeighborhoodDeliveryClient
    {
        public Func<신청개인정보동의기록Request, Task<신청개인정보동의증적Response?>> Consent { get; set; } = _ => Task.FromResult<신청개인정보동의증적Response?>(NeighborhoodDeliveryViewModelTests.Consent());
        public Func<NeighborhoodDeliveryRequest, Task<NeighborhoodDeliveryResponse?>> Create { get; set; } = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(new() { RequestId = "request" });
        public Func<string, Task<NeighborhoodDeliveryResponse?>> Read { get; set; } = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(null);
        public Func<NeighborhoodDeliveryDisclosureRequest, Task<NeighborhoodDeliveryDisclosureResponse?>> Disclosure { get; set; } = _ => Task.FromResult<NeighborhoodDeliveryDisclosureResponse?>(null);
        public List<NeighborhoodDeliveryRequest> Created { get; } = [];
        public List<NeighborhoodDeliveryDisclosureRequest> Disclosures { get; } = [];
        public int ConsentCalls; public int QuoteCalls;
        public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct) { ConsentCalls++; return Consent(request); }
        public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
        { QuoteCalls++; return Task.FromResult<NeighborhoodDeliveryQuoteResponse?>(new() { Fare = new() { 최종운임 = 4000, 예상거리Km = 2 } }); }
        public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) { Created.Add(request); return Create(request); }
        public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodDeliveryResponse>>([]);
        public Task<NeighborhoodDeliveryResponse?> ReadAsync(string requestId, CancellationToken ct) => Read(requestId);
        public Task<NeighborhoodDeliveryDisclosureResponse?> RecordDisclosureAsync(string requestId, NeighborhoodDeliveryDisclosureRequest request, CancellationToken ct)
        { Disclosures.Add(request); return Disclosure(request); }
    }
}
