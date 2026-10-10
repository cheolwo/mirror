using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Domain.Community;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacySupport;
using Ssalddel.Services.Storage;
using 살뜰.도메인.공통;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.Community;

public sealed class NeighborhoodMemberDeliveryRecoveryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 기존배차선택명령은회원전용의뢰만차단하고기사배송선택은유지한다(bool memberOnly)
    {
        await using var f=await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();await PrepareLegacyQueue(f,memberOnly);
        var service=new 생활배송배차선택Service(f.Db,f.Actor);
        var command=new NeighborhoodDispatchChoiceRequest { ClientRequestId=Guid.NewGuid(),ExpectedDispatchRevision=1,DispatchMode=NeighborhoodDispatchModes.Hybrid };
        var result=await service.선택Async("request-1",command,default);
        Assert.Equal(memberOnly,result.IsFailed);f.Db.ChangeTracker.Clear();
        var queue=await f.Db.운송원장.SingleAsync();
        if(memberOnly)
        {
            Assert.Equal("MemberDeliveryDedicatedRouteRequired",result.Errors.Single().Metadata["ErrorCode"]);
            Assert.Equal(409,result.Errors.Single().Metadata["StatusCode"]);
            Assert.Equal(NeighborhoodDispatchModes.PublicCall,queue.생활배송배차방식);Assert.Equal(1,queue.생활배송선택판본);
            Assert.Equal("Pending",(await f.Db.Set<NeighborhoodMemberDelivery>().SingleAsync()).StateCode);
        }
        else { Assert.Equal(NeighborhoodDispatchModes.Hybrid,queue.생활배송배차방식);Assert.Equal(2,queue.생활배송선택판본); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 협업대기취소는회원원장을우회종료하지않고기존기사대기취소는유지한다(bool memberOnly)
    {
        await using var f=await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();await PrepareLegacyQueue(f,memberOnly);
        var service=new 생활배송배차선택Service(f.Db,f.Actor);
        var result=await service.협업대기취소Async(new 생활협업Record { StableId="collaboration-1",OwnerUserId="owner",RequesterUserId="recipient",LinkedDeliveryRequestId="request-1" },default);
        Assert.Equal(memberOnly,result.IsFailed);f.Db.ChangeTracker.Clear();
        var source=await f.Db.화주운송의뢰.SingleAsync();var queue=await f.Db.운송원장.SingleAsync();
        if(memberOnly)
        {
            Assert.Equal("MemberDeliveryDedicatedRouteRequired",result.Errors.Single().Metadata["ErrorCode"]);
            Assert.Equal(409,result.Errors.Single().Metadata["StatusCode"]);
            Assert.Equal(상태값.의뢰상태.생성됨,source.상태);Assert.Equal(상태값.배차상태.매칭중,source.배차상태);
            Assert.NotEqual(상태값.배차큐단계.종료,queue.배차큐단계);Assert.Equal(1,queue.생활배송선택판본);
            Assert.Equal("Pending",(await f.Db.Set<NeighborhoodMemberDelivery>().SingleAsync()).StateCode);
        }
        else { Assert.Equal(상태값.의뢰상태.취소,source.상태);Assert.Equal(상태값.배차큐단계.종료,queue.배차큐단계); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 본인원상세는회원배송의기사선택과기사동의만제외한다(bool memberOnly)
    {
        await using var f=await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();await PrepareLegacyQueue(f,memberOnly);
        var operators=new 화주운송업무담당자UseCase(f.Db,f.Actor,f.Clock);
        var transport=new 화주운송의뢰UseCase(new DetailSender(new 의뢰단건조회QueryHandler(f.Db,operators)),null!,null!,null!,null!,operators,null!);
        var disclosure=new 생활배송기사정보제공동의Service(f.Db,f.Actor,f.Clock,f.Consent);
        var service=new 생활배송의뢰UseCase(f.Db,f.Actor,transport,f.Consent,null!,new NeighborhoodMemberDeliveryTests.Activation(false),driverDisclosure:disclosure);
        var detail=await service.내상세Async("request-1");Assert.NotNull(detail);Assert.Equal(memberOnly,detail.MemberDeliveryRequested);
        Assert.Equal(!memberOnly,detail.CanChangeDispatchMode);
        if(memberOnly) { Assert.Null(detail.DispatchMode);Assert.Null(detail.DriverDisclosure);Assert.Equal("Pending",detail.MemberWorkStateCode); }
        else { Assert.Equal(NeighborhoodDispatchModes.PublicCall,detail.DispatchMode);Assert.NotNull(detail.DriverDisclosure); }
    }

    private static async Task PrepareLegacyQueue(NeighborhoodMemberDeliveryTests.Fixture f,bool memberOnly)
    {
        if(memberOnly)await f.Register();else { f.Actor.UserId="owner";await f.Seed(); }
        var source=await f.Db.화주운송의뢰.SingleAsync();source.배차상태=상태값.배차상태.매칭중;
        var queue=await f.Db.운송원장.SingleAsync();queue.일반회원배송전용=memberOnly;queue.생활배송수락준비완료=true;
        queue.생활배송배차방식=NeighborhoodDispatchModes.PublicCall;queue.생활배송선택판본=1;
        await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
    }

    private sealed class DetailSender(의뢰단건조회QueryHandler query) : ISender
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request,CancellationToken ct=default)
            => request is 의뢰단건조회Query detail ? (TResponse)(object)(await query.Handle(detail,ct))! : throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request,CancellationToken ct=default) where TRequest:IRequest=>throw new NotSupportedException();
        public Task<object?> Send(object request,CancellationToken ct=default)=>throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,CancellationToken ct=default)=>throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request,CancellationToken ct=default)=>throw new NotSupportedException();
    }

    [Fact]
    public async Task 구형관리자취소경로는회원배송책임과슬롯을우회해종료하지않는다()
    {
        await using var f=await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();var offer=await f.Ready();var work=await f.Service().AcceptAsync(offer.OfferId,f.Accept(offer));
        f.Actor.UserId="admin";f.Actor.Role="서버관리자";
        var handler=new Ssalddel.Application.Shipper.Request.관리자운송의뢰취소환불CommandHandler(f.Db,f.Actor,
            new 살뜰.Services.Options.SsalddelExecutionModePolicy(Options.Create(new 살뜰.Services.Options.SsalddelExecutionOptions { Mode=살뜰.Services.Options.SsalddelExecutionMode.Simulation })));
        var result=await handler.Handle(new(work.RequestId,work.RequestId,"시험 취소"),default);
        Assert.True(result.IsFailed);Assert.Equal("MemberDeliveryRecoveryRequired",result.Errors.Single().Metadata["ErrorCode"]);
        Assert.NotNull((await f.Db.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
        Assert.Equal(상태값.의뢰상태.생성됨,(await f.Db.화주운송의뢰.SingleAsync()).상태);
    }
    [Fact]
    public async Task 수행문제는실제기존지원UseCase와SQL당사자에결속하고픽업전복구는슬롯만해제한다()
    {
        await using var f=await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();var offer=await f.Ready();var work=await f.Service().AcceptAsync(offer.OfferId,f.Accept(offer));
        var cases=new Cases();var http=new HttpContextAccessor { HttpContext=new DefaultHttpContext() };
        http.HttpContext.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"member-a")],"test"));
        var options=Options.Create(new 보호지원Options());
        var resolver=new 보호지원SourceResolver(f.Db,new Collaboration());
        Assert.Contains("member-a",(await resolver.조회Async(보호지원출처Codes.생활배송,work.RequestId,"member-a"))!.PartyUserIds);
        Assert.Null(await resolver.조회Async(보호지원출처Codes.생활배송,work.RequestId,"other"));
        var support=new 보호지원UseCase(cases,resolver,new 한국영업일Calendar(options),options,f.Clock,http,new Authorization());
        var issue=new NeighborhoodMemberIssueRequest { ClientRequestId=Guid.NewGuid(),ExpectedRevision=work.Revision,Reason="픽업지에서 연락이 안 됨" };
        work=await f.Service(support:support).IssueAsync(work.RequestId,issue);
        Assert.Equal("Issue",work.StateCode);Assert.NotNull(work.SupportCaseId);Assert.Single(cases.Rows);
        Assert.Equal("member-a",(await f.Db.Set<NeighborhoodMemberParticipation>().SingleAsync()).UserId);
        Assert.NotNull((await f.Db.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
        var replay=await f.Service(support:support).IssueAsync(work.RequestId,issue);Assert.Equal(work.SupportCaseId,replay.SupportCaseId);Assert.Single(cases.Rows);
        var recovery=new NeighborhoodMemberRecoveryRequest { ClientRequestId=Guid.NewGuid(),ExpectedRevision=work.Revision,SupportCaseId=work.SupportCaseId!,ResolutionCode="CancelBeforePickup" };
        Assert.Equal("MemberRecoveryEvidenceRequired",(await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(()=>f.Service().RecoverAsync(work.RequestId,recovery,cases,default))).Code);
        Assert.Equal("MemberRecoveryEvidenceRequired",(await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(()=>f.Service().RecoveryContextAsync(work.RequestId,recovery,cases,default))).Code);
        var row=cases.Rows[work.SupportCaseId!];row.StatusCode="closed";row.ClosedAtUtc=f.Clock.Now;
        Assert.Equal("MemberRecoveryEvidenceRequired",(await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(()=>f.Service().RecoveryContextAsync(work.RequestId,recovery,cases,default))).Code);
        // 이 시험은 지원 저장소 계약에 종결·개별 통지 증거를 공급한다. 실제 외부 발송은 수행하지 않는다.
        row.Notifications=row.PartyUserIds.Select(p=>new 보호지원Notification("result",p,"member","mock-notice",f.Clock.Now,f.Clock.Now,row.ReviewCycle)).ToList();
        f.Actor.UserId="admin";
        var receiptsBefore=await f.Db.Set<NeighborhoodMemberReceipt>().CountAsync();
        var context=await f.Service().RecoveryContextAsync(work.RequestId,recovery,cases,default);
        Assert.Equal(work.Revision,context.Revision);Assert.True(context.CanCancelBeforePickup);Assert.Null(context.AppliedClientRequestId);
        Assert.Equal(receiptsBefore,await f.Db.Set<NeighborhoodMemberReceipt>().CountAsync());
        var result=await f.Service().RecoverAsync(work.RequestId,recovery,cases,default);
        Assert.Equal("Cancelled",result.StateCode);f.Db.ChangeTracker.Clear();
        context=await f.Service().RecoveryContextAsync(work.RequestId,recovery,cases,default);
        Assert.Equal(recovery.ClientRequestId,context.AppliedClientRequestId);Assert.Equal(recovery.ResolutionCode,context.AppliedResolutionCode);
        Assert.False(context.CanResume);Assert.False(context.CanCancelBeforePickup);
        var changedBody=new NeighborhoodMemberRecoveryRequest { ClientRequestId=recovery.ClientRequestId,ExpectedRevision=recovery.ExpectedRevision,
            SupportCaseId=recovery.SupportCaseId,ResolutionCode="Resume" };
        Assert.Equal("IdempotencyConflict",(await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(()=>f.Service().RecoveryContextAsync(work.RequestId,changedBody,cases,default))).Code);
        f.Actor.UserId="other-admin";context=await f.Service().RecoveryContextAsync(work.RequestId,recovery,cases,default);
        Assert.Equal("Cancelled",context.StateCode);Assert.Null(context.AppliedClientRequestId);
        row.SourceId="another-delivery";
        Assert.Equal("MemberRecoveryEvidenceRequired",(await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(()=>f.Service().RecoveryContextAsync(work.RequestId,recovery,cases,default))).Code);
        row.SourceId=work.RequestId;row.ReviewCycle++;
        Assert.Equal("MemberRecoveryEvidenceRequired",(await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(()=>f.Service().RecoveryContextAsync(work.RequestId,recovery,cases,default))).Code);
        Assert.Null((await f.Db.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
        var source=await f.Db.화주운송의뢰.SingleAsync();Assert.Equal(상태값.의뢰상태.취소,source.상태);Assert.Equal(상태값.결제상태.결제대기,source.결제상태);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    public async Task 증빙물리파기는지원보존상태미확인또는열린사건이면보류한다(bool? preserve)
    {
        await using var f=await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();var work=await Deliver(f);
        f.Clock.Now=f.Clock.Now.AddDays(31);var cases=new Cases { Preserve=preserve };
        Assert.Equal(0,await f.Service().SweepPhotosAsync(cases));Assert.Equal(2,f.Storage.Objects.Count);
    }

    [Fact]
    public async Task SQL보존정지와과거복사부재미확인은파기완료로보고하지않는다()
    {
        await using var f=await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();var work=await Deliver(f);
        f.Clock.Now=f.Clock.Now.AddDays(31);var cases=new Cases { Preserve=false };
        f.Db.Set<분산사진보존정지Record>().Add(new() { CaseId="hold-case",SourceKind=보호지원출처Codes.생활배송,SourceId=work.RequestId,Active=true });await f.Db.SaveChangesAsync();
        Assert.Equal(0,await f.Service().SweepPhotosAsync(cases));Assert.Equal(2,f.Storage.Objects.Count);
        var hold=await f.Db.Set<분산사진보존정지Record>().SingleAsync();hold.Active=false;await f.Db.SaveChangesAsync();
        f.Storage.HistoricalAbsent=false;
        Assert.Equal(0,await f.Service().SweepPhotosAsync(cases));Assert.Empty(f.Storage.Objects);
        f.Db.ChangeTracker.Clear();Assert.All(await f.Db.Set<NeighborhoodMemberPhoto>().ToListAsync(),p=> {Assert.Equal("ActiveDeleted",p.StateCode);Assert.NotEmpty(p.PrivatePayload);});
        f.Storage.HistoricalAbsent=true;
        Assert.Equal(2,await f.Service().SweepPhotosAsync(cases));f.Db.ChangeTracker.Clear();
        Assert.All(await f.Db.Set<NeighborhoodMemberPhoto>().ToListAsync(),p=> {Assert.Equal("Deleted",p.StateCode);Assert.Empty(p.PrivatePayload);});
        Assert.Equal(5000m,(await f.Service().WorkAsync(work.RequestId))!.FareKrw);
    }

    private static async Task<NeighborhoodMemberWorkResponse> Deliver(NeighborhoodMemberDeliveryTests.Fixture f)
    {
        var offer=await f.Ready();var work=await f.Service().AcceptAsync(offer.OfferId,f.Accept(offer));
        work=await f.Service().EvidenceAsync(work.RequestId,f.Evidence(work,"Pickup"));work=await f.Service().PickupAsync(work.RequestId,f.Command(work.Revision));
        work=await f.Service().EvidenceAsync(work.RequestId,f.Evidence(work,"Delivery"));return await f.Service().DeliverAsync(work.RequestId,f.Command(work.Revision));
    }
    private sealed class Collaboration : I생활협업연결Query { public Task<생활협업연결Context?> 조회Async(string id,CancellationToken ct=default)=>Task.FromResult<생활협업연결Context?>(null); }
    private sealed class Authorization : IAuthorizationService
    { public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal u,object? r,IEnumerable<IAuthorizationRequirement> requirements)=>Task.FromResult(AuthorizationResult.Failed());public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal u,object? r,string p)=>Task.FromResult(AuthorizationResult.Failed()); }
    public sealed class Cases : I보호지원Store
    {
        public Dictionary<string,보호지원Record> Rows { get; }=[];public bool? Preserve;
        public Task<bool?> HasOpenCaseAsync(string kind,string id,CancellationToken ct=default)=>Task.FromResult(Preserve);
        public Task<보호지원Record?> 조회Async(string id,CancellationToken ct=default)=>Task.FromResult(Rows.GetValueOrDefault(id));
        public Task<bool> 생성Async(보호지원Record row,CancellationToken ct=default)=>Task.FromResult(Rows.TryAdd(row.CaseId,row));
        public Task<bool> 교체Async(보호지원Record row,long expected,CancellationToken ct=default) { if(!Rows.TryGetValue(row.CaseId,out var prior)||prior.Revision!=expected)return Task.FromResult(false);Rows[row.CaseId]=row;return Task.FromResult(true); }
        public Task<IReadOnlyList<보호지원Record>> 목록Async(string? actor,string? kind,int skip,int take,CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<보호지원Record>>(Rows.Values.ToArray());
        public Task<IReadOnlyList<보호지원Record>> 파기후보Async(DateTime now,int take,CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<보호지원Record>>([]);
        public Task<IReadOnlyList<보호지원Record>> 보존조정후보Async(int take,CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<보호지원Record>>([]);
        public Task<bool> 파기Async(보호지원Record row,DateTime now,CancellationToken ct=default)=>Task.FromResult(false);
    }
}
