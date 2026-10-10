using Microsoft.AspNetCore.Components;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace RoleWorkspacePreview;

public static class NeighborhoodMemberDeliveryPreviewRegistration
{
    public static IServiceCollection AddNeighborhoodMemberDeliveryPreview(this IServiceCollection services)
    {
        services.AddScoped<NeighborhoodMemberDeliveryPreviewFixture>();
        services.AddTransient(sp => sp.GetRequiredService<NeighborhoodMemberDeliveryPreviewFixture>().Participation);
        services.AddTransient(sp => sp.GetRequiredService<NeighborhoodMemberDeliveryPreviewFixture>().Offers);
        services.AddTransient(sp => sp.GetRequiredService<NeighborhoodMemberDeliveryPreviewFixture>().Work);
        services.AddTransient(sp => sp.GetRequiredService<NeighborhoodMemberDeliveryPreviewFixture>().Request);
        return services;
    }
}

/// <summary>실제 제품 UI를 모의 자료로 렌더합니다. 업무 서버·DB·실제 GPS·사진 저장·배차를 호출하지 않습니다.</summary>
internal sealed class NeighborhoodMemberDeliveryPreviewFixture(NavigationManager navigation) : INeighborhoodMemberDeliveryClient, INeighborhoodDeliveryClient,
    ISsalddel현재사용자Context, IRoleWorkspaceLocationProvider, I업무사진촬영Adapter
{
    internal static readonly string[] Scenarios = ["settings-off", "settings-ready", "settings-saved", "feature-off", "offers-empty", "offer-ready", "offer-expired", "assigned", "evidence-ready", "picked", "delivered", "received", "issue", "anonymous", "error", "request-cancel"];
    private string Scenario => new Uri(navigation.Uri).AbsolutePath.Split('/').Last();
    private readonly NeighborhoodMemberDeliverySession _session = new(new VolatileNeighborhoodMemberPendingStore());
    private NeighborhoodMemberParticipationViewModel? _participation;
    private NeighborhoodMemberOffersViewModel? _offers;
    private NeighborhoodMemberWorkViewModel? _work;
    private NeighborhoodMemberRequestViewModel? _request;
    private NeighborhoodDeliveryResponse _source = new() { RequestId = "preview-member-work", MemberDeliveryRequested = true, MemberWorkRevision = 1, MemberWorkStateCode = "Assigned", CanCancelMemberDelivery = true };
    private NeighborhoodMemberParticipationResponse? _settings;
    private NeighborhoodMemberWorkResponse? _current;
    private NeighborhoodMemberOfferResponse? _offer;
    public NeighborhoodMemberParticipationViewModel Participation => _participation ??= new(this, this, _session, this);
    public NeighborhoodMemberOffersViewModel Offers => _offers ??= new(this, this, _session, this);
    public NeighborhoodMemberWorkViewModel Work => _work ??= new(this, this, _session, this);
    public NeighborhoodMemberRequestViewModel Request => _request ??= new(this, this, this, _session);
    public 현재사용자Snapshot 현재사용자 => Scenario == "anonymous" ? 현재사용자Snapshot.익명 : new("preview-member", "회원", []);
    public bool IsSupported => true;
    public int Mutations { get; private set; }
    public event Action? Changed;
    private NeighborhoodMemberParticipationResponse Settings => _settings ??= new()
    {
        Enabled = Scenario != "feature-off", BlockCode = Scenario == "feature-off" ? "MemberDeliveryDisabled" : "", Revision = 1,
        ActivityLabel = "사가정 주변", RadiusKm = 3, ReceiveOffers = Scenario == "settings-saved", Latitude = 37.58m, Longitude = 127.08m
    };
    private NeighborhoodMemberOfferResponse Offer => _offer ??= new()
    {
        OfferId = "preview-member-offer", RequestId = "preview-member-work", Revision = 1, CargoName = "책 한 상자", Quantity = 1, WeightKg = 2,
        PickupArea = "사가정 주변", DropoffArea = "면목 주변", FareKrw = 4000, DistanceKm = 2, CanAccept = Scenario != "offer-expired",
        StateCode = Scenario == "offer-expired" ? "Expired" : "Pending", ExpiresAtUtc = Scenario == "offer-expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5)
    };
    private NeighborhoodMemberWorkResponse Current => _current ??= new()
    {
        RequestId = "preview-member-work", Revision = 1, CargoName = "책 한 상자", Quantity = 1, WeightKg = 2, FareKrw = 4000, DistanceKm = 2,
        StateCode = Scenario switch { "picked" => "PickedUp", "delivered" or "received" => "Delivered", "issue" => "Issue", _ => "Assigned" },
        EvidenceStageCode = Scenario switch { "picked" => "Delivery", "delivered" or "received" or "issue" => "", _ => "Pickup" },
        CanPickup = Scenario == "evidence-ready", CanDeliver = false,
        PickupEvidenceId = Scenario == "evidence-ready" ? "preview-saved-photo" : Scenario == "picked" ? "preview-pickup-photo" : null,
        ReceiptConfirmedAtUtc = Scenario == "received" ? DateTime.UtcNow : null,
        RequestNotes = Scenario is "delivered" or "received" ? null : "물품을 비에 젖지 않게 전달해 주세요.",
        SupportCaseId = Scenario == "issue" ? "preview-support-case" : null,
        Pickup = Scenario is "picked" or "delivered" or "received" or "issue" ? null : Place("픽업 장소 1", 37.58m, 127.08m),
        Dropoff = Scenario is "delivered" or "received" or "issue" ? null : Place("전달 장소 1", 37.59m, 127.09m)
    };
    private static Ssalddel.Contracts.Shipper.Request.LocationContactDTO Place(string label, decimal lat, decimal lng) => new()
    { 주소 = new() { 도로명주소 = label, 상세주소 = "현관 앞", 위도 = lat, 경도 = lng }, 연락처 = new() { 이름 = "담당자", 전화번호 = "010-0000-0000" } };
    private Task<T?> Read<T>(T value) => Scenario == "error" ? Task.FromException<T?>(new HttpRequestException("Preview failed read")) : Task.FromResult<T?>(value);
    public Task<NeighborhoodMemberParticipationResponse?> ParticipationAsync(CancellationToken ct) => Read(Settings);
    public Task<NeighborhoodMemberParticipationResponse?> SaveParticipationAsync(NeighborhoodMemberParticipationSaveRequest body, CancellationToken ct)
    {
        _settings = new() { Enabled = Settings.Enabled, Revision = body.ExpectedRevision + 1, ReceiveOffers = body.ReceiveOffers, ActivityLabel = body.ActivityLabel,
            Latitude = body.Latitude, Longitude = body.Longitude, RadiusKm = body.RadiusKm, VehicleType = body.VehicleType, MaxWeightKg = body.MaxWeightKg };
        Mutations++; Changed?.Invoke(); return Read(_settings);
    }
    public Task<IReadOnlyList<NeighborhoodMemberOfferResponse>> OffersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodMemberOfferResponse>>(Scenario == "offers-empty" ? [] : [Offer]);
    public Task<NeighborhoodMemberOfferResponse?> OfferAsync(string id, CancellationToken ct) => Read(Offer);
    public Task<NeighborhoodMemberWorkResponse?> AcceptAsync(string id, NeighborhoodMemberAcceptRequest body, CancellationToken ct)
    { Offer.StateCode = "Accepted"; Offer.CanAccept = false; Current.StateCode = "Assigned"; Current.EvidenceStageCode = "Pickup"; Current.CanPickup = Current.CanDeliver = false; Mutations++; Changed?.Invoke(); return Read(Current); }
    public Task<NeighborhoodMemberOfferResponse?> DeclineAsync(string id, NeighborhoodMemberCommandRequest body, CancellationToken ct)
    { Offer.StateCode = "Declined"; Offer.CanAccept = false; Mutations++; Changed?.Invoke(); return Read(Offer); }
    public Task<IReadOnlyList<NeighborhoodMemberWorkResponse>> WorkAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodMemberWorkResponse>>([Current]);
    public Task<NeighborhoodMemberWorkResponse?> WorkAsync(string id, CancellationToken ct) => Read(Current);
    public Task<NeighborhoodMemberWorkResponse?> EvidenceAsync(string id, NeighborhoodMemberEvidenceRequest body, CancellationToken ct)
    { Current.Revision++; if (body.StageCode == "Pickup") { Current.PickupEvidenceId = "preview-pickup-photo"; Current.CanPickup = true; } else { Current.DeliveryEvidenceId = "preview-delivery-photo"; Current.CanDeliver = true; } Mutations++; Changed?.Invoke(); return Read(Current); }
    public Task<NeighborhoodMemberWorkResponse?> PickupAsync(string id, NeighborhoodMemberCommandRequest body, CancellationToken ct)
    { Current.Revision++; Current.StateCode = "PickedUp"; Current.EvidenceStageCode = "Delivery"; Current.Pickup = null; Current.CanPickup = Current.CanDeliver = false; Mutations++; Changed?.Invoke(); return Read(Current); }
    public Task<NeighborhoodMemberWorkResponse?> DeliverAsync(string id, NeighborhoodMemberCommandRequest body, CancellationToken ct)
    { Current.Revision++; Current.StateCode = "Delivered"; Current.EvidenceStageCode = ""; Current.CanPickup = Current.CanDeliver = false; Current.Pickup = Current.Dropoff = null; Current.RequestNotes = null; Mutations++; Changed?.Invoke(); return Read(Current); }
    public Task<NeighborhoodMemberWorkResponse?> IssueAsync(string id, NeighborhoodMemberIssueRequest body, CancellationToken ct)
    { Current.Revision++; Current.StateCode = "Issue"; Current.EvidenceStageCode = ""; Current.SupportCaseId = "preview-support-case"; Current.CanPickup = Current.CanDeliver = false; Mutations++; Changed?.Invoke(); return Read(Current); }
    public Task CancelRequestAsync(string id, NeighborhoodMemberCommandRequest body, CancellationToken ct)
    { _source.MemberWorkRevision++; _source.MemberWorkStateCode = "Cancelled"; _source.CanCancelMemberDelivery = false; Mutations++; Changed?.Invoke(); return Task.CompletedTask; }
    public Task<Ssalddel.Contracts.Common.Privacy.신청개인정보동의증적Response?> ConsentAsync(Ssalddel.Contracts.Common.Privacy.신청개인정보동의기록Request request, CancellationToken ct) => throw new NotSupportedException();
    public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) => throw new NotSupportedException();
    public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodDeliveryResponse>>([_source]);
    public Task<NeighborhoodDeliveryResponse?> ReadAsync(string requestId, CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryResponse?>(_source);
    public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken ct) => Task.FromResult<RoleWorkspaceLocation?>(new(37.58, 127.08, 10, DateTimeOffset.UtcNow));
    public Task<업무촬영사진?> CaptureAsync(string title, CancellationToken ct)
        => Task.FromResult<업무촬영사진?>(new("preview.png", "image/png", Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jD1kAAAAASUVORK5CYII=")));
}
