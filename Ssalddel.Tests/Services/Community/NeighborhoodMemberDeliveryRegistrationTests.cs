using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Domain.Community;
using Ssalddel.Services.Community;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using Fixture = Ssalddel.Tests.Services.Community.NeighborhoodMemberDeliveryTests.Fixture;

namespace Ssalddel.Tests.Services.Community;

public sealed class NeighborhoodMemberDeliveryRegistrationTests
{
    [Theory]
    [InlineData("", 17L)]
    [InlineData(":member-delivery:v1:", 17L)]
    [InlineData(":dispatch-policy:fixture", 17L)]
    [InlineData(":member-delivery:v1::dispatch-policy:fixture", 17L)]
    [InlineData(":dispatch-policy:fixture:member-delivery:v1:", 17L)]
    [InlineData(":unknown", null)]
    [InlineData(":member-delivery:v1::source-post:18", null)]
    public void 출처는_회원표식과배차근거뒤에도_정확히하나만보존한다(string suffix, long? expected)
        => Assert.Equal(expected, 생활배송출처Evidence.Read("neighborhood-delivery:v1:fixture:source-post:17" + suffix));

    [Fact]
    public void 회원협업접수근거는_전문배차없이_기존큐의협업판본을고정하고수락을닫는다()
    {
        var request = new NeighborhoodDeliveryRequest { MemberDeliveryRequested = true, CollaborationId = "collaboration-1", ExpectedTermsRevision = 3 };
        var queue = new 운송원장();
        생활배송배차Policy.큐초기화(queue, 생활배송배차Policy.MemberMarker + 생활배송배차Policy.신청메모(request));
        Assert.True(queue.일반회원배송전용); Assert.False(queue.생활배송수락준비완료);
        Assert.Null(queue.생활배송배차방식); Assert.Equal("collaboration-1", queue.생활배송협업Id); Assert.Equal(3, queue.생활배송조건판본);
        Assert.False(생활배송배차Policy.자동추천가능(queue)); Assert.False(생활배송배차Policy.공개허용(queue));
        Assert.Throws<InvalidDataException>(() => 생활배송배차Policy.큐초기화(new(), 생활배송배차Policy.신청메모(request)));
    }

    [Theory]
    [InlineData(NeighborhoodDispatchModes.Automatic)]
    [InlineData(NeighborhoodDispatchModes.PublicCall)]
    [InlineData(NeighborhoodDispatchModes.Hybrid)]
    public void 기존전문배차근거는보존하고_회원표식과섞인배차근거는거절한다(string mode)
    {
        var memo = 생활배송배차Policy.신청메모(new() { DispatchMode = mode, CollaborationId = "collaboration-1", ExpectedTermsRevision = 3 });
        var queue = new 운송원장(); 생활배송배차Policy.큐초기화(queue, memo);
        Assert.Equal(mode, queue.생활배송배차방식); Assert.Equal("collaboration-1", queue.생활배송협업Id);
        Assert.False(queue.일반회원배송전용); Assert.False(queue.생활배송수락준비완료);
        Assert.Throws<InvalidDataException>(() => 생활배송배차Policy.큐초기화(new(), 생활배송배차Policy.MemberMarker + memo));
    }

    [Fact]
    public async Task 협업연결확정전에는_회원등록과제안을열지않고_같은접수로확정뒤재개한다()
    {
        await using var f = await Fixture.CreateAsync(); await f.Enroll("member-a");
        var request = await SeedBound(f); var store = Store(request, confirmed: false); var service = Service(f, store);
        var error = await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => service.RegisterAsync("request-1", request, default));
        Assert.Equal("MemberCollaborationPending", error.Code);
        Assert.False((await f.Db.운송원장.AsNoTracking().SingleAsync()).생활배송수락준비완료);
        Assert.Empty(await f.Db.Set<NeighborhoodMemberDelivery>().AsNoTracking().ToListAsync());
        Assert.Empty(await f.Db.Set<NeighborhoodMemberOffer>().AsNoTracking().ToListAsync());
        store.Work.DeliveryIntent!.StateCode = "confirmed"; store.Work.LinkedDeliveryRequestId = "request-1";
        f.Db.ChangeTracker.Clear(); await service.RegisterAsync("request-1", request, default);
        await service.RegisterAsync("request-1", request, default);
        Assert.Single(await f.Db.화주운송의뢰.AsNoTracking().ToListAsync());
        Assert.Single(await f.Db.Set<NeighborhoodMemberDelivery>().AsNoTracking().ToListAsync());
        Assert.Single(await f.Db.Set<NeighborhoodMemberOffer>().AsNoTracking().ToListAsync());
        var queue = await f.Db.운송원장.AsNoTracking().SingleAsync(); Assert.True(queue.생활배송수락준비완료);
        Assert.Equal(request.CollaborationId, queue.생활배송협업Id); Assert.Equal(request.ExpectedTermsRevision, queue.생활배송조건판본);
        Assert.Null(queue.확정기사Id); Assert.Null(queue.생활배송배차방식);
    }

    [Fact]
    public async Task 원의뢰저장후협업연결응답실패는_원요청접수재시도로_새배차없이복구한다()
    {
        await using var f = await Fixture.CreateAsync(); await f.Enroll("member-a");
        var request = await SeedBound(f); var store = Store(request, confirmed: false); var members = Service(f, store);
        var operators = new 화주운송업무담당자UseCase(f.Db, f.Actor, f.Clock);
        var transport = new 화주운송의뢰UseCase(new QuerySender(new 의뢰단건조회QueryHandler(f.Db, operators)), null!, null!, null!, null!, operators, null!);
        var intake = new 생활배송의뢰UseCase(f.Db, f.Actor, transport, f.Consent, null!, new NeighborhoodMemberDeliveryTests.Activation(false),
            collaborationGuard: new FailOnceGuard(new 생활배송협업Guard(store, f.Clock)), members: members);
        var first = await intake.등록Async(request); Assert.True(first.IsFailed);
        Assert.False((await f.Db.운송원장.AsNoTracking().SingleAsync()).생활배송수락준비완료);
        Assert.Empty(await f.Db.Set<NeighborhoodMemberOffer>().AsNoTracking().ToListAsync());
        var resumed = await intake.등록Async(request); Assert.True(resumed.IsSuccess, string.Join("; ", resumed.Errors.Select(x => x.Message)));
        Assert.True(resumed.Value.IdempotentReplay); Assert.Equal("request-1", resumed.Value.RequestId);
        Assert.Equal(request.CollaborationId, resumed.Value.CollaborationId); Assert.Equal(17, resumed.Value.SourcePostId);
        Assert.Single(await f.Db.화주운송의뢰.AsNoTracking().ToListAsync()); Assert.Single(await f.Db.운송원장.AsNoTracking().ToListAsync());
        Assert.Single(await f.Db.Set<NeighborhoodMemberOffer>().AsNoTracking().ToListAsync());
        request.Quantity = 2; var changed = await intake.등록Async(request); Assert.True(changed.IsFailed);
        Assert.Equal("IdempotencyConflict", changed.Errors[0].Metadata["ErrorCode"]);
    }

    [Fact]
    public async Task 저장뒤제안생성이중단돼도_같은회원등록이_누락된제안을한번만복구한다()
    {
        await using var f = await Fixture.CreateAsync(); await f.Enroll("member-a");
        var request = await SeedBound(f); var store = Store(request, confirmed: true); var service = Service(f, store);
        await service.RegisterAsync("request-1", request, default);
        await f.Db.Set<NeighborhoodMemberOffer>().ExecuteDeleteAsync(); f.Db.ChangeTracker.Clear();
        await service.RegisterAsync("request-1", request, default); await service.RegisterAsync("request-1", request, default);
        Assert.Single(await f.Db.Set<NeighborhoodMemberOffer>().AsNoTracking().ToListAsync());
        Assert.Single(await f.Db.Set<NeighborhoodMemberDelivery>().AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("terms")]
    [InlineData("source")]
    [InlineData("owner")]
    public async Task 확정근거가바뀌면_기존제안도수락할수없고_새제안을발행하지않는다(string change)
    {
        await using var f = await Fixture.CreateAsync(); await f.Enroll("member-a");
        var request = await SeedBound(f); var store = Store(request, confirmed: true); var service = Service(f, store);
        await service.RegisterAsync("request-1", request, default);
        f.Actor.UserId = "member-a"; var offer = Assert.Single(await service.OffersAsync()); Assert.True(offer.CanAccept);
        if (change == "pending") store.Work.DeliveryIntent!.StateCode = "pending";
        if (change == "terms") store.Work.TermsRevision++;
        if (change == "source") store.Work.SourcePostId++;
        if (change == "owner") store.Work.DeliveryIntent!.ActorUserId = "stranger";
        Assert.False((await service.OfferAsync(offer.OfferId))!.CanAccept);
        var error = await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => service.AcceptAsync(offer.OfferId, f.Accept(offer)));
        Assert.Equal("MemberCollaborationPending", error.Code);
        await f.Db.Set<NeighborhoodMemberOffer>().ExecuteDeleteAsync(); f.Db.ChangeTracker.Clear();
        await service.FanoutAsync("request-1", default); Assert.Empty(await f.Db.Set<NeighborhoodMemberOffer>().AsNoTracking().ToListAsync());
        Assert.Null((await f.Db.운송원장.AsNoTracking().SingleAsync()).일반회원수행자UserId);
    }

    [Theory]
    [InlineData("collaboration")]
    [InlineData("terms")]
    [InlineData("source")]
    [InlineData("professional")]
    [InlineData("external")]
    [InlineData("automatic")]
    public async Task 기존회원접수의협업출처를바꾸거나_전문외부수행을섞어재등록하지않는다(string change)
    {
        await using var f = await Fixture.CreateAsync(); var request = await SeedBound(f); var store = Store(request, confirmed: true); var service = Service(f, store);
        await service.RegisterAsync("request-1", request, default);
        if (change == "collaboration") request.CollaborationId = "different";
        if (change == "terms") request.ExpectedTermsRevision++;
        if (change == "source") request.SourcePostId++;
        if (change == "professional") request.DispatchMode = NeighborhoodDispatchModes.Automatic;
        if (change == "external") request.FulfillmentSelection = new();
        if (change == "automatic") request.AutoFulfillment = new();
        var error = await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => service.RegisterAsync("request-1", request, default));
        Assert.Equal(change is "professional" or "external" or "automatic" ? "MemberDeliverySourceConflict" : "IdempotencyConflict", error.Code);
        Assert.Single(await f.Db.Set<NeighborhoodMemberDelivery>().AsNoTracking().ToListAsync());
    }

    private static NeighborhoodMemberDeliveryUseCase Service(Fixture f, I생활협업Store store)
        => new(f.Db, f.Actor, Options.Create(new NeighborhoodMemberDeliveryOptions { Enabled = true }),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
            f.Clock, new NeighborhoodMemberDeliveryTests.Encryption(), f.Consent, f.Routes, f.Storage, memberCollaborations: store);

    private static string Fingerprint(NeighborhoodDeliveryRequest request) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
    private static async Task<NeighborhoodDeliveryRequest> SeedBound(Fixture f)
    {
        f.Actor.UserId = "owner"; await f.Seed(); var request = f.Input();
        request.ClientRequestId = Guid.NewGuid(); request.CollaborationId = "collaboration-1"; request.ExpectedTermsRevision = 1; request.SourcePostId = 17;
        request.CargoName = "생활 물품"; request.WeightKg = 1; request.DispatchRequested = true;
        request.Pickup = new() { 주소 = new() { 도로명주소 = "망우로71길 20" }, 연락처 = new() { 이름 = "제공자", 전화번호 = "01012345678" },
            시간창 = new() { 시작일시 = f.Clock.Now, 종료일시 = f.Clock.Now.AddHours(1) } };
        request.Dropoff = new() { 주소 = new() { 도로명주소 = "동일로72길 9" }, 연락처 = new() { 이름 = "수령자", 전화번호 = "01087654321" } };
        var source = await f.Db.화주운송의뢰.SingleAsync(); source.클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + request.ClientRequestId.ToString("N");
        source.정산메모 = "neighborhood-delivery:v1:" + Fingerprint(request) + ":consent:" + f.Consent.Id.ToString("N")
            + 생활배송출처Evidence.Marker + "17" + 생활배송배차Policy.MemberMarker + 생활배송배차Policy.신청메모(request);
        source.배차상태 = 상태값.배차상태.매칭중;
        생활배송배차Policy.큐초기화(await f.Db.운송원장.SingleAsync(), source.정산메모);
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear(); return request;
    }

    private static CollaborationStore Store(NeighborhoodDeliveryRequest request, bool confirmed)
        => new(new 생활협업Record { StableId = request.CollaborationId!, Revision = 3, TermsRevision = request.ExpectedTermsRevision!.Value,
            SourcePostId = request.SourcePostId, OwnerUserId = "provider", RequesterUserId = "owner", ProviderRoleCode = "owner",
            Kind = NeighborhoodCollaborationKinds.Goods, StatusCode = NeighborhoodCollaborationStates.Agreed,
            Terms = new() { TransferMethod = NeighborhoodTransferMethods.DriverDelivery, Quantity = request.Quantity }, OwnerAgreed = true, RequesterAgreed = true,
            OwnerPrivacyNoticeVersion = NeighborhoodGoodsHandoverNotice.Version, RequesterPrivacyNoticeVersion = NeighborhoodGoodsHandoverNotice.Version,
            LinkedDeliveryRequestId = confirmed ? "request-1" : null,
            DeliveryIntent = new() { ClientRequestId = request.ClientRequestId, ActorUserId = "owner", TermsRevision = request.ExpectedTermsRevision.Value,
                Fingerprint = Fingerprint(request), Request = JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(request))!, StateCode = confirmed ? "confirmed" : "pending" } });

    private sealed class CollaborationStore(생활협업Record work) : I생활협업Store
    {
        public 생활협업Record Work { get; } = work;
        public Task<생활협업Record?> 조회Async(string id, CancellationToken ct = default) => Task.FromResult<생활협업Record?>(id == Work.StableId ? Work : null);
        public Task<bool> 생성Async(생활협업Record record, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 교체Async(생활협업Record record, long expectedRevision, CancellationToken ct = default) => Task.FromResult(record.Revision == expectedRevision + 1);
        public Task<IReadOnlyList<생활협업Record>> 목록Async(생활협업StoreQuery query, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class FailOnceGuard(I생활배송협업Guard inner) : I생활배송협업Guard
    {
        private bool failed;
        public Task<Result> 획득Async(NeighborhoodDeliveryRequest request, string actor, string fingerprint, CancellationToken ct) => inner.획득Async(request, actor, fingerprint, ct);
        public Task<Result> 연결확정Async(NeighborhoodDeliveryRequest request, string actor, string id, CancellationToken ct)
        {
            if (!failed) { failed = true; return Task.FromResult(Result.Fail(new Error("연결 응답 실패").WithMetadata("ErrorCode", "RegistrationPending"))); }
            return inner.연결확정Async(request, actor, id, ct);
        }
    }
    private sealed class QuerySender(의뢰단건조회QueryHandler query) : ISender
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request is 의뢰단건조회Query detail ? (TResponse)(object)(await query.Handle(detail, ct))! : throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
