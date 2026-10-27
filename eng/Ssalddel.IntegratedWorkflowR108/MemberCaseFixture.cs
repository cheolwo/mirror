using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Domain.Community;
using Ssalddel.Domain.Notifications;
using Ssalddel.Services.Community;
using Ssalddel.Services.Privacy;
using 살뜰.Data;
using 살뜰.Services.Notifications;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

internal sealed record PreparedCase(string Case, string Database, string RequestId, string OfferId, string MemberUserId,
    string OwnerUserId, string CollaborationId, Guid ConsentId, long OfferRevision, DateTime ExpiresAtUtc);

internal sealed class MemberCaseCli(IServiceProvider services, CargoJourneySettings settings, HarnessActor actor)
{
    private string CaseDirectory(string name)
    {
        if (!Regex.IsMatch(name, "^[a-z0-9][a-z0-9-]{0,47}$")) throw new HarnessFailure("InvalidCaseName");
        return Path.Combine(settings.ArtifactRoot, ".private", "cases", name);
    }
    private async Task<T> As<T>(string userId, Func<IServiceProvider, Task<T>> operation)
    {
        actor.UserId = userId;
        await using var scope = services.CreateAsyncScope();
        return await operation(scope.ServiceProvider);
    }
    private async Task<string> User(string name, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var id = await scope.ServiceProvider.GetRequiredService<SsalddelContext>().Users.AsNoTracking()
            .Where(x => x.UserName == name).Select(x => x.Id).SingleOrDefaultAsync(ct);
        return id ?? throw new HarnessFailure("ExpectedSyntheticAccountMissing");
    }
    private static T Require<T>(Result<T> result)
        => result.IsSuccess ? result.Value : throw new HarnessFailure("ProductUseCaseRejected");
    private static void Check(bool condition, string code) { if (!condition) throw new HarnessFailure(code); }

    internal async Task Prepare(string name, bool configureParticipation, CancellationToken ct)
    {
        Entry.Stage("PrepareJournal");
        var folder = CaseDirectory(name); Directory.CreateDirectory(folder);
        var marker = Path.Combine(folder, "prepare-attempt.json");
        await using (var journal = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(journal, new { status = "Started", synthetic = true, atUtc = DateTime.UtcNow }, Entry.Json, ct);
        Entry.Stage("FindMemberAccount"); var member = await User("field-orderer", ct);
        Entry.Stage("FindProviderAccount"); var provider = await User("cargo-journey-shipper", ct);
        Entry.Stage("FindRequesterAccount"); var requester = await User("shipper1", ct);
        Check(member != provider && member != requester && provider != requester, "DistinctActorsRequired");
        Entry.Stage("ReadFinancialBaseline"); var financialBefore = await FinancialCounts(ct);
        Entry.Stage("ReadMemberParticipation");
        var participation = await As(member, sp => sp.GetRequiredService<NeighborhoodMemberDeliveryUseCase>().ParticipationAsync(ct));
        Check(participation.Enabled, "SimulationMemberFeatureRequired");
        if (configureParticipation)
        {
            Entry.Stage("ConfigureSyntheticParticipation");
            participation = await As(member, sp => sp.GetRequiredService<NeighborhoodMemberDeliveryUseCase>().SaveParticipationAsync(new()
            {
                ClientRequestId = Guid.NewGuid(), ExpectedRevision = participation.Revision, ReceiveOffers = true,
                ActivityLabel = "r108 합성 활동점", Latitude = 37.57m, Longitude = 127.07m,
                RadiusKm = 3, VehicleType = "오토바이", MaxWeightKg = 10
            }, ct));
        }
        Check(participation.ReceiveOffers, "EnableParticipationInAppOrExplicitFixtureOption");
        long postId;
        DateTime updated;
        Entry.Stage("CreateSyntheticPost");
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
            settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
            var now = DateTime.UtcNow;
            var post = new PlatformCommunityPost { AuthorUserId = provider, Category = PlatformCommunityPostCategories.General,
                WorkflowTag = NeighborhoodExchange.WorkflowTag, RoleTag = NeighborhoodExchange.Offer,
                Title = "r108 합성 물품 교류 · " + name, Body = "FCM 단발 검증용 합성 표본", Nickname = "검증 표본",
                UpdatedAtUtc = new DateTime(now.Ticks / 10 * 10, DateTimeKind.Utc) };
            db.PlatformCommunityPosts.Add(post); await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            var stored = await db.PlatformCommunityPosts.AsNoTracking().SingleAsync(x => x.Id == post.Id, ct);
            postId = stored.Id; updated = stored.UpdatedAtUtc;
        }
        Entry.Stage("CreateCollaboration");
        var collaboration = await As(requester, async sp => Require(await sp.GetRequiredService<I생활협업UseCase>().신청Async(new()
        {
            ClientRequestId = Guid.NewGuid(), SourcePostId = postId, ExpectedSourceUpdatedAtUtc = updated,
            Terms = new() { Summary = "r108 합성 물품", Quantity = 1, FromUtc = DateTime.UtcNow.AddMinutes(1),
                UntilUtc = DateTime.UtcNow.AddHours(2), AgreedCostKrw = 0,
                TransferMethod = NeighborhoodTransferMethods.DriverDelivery, HandoverPlace = "r108 합성 인계 장소" }
        }, ct)));
        foreach (var action in new[] { NeighborhoodCollaborationActions.HandoverInfoConsent, NeighborhoodCollaborationActions.Agree })
            foreach (var user in new[] { provider, requester })
            {
                Entry.Stage(action == NeighborhoodCollaborationActions.Agree ? "AgreeCollaboration" : "ConsentCollaborationHandover");
                collaboration = await Command(user, collaboration, action, ct);
            }
        Entry.Stage("StartCollaboration");
        collaboration = await Command(provider, collaboration, NeighborhoodCollaborationActions.Start, ct);
        var consentId = Guid.NewGuid();
        Entry.Stage("RecordApplicationConsent");
        await As(requester, sp => sp.GetRequiredService<I신청개인정보동의증적Service>().동의기록Async(new()
        {
            증적Id = consentId, 업무Code = 신청개인정보업무Codes.운송대행, 출처Code = NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode,
            동의문버전 = 신청개인정보동의정책.현재버전, 수집이용동의 = true, 연령요건확인 = true
        }, requester, ct));
        var requestId = "r108-" + name + "-" + Guid.NewGuid().ToString("N");
        var input = new NeighborhoodDeliveryRequest
        {
            ClientRequestId = Guid.NewGuid(), MemberDeliveryRequested = true, MemberDeliveryDisclosureAgreed = true,
            MemberDeliveryNoticeVersion = NeighborhoodMemberDeliveryRoutes.NoticeVersion, PrivacyConsentEvidenceId = consentId,
            DirectPaymentAgreed = true, AgreedFareKrw = 5000, CollaborationId = collaboration.StableId,
            ExpectedTermsRevision = collaboration.TermsRevision, SourcePostId = postId, Quantity = 1, WeightKg = 1, VehicleType = "오토바이"
        };
        await As(requester, async sp =>
        {
            Entry.Stage("AcquireCollaborationFence");
            var db = sp.GetRequiredService<SsalddelContext>();
            var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input, Entry.Json)));
            var guard = sp.GetRequiredService<I생활배송협업Guard>();
            Check((await guard.획득Async(input, requester, fingerprint, ct)).IsSuccess, "CollaborationFenceRejected");
            var memo = "neighborhood-delivery:v1:" + fingerprint + ":consent:" + consentId.ToString("N")
                + 생활배송출처Evidence.Marker + postId + 생활배송배차Policy.MemberMarker + 생활배송배차Policy.신청메모(input);
            var now = DateTime.UtcNow;
            // Explicit synthetic intake fixture, as in r46. This is not customer order intake/UI evidence.
            var source = new 화주운송의뢰
            {
                의뢰Id = requestId, 화주Id = requester, 주문자UserId = requester,
                클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + input.ClientRequestId.ToString("N"),
                화물종류 = "r108 합성 생활 물품", 화물수량 = 1, 화물중량Kg = 1, 차량종류 = "오토바이", 최종운임 = 5000,
                픽업_도로명주소 = "합성 픽업 지역", 픽업_상세주소 = "비공개 표본", 픽업_연락처_이름 = "합성 제공자", 픽업_연락처_전화번호 = "01000000000",
                픽업_위도 = 37.571m, 픽업_경도 = 127.071m,
                하차_도로명주소 = "합성 전달 지역", 하차_상세주소 = "비공개 표본", 하차_연락처_이름 = "합성 수령자", 하차_연락처_전화번호 = "01000000001",
                하차_위도 = 37.58m, 하차_경도 = 127.08m,
                픽업_시간창_시작일시 = now, 픽업_시간창_종료일시 = now.AddHours(1), 하차_시간창_종료일시 = now.AddHours(2),
                정산메모 = memo, 요청사항 = "합성 fixture · 실물 거래 아님"
            };
            var queue = new 운송원장 { 의뢰Id = requestId, 운송번호 = "transport-" + requestId, 운임 = 5000,
                상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.계획배차 };
            생활배송배차Policy.큐초기화(queue, memo);
            Entry.Stage("SaveSyntheticIntake");
            db.화주운송의뢰.Add(source); db.운송원장.Add(queue);
            db.운임구성.Add(new 운임구성 { 의뢰Id = requestId, 최종운임 = 5000, 예상거리Km = 2.5m });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            Entry.Stage("ConfirmCollaborationLink");
            Check((await guard.연결확정Async(input, requester, requestId, ct)).IsSuccess, "CollaborationLinkRejected");
            Entry.Stage("RegisterMemberDeliveryAndFanout");
            await sp.GetRequiredService<NeighborhoodMemberDeliveryUseCase>().RegisterAsync(requestId, input, ct);
            return true;
        });
        Entry.Stage("ReadGeneratedOffer");
        var offers = await As(member, sp => sp.GetRequiredService<NeighborhoodMemberDeliveryUseCase>().OffersAsync(ct));
        var offer = offers.SingleOrDefault(x => x.RequestId == requestId) ?? throw new HarnessFailure("NoEligibleMemberOfferGenerated");
        Check(offer.CanAccept && offer.StateCode == "Pending" && offer.ExpiresAtUtc > DateTime.UtcNow, "FreshOfferRequired");
        var prepared = new PreparedCase(name, CargoJourneySettings.DatabaseName, requestId, offer.OfferId, member, requester,
            collaboration.StableId, consentId, offer.Revision, offer.ExpiresAtUtc);
        Entry.Stage("ValidatePreparedCanonicalState"); await Validate(prepared, ct);
        Entry.Stage("CheckFinancialBoundaryAfterPrepare");
        Check(financialBefore == await FinancialCounts(ct), "FinancialBoundaryChanged");
        Entry.Stage("WritePreparedEvidence"); await Entry.Save(Path.Combine(folder, "prepared.json"), prepared);
        await Entry.Save(Path.Combine(folder, "prepare-result.json"), new { status = "Prepared", synthetic = true,
            runStableId = settings.RunStableId,
            productUseCasesUsed = true, syntheticIntakeFixture = true, actualLoginVerified = false, actualGpsVerified = false,
            fcmSent = false, paymentChanged = false, offerExpiresAtUtc = offer.ExpiresAtUtc });
        Console.WriteLine("ExternalR108Prepared:" + name + "; fresh offer created; no FCM sent");
    }

    private Task<NeighborhoodCollaborationResponse> Command(string user, NeighborhoodCollaborationResponse current, string action, CancellationToken ct)
        => As(user, async sp => Require(await sp.GetRequiredService<I생활협업UseCase>().변경Async(current.StableId, new()
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRevision = current.Revision, Action = action, Consented = true,
            PrivacyNoticeVersion = NeighborhoodGoodsHandoverNotice.Version
        }, ct)));

    private async Task Validate(PreparedCase prepared, CancellationToken ct)
    {
        Check(prepared.Database == CargoJourneySettings.DatabaseName && prepared.RequestId.StartsWith("r108-" + prepared.Case + "-", StringComparison.Ordinal), "CaseBoundaryMismatch");
        await As(prepared.MemberUserId, async sp =>
        {
            var db = sp.GetRequiredService<SsalddelContext>(); settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
            var offer = await db.Set<NeighborhoodMemberOffer>().AsNoTracking().SingleAsync(x => x.OfferId == prepared.OfferId, ct);
            var delivery = await db.Set<NeighborhoodMemberDelivery>().AsNoTracking().SingleAsync(x => x.RequestId == prepared.RequestId, ct);
            var participation = await db.Set<NeighborhoodMemberParticipation>().AsNoTracking().SingleAsync(x => x.UserId == prepared.MemberUserId, ct);
            var source = await db.화주운송의뢰.AsNoTracking().SingleAsync(x => x.의뢰Id == prepared.RequestId, ct);
            var queue = await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == prepared.RequestId, ct);
            Check(offer.MemberUserId == prepared.MemberUserId && offer.RequestId == prepared.RequestId
                && offer.Revision == prepared.OfferRevision && offer.StateCode == "Pending" && offer.ExpiresAtUtc > DateTime.UtcNow
                && offer.ExpiresAtUtc == prepared.ExpiresAtUtc
                && offer.ParticipationRevision == participation.Revision && offer.DeliveryRevision == delivery.Revision
                && participation.ReceiveOffers && participation.ActiveRequestId is null && delivery.StateCode == "Pending"
                && delivery.OwnerUserId == prepared.OwnerUserId && delivery.AssignedMemberUserId is null
                && delivery.ConsentEvidenceId == prepared.ConsentId && delivery.WindowEndUtc > DateTime.UtcNow
                && source.주문자UserId == prepared.OwnerUserId && source.상태 == 상태값.의뢰상태.생성됨
                && prepared.OwnerUserId != prepared.MemberUserId && !source.모의처리처수행 && !source.자동처리처수행
                && queue.생활배송협업Id == prepared.CollaborationId && source.픽업_시간창_종료일시 > DateTime.UtcNow
                && queue.일반회원배송전용 && queue.생활배송수락준비완료 && queue.확정기사Id is null
                && queue.일반회원수행자UserId is null && queue.상태 == 상태값.배차대기상태.대기, "CanonicalOfferChanged");
            await sp.GetRequiredService<I신청개인정보동의증적Service>().유효한동의요구Async(prepared.ConsentId,
                신청개인정보업무Codes.운송대행, NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode, prepared.OwnerUserId, ct);
            var dto = await sp.GetRequiredService<NeighborhoodMemberDeliveryUseCase>().OfferAsync(prepared.OfferId, ct);
            Check(dto is { CanAccept: true, StateCode: "Pending" }, "ProductOfferRejected");
            return true;
        });
    }

    private async Task<string> FinancialCounts(CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
        return JsonSerializer.Serialize(new { payments = await db.결제.CountAsync(ct),
            payouts = await db.기사운송대금지급요청.CountAsync(ct), outbox = await db.기사지급Outbox.CountAsync(ct) });
    }

}
