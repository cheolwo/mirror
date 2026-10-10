using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using SkiaSharp;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Domain.Community;
using Ssalddel.Services.Community;
using Ssalddel.Services.Storage;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace NeighborhoodMemberCompletionVerification;

internal sealed class CompletionScenarios(IServiceProvider services, Uri address, Func<string, string> token, CancellationToken ct)
{
    private const string Member = "/" + NeighborhoodMemberDeliveryRoutes.Api;
    private const string Collaboration = "/" + NeighborhoodCollaborationRoutes.Api;
    private readonly List<object> calls = [];
    public IReadOnlyList<object> Calls => calls;

    public async Task<IReadOnlyList<object>> RunAsync()
    {
        await Http<object>(null, HttpMethod.Get, Member + "/offers", null, HttpStatusCode.Unauthorized);
        var results = new List<object>();
        results.Add(await Complete("offered", NeighborhoodExchange.Offer, receiverIsOwner: false));
        results.Add(await Complete("needed", NeighborhoodExchange.Need, receiverIsOwner: true));
        return results;
    }

    private async Task<object> Complete(string scenario, string roleTag, bool receiverIsOwner)
    {
        var owner = scenario + "-owner"; var requester = scenario + "-requester"; var member = scenario + "-member";
        var receiver = receiverIsOwner ? owner : requester;
        var provider = receiverIsOwner ? requester : owner;
        var callStart = calls.Count;
        var setting = new NeighborhoodMemberParticipationSaveRequest
        {
            ClientRequestId = Guid.NewGuid(), ReceiveOffers = true, ActivityLabel = "검증 전용 활동점",
            Latitude = 37.57m, Longitude = 127.07m
        };
        await Http<NeighborhoodMemberParticipationResponse>(member, HttpMethod.Put, Member + "/participation", setting);
        var sourcePost = await SeedPost(owner, roleTag);
        var work = await Http<NeighborhoodCollaborationResponse>(requester, HttpMethod.Post, Collaboration,
            new NeighborhoodCollaborationCreateRequest
            {
                ClientRequestId = Guid.NewGuid(), SourcePostId = sourcePost.Id, ExpectedSourceUpdatedAtUtc = sourcePost.UpdatedAtUtc,
                Terms = new() { Summary = scenario + " 검증용 물품", Quantity = 1, FromUtc = DateTime.UtcNow.AddMinutes(1),
                    UntilUtc = DateTime.UtcNow.AddHours(3), AgreedCostKrw = 0, TransferMethod = NeighborhoodTransferMethods.DriverDelivery,
                    HandoverPlace = "r46-private-handover-fixture" }
            }, HttpStatusCode.Created);
        foreach (var actor in new[] { owner, requester })
            work = await Command(work, actor, NeighborhoodCollaborationActions.HandoverInfoConsent, consented: true);
        foreach (var actor in new[] { owner, requester })
            work = await Command(work, actor, NeighborhoodCollaborationActions.Agree);
        Check(work.StatusCode == NeighborhoodCollaborationStates.Agreed, "BothPartiesAgreementMissing");
        work = await Command(work, provider, NeighborhoodCollaborationActions.Start);
        var requestId = await SeedDelivery(work, requester, sourcePost.Id);
        var offers = await Http<List<NeighborhoodMemberOfferResponse>>(member, HttpMethod.Get, Member + "/offers");
        var offer = offers.Single(item => item.RequestId == requestId);
        Check(offer.CanAccept, "RegisteredMemberOfferNotAcceptable");
        await Http<object>(owner, HttpMethod.Get, Member + "/offers/" + offer.OfferId, null, HttpStatusCode.NotFound);
        var accept = new NeighborhoodMemberAcceptRequest
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRevision = offer.Revision, Latitude = 37.57m,
            Longitude = 127.07m, AccuracyM = 10, LocationAtUtc = DateTime.UtcNow
        };
        var delivery = await Http<NeighborhoodMemberWorkResponse>(member, HttpMethod.Post, Member + "/offers/" + offer.OfferId + "/accept", accept);
        var acceptedReplay = await Http<NeighborhoodMemberWorkResponse>(member, HttpMethod.Post, Member + "/offers/" + offer.OfferId + "/accept", accept);
        Check(acceptedReplay.Revision == delivery.Revision && delivery.StateCode == "Assigned", "AcceptReplayChangedWork");
        Check(delivery.EvidenceStageCode == "Pickup" && !delivery.CanPickup, "InitialPickupPhotoPermissionMissing");
        await Http<object>(member, HttpMethod.Post, Member + "/work/" + requestId + "/pickup", Command(delivery.Revision), HttpStatusCode.Conflict);
        delivery = await Photo(delivery, member, "Pickup");
        Check(delivery.CanPickup, "UploadedPickupCannotComplete");
        delivery = await Http<NeighborhoodMemberWorkResponse>(member, HttpMethod.Post, Member + "/work/" + requestId + "/pickup", Command(delivery.Revision));
        Check(delivery.StateCode == "PickedUp" && delivery.EvidenceStageCode == "Delivery" && !delivery.CanDeliver, "DeliveryPhotoPermissionMissing");
        delivery = await Photo(delivery, member, "Delivery");
        Check(delivery.CanDeliver, "UploadedDeliveryCannotComplete");
        delivery = await Http<NeighborhoodMemberWorkResponse>(member, HttpMethod.Post, Member + "/work/" + requestId + "/deliver", Command(delivery.Revision));
        Check(delivery.StateCode == "Delivered" && delivery.ReceiptConfirmedAtUtc is null && delivery.Pickup is null && delivery.Dropoff is null,
            "DeliveryAndReceiptNotSeparated");
        await Http<object>(owner, HttpMethod.Get, Member + "/work/" + requestId, null, HttpStatusCode.NotFound);
        work = await Http<NeighborhoodCollaborationResponse>(provider, HttpMethod.Get, Collaboration + "/" + work.StableId);
        work = await Command(work, provider, NeighborhoodCollaborationActions.ProposeCompletion);

        // 잘못된 당사자의 확인은 Mongo intent와 SQL receipt를 모두 만들지 않아야 한다.
        var before = await ReadDatabase(requestId, work.StableId);
        await Http<object>(provider, HttpMethod.Post, Collaboration + "/" + work.StableId + "/commands",
            new NeighborhoodCollaborationCommandRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = work.Revision,
                Action = NeighborhoodCollaborationActions.ConfirmCompletion }, HttpStatusCode.Conflict);
        var rejected = await ReadDatabase(requestId, work.StableId);
        Check(rejected.ReceiptCount == 0 && rejected.CollaborationRevision == before.CollaborationRevision, "WrongReceiverChangedReceiptOrMongo");
        var confirm = new NeighborhoodCollaborationCommandRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = work.Revision,
            Action = NeighborhoodCollaborationActions.ConfirmCompletion };
        work = await Http<NeighborhoodCollaborationResponse>(receiver, HttpMethod.Post, Collaboration + "/" + work.StableId + "/commands", confirm);
        var replay = await Http<NeighborhoodCollaborationResponse>(receiver, HttpMethod.Post, Collaboration + "/" + work.StableId + "/commands", confirm);
        Check(work.StatusCode == NeighborhoodCollaborationStates.Completed && replay.IdempotentReplay && replay.Revision == work.Revision,
            "ReceiptReplayChangedCollaboration");
        var finalDelivery = await Http<NeighborhoodMemberWorkResponse>(member, HttpMethod.Get, Member + "/work/" + requestId);
        Check(finalDelivery.StateCode == "Delivered" && finalDelivery.ReceiptConfirmedAtUtc.HasValue && finalDelivery.EvidenceStageCode == ""
            && finalDelivery.Pickup is null && finalDelivery.Dropoff is null, "ReceiptReadMissingOrPrivatePlaceExposed");
        await Http<object>(scenario + "-stranger", HttpMethod.Get, Collaboration + "/" + work.StableId, null, HttpStatusCode.NotFound);
        var after = await ReadDatabase(requestId, work.StableId);
        Check(after.ReceiptCount == 1 && after.SourceStatus == 상태값.배차상태.인수완료
            && after.CollaborationStatus == NeighborhoodCollaborationStates.Completed && after.ReceiptIntentConfirmed,
            "SqlMongoReceiptStateMismatch");
        Check(after.PaymentStatus == before.PaymentStatus && after.SettlementStatus == before.SettlementStatus && after.FareKrw == before.FareKrw,
            "ReceiptChangedPaymentOrFare");
        Check(after.MemberState == "Delivered" && after.MemberSlotReleased && after.MemberAssigned && after.DriverIdAbsent
            && after.PhotosBound == 2 && after.PhotoByteHashesVerified && after.MongoProtected,
            "MemberPhotoOrPrivacyBindingMismatch");
        return new { scenario, sourceRoleTag = roleTag, receiverRole = receiverIsOwner ? "owner" : "requester", requestId,
            collaborationId = work.StableId, work.Revision, initialPhotoPermission = true, pickupPhotoAndCompletion = true,
            deliveryPhotoAndCompletion = true, wrongReceiverRejectedWithoutWrite = true, idempotentReceiptReplay = true,
            independentSqlMongoRead = after, paymentAndFarePreserved = true, httpCalls = calls.Skip(callStart).ToArray() };
    }

    private async Task<NeighborhoodMemberWorkResponse> Photo(NeighborhoodMemberWorkResponse work, string actor, string stage)
    {
        using var bitmap = new SKBitmap(8, 8); bitmap.Erase(stage == "Pickup" ? SKColors.Blue : SKColors.Green);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var request = new NeighborhoodMemberEvidenceRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = work.Revision,
            StageCode = stage, ContentType = "image/png", ContentBase64 = Convert.ToBase64String(data.ToArray()) };
        var path = Member + "/work/" + work.RequestId;
        var result = await Http<NeighborhoodMemberWorkResponse>(actor, HttpMethod.Post, path + "/evidence", request);
        var replay = await Http<NeighborhoodMemberWorkResponse>(actor, HttpMethod.Post, path + "/evidence", request);
        Check(result.Revision == replay.Revision, "PhotoReplayChangedWork");
        var receipt = await Http<NeighborhoodMemberEvidenceReceiptResponse>(actor, HttpMethod.Get, path + "/evidence-receipts/" + request.ClientRequestId);
        Check(receipt.Committed && receipt.StageCode == stage && receipt.WorkRevision == result.Revision, "PhotoReceiptNotRecoverable");
        return result;
    }

    private async Task<NeighborhoodCollaborationResponse> Command(NeighborhoodCollaborationResponse current, string actor, string action, bool? consented = null)
        => await Http<NeighborhoodCollaborationResponse>(actor, HttpMethod.Post, Collaboration + "/" + current.StableId + "/commands",
            new NeighborhoodCollaborationCommandRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = current.Revision,
                Action = action, Consented = consented, PrivacyNoticeVersion = NeighborhoodGoodsHandoverNotice.Version });
    private static NeighborhoodMemberCommandRequest Command(long revision) => new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = revision };

    private async Task<PlatformCommunityPost> SeedPost(string owner, string roleTag)
    {
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
        var post = new PlatformCommunityPost { AuthorUserId = owner, Category = PlatformCommunityPostCategories.General,
            WorkflowTag = NeighborhoodExchange.WorkflowTag, RoleTag = roleTag, Title = "r46 합성 물품 교류",
            Body = "격리 검증용 공개 표본", Nickname = "검증 표본", UpdatedAtUtc = Microseconds(DateTime.UtcNow) };
        db.PlatformCommunityPosts.Add(post); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        return await db.PlatformCommunityPosts.AsNoTracking().SingleAsync(item => item.Id == post.Id, ct);
    }

    private async Task<string> SeedDelivery(NeighborhoodCollaborationResponse collaboration, string actor, long postId)
    {
        using var scope = services.CreateScope(); var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<SsalddelContext>(); var consent = sp.GetRequiredService<SyntheticConsent>();
        var requestId = "r46-" + Guid.NewGuid().ToString("N"); var now = DateTime.UtcNow;
        var input = new NeighborhoodDeliveryRequest { ClientRequestId = Guid.NewGuid(), MemberDeliveryRequested = true,
            MemberDeliveryDisclosureAgreed = true, MemberDeliveryNoticeVersion = NeighborhoodMemberDeliveryRoutes.NoticeVersion,
            PrivacyConsentEvidenceId = consent.Id, DirectPaymentAgreed = true, AgreedFareKrw = 5000,
            CollaborationId = collaboration.StableId, ExpectedTermsRevision = collaboration.TermsRevision,
            SourcePostId = postId, Quantity = 1, WeightKg = 1, VehicleType = "오토바이" };
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input, Program.Json)));
        var guard = sp.GetRequiredService<I생활배송협업Guard>();
        Check((await guard.획득Async(input, actor, digest, ct)).IsSuccess, "FixtureCollaborationIntakeFenceFailed");
        var memo = "neighborhood-delivery:v1:" + digest + ":consent:" + consent.Id.ToString("N")
            + 생활배송출처Evidence.Marker + postId + 생활배송배차Policy.MemberMarker + 생활배송배차Policy.신청메모(input);
        var source = new 화주운송의뢰 { 의뢰Id = requestId, 화주Id = actor, 주문자UserId = actor,
            클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + input.ClientRequestId.ToString("N"),
            화물종류 = "생활 물품", 화물수량 = 1, 화물중량Kg = 1, 차량종류 = "오토바이", 최종운임 = 5000,
            픽업_도로명주소 = "검증용 픽업 장소", 픽업_상세주소 = "비공개 표본", 픽업_연락처_이름 = "가상 제공자",
            픽업_연락처_전화번호 = "01000000000", 픽업_위도 = 37.571m, 픽업_경도 = 127.071m,
            하차_도로명주소 = "검증용 전달 장소", 하차_상세주소 = "비공개 표본", 하차_연락처_이름 = "가상 수령자",
            하차_연락처_전화번호 = "01000000001", 하차_위도 = 37.58m, 하차_경도 = 127.08m,
            픽업_시간창_시작일시 = now, 픽업_시간창_종료일시 = now.AddHours(1), 하차_시간창_종료일시 = now.AddHours(2),
            정산메모 = memo, 요청사항 = "격리 검증 표본" };
        var queue = new 운송원장 { 의뢰Id = requestId, 운송번호 = "transport-" + requestId, 운임 = 5000,
            상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.계획배차 };
        생활배송배차Policy.큐초기화(queue, memo);
        db.화주운송의뢰.Add(source); db.운송원장.Add(queue);
        db.운임구성.Add(new 운임구성 { 의뢰Id = requestId, 최종운임 = 5000, 예상거리Km = 2.5m });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        Check((await guard.연결확정Async(input, actor, requestId, ct)).IsSuccess, "FixtureCollaborationIntakeLinkFailed");
        await FixtureMemberService.ForActor(sp, actor).RegisterAsync(requestId, input, ct);
        return requestId;
    }

    private async Task<DatabaseEvidence> ReadDatabase(string requestId, string collaborationId)
    {
        // HTTP 처리 scope와 다른 SQL context와 Mongo read로 같은 요청을 대조한다.
        using var scope = services.CreateScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<SsalddelContext>();
        var source = await db.화주운송의뢰.AsNoTracking().SingleAsync(item => item.의뢰Id == requestId, ct);
        var queue = await db.운송원장.AsNoTracking().SingleAsync(item => item.의뢰Id == requestId, ct);
        var member = await db.Set<NeighborhoodMemberDelivery>().AsNoTracking().SingleAsync(item => item.RequestId == requestId, ct);
        var slot = await db.Set<NeighborhoodMemberParticipation>().AsNoTracking().SingleAsync(item => item.UserId == member.AssignedMemberUserId, ct);
        var photos = await db.Set<NeighborhoodMemberPhoto>().AsNoTracking().Where(item => item.RequestId == requestId).ToListAsync(ct);
        var encryption = sp.GetRequiredService<IPersonalDataEncryptionService>(); var storage = sp.GetRequiredService<IObjectStorageService>();
        var hashesVerified = true;
        foreach (var photo in photos)
        {
            using var pointer = JsonDocument.Parse(encryption.Unprotect(photo.PrivatePayload)!);
            var bytes = await storage.DownloadAsync(pointer.RootElement.GetProperty("container").GetString()!, pointer.RootElement.GetProperty("objectName").GetString()!, ct);
            hashesVerified &= Convert.ToHexString(SHA256.HashData(bytes)) == photo.StoredSha256;
        }
        var collaboration = (await sp.GetRequiredService<I생활협업Store>().조회Async(collaborationId, ct))!;
        var mongo = sp.GetRequiredService<IMongoClient>().GetDatabase(sp.GetRequiredService<IOptions<MongoDbOptions>>().Value.Database);
        var raw = await mongo.GetCollection<BsonDocument>(Mongo생활협업Store.CollectionName)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", collaborationId)).SingleAsync(ct);
        var protectedJson = raw.ToJson();
        var sync = await sp.GetRequiredService<I운송원장Mongo동기화Service>().상태조회Async(requestId, ct);
        Check(sync.Mongo원장존재, "ActualMongoTransportProjectionMissing");
        var ledger = await sp.GetRequiredService<I커뮤니티원장저장소>()
            .원장조회Async(운송원장Mongo동기화Builder.원장Id생성(requestId), ct);
        Check(ledger is not null, "IndependentMongoTransportReadMissing");
        var requestBlock = ledger!.블록목록.SingleOrDefault(item => item.BlockId == "transport-request");
        var transportMatches = ledger.외부참조.GetValueOrDefault("화주운송의뢰Id") == requestId
            && ledger.확장속성.GetValueOrDefault("RdbBusinessSnapshotRequestId") == requestId
            && ledger.확장속성.GetValueOrDefault("RdbBusinessSnapshotSource") == "persisted-rdb-v1"
            && ledger.확장속성.GetValueOrDefault("의뢰상태") == source.상태
            && ledger.확장속성.GetValueOrDefault("배차상태") == source.배차상태
            && requestBlock?.Data.GetValueOrDefault("의뢰Id") == requestId
            && requestBlock.Data.GetValueOrDefault("의뢰상태") == source.상태
            && requestBlock.Data.GetValueOrDefault("배차상태") == source.배차상태
            && (source.배차상태 != 상태값.배차상태.인수완료 || ledger.상태 == 커뮤니티원장상태.완료);
        Check(transportMatches, "MongoTransportSqlStateMismatch");
        // 현재 운송 투영에는 협업/조건/일반회원 필드가 없다. 보호된 협업 Mongo를 SQL과 별도로 대조한다.
        var collaborationMatches = collaboration.LinkedDeliveryRequestId == requestId
            && collaboration.StableId == queue.생활배송협업Id && collaboration.TermsRevision == queue.생활배송조건판본
            && collaboration.DeliveryIntent?.StateCode == "confirmed"
            && collaboration.DeliveryIntent.TermsRevision == collaboration.TermsRevision
            && collaboration.DeliveryIntent.ActorUserId == source.주문자UserId && member.OwnerUserId == source.주문자UserId
            && queue.일반회원수행자UserId == member.AssignedMemberUserId;
        Check(collaborationMatches, "MongoCollaborationSqlMemberBindingMismatch");
        return new(collaboration.Revision, collaboration.StatusCode, collaboration.ReceiptConfirmationIntent?.StateCode == "confirmed",
            await db.Set<NeighborhoodMemberReceipt>().CountAsync(item => item.AggregateId == requestId && item.Kind == "ReceiptConfirmation", ct),
            source.배차상태, source.결제상태, source.정산상태, source.최종운임, member.StateCode,
            slot.ActiveRequestId is null, queue.일반회원수행자UserId == member.AssignedMemberUserId, queue.확정기사Id is null,
            photos.Count(item => item.StateCode == "Bound"), hashesVerified,
            raw.Contains("ProtectedPayload") && !protectedJson.Contains("r46-private-handover-fixture", StringComparison.Ordinal)
                && !protectedJson.Contains(collaboration.OwnerUserId, StringComparison.Ordinal), sync.Mongo원장존재,
            transportMatches, collaborationMatches, ledger.Revision);
    }

    private async Task<T> Http<T>(string? actor, HttpMethod method, string path, object? body = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = address };
        using var request = new HttpRequestMessage(method, path);
        if (actor is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token(actor));
        if (body is not null) request.Content = JsonContent.Create(body, options: Program.Json);
        using var response = await client.SendAsync(request, ct);
        calls.Add(new { actor = actor ?? "anonymous", method = method.Method, path, status = (int)response.StatusCode });
        if (response.StatusCode != expected)
        {
            var code = "UnexpectedHttpStatus";
            try { using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (problem.RootElement.TryGetProperty("code", out var value) || problem.RootElement.TryGetProperty("errorCode", out value)) code += ":" + value.GetString(); } catch (JsonException) { }
            throw new VerificationFailure(code + ":" + (int)response.StatusCode);
        }
        if ((int)expected >= 400) return default!;
        Check(response.Headers.CacheControl?.NoStore == true, "ProductPrivateHttpCachePolicyMissing");
        return (await response.Content.ReadFromJsonAsync<T>(Program.Json, ct)) ?? throw new VerificationFailure("EmptyProductHttpResponse");
    }

    private static DateTime Microseconds(DateTime value) => new(value.Ticks / 10 * 10, DateTimeKind.Utc);
    private static void Check(bool condition, string code) { if (!condition) throw new VerificationFailure(code); }
    private sealed record DatabaseEvidence(long CollaborationRevision, string CollaborationStatus, bool ReceiptIntentConfirmed,
        int ReceiptCount, string SourceStatus, string PaymentStatus, string SettlementStatus, decimal? FareKrw, string MemberState,
        bool MemberSlotReleased, bool MemberAssigned, bool DriverIdAbsent, int PhotosBound, bool PhotoByteHashesVerified,
        bool MongoProtected, bool ActualMongoTransportProjectionExists, bool MongoTransportSqlStateMatches,
        bool MongoCollaborationSqlMemberBindingMatches, long MongoTransportRevision);
}
