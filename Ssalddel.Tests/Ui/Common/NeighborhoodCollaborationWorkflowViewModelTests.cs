using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodCollaborationWorkflowViewModelTests
{
    [Fact]
    public async Task 익명은_협업을_신청하거나_상태를_변경하지_않는다()
    {
        var client = new CollaborationClient(); var session = new NeighborhoodCollaborationDraftSession(); var user = new User();
        using var author = new NeighborhoodCollaborationAuthoringViewModel(client, new Posts(), new StorageClient(), session, user);
        await author.InitializeAsync(1, null); Assert.Null(await author.SubmitAsync());
        using var query = new NeighborhoodCollaborationQueryViewModel(client, session, user);
        await query.LoadDetailAsync("work"); await query.ExecuteAsync(NeighborhoodCollaborationActions.Start);
        Assert.Empty(client.Created); Assert.Empty(client.Commands); Assert.Equal(0, client.ReadCalls);
    }

    [Fact]
    public async Task 보관_신청은_제공판본과_공간단위_사용자수량을_보존한다()
    {
        var client = new CollaborationClient(); using var vm = await Ready(client);
        vm.Form.Draft.Terms.Quantity = 2;
        Assert.Equal("work", await vm.SubmitAsync());
        var request = Assert.Single(client.Created);
        Assert.Equal(7, request.ExpectedStorageRevision); Assert.Equal(2, request.Terms.StorageQuantity);
        Assert.Equal("상자", request.Terms.StorageUnit); Assert.Equal(request.Terms.FromUtc, request.Terms.StorageFromUtc);
    }

    [Fact]
    public async Task 요약이_서버한도보다_길면_신청하지_않는다()
    {
        var client = new CollaborationClient(); using var vm = await Ready(client);
        vm.Form.Draft.Terms.Summary = new string('가', 121);
        Assert.Null(await vm.SubmitAsync()); Assert.Empty(client.Created); Assert.Contains("120", vm.Error);
    }

    [Fact]
    public async Task 모호한_신청은_결과조회만_하고_명시재시도에_원래입력과번호를_쓴다()
    {
        var client = new CollaborationClient { Create = _ => Task.FromException<NeighborhoodCollaborationResponse?>(new IOException("private diagnostics")) };
        var session = new NeighborhoodCollaborationDraftSession(); var user = new User("owner");
        using var first = await Ready(client, session, user);
        Assert.Null(await first.SubmitAsync()); var original = Assert.Single(client.Created);
        first.Dispose();
        using var next = new NeighborhoodCollaborationAuthoringViewModel(client, new Posts(), new StorageClient(), session, user);
        await next.InitializeAsync(2, null); Assert.True(next.NeedsReview); Assert.Single(client.Created);
        Assert.Null(await next.RecoverAsync()); Assert.Single(client.Created); Assert.Equal(original.ClientRequestId, client.LastReceipt);
        next.Form.Draft.Terms.Summary = "수정된 입력";
        client.Create = _ => Task.FromResult<NeighborhoodCollaborationResponse?>(Work());
        Assert.Equal("work", await next.SubmitAsync()); Assert.Equal(2, client.Created.Count);
        Assert.Equal(original.ClientRequestId, client.Created[1].ClientRequestId); Assert.Equal("보관 신청", client.Created[1].Terms.Summary);
        Assert.DoesNotContain("private diagnostics", next.Error ?? "");
    }

    [Fact]
    public async Task 계정변경_후_늦은_신청결과는_새계정_초안과진행표시를_건드리지_않는다()
    {
        var old = new TaskCompletionSource<NeighborhoodCollaborationResponse?>(); var next = new TaskCompletionSource<NeighborhoodCollaborationResponse?>();
        var client = new CollaborationClient { Create = _ => old.Task }; var user = new User("old");
        using var vm = await Ready(client, user: user); var first = vm.SubmitAsync();
        user.Current = new("new", "다른 사람", []); vm.SynchronizeOwner();
        await vm.InitializeAsync(null, "space"); Fill(vm); client.Create = _ => next.Task;
        var second = vm.SubmitAsync(); old.SetResult(Work()); Assert.Null(await first); Assert.True(vm.IsSending);
        next.SetResult(Work("new-work")); Assert.Equal("new-work", await second); Assert.False(vm.IsSending);
    }

    [Fact]
    public async Task 페이지_종료_후_신청응답은_화면에_반영하지_않고_접수번호는_복구용으로_유지한다()
    {
        var delayed = new TaskCompletionSource<NeighborhoodCollaborationResponse?>(); var client = new CollaborationClient { Create = _ => delayed.Task };
        var session = new NeighborhoodCollaborationDraftSession(); var vm = await Ready(client, session);
        var sending = vm.SubmitAsync(); vm.Dispose(); delayed.SetResult(Work());
        Assert.Null(await sending); Assert.NotNull(session.PendingCreate);
        Assert.Null(await vm.SubmitAsync()); Assert.Single(client.Created);
    }

    [Fact]
    public async Task 서버가_허용하지_않은_협업행동은_전송하지_않는다()
    {
        var client = new CollaborationClient { Read = _ => Task.FromResult<NeighborhoodCollaborationResponse?>(Work()) };
        using var vm = new NeighborhoodCollaborationQueryViewModel(client, new(), new User("owner"));
        await vm.LoadDetailAsync("work"); await vm.ExecuteAsync(NeighborhoodCollaborationActions.Start);
        Assert.Empty(client.Commands); Assert.Null(vm.PrimaryAction); Assert.NotNull(vm.Error);
    }

    [Fact]
    public async Task 보관_조건동의는_인계정보제공_확인과_문안판본이_필수다()
    {
        var client = new CollaborationClient { Read = _ => Task.FromResult<NeighborhoodCollaborationResponse?>(Work(actions: [NeighborhoodCollaborationActions.Agree])) };
        using var vm = new NeighborhoodCollaborationQueryViewModel(client, new(), new User("owner"));
        await vm.LoadDetailAsync("work"); await vm.ExecuteAsync(NeighborhoodCollaborationActions.Agree); Assert.Empty(client.Commands);
        vm.HandoverPrivacyConfirmed = true; await vm.ExecuteAsync(NeighborhoodCollaborationActions.Agree);
        Assert.Equal(NeighborhoodCollaborationAgreementNotice.Version, Assert.Single(client.Commands).PrivacyNoticeVersion);
        Assert.False(vm.HandoverPrivacyConfirmed);
    }

    [Fact]
    public async Task 참여자의_기록공개선택은_본인선택값이며_완료확인과_별개다()
    {
        var value = Work(actions: [NeighborhoodCollaborationActions.PublicHistoryConsent]); value.MyPublicHistoryConsented = true; value.RequesterPublicHistoryConsented = false;
        var client = new CollaborationClient { Read = _ => Task.FromResult<NeighborhoodCollaborationResponse?>(value) };
        using var vm = new NeighborhoodCollaborationQueryViewModel(client, new(), new User("helper"));
        await vm.LoadDetailAsync("work"); Assert.True(vm.PublicRecordConsent); Assert.Null(vm.PrimaryAction);
        await vm.ExecuteAsync(NeighborhoodCollaborationActions.PublicHistoryConsent, consented: false);
        Assert.False(Assert.Single(client.Commands).Consented); Assert.Null(client.Commands[0].PrivacyNoticeVersion);
    }

    [Fact]
    public async Task 과거_상세응답과_오류는_최신선택을_덮지_않고_로그아웃하면_지운다()
    {
        var delayed = new TaskCompletionSource<NeighborhoodCollaborationResponse?>(); var user = new User("owner");
        var client = new CollaborationClient { Read = id => id == "old" ? delayed.Task : Task.FromResult<NeighborhoodCollaborationResponse?>(Work(id)) };
        using var vm = new NeighborhoodCollaborationQueryViewModel(client, new(), user);
        var first = vm.LoadDetailAsync("old"); await vm.LoadDetailAsync("latest"); delayed.SetException(new IOException()); await first;
        Assert.Equal("latest", vm.Detail?.StableId); Assert.Null(vm.Error);
        user.Current = 현재사용자Snapshot.익명; vm.SynchronizeOwner(); Assert.Null(vm.Detail); Assert.Empty(vm.Items);
    }

    [Fact]
    public async Task 공개_참여는_사적상세조회_없이_현재공개판본으로_명시접수한다()
    {
        var client = new CollaborationClient(); using var vm = new NeighborhoodCollaborationSourceViewModel(client, new MapClient(), new(), new User("helper"));
        await vm.LoadAsync(1); Assert.Equal("work", await vm.ParticipateAsync("work"));
        Assert.Equal(0, client.ReadCalls); var command = Assert.Single(client.Commands);
        Assert.Equal(15, command.ExpectedRevision); Assert.Equal(NeighborhoodCollaborationActions.RequestParticipation, command.Action);
        Assert.NotEqual(Guid.Empty, command.ClientRequestId);
    }

    [Fact]
    public async Task 익명_공개완료이력은_읽을수_있지만_참여는_전송하지_않는다()
    {
        var client = new CollaborationClient(); using var vm = new NeighborhoodCollaborationSourceViewModel(client, new MapClient(), new(), new User());
        await vm.LoadAsync(1); Assert.Single(vm.PublicHistory); Assert.Equal("시험 동네", vm.RegionLabel("region"));
        Assert.Null(await vm.ParticipateAsync("work")); Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task 공개협업_늦은응답은_다른원글이나_종료된화면에_복구되지_않는다()
    {
        var delayed = new TaskCompletionSource<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>>();
        var client = new CollaborationClient { Opportunities = id => id == 1 ? delayed.Task : Task.FromResult<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>>([]) };
        using var vm = new NeighborhoodCollaborationSourceViewModel(client, new MapClient(), new(), new User());
        var first = vm.LoadAsync(1); await vm.LoadAsync(2); delayed.SetResult([new() { StableId = "old", SourcePostId = 1 }]); await first;
        Assert.Equal(2, vm.PostId); Assert.Empty(vm.Opportunities); Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task 보관_익명_공개상세는_개인인계_API를_호출하지_않는다()
    {
        var client = new StorageClient(); using var vm = new NeighborhoodStorageQueryViewModel(client, new CollaborationClient(), new(), new User());
        await vm.LoadDetailAsync("space", "work"); Assert.NotNull(vm.Public); Assert.Null(vm.Private); Assert.Equal(0, client.PrivateCalls);
        await vm.ReserveAsync(); Assert.Empty(client.Reservations);
    }

    [Fact]
    public async Task 본인공간_인계정보는_계정전환_즉시_제거하며_늦은응답도_차단한다()
    {
        var delayed = new TaskCompletionSource<NeighborhoodStorageSpaceDto?>(); var user = new User("owner");
        var client = new StorageClient { Private = _ => delayed.Task };
        using var vm = new NeighborhoodStorageQueryViewModel(client, new CollaborationClient(), new(), user);
        var first = vm.LoadDetailAsync("space", null); user.Current = new("other", "다른 계정", []); vm.SynchronizeOwner();
        delayed.SetResult(Space(privateAddress: "개인 주소")); await first;
        Assert.Null(vm.Private); Assert.Null(vm.Public); Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task 인계는_서버_허용행동만_전송하고_공간의_현재CAS판본을_쓴다()
    {
        var client = new StorageClient { Private = _ => Task.FromResult<NeighborhoodStorageSpaceDto?>(Space(reservations: [new() { CollaborationId = "work", AllowedActions = ["confirm-return"] }])) };
        using var vm = new NeighborhoodStorageQueryViewModel(client, new CollaborationClient(), new(), new User("owner"));
        await vm.LoadDetailAsync("space", null); await vm.HandoverAsync("work", "confirm-intake"); Assert.Empty(client.Handovers);
        await vm.HandoverAsync("work", "confirm-return"); Assert.Equal(11, Assert.Single(client.Handovers).ExpectedRevision);
    }

    [Fact]
    public async Task 모호한_예약은_복구조회에_같은번호와_협업문맥을_전달한다()
    {
        var client = new StorageClient { Private = _ => Task.FromResult<NeighborhoodStorageSpaceDto?>(Space(owner: false)),
            Reserve = _ => Task.FromException<NeighborhoodStorageSpaceDto?>(new IOException()) };
        var collaboration = Work(); collaboration.OwnerAgreed = true; collaboration.RequesterAgreed = true; collaboration.StatusCode = "agreed";
        var work = new CollaborationClient { Read = _ => Task.FromResult<NeighborhoodCollaborationResponse?>(collaboration) };
        using var vm = new NeighborhoodStorageQueryViewModel(client, work, new(), new User("requester"));
        await vm.LoadDetailAsync("space", "work"); await vm.ReserveAsync(); var reservation = Assert.Single(client.Reservations);
        Assert.True(vm.HasPending); await vm.RecoverAsync(); Assert.Single(client.Reservations);
        Assert.Equal(reservation.RequestId, client.LastReceipt); Assert.Equal("work", client.ReceiptCollaboration);
    }

    [Fact]
    public async Task 취소된_예약접수결과는_저장성공으로_안내하지_않는다()
    {
        var pending = new NeighborhoodStorageDraftSession(); pending.Bind("owner");
        pending.PendingTarget = "space"; pending.PendingRequestId = Guid.NewGuid(); pending.PendingAction = "reserve"; pending.PendingCollaboration = "work";
        var client = new StorageClient { Receipt = () => Task.FromResult<NeighborhoodStorageSpaceDto?>(new() { SpaceId = "space", Public = PublicSpace(), RequestOutcome = "aborted" }) };
        using var vm = new NeighborhoodStorageQueryViewModel(client, new CollaborationClient(), pending, new User("owner"));
        Assert.Null(await vm.RecoverAsync()); Assert.False(vm.HasPending); Assert.Null(vm.Notice); Assert.Contains("예약하지 못", vm.Error);
    }

    [Fact]
    public async Task 재시작후_본인_미확정예약은_서버원래번호로_명시조회만_한다()
    {
        var originalId = Guid.NewGuid(); var work = Work(); work.MyPendingStorageRequestId = originalId; work.MyPendingStorageSpaceId = "space";
        work.OwnerAgreed = true; work.RequesterAgreed = true; work.StatusCode = "agreed";
        var client = new StorageClient { Private = _ => Task.FromResult<NeighborhoodStorageSpaceDto?>(Space(owner: false)) };
        var collaboration = new CollaborationClient { Read = _ => Task.FromResult<NeighborhoodCollaborationResponse?>(work) };
        using var vm = new NeighborhoodStorageQueryViewModel(client, collaboration, new(), new User("requester"));
        await vm.LoadDetailAsync("space", "work"); Assert.True(vm.HasPending); Assert.False(vm.CanReserve); Assert.False(vm.CanRetryMutation);
        Assert.Empty(client.Reservations); await vm.RecoverAsync(); Assert.Equal(originalId, client.LastReceipt);
        Assert.Equal("work", client.ReceiptCollaboration); Assert.Empty(client.Reservations);
    }

    [Fact]
    public async Task 저장_미확인_후_명시재시도는_주소와요청번호를_보존한다()
    {
        var client = new StorageClient { Save = _ => Task.FromException<NeighborhoodStorageSpaceDto?>(new IOException("raw private")) };
        var session = new NeighborhoodStorageDraftSession(); using var vm = new NeighborhoodStorageAuthoringViewModel(client, new MapClient(), session, new User("owner"));
        await vm.InitializeAsync(null); FillSpace(vm); Assert.Null(await vm.SaveAsync()); var original = Assert.Single(client.Saved);
        await vm.RecoverAsync(); Assert.Single(client.Saved); vm.Form.Address = "새 주소";
        client.Save = _ => Task.FromResult<NeighborhoodStorageSpaceDto?>(Space()); Assert.Equal("space", await vm.SaveAsync());
        Assert.Equal(original.RequestId, client.Saved[1].RequestId); Assert.Equal("개인 주소", client.Saved[1].PrivateAddress);
    }

    [Fact]
    public async Task 공간초안과_미확인쓰기의_개인주소는_로그아웃시_초기화한다()
    {
        var user = new User("owner"); var session = new NeighborhoodStorageDraftSession();
        var client = new StorageClient { Save = _ => Task.FromException<NeighborhoodStorageSpaceDto?>(new IOException()) };
        using var vm = new NeighborhoodStorageAuthoringViewModel(client, new MapClient(), session, user);
        await vm.InitializeAsync(null); FillSpace(vm); await vm.SaveAsync(); Assert.NotNull(session.PendingSave);
        user.Current = 현재사용자Snapshot.익명; vm.SynchronizeOwner(); Assert.Equal("", session.Address); Assert.Null(session.PendingSave); Assert.False(vm.IsAuthenticated);
    }

    [Fact]
    public async Task 보관지도는_집좌표대신_공식대표점을_쓰고_기존동네핀과_합쳐진다()
    {
        var client = new MapClient { Storage = () => Task.FromResult<NeighborhoodStorageMapResponse?>(new() { Items = [new() { Region = new() { RegionKey = "region", Latitude = 38, Longitude = 128 }, SpaceCount = 2 }, new() { Region = new() { RegionKey = "unknown" }, SpaceCount = 9 }] }) };
        using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), new User());
        await vm.InitializeAsync("offer,storage", null, "region"); var marker = Assert.Single(vm.RenderState.Markers);
        Assert.Equal("region:region", marker.Id); Assert.Equal(37.5, marker.Latitude); Assert.Equal(127.1, marker.Longitude);
        Assert.Equal(3, marker.Count); Assert.Equal(2, vm.SelectedStorageCount);
    }

    [Fact]
    public async Task 보관레이어를_끄면_늦게온_지도응답이_복구되지_않는다()
    {
        var delayed = new TaskCompletionSource<NeighborhoodStorageMapResponse?>(); var client = new MapClient { Storage = () => delayed.Task };
        using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), new User());
        await vm.InitializeAsync("none", null, null); var first = vm.ToggleLayerAsync(NeighborhoodMapNavigation.Storage);
        await vm.ToggleLayerAsync(NeighborhoodMapNavigation.Storage); delayed.SetResult(new() { Items = [new() { Region = Region(), SpaceCount = 1 }] }); await first;
        Assert.Empty(vm.StorageMarkers); Assert.Empty(vm.RenderState.Markers); Assert.False(vm.StorageLoading);
    }

    private static async Task<NeighborhoodCollaborationAuthoringViewModel> Ready(CollaborationClient client, NeighborhoodCollaborationDraftSession? session = null, User? user = null)
    {
        var vm = new NeighborhoodCollaborationAuthoringViewModel(client, new Posts(), new StorageClient(), session ?? new(), user ?? new("owner"));
        await vm.InitializeAsync(null, "space"); Fill(vm); return vm;
    }
    private static void Fill(NeighborhoodCollaborationAuthoringViewModel vm) { vm.Form.Draft.Terms.Summary = "보관 신청"; vm.Form.ConditionsConfirmed = true; }
    private static void FillSpace(NeighborhoodStorageAuthoringViewModel vm) { vm.Form.Title = "상자 보관"; vm.Form.RegionKey = "region"; vm.Form.Address = "개인 주소"; vm.Form.Contact = "010-0000-0000"; vm.Form.PrivacyConfirmed = true; }
    private static NeighborhoodPublicRegionDto Region() => new() { RegionKey = "region", DisplayName = "시험 동네", Latitude = 37.5, Longitude = 127.1 };
    private static NeighborhoodStoragePublicDto PublicSpace() => new() { SpaceId = "space", Revision = 11, OfferRevision = 7, PublicTitle = "공간", Region = Region(), CapacityQuantity = 3, CapacityUnit = "상자", AvailableFromUtc = DateTimeOffset.Now.AddHours(1), AvailableUntilUtc = DateTimeOffset.Now.AddDays(1) };
    private static NeighborhoodStorageSpaceDto Space(bool owner = true, string privateAddress = "", IReadOnlyList<NeighborhoodStorageReservationDto>? reservations = null) => new() { SpaceId = "space", Revision = 11, Public = PublicSpace(), Status = "published", IsOwner = owner, PrivateAddress = privateAddress, Reservations = reservations ?? [] };
    private static NeighborhoodCollaborationResponse Work(string id = "work", IReadOnlyList<string>? actions = null) => new() { StableId = id, Revision = 15, Kind = "storage", Terms = new() { Summary = "약속", StorageSpaceId = "space" }, AllowedActions = actions ?? [] };
    private sealed class User(string? id = null) : ISsalddel현재사용자Context { public 현재사용자Snapshot Current { get; set; } = id is null ? 현재사용자Snapshot.익명 : new(id, id, []); public 현재사용자Snapshot 현재사용자 => Current; }
    private sealed class CollaborationClient : INeighborhoodCollaborationClient
    {
        public List<NeighborhoodCollaborationCreateRequest> Created { get; } = []; public List<NeighborhoodCollaborationCommandRequest> Commands { get; } = [];
        public int ReadCalls; public Guid? LastReceipt;
        public Func<NeighborhoodCollaborationCreateRequest, Task<NeighborhoodCollaborationResponse?>> Create { get; set; } = _ => Task.FromResult<NeighborhoodCollaborationResponse?>(Work());
        public Func<string, Task<NeighborhoodCollaborationResponse?>> Read { get; set; } = id => Task.FromResult<NeighborhoodCollaborationResponse?>(Work(id));
        public Func<long, Task<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>>> Opportunities { get; set; } = id => Task.FromResult<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>>([new() { SourcePostId = id, StableId = "work", Revision = 15 }]);
        public Task<NeighborhoodCollaborationListResponse> MineAsync(string scope, int page, CancellationToken ct) => Task.FromResult(new NeighborhoodCollaborationListResponse());
        public Task<NeighborhoodCollaborationResponse?> ReadAsync(string id, CancellationToken ct) { ReadCalls++; return Read(id); }
        public Task<NeighborhoodCollaborationResponse?> CreateAsync(NeighborhoodCollaborationCreateRequest request, CancellationToken ct) { Created.Add(request); return Create(request); }
        public Task<NeighborhoodCollaborationResponse?> CommandAsync(string id, NeighborhoodCollaborationCommandRequest request, CancellationToken ct) { Commands.Add(request); return Task.FromResult<NeighborhoodCollaborationResponse?>(Work(id)); }
        public Task<NeighborhoodCollaborationResponse?> ReceiptAsync(Guid requestId, string? stableId, CancellationToken ct) { LastReceipt = requestId; return Task.FromResult<NeighborhoodCollaborationResponse?>(null); }
        public Task<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>> OpportunitiesAsync(long postId, CancellationToken ct) => Opportunities(postId);
        public Task<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>> PublicHistoryAsync(long postId, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>>([new() { Kind = "goods", PublicNeighborhoodRegionKey = "region", CompletedOnUtc = DateTime.UtcNow }]);
    }
    private sealed class StorageClient : INeighborhoodStorageClient
    {
        public List<NeighborhoodStorageSpaceRequest> Saved { get; } = []; public List<NeighborhoodStorageReservationRequest> Reservations { get; } = []; public List<NeighborhoodStorageReservationActionRequest> Handovers { get; } = [];
        public int PrivateCalls; public Guid? LastReceipt; public string? ReceiptCollaboration;
        public Func<string, Task<NeighborhoodStorageSpaceDto?>> Private { get; set; } = _ => Task.FromResult<NeighborhoodStorageSpaceDto?>(Space());
        public Func<NeighborhoodStorageSpaceRequest, Task<NeighborhoodStorageSpaceDto?>> Save { get; set; } = _ => Task.FromResult<NeighborhoodStorageSpaceDto?>(Space());
        public Func<NeighborhoodStorageReservationRequest, Task<NeighborhoodStorageSpaceDto?>> Reserve { get; set; } = _ => Task.FromResult<NeighborhoodStorageSpaceDto?>(Space());
        public Func<Task<NeighborhoodStorageSpaceDto?>> Receipt { get; set; } = () => Task.FromResult<NeighborhoodStorageSpaceDto?>(null);
        public Task<NeighborhoodStorageListResponse> ListAsync(string? region, int page, CancellationToken ct) => Task.FromResult(new NeighborhoodStorageListResponse());
        public Task<IReadOnlyList<NeighborhoodStorageSpaceDto>> MineAsync(int page, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodStorageSpaceDto>>([]);
        public Task<NeighborhoodStoragePublicDto?> PublicAsync(string id, CancellationToken ct) => Task.FromResult<NeighborhoodStoragePublicDto?>(PublicSpace());
        public Task<NeighborhoodStorageSpaceDto?> PrivateAsync(string id, string? collaborationId, CancellationToken ct) { PrivateCalls++; return Private(id); }
        public Task<NeighborhoodStorageSpaceDto?> SaveAsync(string? id, NeighborhoodStorageSpaceRequest request, CancellationToken ct) { Saved.Add(request); return Save(request); }
        public Task<NeighborhoodStorageSpaceDto?> StateAsync(string id, string action, NeighborhoodStorageMutationRequest request, CancellationToken ct) => Task.FromResult<NeighborhoodStorageSpaceDto?>(Space());
        public Task<NeighborhoodStorageSpaceDto?> ReserveAsync(string id, NeighborhoodStorageReservationRequest request, CancellationToken ct) { Reservations.Add(request); return Reserve(request); }
        public Task<NeighborhoodStorageSpaceDto?> HandoverAsync(string id, string collaborationId, NeighborhoodStorageReservationActionRequest request, CancellationToken ct) { Handovers.Add(request); return Task.FromResult<NeighborhoodStorageSpaceDto?>(Space()); }
        public Task<NeighborhoodStorageSpaceDto?> ReceiptAsync(Guid requestId, string? spaceId, CancellationToken ct, string? collaborationId = null) { LastReceipt = requestId; ReceiptCollaboration = collaborationId; return Receipt(); }
    }
    private sealed class Posts : INeighborhoodExchangeClient
    {
        public Task<PlatformCommunityPostResponse?> ReadAsync(long id, CancellationToken ct) => Task.FromResult<PlatformCommunityPostResponse?>(new() { Id = id, Title = "교류 글", WorkflowTag = NeighborhoodExchange.WorkflowTag, RoleTag = NeighborhoodExchange.Offer, Category = PlatformCommunityPostCategories.General });
        public Task<PlatformCommunityPostListResponse> ListAsync(string? intent, int page, CancellationToken ct) => Task.FromResult(new PlatformCommunityPostListResponse());
        public Task<PlatformCommunityPostResponse?> PublishAsync(PlatformCommunityPostCreateRequest request, CancellationToken ct) => Task.FromResult<PlatformCommunityPostResponse?>(null);
        public Task<IReadOnlyList<PlatformCommunityPostCommentResponse>> CommentsAsync(long id, CancellationToken ct) => Task.FromResult<IReadOnlyList<PlatformCommunityPostCommentResponse>>([]);
        public Task<PlatformCommunityPostCommentResponse?> CommentAsync(long id, PlatformCommunityPostCommentCreateRequest request, CancellationToken ct) => Task.FromResult<PlatformCommunityPostCommentResponse?>(null);
        public Task DeleteAsync(long id, string? password, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteCommentAsync(long id, long commentId, string password, CancellationToken ct) => Task.CompletedTask;
        public Task ReportCommentAsync(long commentId, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class MapClient : INeighborhoodExchangeMapClient
    {
        public Func<Task<NeighborhoodStorageMapResponse?>> Storage { get; set; } = () => Task.FromResult<NeighborhoodStorageMapResponse?>(new());
        public Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct) => Task.FromResult(new NeighborhoodExchangeRegionListResponse { Items = [Region()] });
        public Task<NeighborhoodExchangeMapResponse> MapAsync(string? intent, CancellationToken ct) => Task.FromResult(new NeighborhoodExchangeMapResponse { Items = [new() { Region = Region(), OfferCount = 1 }] });
        public Task<PlatformCommunityPostListResponse> PostsAsync(string? regionKey, string? intent, int page, CancellationToken ct) => Task.FromResult(new PlatformCommunityPostListResponse());
        public Task<NeighborhoodMapDeliveryPage> MineAsync(int page, CancellationToken ct) => Task.FromResult(new NeighborhoodMapDeliveryPage([], false));
        public Task<NeighborhoodDeliveryMapResponse?> DeliveryMapAsync(string requestId, bool includeRoute, CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(null);
        public Task<NeighborhoodStorageMapResponse?> StorageAsync(CancellationToken ct) => Storage();
    }
    private sealed class Preferences : INeighborhoodMapPreferenceStore
    {
        public Task<NeighborhoodMapPreferences?> LoadAsync(string? ownerId, CancellationToken cancellationToken = default) => Task.FromResult<NeighborhoodMapPreferences?>(null);
        public Task SaveAsync(string? ownerId, NeighborhoodMapPreferences preferences, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
