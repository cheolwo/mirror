using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.DataProtection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Services.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Tests.Services.Community;

public sealed class NeighborhoodStorageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
    private const string Region = "region:kr:hjd:1126057500";

    [Fact]
    public async Task 예약_요청Guid도_BSON_표준UUID로_저장하고_재조회할수있다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Contexts.Set("work-1", created.SpaceId);
        var request = Reserve("work-1", created.Revision);
        Success(await fixture.Requester.예약Async(created.SpaceId, request));
        var stored = fixture.Store.Documents[created.SpaceId];
        var bson = stored.ToBsonDocument();
        var reservation = bson["Reservations"].AsBsonArray.Single().AsBsonDocument;
        Assert.Equal(BsonBinarySubType.UuidStandard, reservation["IntentRequestId"].AsBsonBinaryData.SubType);
        Assert.Equal(request.RequestId, reservation["IntentRequestId"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard));
        var reread = BsonSerializer.Deserialize<생활보관공간문서>(bson);
        Assert.Equal(request.RequestId, Assert.Single(reread.Reservations).IntentRequestId);
        Assert.Equal(stored.ProtectedHandover, reread.ProtectedHandover);
        Assert.Equal(stored.Revision, reread.Revision);
    }

    [Fact]
    public void 인계_주소연락출입정보는_기존_ISMSP_전송보호_대상이고_공개필드와_구분한다()
    {
        Assert.True(SsalddelIsmsPClientEncryptionService.RequiresEncryptedTransport<NeighborhoodStorageSpaceRequest>());
        var plan = SsalddelIsmsPClientEncryptionService.BuildProtectionPlan<NeighborhoodStorageSpaceRequest>();
        Assert.False(plan.HasUnknownFields);
        Assert.Contains(plan.Rules, x => x.FieldKey == PersonalDataFieldKey.DetailedAddress);
        Assert.Contains(plan.Rules, x => x.FieldKey == PersonalDataFieldKey.PhoneNumber);
        var responseFields = IsmsPProtectedDataAttributeReader.Read<NeighborhoodStorageSpaceDto>();
        Assert.Equal(3, responseFields.Count);
        Assert.All(responseFields, field => Assert.True(field.IsPersonalData));
        Assert.Empty(IsmsPProtectedDataAttributeReader.Read<NeighborhoodStoragePublicDto>());
    }

    [Fact]
    public async Task 일반_회원이_한상자_능력을_등록하고_암호화_인계정보를_본인만_재조회한다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        Assert.Equal(NeighborhoodStorageStatus.Published, created.Status);
        Assert.True(created.IsOwner);
        Assert.Equal("시험로 123 1층", created.PrivateAddress);
        var document = Assert.Single(fixture.Store.Documents.Values);
        var bson = document.ToBsonDocument().ToJson();
        Assert.StartsWith("pd:v1:", document.ProtectedHandover, StringComparison.Ordinal);
        Assert.DoesNotContain("시험로 123", bson, StringComparison.Ordinal);
        Assert.DoesNotContain("010-1111-2222", bson, StringComparison.Ordinal);
        Assert.DoesNotContain("출입 안내", bson, StringComparison.Ordinal);
        Assert.Null(await fixture.Stranger.비공개상세Async(created.SpaceId));
        Assert.Single(await fixture.Owner.내목록Async());
        Assert.Empty(await fixture.Stranger.내목록Async());
        Assert.Equal(created.PrivateAddress, (await fixture.Owner.비공개상세Async(created.SpaceId))!.PrivateAddress);
    }

    [Fact]
    public async Task 공개_목록_지도_상세에는_동네대표점과_능력만_포함한다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        var list = await fixture.Stranger.공개목록Async(Region);
        var item = Assert.Single(list.Items);
        Assert.Equal(created.SpaceId, item.SpaceId);
        Assert.Equal("neighborhood", item.Region.PrecisionCode);
        Assert.Equal(Region, item.Region.RegionKey);
        var json = JsonSerializer.Serialize(list);
        Assert.DoesNotContain("owner-1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivateAddress", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RequesterUserId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("010-1111-2222", json, StringComparison.Ordinal);
        Assert.DoesNotContain("시험로 123", json, StringComparison.Ordinal);
        Assert.Equal(1, Assert.Single((await fixture.Owner.공개지도Async()).Items).SpaceCount);
        Assert.NotNull(await fixture.Stranger.공개상세Async(created.SpaceId));
        Assert.Empty((await fixture.Owner.공개목록Async("region:kr:hjd:1199999999")).Items);
    }

    [Fact]
    public async Task 초안은_공개되지_않고_본인의_명시적_공개와_중지만_반영한다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request(publish: false)));
        Assert.Empty((await fixture.Stranger.공개목록Async(null)).Items);
        Failure(await fixture.Stranger.상태변경Async(created.SpaceId, NeighborhoodStorageStatus.Published, Mutation(created.Revision)), "NotFound");
        var published = Success(await fixture.Owner.상태변경Async(created.SpaceId, NeighborhoodStorageStatus.Published, Mutation(created.Revision)));
        Assert.Equal(2, published.Public.OfferRevision);
        Assert.Single((await fixture.Stranger.공개목록Async(null)).Items);
        var paused = Success(await fixture.Owner.상태변경Async(created.SpaceId, NeighborhoodStorageStatus.Paused, Mutation(published.Revision)));
        Assert.Empty((await fixture.Stranger.공개지도Async()).Items);
        Assert.Null(await fixture.Stranger.공개상세Async(created.SpaceId));
        var closed = Success(await fixture.Owner.상태변경Async(created.SpaceId, NeighborhoodStorageStatus.Closed, Mutation(paused.Revision)));
        Failure(await fixture.Owner.상태변경Async(created.SpaceId, NeighborhoodStorageStatus.Published, Mutation(closed.Revision)), "SpaceClosed");
    }

    [Fact]
    public async Task 생성_재전송은_원본_하나이고_내용변경과_타인의_요청결과는_거절한다()
    {
        var fixture = new Fixture();
        var request = Request();
        var created = Success(await fixture.Owner.등록Async(request));
        var replay = Success(await fixture.Owner.등록Async(request));
        Assert.True(replay.IdempotentReplay);
        Assert.Equal(created.SpaceId, replay.SpaceId);
        Assert.Single(fixture.Store.Documents);
        Assert.Single(fixture.Store.Documents[created.SpaceId].Operations);
        Failure(await fixture.Owner.등록Async(Request(request.RequestId, title: "다른 물건")), "IdempotencyConflict");
        var result = await fixture.Owner.요청결과Async(request.RequestId);
        Assert.True(result!.IdempotentReplay);
        Assert.Null(await fixture.Stranger.요청결과Async(request.RequestId, created.SpaceId));
    }

    [Fact]
    public async Task 저장_뒤_응답이_유실돼도_같은_요청과_조회로_복구한다()
    {
        var fixture = new Fixture();
        var request = Request();
        fixture.Store.ThrowAfterWrite = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.Owner.등록Async(request));
        Assert.NotNull(await fixture.Owner.요청결과Async(request.RequestId));
        Assert.True(Success(await fixture.Owner.등록Async(request)).IdempotentReplay);
        Assert.Single(fixture.Store.Documents);
    }

    [Fact]
    public async Task 수정은_현재판본_CAS이며_예약과_조건판본을_구분한다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        var edit = Request(revision: created.Revision, title: "책 한 상자 잠시 맡습니다");
        var updated = Success(await fixture.Owner.수정Async(created.SpaceId, edit));
        Assert.Equal(2, updated.Revision);
        Assert.Equal(2, updated.Public.OfferRevision);
        Failure(await fixture.Owner.수정Async(created.SpaceId, Request(revision: created.Revision)), "RevisionConflict");
        Assert.True(Success(await fixture.Owner.수정Async(created.SpaceId, edit)).IdempotentReplay);
        fixture.Contexts.Set("work-1", created.SpaceId, offerRevision: updated.Public.OfferRevision);
        var reserved = Success(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-1", updated.Revision)));
        Assert.Equal(3, reserved.Revision);
        Assert.Equal(2, reserved.Public.OfferRevision);
        Assert.Equal(NeighborhoodStorageReservationStatus.Reserved, Assert.Single(reserved.Reservations).Status);
    }

    [Theory]
    [InlineData("불확실한 동네", 1, 1)]
    [InlineData(Region, 0, 1)]
    [InlineData(Region, -1, 1)]
    [InlineData(Region, 1001, 1)]
    [InlineData(Region, 1, -1)]
    public async Task 미지원지역_음수과다수량_역순시간을_거절한다(string region, int capacity, int endHours)
    {
        var fixture = new Fixture();
        Failure(await fixture.Owner.등록Async(Request(region: region, capacity: capacity, until: Now.AddHours(endHours))), "InvalidRequest");
        Assert.Empty(fixture.Store.Documents);
    }

    [Theory]
    [InlineData("시험로 123 2층")]
    [InlineData("010-1111-2222로 연락")]
    [InlineData("연락01011112222로 주세요")]
    [InlineData("02-123-4567입니다")]
    [InlineData("공동현관: 1234")]
    [InlineData("101동 202호")]
    [InlineData("test@example.com")]
    public async Task 공개문장의_흔한_상세주소_연락처_출입코드는_비공개필드로_안내한다(string text)
    {
        var fixture = new Fixture();
        Failure(await fixture.Owner.등록Async(Request(title: text)), "InvalidRequest");
        Assert.Empty(fixture.Store.Documents);
    }

    [Fact]
    public async Task 현재_양측조건_정보제공동의_참여자_공간을_모두_검사한다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Contexts.Set("work-1", created.SpaceId, accepted: false);
        Failure(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-1", created.Revision)), "AgreementRequired");
        Assert.Null(await fixture.Requester.비공개상세Async(created.SpaceId, "work-1"));
        fixture.Contexts.Set("work-1", created.SpaceId, disclosure: false);
        Failure(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-1", created.Revision)), "AgreementRequired");
        fixture.Contexts.Set("work-1", created.SpaceId);
        Assert.Null(await fixture.Stranger.비공개상세Async(created.SpaceId, "work-1"));
        Failure(await fixture.Stranger.예약Async(created.SpaceId, Reserve("work-1", created.Revision)), "NotFound");
        fixture.Contexts.Set("work-wrong", "different-space");
        Assert.Null(await fixture.Requester.비공개상세Async(created.SpaceId, "work-wrong"));
        Assert.NotNull(await fixture.Requester.비공개상세Async(created.SpaceId, "work-1"));
        fixture.Contexts.Set("work-1", created.SpaceId, closed: true);
        Assert.Null(await fixture.Requester.비공개상세Async(created.SpaceId, "work-1"));
    }

    [Theory]
    [InlineData(-1, "상자", 1, 3)]
    [InlineData(2, "상자", 1, 3)]
    [InlineData(1, "개", 1, 3)]
    [InlineData(1, "상자", 3, 2)]
    [InlineData(1, "상자", 0, 3)]
    [InlineData(1, "상자", 1, 7)]
    public async Task 합의조건의_수량단위_기간이_공간을_벗어나면_예약할수없다(int quantity, string unit, int fromHours, int untilHours)
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Contexts.Set("work-1", created.SpaceId, quantity: quantity, unit: unit,
            from: Now.AddHours(fromHours), until: Now.AddHours(untilHours));
        Failure(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-1", created.Revision)), "InvalidReservation");
        Assert.Empty(fixture.Store.Documents[created.SpaceId].Reservations);
    }

    [Fact]
    public async Task 같은시간_중복예약은_한문서_CAS로_한건만_성공한다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Contexts.Set("work-1", created.SpaceId);
        fixture.Contexts.Set("work-2", created.SpaceId, requester: "requester-2");
        fixture.Store.BarrierRevision = created.Revision;
        var second = fixture.Service("requester-2");
        var results = await Task.WhenAll(fixture.Requester.예약Async(created.SpaceId, Reserve("work-1", created.Revision)),
            second.예약Async(created.SpaceId, Reserve("work-2", created.Revision)));
        Assert.Single(results.Where(x => x.IsSuccess));
        Failure(Assert.Single(results.Where(x => x.IsFailed)), "RevisionConflict");
        var document = fixture.Store.Documents[created.SpaceId];
        Assert.Single(document.Reservations);
        Assert.Equal(3, document.Revision); // 승리 예약과 패배 요청의 abort fence가 각각 한 판본을 저장합니다.
        var loser = results[0].IsSuccess ? second : fixture.Requester;
        var workId = results[0].IsSuccess ? "work-2" : "work-1";
        Failure(await loser.예약Async(created.SpaceId, Reserve(workId, document.Revision)), "CapacityExceeded");
    }

    [Fact]
    public async Task 종료시간과_다음시작시간이_같거나_겹치지않는예약은_같은용량을_재사용한다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Contexts.Set("work-1", created.SpaceId, from: Now.AddHours(1), until: Now.AddHours(2));
        var first = Success(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-1", created.Revision)));
        fixture.Contexts.Set("work-2", created.SpaceId, from: Now.AddHours(2), until: Now.AddHours(3));
        var second = Success(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-2", first.Revision)));
        Assert.Equal("work-2", Assert.Single(second.Reservations).CollaborationId);
        Assert.Equal(2, fixture.Store.Documents[created.SpaceId].Reservations.Count);
        Assert.Equal(1, 생활보관수량Policy.최대동시수량(fixture.Store.Documents[created.SpaceId].Reservations,
            Now.AddHours(1).UtcDateTime, Now.AddHours(3).UtcDateTime));
    }

    [Fact]
    public async Task 인수_일부확인과_기간초과보관도_반환완료전에는_용량을_차지한다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Contexts.Set("work-1", created.SpaceId, from: Now.AddHours(1), until: Now.AddHours(2));
        var reserved = Success(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-1", created.Revision)));
        var partial = Success(await fixture.Owner.예약변경Async(created.SpaceId, "work-1", Action(reserved.Revision, "confirm-intake")));
        fixture.Contexts.Set("work-2", created.SpaceId, from: Now.AddHours(2), until: Now.AddHours(3));
        Failure(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-2", partial.Revision)), "CapacityExceeded");
        Failure(await fixture.Requester.예약변경Async(created.SpaceId, "work-1", Action(partial.Revision, "cancel")), "InvalidTransition");
        var ownerReturn = Success(await fixture.Owner.예약변경Async(created.SpaceId, "work-1", Action(partial.Revision, "confirm-return")));
        Failure(await fixture.Requester.예약변경Async(created.SpaceId, "work-1", Action(ownerReturn.Revision, "confirm-intake")), "InvalidTransition");
        var bothReturn = Success(await fixture.Requester.예약변경Async(created.SpaceId, "work-1", Action(ownerReturn.Revision, "confirm-return")));
        Assert.Equal(NeighborhoodStorageReservationStatus.Returned, Assert.Single(bothReturn.Reservations).Status);
    }

    [Fact]
    public async Task 양측인수와_양측반환_확인후에만_보관이_종결된다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Contexts.Set("work-1", created.SpaceId);
        var reserved = Success(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-1", created.Revision)));
        Failure(await fixture.Requester.예약변경Async(created.SpaceId, "work-1", Action(reserved.Revision, "confirm-return")), "InvalidTransition");
        var one = Success(await fixture.Owner.예약변경Async(created.SpaceId, "work-1", Action(reserved.Revision, "confirm-intake")));
        Assert.Equal(NeighborhoodStorageReservationStatus.Reserved, Assert.Single(one.Reservations).Status);
        var two = Success(await fixture.Requester.예약변경Async(created.SpaceId, "work-1", Action(one.Revision, "confirm-intake")));
        Assert.Equal(NeighborhoodStorageReservationStatus.InCustody, Assert.Single(two.Reservations).Status);
        var ownerReturn = Success(await fixture.Owner.예약변경Async(created.SpaceId, "work-1", Action(two.Revision, "confirm-return")));
        Assert.Equal(NeighborhoodStorageReservationStatus.InCustody, Assert.Single(ownerReturn.Reservations).Status);
        var intentId = fixture.Store.Documents[created.SpaceId].Reservations.Single().IntentRequestId;
        Assert.Equal("confirmed", fixture.Guard.Intents[intentId].StateCode);
        var requesterReturn = Success(await fixture.Requester.예약변경Async(created.SpaceId, "work-1", Action(ownerReturn.Revision, "confirm-return")));
        Assert.Equal(NeighborhoodStorageReservationStatus.Returned, Assert.Single(requesterReturn.Reservations).Status);
        Assert.Equal("released", fixture.Guard.Intents[intentId].StateCode);
        Assert.Empty(Assert.Single(requesterReturn.Reservations).AllowedActions);
        Assert.Equal(0, 생활보관수량Policy.최대동시수량(fixture.Store.Documents[created.SpaceId].Reservations,
            Now.AddHours(1).UtcDateTime, Now.AddHours(6).UtcDateTime));
    }

    [Fact]
    public async Task 중지와_종료는_새예약만막고_기존인수반환을_보존한다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Contexts.Set("work-1", created.SpaceId);
        fixture.Contexts.Set("work-2", created.SpaceId, from: Now.AddHours(4), until: Now.AddHours(5));
        var reserved = Success(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-1", created.Revision)));
        var paused = Success(await fixture.Owner.상태변경Async(created.SpaceId, NeighborhoodStorageStatus.Paused, Mutation(reserved.Revision)));
        Failure(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-2", paused.Revision)), "SpaceUnavailable");
        var one = Success(await fixture.Owner.예약변경Async(created.SpaceId, "work-1", Action(paused.Revision, "confirm-intake")));
        var two = Success(await fixture.Requester.예약변경Async(created.SpaceId, "work-1", Action(one.Revision, "confirm-intake")));
        var closed = Success(await fixture.Owner.상태변경Async(created.SpaceId, NeighborhoodStorageStatus.Closed, Mutation(two.Revision)));
        var returnedOne = Success(await fixture.Owner.예약변경Async(created.SpaceId, "work-1", Action(closed.Revision, "confirm-return")));
        var returnedBoth = Success(await fixture.Requester.예약변경Async(created.SpaceId, "work-1", Action(returnedOne.Revision, "confirm-return")));
        Assert.Equal(NeighborhoodStorageStatus.Closed, returnedBoth.Status);
        Assert.Equal(NeighborhoodStorageReservationStatus.Returned, Assert.Single(returnedBoth.Reservations).Status);
    }

    [Fact]
    public async Task 예약이_있으면_인계장소와_종류단위_수량축소를_임의로_변경할수없다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Contexts.Set("work-1", created.SpaceId);
        var reserved = Success(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-1", created.Revision)));
        Failure(await fixture.Owner.수정Async(created.SpaceId, Request(revision: reserved.Revision, address: "다른 시험로 999")), "ReservationConflict");
        Failure(await fixture.Owner.수정Async(created.SpaceId, Request(revision: reserved.Revision, capacity: 0.5m)), "ReservationConflict");
        var updated = Success(await fixture.Owner.수정Async(created.SpaceId, Request(revision: reserved.Revision, title: "같은 장소 보관")));
        Assert.Single(updated.Reservations);
        Assert.Equal("시험로 123 1층", updated.PrivateAddress);
    }

    [Fact]
    public async Task 다른_협업자의_원장을_노출하지않고_철회후_취소응답도_주소를가린다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request(capacity: 2)));
        fixture.Contexts.Set("work-1", created.SpaceId);
        fixture.Contexts.Set("work-2", created.SpaceId, requester: "requester-2");
        var first = Success(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-1", created.Revision)));
        var second = Success(await fixture.Service("requester-2").예약Async(created.SpaceId, Reserve("work-2", first.Revision)));
        var detail = await fixture.Requester.비공개상세Async(created.SpaceId, "work-1");
        Assert.Equal("work-1", Assert.Single(detail!.Reservations).CollaborationId);
        fixture.Contexts.Set("work-1", created.SpaceId, disclosure: false);
        Assert.Null(await fixture.Requester.비공개상세Async(created.SpaceId, "work-1"));
        var cancelRequest = Action(second.Revision, "cancel");
        var cancelled = Success(await fixture.Requester.예약변경Async(created.SpaceId, "work-1", cancelRequest));
        Assert.Empty(cancelled.PrivateAddress);
        Assert.Empty(cancelled.PrivateContact);
        Assert.Empty(cancelled.PrivateHandoverInstructions);
        Assert.Equal(NeighborhoodStorageReservationStatus.Cancelled, Assert.Single(cancelled.Reservations).Status);
        Assert.Equal("released", fixture.Guard.Intents[fixture.Store.Documents[created.SpaceId].Reservations.Single(x => x.CollaborationId == "work-1").IntentRequestId].StateCode);
        var recovered = await fixture.Requester.요청결과Async(cancelRequest.RequestId, created.SpaceId);
        Assert.True(recovered!.IdempotentReplay);
        Assert.Empty(recovered.PrivateAddress);
        Assert.Equal(NeighborhoodStorageReservationStatus.Cancelled, Assert.Single(recovered.Reservations).Status);
    }

    [Fact]
    public async Task 만료된_공간과_비로그인_쓰기를_노출하거나_허용하지않는다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Clock.Value = Now.AddHours(7);
        Assert.Empty((await fixture.Stranger.공개목록Async(null)).Items);
        Assert.Empty((await fixture.Stranger.공개지도Async()).Items);
        Assert.NotNull(await fixture.Owner.비공개상세Async(created.SpaceId));
        Failure(await fixture.Service(null).등록Async(Request()), "AuthenticationRequired");
    }

    [Fact]
    public async Task 예약_저장뒤_합의확정응답이_유실돼도_조회로_원래예약을_확정한다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Contexts.Set("work-1", created.SpaceId);
        var request = Reserve("work-1", created.Revision);
        fixture.Guard.FailConfirmationOnce = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Requester.예약Async(created.SpaceId, request));
        Assert.Single(fixture.Store.Documents[created.SpaceId].Reservations);
        Assert.Equal("pending", fixture.Guard.Intents[request.RequestId].StateCode);
        var recovered = await fixture.Requester.요청결과Async(request.RequestId, created.SpaceId, "work-1");
        Assert.Equal("committed", recovered!.RequestOutcome);
        Assert.Equal("confirmed", fixture.Guard.Intents[request.RequestId].StateCode);
        Assert.True(Success(await fixture.Requester.예약Async(created.SpaceId, request)).IdempotentReplay);
        Assert.Single(fixture.Store.Documents[created.SpaceId].Reservations);
    }

    [Fact]
    public async Task 예약권획득뒤_공간쓰기_불명확은_판본fence를_남겨_지연쓰기를막고_해제한다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Contexts.Set("work-1", created.SpaceId);
        var request = Reserve("work-1", created.Revision);
        fixture.Store.ThrowBeforeWrite = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.Requester.예약Async(created.SpaceId, request));
        Assert.Equal("pending", fixture.Guard.Intents[request.RequestId].StateCode);
        Assert.Empty(fixture.Store.Documents[created.SpaceId].Reservations);
        var recovered = await fixture.Requester.요청결과Async(request.RequestId, created.SpaceId, "work-1");
        Assert.Equal("aborted", recovered!.RequestOutcome);
        Assert.Equal("ReservationAborted", recovered.RequestFailureCode);
        Assert.Equal("released", fixture.Guard.Intents[request.RequestId].StateCode);
        Assert.Equal(2, fixture.Store.Documents[created.SpaceId].Revision);
        Assert.False(await fixture.Store.저장Async(fixture.Store.DelayedDocument!, created.Revision));
        Failure(await fixture.Requester.예약Async(created.SpaceId, request), "ReservationAborted");
        Assert.Empty(fixture.Store.Documents[created.SpaceId].Reservations);
    }

    [Fact]
    public async Task 양측동의_뒤_예약전_제공조건이_바뀌면_새주소조회와_예약을_거절한다()
    {
        var fixture = new Fixture();
        var created = Success(await fixture.Owner.등록Async(Request()));
        fixture.Contexts.Set("work-1", created.SpaceId);
        var updated = Success(await fixture.Owner.수정Async(created.SpaceId, Request(revision: created.Revision, address: "새로운 시험로 999")));
        Assert.Null(await fixture.Requester.비공개상세Async(created.SpaceId, "work-1"));
        Failure(await fixture.Requester.예약Async(created.SpaceId, Reserve("work-1", updated.Revision)), "StorageOfferChanged");
    }

    private static NeighborhoodStorageSpaceRequest Request(Guid? requestId = null, long revision = 0, string title = "책 한 상자를 맡을 수 있어요",
        string region = Region, decimal capacity = 1, DateTimeOffset? until = null, bool publish = true, string address = "시험로 123 1층") => new()
    {
        RequestId = requestId ?? Guid.NewGuid(), ExpectedRevision = revision, PublicTitle = title,
        PublicDescription = "마른 책을 잠시 맡습니다.", PublicNeighborhoodRegionKey = region, GoodsKind = "책",
        CapacityQuantity = capacity, CapacityUnit = "상자", AvailableFromUtc = Now.AddHours(1), AvailableUntilUtc = until ?? Now.AddHours(6),
        PrivateAddress = address, PrivateContact = "010-1111-2222", PrivateHandoverInstructions = "출입 안내", PublishOnCreate = publish
    };
    private static NeighborhoodStorageMutationRequest Mutation(long revision) => new() { RequestId = Guid.NewGuid(), ExpectedRevision = revision };
    private static NeighborhoodStorageReservationRequest Reserve(string work, long revision) => new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = revision, CollaborationId = work };
    private static NeighborhoodStorageReservationActionRequest Action(long revision, string action) => new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = revision, Action = action };
    private static NeighborhoodStorageSpaceDto Success(Result<NeighborhoodStorageSpaceDto> result)
    {
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(x => x.Message)));
        return result.Value;
    }
    private static void Failure(Result<NeighborhoodStorageSpaceDto> result, string code)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(code, Assert.Single(result.Errors).Metadata["ErrorCode"]);
    }

    private sealed class Fixture
    {
        public MemoryStore Store { get; } = new();
        public Contexts Contexts { get; } = new();
        public Clock Clock { get; } = new();
        public Guard Guard { get; } = new();
        private readonly 생활보관인계정보Protection protection = new(new DataProtectionPersonalDataEncryptionService(new EphemeralDataProtectionProvider()));
        public 생활보관공간Service Owner => Service("owner-1");
        public 생활보관공간Service Requester => Service("requester-1");
        public 생활보관공간Service Stranger => Service("stranger-1");
        public 생활보관공간Service Service(string? actor) => new(Store, new Actor(actor), protection, new Official생활교류공개지역Source(), Contexts, Clock, Guard);
    }
    private sealed class Actor(string? userId) : ICurrentUserAccessor { public string? UserId => userId; public string? Role => null; }
    private sealed class Clock : TimeProvider { public DateTimeOffset Value { get; set; } = Now; public override DateTimeOffset GetUtcNow() => Value; }
    private sealed class Contexts : I생활보관협업ContextSource
    {
        private readonly Dictionary<string, NeighborhoodStorageCollaborationContext> contexts = [];
        public void Set(string work, string spaceId, bool accepted = true, bool disclosure = true, bool closed = false,
            decimal quantity = 1, string unit = "상자", DateTimeOffset? from = null, DateTimeOffset? until = null, string requester = "requester-1", long offerRevision = 1)
            => contexts[work] = new()
            {
                CollaborationId = work, StorageSpaceId = spaceId, TermsRevision = 1, ExpectedStorageOfferRevision = offerRevision, OwnerUserId = "owner-1", RequesterUserId = requester,
                AcceptedCurrentTerms = accepted, PrivateDisclosureConsented = disclosure, IsClosed = closed,
                Quantity = quantity, Unit = unit, FromUtc = from ?? Now.AddHours(1), UntilUtc = until ?? Now.AddHours(3)
            };
        public Task<NeighborhoodStorageCollaborationContext?> 조회Async(string collaborationId, CancellationToken cancellationToken = default)
            => Task.FromResult(contexts.GetValueOrDefault(collaborationId));
    }
    private sealed class MemoryStore : I생활보관공간Store
    {
        public Dictionary<string, 생활보관공간문서> Documents { get; } = [];
        public bool ThrowAfterWrite { get; set; }
        public bool ThrowBeforeWrite { get; set; }
        public 생활보관공간문서? DelayedDocument { get; private set; }
        public long? BarrierRevision { get; set; }
        private readonly object gate = new();
        private readonly TaskCompletionSource barrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int barrierCount;
        public Task<생활보관공간문서?> 조회Async(string spaceId, CancellationToken cancellationToken = default)
        { lock (gate) return Task.FromResult(Documents.TryGetValue(spaceId, out var document) ? Clone(document) : null); }
        public async Task<bool> 저장Async(생활보관공간문서 document, long expectedRevision, CancellationToken cancellationToken = default)
        {
            if (BarrierRevision == expectedRevision)
            {
                if (Interlocked.Increment(ref barrierCount) == 2) barrier.TrySetResult();
                await barrier.Task.WaitAsync(cancellationToken);
            }
            lock (gate)
            {
                if (ThrowBeforeWrite) { ThrowBeforeWrite = false; DelayedDocument = Clone(document); throw new IOException("지연 쓰기 시험"); }
                var actual = Documents.GetValueOrDefault(document.SpaceId)?.Revision ?? 0;
                if (actual != expectedRevision) return false;
                Assert.Equal(expectedRevision + 1, document.Revision);
                Documents[document.SpaceId] = Clone(document);
                if (ThrowAfterWrite) { ThrowAfterWrite = false; throw new IOException("저장 후 응답 유실 시험"); }
                return true;
            }
        }
        public Task<(IReadOnlyList<생활보관공간문서> Items, int Total)> 목록Async(string? ownerId, string? regionKey,
            bool publishedOnly, DateTime nowUtc, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                var documents = Documents.Values.Where(x => ownerId is null || x.OwnerUserId == ownerId)
                    .Where(x => regionKey is null || x.PublicNeighborhoodRegionKey == regionKey)
                    .Where(x => !publishedOnly || x.Status == NeighborhoodStorageStatus.Published && x.AvailableUntilUtc > nowUtc)
                    .OrderByDescending(x => x.UpdatedAtUtc).ToArray();
                return Task.FromResult(((IReadOnlyList<생활보관공간문서>)documents.Skip((page - 1) * pageSize).Take(pageSize).Select(Clone).ToArray(), documents.Length));
            }
        }
        public Task<IReadOnlyDictionary<string, int>> 공개동네집계Async(DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            lock (gate) return Task.FromResult<IReadOnlyDictionary<string, int>>(Documents.Values
                .Where(x => x.Status == NeighborhoodStorageStatus.Published && x.AvailableUntilUtc > nowUtc)
                .GroupBy(x => x.PublicNeighborhoodRegionKey).ToDictionary(x => x.Key, x => x.Count()));
        }
        private static 생활보관공간문서 Clone(생활보관공간문서 document) => JsonSerializer.Deserialize<생활보관공간문서>(JsonSerializer.Serialize(document))!;
    }

    private sealed class Guard : I생활협업보관예약Guard
    {
        public Dictionary<Guid, 생활협업예약IntentRecord> Intents { get; } = [];
        public bool FailConfirmationOnce { get; set; }
        private readonly object gate = new();
        public Task<Result<생활협업예약IntentRecord>> 획득Async(string collaborationId, long termsRevision, string spaceId,
            string actorUserId, Guid requestId, long expectedSpaceRevision, string fingerprint, CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                if (Intents.TryGetValue(requestId, out var existing)) return Task.FromResult(Result.Ok(existing));
                var intent = new 생활협업예약IntentRecord { ActorUserId = actorUserId, RequestId = requestId, TermsRevision = termsRevision,
                    SpaceId = spaceId, ExpectedSpaceRevision = expectedSpaceRevision, Fingerprint = fingerprint, StateCode = "pending" };
                Intents[requestId] = intent;
                return Task.FromResult(Result.Ok(intent));
            }
        }
        public Task<Result<생활협업예약IntentRecord>> 확정Async(string collaborationId, Guid requestId, string actorUserId, CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                if (FailConfirmationOnce) { FailConfirmationOnce = false; return Task.FromResult(Result.Fail<생활협업예약IntentRecord>("시험용 확정 응답 실패")); }
                Intents[requestId].StateCode = "confirmed";
                return Task.FromResult(Result.Ok(Intents[requestId]));
            }
        }
        public Task<Result<생활협업예약IntentRecord>> 해제Async(string collaborationId, Guid requestId, string actorUserId, CancellationToken cancellationToken = default)
        {
            lock (gate) { Intents[requestId].StateCode = "released"; return Task.FromResult(Result.Ok(Intents[requestId])); }
        }
        public Task<생활협업예약IntentRecord?> 조회Async(string collaborationId, Guid requestId, string actorUserId, CancellationToken cancellationToken = default)
        { lock (gate) return Task.FromResult(Intents.GetValueOrDefault(requestId)); }
    }
}
