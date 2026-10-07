using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Infrastructure.BackgroundJobs;
using Ssalddel.Services.Community;
using Ssalddel.Services.Operations;
using Ssalddel.Services.Privacy;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.External.Google;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

/// <summary>
/// 생산용 생활배송 adapter→기존 화주 UseCase/Command Handler→실제 배차대기 Service/원천분류/배달권 투영을 검증한다.
/// EF InMemory와 Mongo 투영·동의·주소·운임·OS 인계 대역을 사용한다. 외부 운영 배차/푸시·실결제 증거가 아니다.
/// </summary>
public sealed class NeighborhoodDeliveryTests
{
    [Fact]
    public async Task 일반회원의_명시의뢰는_화물큐에한번저장하고_입금과기사수락을만들지않는다()
    {
        await using var fixture = new Fixture();
        var request = Input();
        var first = await fixture.UseCase().등록Async(request);
        Assert.True(first.IsSuccess, string.Join(";", first.Errors.Select(x => x.Message)));
        fixture.Db.ChangeTracker.Clear();
        var retry = await fixture.UseCase().등록Async(request);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value.RequestId, retry.Value.RequestId);
        Assert.True(retry.Value.IdempotentReplay);
        var stored = Assert.Single(await fixture.Db.화주운송의뢰.AsNoTracking().ToListAsync());
        var queue = Assert.Single(await fixture.Db.운송원장.AsNoTracking().ToListAsync());
        Assert.Equal("member-a", stored.주문자UserId);
        Assert.Equal("member-a", stored.화주Id);
        Assert.Equal(상태값.결제상태.결제대기, stored.결제상태);
        Assert.Equal(운임정산상태.현장수금예정.ToString(), stored.정산상태);
        Assert.Null(stored.현장수금확인일시);
        Assert.Equal(상태값.배차상태.매칭중, stored.배차상태);
        Assert.Equal(8200m, stored.최종운임);
        Assert.Equal(1, await fixture.Db.운임구성.CountAsync());
        Assert.Equal(상태값.배차업무유형.용달운송, queue.배차업무유형);
        Assert.Equal(운송의뢰배차원천유형.화주운송의뢰, queue.원본의뢰유형);
        Assert.Equal(상태값.배차큐단계.계획배차, queue.배차큐단계);
        Assert.Null(queue.현재추천대상기사Id);
        Assert.Null(queue.확정기사Id);
        Assert.Equal(NeighborhoodDeliveryProposalStates.AwaitingActivation, retry.Value.ProposalStateCode);
        Assert.Equal(SsalddelBackgroundWorkloadActivationCodes.OperationalModeRequired, retry.Value.AutomaticDispatchBlockCode);
        Assert.Empty(await fixture.FoodSpace.SnapshotAsync());
        var cargoSpace = Assert.Single(await fixture.CargoSpace.SnapshotAsync());
        Assert.Contains(queue.의뢰Id, cargoSpace.미처리운송의뢰Ids);
        Assert.Equal(2, await fixture.Db.원장배달권투영.CountAsync());
    }

    [Fact]
    public async Task 원장저장뒤_큐실패는_같은요청으로재개하고_배송원장을중복생성하지않는다()
    {
        await using var fixture = new Fixture(failQueueOnce: true);
        var request = Input();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.UseCase().등록Async(request));
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(1, await fixture.Db.화주운송의뢰.CountAsync());
        Assert.Equal(0, await fixture.Db.운송원장.CountAsync());
        var pendingId = await fixture.Db.화주운송의뢰.Select(x => x.의뢰Id).SingleAsync();
        Assert.Equal(NeighborhoodDeliveryProposalStates.RegistrationPending,
            (await fixture.UseCase().내상세Async(pendingId))!.ProposalStateCode);
        var retry = await fixture.UseCase().등록Async(request);
        Assert.True(retry.IsSuccess);
        Assert.True(retry.Value.IdempotentReplay);
        Assert.Equal(1, await fixture.Db.화주운송의뢰.CountAsync());
        Assert.Equal(1, await fixture.Db.운송원장.CountAsync());
        Assert.Equal(1, await fixture.Db.운임구성.CountAsync());
        Assert.Equal(NeighborhoodDeliveryProposalStates.AwaitingActivation, retry.Value.ProposalStateCode);
    }

    [Fact]
    public async Task 같은키의다른내용은409이며_다른회원의목록과상세는노출하지않는다()
    {
        await using var fixture = new Fixture();
        var request = Input();
        var created = await fixture.UseCase().등록Async(request);
        Assert.True(created.IsSuccess);
        request.CargoName = "바뀐 물품";
        var conflict = await fixture.UseCase().등록Async(request);
        Assert.True(conflict.IsFailed);
        Assert.Equal("IdempotencyConflict", Assert.Single(conflict.Errors).Metadata["ErrorCode"]);
        Assert.Equal(409, Assert.Single(conflict.Errors).Metadata["StatusCode"]);
        fixture.Current.UserId = "member-b";
        Assert.Null(await fixture.UseCase().내상세Async(created.Value.RequestId));
        Assert.Empty(await fixture.UseCase().내목록Async());
        Assert.Single(fixture.Db.화주운송의뢰);
    }

    [Theory]
    [InlineData("dispatch", "DeliveryAgreementRequired")]
    [InlineData("direct-payment", "DeliveryAgreementRequired")]
    [InlineData("price", "QuoteChanged")]
    [InlineData("consent", "PrivacyConsentRequired")]
    [InlineData("source", "PrivacyConsentRequired")]
    [InlineData("withdrawn-consent", "PrivacyConsentRequired")]
    public async Task 명시합의나현재동의나확인된서버금액없이는원장을생성하지않는다(string change, string expected)
    {
        await using var fixture = new Fixture();
        var request = Input();
        switch (change)
        {
            case "dispatch": request.DispatchRequested = false; break;
            case "direct-payment": request.DirectPaymentAgreed = false; break;
            case "price": request.AgreedFareKrw = 1m; break;
            case "consent": request.PrivacyConsentEvidenceId = null; break;
            case "source": request.PrivacyConsentSourceCode = "arbitrary-source"; break;
            case "withdrawn-consent": fixture.Consent.Active = false; break;
        }
        var result = await fixture.UseCase().등록Async(request);
        Assert.True(result.IsFailed);
        Assert.Equal(expected, Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Empty(fixture.Db.화주운송의뢰);
        Assert.Empty(fixture.Db.운송원장);
    }

    [Fact]
    public async Task 조작한입력좌표는무시하고_서버주소위치로견적하며_주소실패를샘플로대신하지않는다()
    {
        await using var fixture = new Fixture();
        var request = Input();
        request.Pickup.주소.위도 = 1m;
        request.Pickup.주소.경도 = 1m;
        request.Dropoff.주소.위도 = 1.0001m;
        request.Dropoff.주소.경도 = 1.0001m;
        var quote = await fixture.UseCase().견적Async(request);
        Assert.True(quote.IsSuccess);
        Assert.Equal(37.5800m, fixture.Fare.LastInput!.상차위도);
        Assert.Equal(127.0830m, fixture.Fare.LastInput!.상차경도);
        Assert.Equal(37.5900m, fixture.Fare.LastInput!.하차위도);
        Assert.Equal(1m, request.Pickup.주소.위도);
        fixture.Geo.Resolve = false;
        var unresolved = await fixture.UseCase().견적Async(request);
        Assert.True(unresolved.IsFailed);
        Assert.Equal("AddressNotResolved", Assert.Single(unresolved.Errors).Metadata["ErrorCode"]);
        Assert.Empty(fixture.Db.화주운송의뢰);
    }

    [Fact]
    public async Task 출처글만있다고배차하지않으며_생성뒤글삭제에도같은의뢰를조회하고재송신할수있다()
    {
        await using var fixture = new Fixture();
        var post = new Ssalddel.Domain.Community.PlatformCommunityPost
        {
            Category = PlatformCommunityPostCategories.General, WorkflowTag = NeighborhoodExchange.WorkflowTag,
            RoleTag = NeighborhoodExchange.Offer, Title = "제공 글", Body = "교류 내용", AuthorUserId = "member-a"
        };
        fixture.Db.PlatformCommunityPosts.Add(post);
        await fixture.Db.SaveChangesAsync();
        Assert.Empty(fixture.Db.운송원장);
        var request = Input();
        request.SourcePostId = post.Id;
        var created = await fixture.UseCase().등록Async(request);
        Assert.True(created.IsSuccess, string.Join("; ", created.Errors.Select(error => error.Message)));
        Assert.Equal(post.Id, created.Value.SourcePostId);
        var source = await new Ef생활협업배송Source(fixture.Db).조회Async(created.Value.RequestId);
        Assert.Equal(post.Id, source!.SourcePostId);
        Assert.False(source.Completed);
        var stored = await fixture.Db.화주운송의뢰.SingleAsync();
        stored.배차상태 = 상태값.배차상태.인수완료;
        await fixture.Db.SaveChangesAsync();
        Assert.True((await new Ef생활협업배송Source(fixture.Db).조회Async(created.Value.RequestId))!.Completed);
        stored.배차상태 = 상태값.배차상태.취소;
        await fixture.Db.SaveChangesAsync();
        var cancelledSource = await new Ef생활협업배송Source(fixture.Db).조회Async(created.Value.RequestId);
        Assert.True(cancelledSource!.Cancelled); Assert.False(cancelledSource.Completed);
        post.IsDeleted = true;
        await fixture.Db.SaveChangesAsync();
        var replay = await fixture.UseCase().등록Async(request);
        Assert.True(replay.IsSuccess, string.Join("; ", replay.Errors.Select(error => error.Message)));
        Assert.Equal(created.Value.RequestId, replay.Value.RequestId);
        Assert.Single(fixture.Db.운송원장);
    }

    [Fact]
    public async Task 구형_생활배송_재접수는_기존의뢰를보존하며_출처를역추정하지않는다()
    {
        await using var fixture = new Fixture();
        var post = new Ssalddel.Domain.Community.PlatformCommunityPost
        { Category = PlatformCommunityPostCategories.General, WorkflowTag = NeighborhoodExchange.WorkflowTag,
            RoleTag = NeighborhoodExchange.Offer, Title = "출처", Body = "내용", AuthorUserId = "member-a" };
        fixture.Db.PlatformCommunityPosts.Add(post); await fixture.Db.SaveChangesAsync();
        var request = Input(); request.SourcePostId = post.Id;
        var created = await fixture.UseCase().등록Async(request); Assert.True(created.IsSuccess);
        var stored = await fixture.Db.화주운송의뢰.SingleAsync();
        stored.정산메모 = stored.정산메모[..stored.정산메모.IndexOf(생활배송출처Evidence.Marker, StringComparison.Ordinal)];
        await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear();
        var replay = await fixture.UseCase().등록Async(request);
        Assert.True(replay.IsSuccess); Assert.True(replay.Value.IdempotentReplay);
        Assert.Equal(created.Value.RequestId, replay.Value.RequestId); Assert.Null(replay.Value.SourcePostId);
        Assert.Single(fixture.Db.화주운송의뢰);
    }

    [Theory]
    [InlineData("neighborhood-delivery:v1:fingerprint:consent:abc:source-post:12", 12L)]
    [InlineData("neighborhood-delivery:v1:fingerprint:consent:abc", null)]
    [InlineData("neighborhood-delivery:v1:fingerprint:source-post:0", null)]
    [InlineData("neighborhood-delivery:v1:fingerprint:source-post:12:source-post:13", null)]
    [InlineData("other:source-post:12", null)]
    public void 출처marker는_양의_서버보존번호만읽는다(string memo, long? expected)
        => Assert.Equal(expected, 생활배송출처Evidence.Read(memo));

    [Fact]
    public async Task 취소된의뢰의같은요청재송신은배차를복원하지않는다()
    {
        await using var fixture = new Fixture();
        var request = Input();
        var result = await fixture.UseCase().등록Async(request);
        Assert.True(result.IsSuccess);
        var stored = await fixture.Db.화주운송의뢰.SingleAsync();
        stored.상태 = 상태값.의뢰상태.취소;
        stored.배차상태 = 상태값.배차상태.취소;
        fixture.Db.운송원장.RemoveRange(fixture.Db.운송원장);
        await fixture.Db.SaveChangesAsync();
        var replay = await fixture.UseCase().등록Async(request);
        Assert.True(replay.IsSuccess);
        Assert.Equal(NeighborhoodDeliveryProposalStates.Closed, replay.Value.ProposalStateCode);
        Assert.Empty(fixture.Db.운송원장);
        Assert.Single(fixture.Db.화주운송의뢰);
    }

    private static NeighborhoodDeliveryRequest Input() => new()
    {
        ClientRequestId = Guid.NewGuid(), CargoName = "생활 소포", Quantity = 1, WeightKg = 1m,
        VehicleType = "오토바이", PrivacyConsentEvidenceId = Guid.NewGuid(), DispatchRequested = true,
        DirectPaymentAgreed = true, AgreedFareKrw = 8200m,
        Pickup = new() { 주소 = new() { 도로명주소 = "서울특별시 중랑구 사가정로 픽업" },
            연락처 = new() { 이름 = "테스트 픽업 담당", 전화번호 = "010-0000-0000" },
            시간창 = new() { 시작일시 = DateTime.UtcNow.AddHours(1), 종료일시 = DateTime.UtcNow.AddHours(2) } },
        Dropoff = new() { 주소 = new() { 도로명주소 = "서울특별시 중랑구 망우로 전달" },
            연락처 = new() { 이름 = "테스트 전달 담당", 전화번호 = "010-0000-0001" } }
    };

    private sealed class Fixture : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase("neighborhood-delivery-" + Guid.NewGuid()).Options, new Encryption());
        public Current Current { get; } = new();
        public ConsentService Consent { get; } = new();
        public Geocoding Geo { get; } = new();
        public FareService Fare { get; } = new();
        public InMemory음식배달권실행공간Store FoodSpace { get; } = new();
        public InMemory국내화물배달권실행공간Store CargoSpace { get; } = new();
        public I화주운송의뢰UseCase Transport { get; }

        public Fixture(bool failQueueOnce = false)
        {
            var queue = new 운송의뢰배차대기Service(Db, new 운송의뢰배차원천분류Service(),
                new 운송원장배달권연결Service(new 원장배달권투영Service(Db)), FoodSpace, CargoSpace);
            var operators = new 화주운송업무담당자UseCase(Db, Current, TimeProvider.System);
            var sync = new ProjectionBridge(Db);
            var sender = new Sender(
                new 의뢰생성CommandHandler(Db, Geo, Current, sync, new FreightPolicy(), new Handoff()),
                new 화주운송의뢰현장지급처리CommandHandler(Db, Current, failQueueOnce ? new FailOnceQueue(queue) : queue, sync, operators),
                new 의뢰단건조회QueryHandler(Db, operators));
            Transport = new 화주운송의뢰UseCase(sender, null!, null!, Fare, new 화주운송요금정책검토Service(), operators, null!);
        }
        public 생활배송의뢰UseCase UseCase() => new(Db, Current, Transport, Consent, Geo, new Activation());
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class Current : ICurrentUserAccessor
    {
        public string? UserId { get; set; } = "member-a";
        public string? Role => "일반회원";
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Geocoding : IGeocodingService
    {
        public bool Resolve { get; set; } = true;
        public Task<(decimal lat, decimal lng)?> GeocodeAsync(string address)
            => Task.FromResult<(decimal lat, decimal lng)?>(!Resolve ? null : address.Contains("픽업", StringComparison.Ordinal)
                ? (37.5800m, 127.0830m) : (37.5900m, 127.0900m));
    }
    private sealed class FareService : I화주운송기준운임Service
    {
        public 화주운송기준운임견적요청? LastInput { get; private set; }
        public Task<Result<화주운송기준운임견적응답>> 견적Async(화주운송기준운임견적요청 request, CancellationToken cancellationToken = default)
        {
            LastInput = request;
            return Task.FromResult(Result.Ok(new 화주운송기준운임견적응답
            {
                차량종류 = "오토바이", 예상거리Km = 3.2m, 기본운임 = 5000m, Km당단가 = 1000m,
                거리운임 = 3200m, 최소운임 = 5000m, 최종운임 = 8200m, 거리계산방식 = "FixtureRoute", 단가출처 = "FixtureRate"
            }));
        }
    }
    private sealed class Activation : ISsalddelBackgroundJobActivationPolicy
    {
        public SsalddelBackgroundWorkloadActivation Evaluate(string workloadKey)
            => new(false, SsalddelBackgroundWorkloadActivationCodes.OperationalModeRequired, "DomesticTransportWorkflow");
    }
    private sealed class ConsentService : I신청개인정보동의증적Service
    {
        public bool Active { get; set; } = true;
        public Task 유효한동의요구Async(Guid? evidenceId, string workCode, string sourceCode, string userId, CancellationToken cancellationToken = default)
        {
            Assert.Equal(신청개인정보업무Codes.운송대행, workCode);
            Assert.Equal(NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode, sourceCode);
            if (!Active) throw new InvalidOperationException("철회된 동의");
            return Task.CompletedTask;
        }
        public Task<신청개인정보동의증적Response> 동의기록Async(신청개인정보동의기록Request request, string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<신청개인정보동의증적Response?> 내증적조회Async(Guid id, string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<신청개인정보동의증적Response> 철회Async(Guid id, 신청개인정보동의철회Request request, string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class FreightPolicy : IOperatingMarketFreightWorkflowPolicy
    {
        public string MarketCode => "KR";
        public OperatingMarketFreightWorkflowDecision Evaluate(OperatingMarketFreightWorkflowRequest request) => new() { CanProceed = true };
    }
    private sealed class Handoff : I화주운송의뢰화물운송인계Service
    {
        public Task<운영체제업무인계Dto> 인계Async(string requestId, CancellationToken cancellationToken = default) => Task.FromResult(new 운영체제업무인계Dto());
    }
    // Mongo 원장/투영 boundary만 격리한다. 그 이후 실제 생성/운임/현장지급 Handler는 유지한다.
    private sealed class ProjectionBridge(SsalddelContext db) : I운송원장Mongo동기화Service
    {
        public async Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 request, string user, CancellationToken cancellationToken = default)
        {
            if (!await db.화주운송의뢰.AnyAsync(x => x.의뢰Id == request.의뢰Id, cancellationToken)) db.화주운송의뢰.Add(request);
            await db.SaveChangesAsync(cancellationToken);
            return new 커뮤니티원장Dto { 원장Id = request.의뢰Id };
        }
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 request, string user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class FailOnceQueue(I운송의뢰배차대기Service inner) : I운송의뢰배차대기Service
    {
        private bool failed;
        public Task<운송원장> 생성또는조회Async(Ssalddel.Contracts.Common.Warehouse.출고예정운송대상 target, 운송의뢰배차대기생성옵션? options = null, CancellationToken cancellationToken = default)
        {
            if (!failed) { failed = true; throw new InvalidOperationException("격리된 큐 실패"); }
            return inner.생성또는조회Async(target, options, cancellationToken);
        }
    }
    private sealed class Sender(의뢰생성CommandHandler create, 화주운송의뢰현장지급처리CommandHandler onsite, 의뢰단건조회QueryHandler query) : ISender
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object? response;
            switch (request)
            {
                case 의뢰생성Command command:
                    response = (object)await create.Handle(command, cancellationToken);
                    break;
                case 화주운송의뢰현장지급처리Command command:
                    response = (object)await onsite.Handle(command, cancellationToken);
                    break;
                case 의뢰단건조회Query detail:
                    response = (object?)await query.Handle(detail, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException(request.GetType().Name);
            }
            return (TResponse)response!;
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
