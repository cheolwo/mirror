using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace RoleWorkspacePreview;

/// <summary>실제 공고·계정·운송·DB 없이 공통 제품 컴포넌트를 렌더하는 검토 호스트 전용 합성 자료입니다.</summary>
public sealed class CargoNoticePreviewFixture : ICargoNoticeClient, ISsalddel현재사용자Context,
    ISsalddel현재사용자변경알림, IWorkflowRecoveryContext
{
    private static readonly string Id = "cargo-notice:" + new string('a', 64);
    private readonly Dictionary<Guid, CargoNoticeCommandResponse> receipts = [];
    private string scenario = "public";
    private CargoNoticeOwnerDto? notice;
    private string owner = "synthetic-cargo-notice-owner";
    private long epoch = 1;
    private bool unknownTriggered;
    public IWorkflowRecoveryStore Store { get; } = new VolatileWorkflowRecoveryStore();
    public CargoNoticeDraftSession Session { get; } = new();
    public 현재사용자Snapshot 현재사용자 => owner.Length == 0 ? 현재사용자Snapshot.익명 : new(owner, "합성 작성자", []);
    public event Action? Changed;
    public WorkflowRecoverySnapshot Capture() => owner.Length == 0 ? throw new InvalidOperationException()
        : new(owner, epoch, new string('a', 32));
    public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => owner.Length > 0 && snapshot == Capture();
    public void Prepare(string value)
    {
        scenario = value; owner = scenario == "anonymous" ? "" : "synthetic-cargo-notice-owner"; epoch++;
        notice = scenario is "new" or "empty" or "unknown" ? null : MakeNotice();
        if (notice is not null) notice.Status = scenario switch
        { "draft" => CargoNoticeStatus.Draft, "expired" => CargoNoticeStatus.Expired, "withdrawn" => CargoNoticeStatus.Withdrawn, _ => CargoNoticeStatus.Published };
        if (notice?.Status == CargoNoticeStatus.Published)
        { notice.PublishedAtUtc = DateTimeOffset.UtcNow.AddHours(-1); notice.ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1); notice.ConsentedContentRevision = notice.ContentRevision; }
        receipts.Clear(); unknownTriggered = false;
    }
    public void ChangeAccount() { owner = "synthetic-second-owner"; epoch++; Changed?.Invoke(); }
    public async Task<CargoNoticeListResponse?> ListAsync(int page, CancellationToken ct = default)
    {
        Guard();
        if (scenario == "loading") await Task.Delay(2500, ct);
        return new() { Page = page, Items = notice?.Status == CargoNoticeStatus.Published ? [new()
        { NoticeId = notice.NoticeId, Revision = notice.Revision, Content = Copy(notice.Content),
            PickupRegionName = "서울 중랑구", DropoffRegionName = "서울 동대문구",
            PublishedAtUtc = DateTimeOffset.UtcNow.AddHours(-1), ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1) }] : [], TotalCount = notice?.Status == CargoNoticeStatus.Published ? 1 : 0 };
    }
    public Task<IReadOnlyList<CargoNoticeRegion>?> RegionsAsync(CancellationToken ct = default)
    { Guard(); return Task.FromResult<IReadOnlyList<CargoNoticeRegion>?>([new() { RegionKey = "sample-jn", DisplayName = "서울 중랑구" }, new() { RegionKey = "sample-ddm", DisplayName = "서울 동대문구" }]); }
    public Task<CargoNoticeOwnerListResponse?> MineAsync(int page, CancellationToken ct = default)
    { Guard(); Auth(); return Task.FromResult<CargoNoticeOwnerListResponse?>(new() { Page = page, Items = notice is null ? [] : [Copy(notice)], TotalCount = notice is null ? 0 : 1 }); }
    public Task<CargoNoticeOwnerDto?> OwnerAsync(string noticeId, CancellationToken ct = default)
    { Guard(); Auth(); return Task.FromResult<CargoNoticeOwnerDto?>(notice?.NoticeId == noticeId ? Copy(notice) : throw Missing()); }
    public Task<CargoNoticeCommandResponse?> ResultAsync(Guid requestId, string? noticeId, CancellationToken ct = default)
    { Guard(); Auth(); return Task.FromResult<CargoNoticeCommandResponse?>(receipts.TryGetValue(requestId, out var value) && (noticeId is null || noticeId == value.Notice.NoticeId) ? Copy(value) : throw Missing()); }
    public Task<CargoNoticeCommandResponse?> CreateAsync(CargoNoticeDraftRequest request, CancellationToken ct = default)
        => Write(request.ClientRequestId, "create", () =>
        { notice = MakeNotice(); notice.Content = Copy(request.Content); notice.Status = CargoNoticeStatus.Draft; notice.PublishedAtUtc = null; notice.ExpiresAtUtc = null; });
    public Task<CargoNoticeCommandResponse?> UpdateAsync(string noticeId, CargoNoticeDraftRequest request, CancellationToken ct = default)
        => Write(request.ClientRequestId, "update", () =>
        { Check(noticeId, request.ExpectedRevision); notice!.Content = Copy(request.Content); notice.ContentRevision++; notice.Status = CargoNoticeStatus.Draft; notice.PublishedAtUtc = null; notice.ExpiresAtUtc = null; notice.ConsentedContentRevision = null; });
    public Task<CargoNoticeCommandResponse?> PublishAsync(string noticeId, CargoNoticePublishRequest request, CancellationToken ct = default)
        => Write(request.ClientRequestId, "publish", () =>
        {
            Check(noticeId, request.ExpectedRevision);
            if (!request.ConsentToPublicPublication || request.ExpectedContentRevision != notice!.ContentRevision) throw Failure(400, "PublicConsentRequired");
            notice.Status = CargoNoticeStatus.Published; notice.PublishedAtUtc = DateTimeOffset.UtcNow; notice.ExpiresAtUtc = request.ExpiresAtUtc; notice.ConsentedContentRevision = notice.ContentRevision;
        });
    public Task<CargoNoticeCommandResponse?> WithdrawAsync(string noticeId, CargoNoticeMutationRequest request, CancellationToken ct = default)
        => Write(request.ClientRequestId, "withdraw", () => { Check(noticeId, request.ExpectedRevision); notice!.Status = CargoNoticeStatus.Withdrawn; });
    private Task<CargoNoticeCommandResponse?> Write(Guid request, string action, Action change)
    {
        Guard(); Auth();
        if (receipts.TryGetValue(request, out var old)) return Task.FromResult<CargoNoticeCommandResponse?>(Copy(old));
        change(); notice!.Revision++; notice.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var result = new CargoNoticeCommandResponse { ClientRequestId = request, Operation = action, AppliedAtUtc = DateTimeOffset.UtcNow, Notice = Copy(notice) };
        receipts.Add(request, Copy(result));
        if (scenario == "unknown" && !unknownTriggered) { unknownTriggered = true; throw new HttpRequestException("Synthetic lost response"); }
        return Task.FromResult<CargoNoticeCommandResponse?>(Copy(result));
    }
    private void Guard()
    { if (scenario is "disabled" or "manage-disabled") throw Failure(404, "FeatureDisabled"); if (scenario == "error") throw new HttpRequestException("Synthetic unavailable"); }
    private void Auth() { if (owner.Length == 0) throw Failure(401, "AuthenticationRequired"); }
    private void Check(string id, long revision)
    { if (notice?.NoticeId != id) throw Missing(); if (notice.Revision != revision) throw Failure(409, "RevisionConflict"); }
    private static SsalddelApiException Missing() => Failure(404, "NotFound");
    private static SsalddelApiException Failure(int status, string code) => new("합성 검토 응답", status, "공고 검토", "", null, errorCode: "CargoNotice." + code);
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static CargoNoticeOwnerDto MakeNotice() => new() { NoticeId = Id, Revision = 1, ContentRevision = 1, Status = CargoNoticeStatus.Draft,
        Content = new() { Title = "행사 물품 운송 정보", GoodsName = "포장된 생활용품 상자", Quantity = 8, WeightKg = 24,
            VehicleKind = CargoNoticeVehicleKinds.Van, PickupRegionKey = "sample-jn", DropoffRegionKey = "sample-ddm",
            DesiredFromUtc = new(2026, 10, 26, 1, 0, 0, TimeSpan.Zero), DesiredUntilUtc = new(2026, 10, 26, 3, 0, 0, TimeSpan.Zero) },
        UpdatedAtUtc = DateTimeOffset.UtcNow };
}
