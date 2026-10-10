using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodMemberDeliveryUiTests
{
    [Fact]
    public async Task 이전요청의늦은종결이_다음요청의복구정보를_지우지않는다()
    {
        var session = new NeighborhoodMemberDeliverySession(new VolatileNeighborhoodMemberPendingStore());
        await session.BindAsync("member", CancellationToken.None);
        var first = new NeighborhoodMemberCommandRequest { ClientRequestId = Guid.NewGuid() };
        await session.PrepareAsync("member", "pickup", "work", first.ClientRequestId, 1, first, CancellationToken.None);
        await session.ClearAsync("member", CancellationToken.None, first.ClientRequestId);
        var next = new NeighborhoodMemberCommandRequest { ClientRequestId = Guid.NewGuid() };
        await session.PrepareAsync("member", "deliver", "work", next.ClientRequestId, 2, next, CancellationToken.None);
        await session.ClearAsync("member", CancellationToken.None, first.ClientRequestId);
        Assert.Equal(next.ClientRequestId, session.Pending!.ClientRequestId);
    }
    [Fact]
    public async Task 같은계정의_동시명령은_하나의_원요청으로_보관한다()
    {
        var store = new DelayedSaveStore(); var session = new NeighborhoodMemberDeliverySession(store);
        await session.BindAsync("member", CancellationToken.None);
        var first = new NeighborhoodMemberCommandRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 1 };
        var second = new NeighborhoodMemberCommandRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 1 };
        var sending = session.PrepareAsync("member", "pickup", "work", first.ClientRequestId, 1, first, CancellationToken.None);
        await store.Started.Task;
        var concurrent = session.PrepareAsync("member", "pickup", "work", second.ClientRequestId, 1, second, CancellationToken.None);
        store.Release.SetResult();
        Assert.Equal(first.ClientRequestId, (await sending).ClientRequestId);
        Assert.Equal(first.ClientRequestId, (await concurrent).ClientRequestId);
        Assert.Equal(1, store.SaveCount);
    }
    [Fact]
    public async Task 익명은_API와_GPS를_호출하지않는다()
    {
        var f = new Fixture { UserId = null }; using var vm = f.Participation();
        await vm.LoadAsync(); await vm.SelectCurrentLocationAsync(); await vm.SaveAsync();
        Assert.Empty(f.Calls); Assert.Equal(0, f.LocationCalls); Assert.True(vm.RequiresLogin);
    }
    [Fact]
    public async Task 기본수신OFF이고_조회로_가입_GPS_배차를_실행하지않는다()
    {
        var f = new Fixture(); using var vm = f.Participation(); await vm.LoadAsync();
        Assert.False(vm.Draft.ReceiveOffers); Assert.Equal(3, vm.Draft.RadiusKm); Assert.Equal(0, f.LocationCalls);
        Assert.Equal(["settings-get"], f.Calls);
    }
    [Fact]
    public async Task 비공개기준점선택은_저장할때까지_서버에_보내지않는다()
    {
        var f = new Fixture(); using var vm = f.Participation(); await vm.LoadAsync(); await vm.SelectCurrentLocationAsync();
        Assert.True(vm.HasActivityPoint); Assert.Equal(1, f.LocationCalls); Assert.DoesNotContain("settings-put", f.Calls);
        vm.Draft.ActivityLabel = "동네"; vm.Draft.ReceiveOffers = true; await vm.SaveAsync();
        Assert.Equal(["settings-get", "settings-put", "settings-get"], f.Calls); Assert.Null(vm.Pending); Assert.NotNull(vm.Notice);
    }
    [Fact]
    public async Task 서비스가닫혀도_기존수신을_중단할수있다()
    {
        var f = new Fixture(); f.Settings.Enabled = false; f.Settings.ReceiveOffers = true;
        using var vm = f.Participation(); await vm.LoadAsync(); Assert.False(vm.CanSave);
        vm.Draft.ReceiveOffers = false; Assert.True(vm.CanSave); await vm.SaveAsync(); Assert.False(f.Settings.ReceiveOffers);
    }
    [Fact]
    public async Task 저장응답만믿지않고_재조회실패는_원요청을_보존한다()
    {
        var f = new Fixture { FailAfterSaveRead = true }; using var vm = f.Participation(); await vm.LoadAsync();
        vm.Draft.ActivityLabel = "비공개 기준점"; await vm.SaveAsync();
        Assert.NotNull(vm.Pending); Assert.Null(vm.Notice); Assert.NotNull(vm.Error);
        var requestId = vm.Pending!.ClientRequestId; f.FailAfterSaveRead = false;
        using var restored = f.Participation(); await restored.LoadAsync(); await restored.SaveAsync();
        Assert.All(f.SavedSettings, value => Assert.Equal(requestId, value.ClientRequestId)); Assert.Null(restored.Pending);
    }
    [Fact]
    public async Task 이전계정의늦은_설정조회가_새계정화면에_들어오지않는다()
    {
        var f = new Fixture(); var reply = new TaskCompletionSource<NeighborhoodMemberParticipationResponse?>();
        f.SettingsRead = () => reply.Task; using var vm = f.Participation(); var loading = vm.LoadAsync();
        f.UserId = "other"; vm.SynchronizeOwner(); reply.SetResult(new() { ActivityLabel = "기존 계정 비공개" }); await loading;
        Assert.Null(vm.Settings); Assert.False(vm.Loaded); Assert.DoesNotContain("기존 계정", vm.Draft.ActivityLabel);
    }
    [Fact]
    public async Task 만료제안이나_현재GPS누락은_수락하지않는다()
    {
        var f = new Fixture(); f.Offer.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
        using var vm = f.Offers(); await vm.LoadAsync("offer"); Assert.Null(await vm.AcceptAsync()); Assert.Equal(0, f.LocationCalls);
        f.Offer.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5); f.Position = null;
        await vm.LoadAsync("offer"); Assert.Null(await vm.AcceptAsync()); Assert.DoesNotContain("accept", f.Calls);
    }
    [Fact]
    public async Task 수락은_현재위치를사용하고_자신의업무를_재조회한뒤에만_이동한다()
    {
        var f = new Fixture(); using var vm = f.Offers(); await vm.LoadAsync("offer");
        Assert.Equal("work", await vm.AcceptAsync()); Assert.Equal(1, f.LocationCalls);
        Assert.Equal(["offer-get", "accept", "work-get"], f.Calls); Assert.Null(vm.Pending);
    }
    [Fact]
    public async Task 수락결과가_끊겨도_같은요청과GPS로_재시도한다()
    {
        var f = new Fixture { LoseAccept = true }; using var vm = f.Offers(); await vm.LoadAsync("offer");
        Assert.Null(await vm.AcceptAsync()); var first = vm.Pending!; Assert.NotNull(first);
        f.LoseAccept = false; Assert.Equal("work", await vm.AcceptAsync());
        Assert.Equal(1, f.LocationCalls); Assert.Equal(f.Accepted[0].ClientRequestId, f.Accepted[1].ClientRequestId);
        Assert.Equal(f.Accepted[0].Latitude, f.Accepted[1].Latitude); Assert.Null(vm.Pending);
    }
    [Fact]
    public async Task 조회로확정한_이미수락된업무는_원요청을해결한다()
    {
        var f = new Fixture { LoseAccept = true }; using (var vm = f.Offers()) { await vm.LoadAsync("offer"); await vm.AcceptAsync(); }
        f.Offer.StateCode = "Accepted"; f.Offer.CanAccept = false;
        using var next = f.Offers(); await next.LoadAsync("offer");
        Assert.Equal("work", next.AcceptedRequestId); Assert.Null(next.Pending);
    }
    [Fact]
    public async Task 사진이없으면_픽업완료를_보내지않는다()
    {
        var f = new Fixture(); using var vm = f.Work(); await vm.LoadAsync("work"); await vm.CompleteAsync();
        Assert.False(vm.CanComplete); Assert.Equal(["work-get"], f.Calls);
    }
    [Theory]
    [InlineData("Pickup", "Assigned")]
    [InlineData("Delivery", "PickedUp")]
    public async Task 완료권한없는_최초업무단계도_사진촬영을_연다(string stage, string state)
    {
        var f = new Fixture(); f.CurrentWork.StateCode = state; f.CurrentWork.EvidenceStageCode = stage;
        using var vm = f.Work(); await vm.LoadAsync("work");
        Assert.False(vm.Work!.CanPickup); Assert.False(vm.Work.CanDeliver); Assert.Equal(stage, vm.Stage); Assert.False(vm.CanComplete);
        await vm.CaptureAsync();
        Assert.NotNull(vm.Photo); Assert.True(vm.CanComplete); Assert.Equal(1, f.CaptureCalls);
        Assert.Equal(["work-get"], f.Calls);
    }
    [Theory]
    [InlineData("")]
    [InlineData("Other")]
    public async Task 서버가_사진단계를_허용하지않으면_완료값만으로_촬영하지않는다(string stage)
    {
        var f = new Fixture(); f.CurrentWork.EvidenceStageCode = stage; f.CurrentWork.CanPickup = true;
        using var vm = f.Work(); await vm.LoadAsync("work"); await vm.CaptureAsync(); await vm.CompleteAsync();
        Assert.Empty(vm.Stage); Assert.Null(vm.Photo); Assert.False(vm.CanComplete); Assert.Equal(0, f.CaptureCalls);
        Assert.Equal(["work-get"], f.Calls);
    }
    [Fact]
    public async Task 계정이_바뀌면_이전업무단계로_카메라를_열지않는다()
    {
        var f = new Fixture(); using var vm = f.Work(); await vm.LoadAsync("work");
        f.UserId = "other"; await vm.CaptureAsync();
        Assert.Null(vm.Work); Assert.Null(vm.Photo); Assert.Equal(0, f.CaptureCalls); Assert.Equal(["work-get"], f.Calls);
    }
    [Fact]
    public async Task 사진저장증빙조회_후_별도픽업명령과_최종재조회가_이어진다()
    {
        var f = new Fixture(); using var vm = f.Work(); await vm.LoadAsync("work"); await vm.CaptureAsync(); await vm.CompleteAsync();
        Assert.Equal(["work-get", "evidence", "work-get", "pickup", "work-get"], f.Calls);
        Assert.Equal("PickedUp", vm.Work!.StateCode); Assert.Null(vm.Pending);
        Assert.All(f.Store.Saved, operation => Assert.DoesNotContain("ContentBase64", operation.PayloadJson));
    }
    [Fact]
    public async Task 픽업뒤_첫전달사진도_완료권한과_분리하고_최신판본으로_완료한다()
    {
        var f = new Fixture(); using var vm = f.Work(); await vm.LoadAsync("work"); await vm.CaptureAsync(); await vm.CompleteAsync();
        Assert.Equal("Delivery", vm.Stage); Assert.False(vm.Work!.CanDeliver); Assert.Null(vm.Work.DeliveryEvidenceId); Assert.False(vm.CanComplete);
        await vm.CaptureAsync(); await vm.CompleteAsync();
        Assert.Equal("Delivered", vm.Work.StateCode); Assert.Empty(vm.Stage); Assert.Null(vm.Work.ReceiptConfirmedAtUtc); Assert.Null(vm.Pending);
        Assert.Equal([1L, 3L], f.EvidenceRequests.Select(x => x.ExpectedRevision));
        Assert.Equal(2, Assert.Single(f.PickupCommands).ExpectedRevision); Assert.Equal(4, Assert.Single(f.DeliveryCommands).ExpectedRevision);
        Assert.Equal(["work-get", "evidence", "work-get", "pickup", "work-get", "evidence", "work-get", "deliver", "work-get"], f.Calls);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 사진저장후_조회가_같은단계와_완료권한을_확인하지않으면_완료명령을_막는다(bool stageChanged)
    {
        var f = new Fixture { AfterEvidence = work => { if (stageChanged) work.EvidenceStageCode = "Delivery"; else work.CanPickup = false; } };
        using var vm = f.Work(); await vm.LoadAsync("work"); await vm.CaptureAsync(); await vm.CompleteAsync();
        Assert.NotNull(f.CurrentWork.PickupEvidenceId); Assert.DoesNotContain("pickup", f.Calls); Assert.DoesNotContain("deliver", f.Calls);
        Assert.Equal("evidence", vm.Pending?.Kind); Assert.NotNull(vm.Error); Assert.Null(vm.Notice);
    }
    [Fact]
    public async Task 카메라촬영을_취소하면_사진저장과_완료를_보내지않는다()
    {
        var f = new Fixture { CapturePhoto = () => Task.FromResult<업무촬영사진?>(null) };
        using var vm = f.Work(); await vm.LoadAsync("work"); await vm.CaptureAsync(); await vm.CompleteAsync();
        Assert.Null(vm.Photo); Assert.False(vm.CanComplete); Assert.Null(vm.Pending); Assert.Equal(["work-get"], f.Calls);
    }
    [Theory]
    [InlineData("image/gif", 3)]
    [InlineData("image/png", 0)]
    [InlineData("image/png", 5 * 1024 * 1024 + 1)]
    public async Task 허용하지않는_사진은_전송과_완료에_사용하지않는다(string contentType, int length)
    {
        var f = new Fixture { CapturePhoto = () => Task.FromResult<업무촬영사진?>(new("photo", contentType, new byte[length])) };
        using var vm = f.Work(); await vm.LoadAsync("work"); await vm.CaptureAsync(); await vm.CompleteAsync();
        Assert.Null(vm.Photo); Assert.False(vm.CanComplete); Assert.NotNull(vm.Error); Assert.Null(vm.Pending); Assert.Equal(["work-get"], f.Calls);
    }
    [Fact]
    public async Task 사진API응답만있고_서버증빙조회가없으면_완료하지않는다()
    {
        var f = new Fixture { SaveEvidence = false }; using var vm = f.Work(); await vm.LoadAsync("work"); await vm.CaptureAsync(); await vm.CompleteAsync();
        Assert.DoesNotContain("pickup", f.Calls); Assert.Equal("Assigned", vm.Work!.StateCode);
        Assert.True(vm.PhotoUploadPending); Assert.Null(vm.Notice);
    }
    [Theory]
    [InlineData("Pickup")]
    [InlineData("Delivery")]
    public async Task 사진전송응답유실후_재시작하면_원요청증빙을조회하고_원본을보관하지않는다(string stage)
    {
        var f = new Fixture { LoseEvidence = true };
        f.CurrentWork.StateCode = stage == "Pickup" ? "Assigned" : "PickedUp"; f.CurrentWork.EvidenceStageCode = stage;
        using (var vm = f.Work()) { await vm.LoadAsync("work"); await vm.CaptureAsync(); await vm.CompleteAsync(); Assert.Equal("evidence", vm.Pending?.Kind); }
        using var restored = f.Work(); await restored.LoadAsync("work");
        Assert.Null(restored.Pending); Assert.Null(restored.Photo);
        Assert.Equal(stage == "Pickup" ? "pickup-photo" : "delivery-photo", stage == "Pickup" ? restored.Work!.PickupEvidenceId : restored.Work!.DeliveryEvidenceId);
        Assert.Contains("evidence-receipt", f.Calls); Assert.DoesNotContain("pickup", f.Calls); Assert.DoesNotContain("deliver", f.Calls);
        Assert.True(restored.CanComplete);
        Assert.All(f.Store.Saved, value => Assert.DoesNotContain("ContentBase64", value.PayloadJson));
    }
    [Theory]
    [InlineData("Pickup")]
    [InlineData("Delivery")]
    public async Task 사진원본없이_미완결사진요청을복구하면_완료대신_재촬영을안내한다(string stage)
    {
        var f = new Fixture { LoseEvidence = true, SaveEvidence = false };
        f.CurrentWork.StateCode = stage == "Pickup" ? "Assigned" : "PickedUp"; f.CurrentWork.EvidenceStageCode = stage;
        using (var vm = f.Work()) { await vm.LoadAsync("work"); await vm.CaptureAsync(); await vm.CompleteAsync(); }
        using var restored = f.Work(); await restored.LoadAsync("work");
        Assert.Null(restored.Pending); Assert.False(restored.CanComplete); Assert.Contains("다시 촬영", restored.Notice);
        Assert.DoesNotContain("pickup", f.Calls); Assert.DoesNotContain("deliver", f.Calls);
        await restored.CaptureAsync(); Assert.NotNull(restored.Photo); Assert.True(restored.CanComplete);
    }
    [Theory]
    [InlineData("Pickup")]
    [InlineData("Delivery")]
    public async Task 완료응답을잃고_앱을다시열면_서버상태로_복구하고_중복완료하지않는다(string stage)
    {
        var f = new Fixture { LosePickup = stage == "Pickup", LoseDeliver = stage == "Delivery" };
        f.CurrentWork.StateCode = stage == "Pickup" ? "Assigned" : "PickedUp"; f.CurrentWork.EvidenceStageCode = stage;
        using (var vm = f.Work()) { await vm.LoadAsync("work"); await vm.CaptureAsync(); await vm.CompleteAsync(); Assert.NotNull(vm.Pending); }
        using var recovered = f.Work(); await recovered.LoadAsync("work"); Assert.Equal(stage == "Pickup" ? "PickedUp" : "Delivered", recovered.Work!.StateCode);
        Assert.Null(recovered.Pending); Assert.Single(f.Calls, value => value == (stage == "Pickup" ? "pickup" : "deliver")); Assert.Null(recovered.Photo);
    }
    [Fact]
    public async Task 전달완료도_증빙을확인하고_수령과지급을_완료로합치지않는다()
    {
        var f = new Fixture(); f.CurrentWork.StateCode = "PickedUp"; f.CurrentWork.EvidenceStageCode = "Delivery";
        using var vm = f.Work(); await vm.LoadAsync("work"); await vm.CaptureAsync(); await vm.CompleteAsync();
        Assert.Equal("Delivered", vm.Work!.StateCode); Assert.Contains("수령자의 확인과 지급 확인은 별도", vm.Notice);
        Assert.DoesNotContain("payment", f.Calls); Assert.DoesNotContain("receipt", f.Calls);
    }

    internal sealed class Fixture : INeighborhoodMemberDeliveryClient, ISsalddel현재사용자Context, IRoleWorkspaceLocationProvider, I업무사진촬영Adapter
    {
        internal readonly Store Store = new();
        internal readonly List<string> Calls = [];
        internal readonly List<NeighborhoodMemberParticipationSaveRequest> SavedSettings = [];
        internal readonly List<NeighborhoodMemberAcceptRequest> Accepted = [];
        internal readonly List<NeighborhoodMemberCommandRequest> Cancellations = [];
        internal readonly List<NeighborhoodMemberEvidenceRequest> EvidenceRequests = [];
        internal readonly List<NeighborhoodMemberCommandRequest> PickupCommands = [], DeliveryCommands = [];
        internal Func<NeighborhoodMemberCommandRequest, Task>? Cancellation;
        internal Action<NeighborhoodMemberWorkResponse>? AfterEvidence;
        internal Func<Task<업무촬영사진?>>? CapturePhoto;
        internal string? UserId = "member";
        internal int LocationCalls, CaptureCalls;
        internal bool FailAfterSaveRead, LoseAccept, LosePickup, LoseDeliver, LoseEvidence, SaveEvidence = true;
        private NeighborhoodMemberEvidenceRequest? lastEvidence;
        internal Func<Task<NeighborhoodMemberParticipationResponse?>>? SettingsRead;
        internal RoleWorkspaceLocation? Position = new(37.58, 127.08, 15, DateTimeOffset.UtcNow);
        internal NeighborhoodMemberParticipationResponse Settings = new() { Revision = 1, Enabled = true };
        internal NeighborhoodMemberOfferResponse Offer = new() { OfferId = "offer", RequestId = "work", Revision = 1, CargoName = "책 한 상자", Quantity = 1,
            WeightKg = 1, FareKrw = 4000, DistanceKm = 2, PickupArea = "픽업 지역", DropoffArea = "전달 지역", CanAccept = true, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5) };
        internal NeighborhoodMemberWorkResponse CurrentWork = new() { RequestId = "work", Revision = 1, CargoName = "책 한 상자", EvidenceStageCode = "Pickup", Quantity = 1, WeightKg = 1, FareKrw = 4000,
            Pickup = new() { 주소 = new() { 도로명주소 = "픽업 장소", 상세주소 = "비공개 상세", 위도 = 37.58m, 경도 = 127.08m }, 연락처 = new() { 이름 = "담당자", 전화번호 = "010-0000-0000" } },
            Dropoff = new() { 주소 = new() { 도로명주소 = "전달 장소", 위도 = 37.59m, 경도 = 127.09m }, 연락처 = new() { 이름 = "수령인", 전화번호 = "010-0000-0000" } } };
        public 현재사용자Snapshot 현재사용자 => UserId is null ? 현재사용자Snapshot.익명 : new(UserId, "회원", []);
        internal NeighborhoodMemberDeliverySession Session => new(Store);
        internal NeighborhoodMemberParticipationViewModel Participation() => new(this, this, Session, this);
        internal NeighborhoodMemberOffersViewModel Offers() => new(this, this, Session, this);
        internal NeighborhoodMemberWorkViewModel Work() => new(this, this, Session, this);
        public bool IsSupported => true;
        public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken ct) { LocationCalls++; return Task.FromResult(Position); }
        public Task<업무촬영사진?> CaptureAsync(string title, CancellationToken ct) { CaptureCalls++; return CapturePhoto?.Invoke() ?? Task.FromResult<업무촬영사진?>(new("photo.png", "image/png", [1, 2, 3])); }
        public Task<NeighborhoodMemberParticipationResponse?> ParticipationAsync(CancellationToken ct)
        { Calls.Add("settings-get"); return SettingsRead?.Invoke() ?? (FailAfterSaveRead && SavedSettings.Count > 0 ? Task.FromException<NeighborhoodMemberParticipationResponse?>(new HttpRequestException()) : Task.FromResult<NeighborhoodMemberParticipationResponse?>(Clone(Settings))); }
        public Task<NeighborhoodMemberParticipationResponse?> SaveParticipationAsync(NeighborhoodMemberParticipationSaveRequest request, CancellationToken ct)
        {
            Calls.Add("settings-put"); SavedSettings.Add(Clone(request)); Settings = new() { Revision = request.ExpectedRevision + 1, Enabled = Settings.Enabled,
                ReceiveOffers = request.ReceiveOffers, ActivityLabel = request.ActivityLabel, Latitude = request.Latitude, Longitude = request.Longitude, RadiusKm = request.RadiusKm,
                VehicleType = request.VehicleType, MaxWeightKg = request.MaxWeightKg };
            return Task.FromResult<NeighborhoodMemberParticipationResponse?>(Clone(Settings));
        }
        public Task<IReadOnlyList<NeighborhoodMemberOfferResponse>> OffersAsync(CancellationToken ct) { Calls.Add("offers-get"); return Task.FromResult<IReadOnlyList<NeighborhoodMemberOfferResponse>>([Clone(Offer)]); }
        public Task<NeighborhoodMemberOfferResponse?> OfferAsync(string id, CancellationToken ct) { Calls.Add("offer-get"); return Task.FromResult<NeighborhoodMemberOfferResponse?>(Clone(Offer)); }
        public Task<NeighborhoodMemberWorkResponse?> AcceptAsync(string id, NeighborhoodMemberAcceptRequest request, CancellationToken ct)
        { Calls.Add("accept"); Accepted.Add(Clone(request)); return LoseAccept ? Task.FromException<NeighborhoodMemberWorkResponse?>(new HttpRequestException()) : Task.FromResult<NeighborhoodMemberWorkResponse?>(Clone(CurrentWork)); }
        public Task<NeighborhoodMemberOfferResponse?> DeclineAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct) { Calls.Add("decline"); Offer.StateCode = "Declined"; return Task.FromResult<NeighborhoodMemberOfferResponse?>(Clone(Offer)); }
        public Task<IReadOnlyList<NeighborhoodMemberWorkResponse>> WorkAsync(CancellationToken ct) { Calls.Add("work-list"); return Task.FromResult<IReadOnlyList<NeighborhoodMemberWorkResponse>>([Clone(CurrentWork)]); }
        public Task<NeighborhoodMemberWorkResponse?> WorkAsync(string id, CancellationToken ct) { Calls.Add("work-get"); return Task.FromResult<NeighborhoodMemberWorkResponse?>(Clone(CurrentWork)); }
        public Task<NeighborhoodMemberWorkResponse?> EvidenceAsync(string id, NeighborhoodMemberEvidenceRequest request, CancellationToken ct)
        {
            Calls.Add("evidence"); lastEvidence = Clone(request); EvidenceRequests.Add(Clone(request));
            if (SaveEvidence)
            {
                CurrentWork.Revision++;
                if (request.StageCode == "Pickup") { CurrentWork.PickupEvidenceId = "pickup-photo"; CurrentWork.CanPickup = true; }
                else { CurrentWork.DeliveryEvidenceId = "delivery-photo"; CurrentWork.CanDeliver = true; }
                AfterEvidence?.Invoke(CurrentWork);
            }
            return LoseEvidence ? Task.FromException<NeighborhoodMemberWorkResponse?>(new HttpRequestException()) : Task.FromResult<NeighborhoodMemberWorkResponse?>(Clone(CurrentWork));
        }
        public Task<NeighborhoodMemberEvidenceReceiptResponse?> EvidenceReceiptAsync(string id, Guid requestId, CancellationToken ct)
        { Calls.Add("evidence-receipt"); return Task.FromResult<NeighborhoodMemberEvidenceReceiptResponse?>(lastEvidence is null ? null : new() { RequestId = id, ClientRequestId = requestId, StageCode = lastEvidence.StageCode, EvidenceId = SaveEvidence ? lastEvidence.StageCode == "Pickup" ? "pickup-photo" : "delivery-photo" : "", Committed = SaveEvidence, WorkRevision = CurrentWork.Revision, StateCode = SaveEvidence ? "Uploaded" : "Uploading" }); }
        public Task<NeighborhoodMemberWorkResponse?> PickupAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct)
        { Calls.Add("pickup"); PickupCommands.Add(Clone(request)); CurrentWork.Revision++; CurrentWork.StateCode = "PickedUp"; CurrentWork.EvidenceStageCode = "Delivery"; CurrentWork.Pickup = null; CurrentWork.CanPickup = CurrentWork.CanDeliver = false; return LosePickup ? Task.FromException<NeighborhoodMemberWorkResponse?>(new HttpRequestException()) : Task.FromResult<NeighborhoodMemberWorkResponse?>(Clone(CurrentWork)); }
        public Task<NeighborhoodMemberWorkResponse?> DeliverAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct)
        { Calls.Add("deliver"); DeliveryCommands.Add(Clone(request)); CurrentWork.Revision++; CurrentWork.StateCode = "Delivered"; CurrentWork.EvidenceStageCode = ""; CurrentWork.Pickup = CurrentWork.Dropoff = null; CurrentWork.RequestNotes = null; CurrentWork.CanPickup = CurrentWork.CanDeliver = false; return LoseDeliver ? Task.FromException<NeighborhoodMemberWorkResponse?>(new HttpRequestException()) : Task.FromResult<NeighborhoodMemberWorkResponse?>(Clone(CurrentWork)); }
        public Task<NeighborhoodMemberWorkResponse?> IssueAsync(string id, NeighborhoodMemberIssueRequest request, CancellationToken ct)
        { Calls.Add("issue"); CurrentWork.Revision++; CurrentWork.StateCode = "Issue"; CurrentWork.EvidenceStageCode = ""; CurrentWork.SupportCaseId = "support"; CurrentWork.CanPickup = CurrentWork.CanDeliver = false; return Task.FromResult<NeighborhoodMemberWorkResponse?>(Clone(CurrentWork)); }
        public Task CancelRequestAsync(string id, NeighborhoodMemberCommandRequest request, CancellationToken ct)
        { Calls.Add("request-cancel"); Cancellations.Add(Clone(request)); return Cancellation?.Invoke(request) ?? Task.CompletedTask; }
        private static T Clone<T>(T value) => System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(value))!;
    }
    internal sealed class Store : INeighborhoodMemberPendingStore
    {
        internal readonly List<NeighborhoodMemberPendingOperation> Saved = [];
        private readonly Dictionary<string, NeighborhoodMemberPendingOperation> values = [];
        public Task<NeighborhoodMemberPendingOperation?> LoadAsync(string owner, CancellationToken ct) => Task.FromResult(values.GetValueOrDefault(owner));
        public Task SaveAsync(NeighborhoodMemberPendingOperation operation, CancellationToken ct) { Saved.Add(operation); values[operation.Owner] = operation; return Task.CompletedTask; }
        public Task RemoveAsync(string owner, CancellationToken ct) { values.Remove(owner); return Task.CompletedTask; }
    }
    private sealed class DelayedSaveStore : INeighborhoodMemberPendingStore
    {
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int SaveCount;
        public Task<NeighborhoodMemberPendingOperation?> LoadAsync(string owner, CancellationToken ct) => Task.FromResult<NeighborhoodMemberPendingOperation?>(null);
        public async Task SaveAsync(NeighborhoodMemberPendingOperation operation, CancellationToken ct) { SaveCount++; Started.SetResult(); await Release.Task.WaitAsync(ct); }
        public Task RemoveAsync(string owner, CancellationToken ct) => Task.CompletedTask;
    }
}
