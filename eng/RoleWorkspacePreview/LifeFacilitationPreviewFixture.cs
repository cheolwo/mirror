using Microsoft.AspNetCore.Components;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace RoleWorkspacePreview;

// 제품 상세를 읽기 전용 예시 DTO로 렌더합니다. 업무 서버·DB·외부 배차·지급을 호출하지 않습니다.
public static class LifeFacilitationPreviewRegistration
{
    public static IServiceCollection AddLifeFacilitationPreview(this IServiceCollection services)
    {
        services.AddScoped<LifeFacilitationPreviewFixture>();
        services.AddTransient(sp =>
        {
            var navigation = sp.GetRequiredService<NavigationManager>();
            if (LifeFacilitationPreviewFixture.IsPreview(navigation))
            {
                var fixture = sp.GetRequiredService<LifeFacilitationPreviewFixture>();
                var pending = new NeighborhoodCollaborationDraftSession();
                pending.Bind("preview-owner");
                if (fixture.Scenario == "recovery")
                {
                    pending.PendingTarget = fixture.WorkId;
                    pending.PendingCommand = new()
                    {
                        ClientRequestId = Guid.Parse("11111111-1111-4111-8111-111111111111"),
                        ExpectedRevision = 1, Action = NeighborhoodCollaborationActions.ProposeCompletion
                    };
                }
                return new NeighborhoodCollaborationQueryViewModel(fixture, pending, fixture);
            }
            var prior = sp.GetRequiredService<PreviewCommunityToolsFixture>();
            return new NeighborhoodCollaborationQueryViewModel(prior, sp.GetRequiredService<NeighborhoodCollaborationDraftSession>(), prior);
        });
        services.AddTransient(sp =>
        {
            var navigation = sp.GetRequiredService<NavigationManager>();
            if (LifeFacilitationPreviewFixture.IsPreview(navigation))
            {
                var fixture = sp.GetRequiredService<LifeFacilitationPreviewFixture>();
                return new NeighborhoodDeliveryQueryViewModel(fixture, fixture);
            }
            if (new Uri(navigation.Uri).AbsolutePath.StartsWith("/community/", StringComparison.Ordinal))
            {
                var prior = sp.GetRequiredService<PreviewCommunityToolsFixture>();
                return new NeighborhoodDeliveryQueryViewModel(prior, prior);
            }
            return new NeighborhoodDeliveryQueryViewModel(sp.GetRequiredService<INeighborhoodDeliveryClient>(), sp.GetRequiredService<ISsalddel현재사용자Context>());
        });
        return services;
    }
}

internal sealed class LifeFacilitationPreviewFixture(NavigationManager navigation) :
    ISsalddel현재사용자Context, INeighborhoodCollaborationClient, INeighborhoodDeliveryClient
{
    internal static readonly string[] WorkScenarios = ["pickup-null", "delivery-zero", "driver-delivery", "waiting-other", "completed", "recovery", "error", "anonymous"];
    internal static readonly string[] DeliveryScenarios = ["automatic-proposed", "public-call", "hybrid-no-candidate", "registration-pending", "assigned-disclosure", "disclosure-consented", "completed-unpaid", "error", "anonymous"];
    private static readonly DateTime RecordedAt = new(2026, 10, 7, 1, 0, 0, DateTimeKind.Utc);
    public static bool IsPreview(NavigationManager value) => new Uri(value.Uri).AbsolutePath.StartsWith("/life-facilitation-preview/", StringComparison.Ordinal);
    public string Scenario => new Uri(navigation.Uri).AbsolutePath.Split('/').Last();
    public string WorkId => "preview-work-" + Scenario;
    public string DeliveryId => "preview-delivery-" + Scenario;
    public 현재사용자Snapshot 현재사용자 => Scenario == "anonymous" ? 현재사용자Snapshot.익명 : new("preview-owner", "생활 참여자", []);

    public Task<NeighborhoodCollaborationResponse?> ReadAsync(string id, CancellationToken ct)
    {
        if (!WorkScenarios.Contains(Scenario) || id != WorkId) return Task.FromResult<NeighborhoodCollaborationResponse?>(null);
        if (Scenario == "error") return Task.FromException<NeighborhoodCollaborationResponse?>(new InvalidOperationException("Read-only preview read failure"));
        var waiting = Scenario == "waiting-other";
        var completed = Scenario == "completed";
        return Task.FromResult<NeighborhoodCollaborationResponse?>(new()
        {
            StableId = id, SourceTitle = "책 한 상자", Kind = NeighborhoodCollaborationKinds.Goods,
            MyRoleCode = "owner", IsSourceOwner = true, ProviderRoleCode = "owner", Revision = 1, TermsRevision = 1,
            StatusCode = completed ? NeighborhoodCollaborationStates.Completed : waiting ? NeighborhoodCollaborationStates.CompletionProposed : NeighborhoodCollaborationStates.InProgress,
            CompletionProposedByMe = waiting, OwnerAgreed = true, RequesterAgreed = true,
            AllowedActions = completed ? [NeighborhoodCollaborationActions.PublicHistoryConsent]
                : waiting ? [NeighborhoodCollaborationActions.Cancel]
                : [NeighborhoodCollaborationActions.ProposeCompletion, NeighborhoodCollaborationActions.Cancel, NeighborhoodCollaborationActions.WithdrawHandoverConsent],
            Terms = new()
            {
                Summary = "책 한 상자를 주고받기로 했어요", Quantity = 1, Unit = "상자", FromUtc = RecordedAt, UntilUtc = RecordedAt.AddDays(1),
                TransferMethod = Scenario == "delivery-zero" ? NeighborhoodTransferMethods.ProviderDelivery
                    : Scenario == "driver-delivery" ? NeighborhoodTransferMethods.DriverDelivery : NeighborhoodTransferMethods.RecipientPickup,
                HandoverPlace = "합의한 인계 장소", AgreedCostKrw = Scenario == "pickup-null" ? null : Scenario == "driver-delivery" ? 12000m : 0m,
                Notes = "상자에 담아 인계하기로 했어요."
            },
            CanRequestDelivery = Scenario == "driver-delivery", CreatedAtUtc = RecordedAt, UpdatedAtUtc = RecordedAt, ExpiresAtUtc = RecordedAt.AddDays(1),
            History = [new() { Revision = 1, Action = "start", StatusCode = NeighborhoodCollaborationStates.InProgress, ActorRoleCode = "owner", RecordedAtUtc = RecordedAt }]
        });
    }

    Task<NeighborhoodDeliveryResponse?> INeighborhoodDeliveryClient.ReadAsync(string id, CancellationToken ct)
    {
        if (!DeliveryScenarios.Contains(Scenario) || id != DeliveryId) return Task.FromResult<NeighborhoodDeliveryResponse?>(null);
        if (Scenario == "error") return Task.FromException<NeighborhoodDeliveryResponse?>(new InvalidOperationException("Read-only preview read failure"));
        var assigned = Scenario is "assigned-disclosure" or "disclosure-consented";
        var completed = Scenario == "completed-unpaid";
        return Task.FromResult<NeighborhoodDeliveryResponse?>(new()
        {
            RequestId = id, CargoName = "책 한 상자", CreatedAtUtc = RecordedAt,
            DispatchMode = Scenario == "public-call" ? NeighborhoodDispatchModes.PublicCall
                : Scenario == "hybrid-no-candidate" ? NeighborhoodDispatchModes.Hybrid : NeighborhoodDispatchModes.Automatic,
            DispatchRevision = 1, CanChangeDispatchMode = !assigned && !completed && Scenario != "registration-pending",
            ProposalStateCode = completed ? NeighborhoodDeliveryProposalStates.Closed : assigned ? NeighborhoodDeliveryProposalStates.Assigned
                : Scenario == "registration-pending" ? NeighborhoodDeliveryProposalStates.RegistrationPending
                : Scenario == "automatic-proposed" ? NeighborhoodDeliveryProposalStates.Proposed : NeighborhoodDeliveryProposalStates.Queued,
            DispatchStatusCode = completed ? "인수완료" : assigned ? "배차확정" : "매칭중", RequestStatusCode = "생성됨",
            AutomaticDispatchEnabled = true, FareKrw = 4800, DistanceKm = 2.4m, DistanceBasis = "화면 검토용 거리", RateSource = "화면 검토용 운임",
            SettlementStatusCode = "현장수금예정", PaymentStatusCode = "결제대기",
            DriverDisclosure = assigned ? new()
            {
                RequestId = id, ConfirmedDriverId = "preview-driver", ConfirmedDriverName = "기사 1", RecommendationRound = 1,
                CanRecordConsent = true, Consented = Scenario == "disclosure-consented", ExpiresAtUtc = RecordedAt.AddDays(2)
            } : null,
            Request = new()
            {
                의뢰Id = id, 주문자UserId = "preview-owner", 운송상태 = completed ? "인수완료" : assigned ? "상차지이동중" : "대기",
                차량종류 = "오토바이", 픽업지 = "픽업 장소 1", 하차지 = "전달 장소 1",
                화물 = new() { 화물종류 = "책", 수량 = 1, 중량Kg = 1.5m },
                현장지급메모 = "기사에게 직접 지급 예정. 실제 지급·입금 미확인."
            }
        });
    }

    // 읽기/결과 재조회만 허용합니다. 버튼을 눌러도 업무 상태나 외부 기록은 변경하지 않습니다.
    public Task<NeighborhoodCollaborationResponse?> ReceiptAsync(Guid requestId, string? stableId, CancellationToken ct) => Task.FromResult<NeighborhoodCollaborationResponse?>(null);
    public Task<NeighborhoodCollaborationListResponse> MineAsync(string scope, int page, CancellationToken ct) => Task.FromResult(new NeighborhoodCollaborationListResponse());
    public Task<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>> OpportunitiesAsync(long id, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>>([]);
    public Task<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>> PublicHistoryAsync(long id, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>>([]);
    Task<IReadOnlyList<NeighborhoodDeliveryResponse>> INeighborhoodDeliveryClient.MineAsync(int page, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodDeliveryResponse>>([]);
    public Task<NeighborhoodCollaborationResponse?> CreateAsync(NeighborhoodCollaborationCreateRequest request, CancellationToken ct) => Block<NeighborhoodCollaborationResponse?>();
    public Task<NeighborhoodCollaborationResponse?> CommandAsync(string id, NeighborhoodCollaborationCommandRequest request, CancellationToken ct) => Block<NeighborhoodCollaborationResponse?>();
    public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct) => Block<신청개인정보동의증적Response?>();
    public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) => Block<NeighborhoodDeliveryQuoteResponse?>();
    public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) => Block<NeighborhoodDeliveryResponse?>();
    public Task<NeighborhoodDeliveryResponse?> ChangeDispatchAsync(string id, NeighborhoodDispatchChoiceRequest request, CancellationToken ct) => Block<NeighborhoodDeliveryResponse?>();
    public Task<NeighborhoodDeliveryDisclosureResponse?> RecordDisclosureAsync(string id, NeighborhoodDeliveryDisclosureRequest request, CancellationToken ct) => Block<NeighborhoodDeliveryDisclosureResponse?>();
    private static Task<T> Block<T>() => Task.FromException<T>(new InvalidOperationException("읽기 전용 화면에서는 변경을 저장하지 않습니다."));
}
