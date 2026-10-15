using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;
using Ssalddel.Infrastructure.BackgroundJobs;
using 살뜰.Infrastructure.BackgroundJobs.FoodDeliveryDispatch;
using Ssalddel.Application.Admin.Food;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Application.Food;

public sealed partial class 음식배달정상수직생명주기Tests
{
    [Fact]
    public async Task 지원후속재탐색은_원Case결과통지와주문판본을묶고_중복실행과금융완료를막는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        order = store.음식점수락멱등(order.주문번호, new() { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41")!.주문;
        var queue = CreateRecommendedTransport($"food-dispatch:{order.주문번호}", order.주문번호, "candidate");
        queue.현재추천대상기사Id = null; queue.추천시작시각 = queue.추천만료시각 = null;
        queue.배차노출상태 = 상태값.배차노출상태.추천후보없음;
        queue.UpdatedAt = DateTime.UtcNow.AddMinutes(-3);
        database.Context.운송원장.Add(queue);
        await database.Context.SaveChangesAsync();
        order = store.배차대기반영(order.주문번호, queue.Id, DateTime.UtcNow)!;
        var fixture = new R76SupportFixture(order.주문번호);
        var retry = new R76Transition { ThrowAfterRead = true };
        var service = new 음식주문지원후속UseCase(database.Context, fixture.Service, fixture.Actor, retry,
            NullLogger<음식주문지원후속UseCase>.Instance);
        var context = await service.조회Async(order.주문번호, fixture.Row.CaseId);
        Assert.True(context!.CanRetryDispatch);
        var request = new FoodSupportFollowUpRequest { ClientRequestId = Guid.NewGuid(), SupportCaseId = fixture.Row.CaseId,
            ExpectedCaseRevision = context.CaseRevision, ExpectedOrderRevision = context.OrderRevision, ActionCode = FoodSupportFollowUpActions.RetryDispatch };
        var result = await service.처리Async(order.주문번호, request);
        Assert.Equal("DispatchRetryRequested", result!.WorkStatusCode);
        Assert.Equal("OperatorReviewRequired", result.FinanceStatusCode);
        Assert.Equal(1, retry.Calls); // 후속 실패가 이미 저장한 요청을 실패로 바꾸지 않는다.
        var replay = await service.처리Async(order.주문번호, request);
        Assert.True(replay!.Replay);
        Assert.Equal(1, retry.Calls);
        database.Context.ChangeTracker.Clear();
        var proof = await service.조회Async(order.주문번호, fixture.Row.CaseId, request.ClientRequestId);
        Assert.Equal(request.ClientRequestId, proof!.Receipt!.ClientRequestId);
        Assert.Equal(context.OrderRevision + 1, proof.Receipt.OrderRevision);
        Assert.Equal(음식주문상태코드.주문확인, proof.OrderStatus);
        Assert.Empty(await database.Context.음식주문기사정산.ToListAsync());
        Assert.Null((await database.Context.운송원장.SingleAsync()).확정기사Id);
        Assert.NotNull(proof.LastFollowUp);
        retry.ThrowAfterRead = false;
        var job = new 음식배달배차큐스캔Job(database.Context, retry, new R76Activation(), Options.Create(new 음식배달배차배치작업Options()),
            NullLogger<음식배달배차큐스캔Job>.Instance);
        // 별도 영속 후속 예약을 주장하지 않는다. 기존 큐의 실제 소비 query가 같은 대기를 반복 복구한다.
        await job.Execute(DispatchProxy.Create<IJobExecutionContext, R76JobContext>());
        await job.Execute(DispatchProxy.Create<IJobExecutionContext, R76JobContext>());
        Assert.Equal(3, retry.Calls);
        request.ActionCode = FoodSupportFollowUpActions.OperatorReview;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.처리Async(order.주문번호, request));
    }

    [Theory]
    [InlineData("missing-notice")]
    [InlineData("old-result-notice")]
    [InlineData("wrong-source")]
    [InlineData("changed-case")]
    [InlineData("unauthorized")]
    public async Task 음식지원후속은_통지누락_구형문안_원천불일치_판본충돌_무권한을저장하지않는다(string failure)
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        var fixture = new R76SupportFixture(order.주문번호);
        var revision = fixture.Row.Revision;
        switch (failure)
        {
            case "missing-notice": fixture.Row.Notifications.RemoveAt(0); break;
            case "old-result-notice": fixture.Row.PreparedResult = "changed-resolution"; break;
            case "wrong-source": fixture.Row.SourceId = "different-order"; break;
            case "changed-case": fixture.Row.Revision++; break;
            case "unauthorized": fixture.Http.HttpContext!.User = new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "admin")], "test")); break;
        }
        var retry = new R76Transition();
        var service = new 음식주문지원후속UseCase(database.Context, fixture.Service, fixture.Actor, retry,
            NullLogger<음식주문지원후속UseCase>.Instance);
        var request = new FoodSupportFollowUpRequest { ClientRequestId = Guid.NewGuid(), SupportCaseId = fixture.Row.CaseId,
            ExpectedCaseRevision = revision, ExpectedOrderRevision = order.Revision, ActionCode = FoodSupportFollowUpActions.OperatorReview };
        Assert.NotNull(await Record.ExceptionAsync(() => service.처리Async(order.주문번호, request)));
        Assert.Equal(order.Revision, (await database.Context.음식주문.Include(x => x.상태이력).SingleAsync()).상태이력.Count);
        Assert.Equal(0, retry.Calls);
    }

    private sealed class R76SupportFixture
    {
        public HttpContextAccessor Http { get; } = new() { HttpContext = new DefaultHttpContext() };
        public 보호지원Record Row { get; }
        public 보호지원UseCase Service { get; }
        public ICurrentUserAccessor Actor { get; }
        public R76SupportFixture(string orderNo)
        {
            Http.HttpContext!.User = new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "admin"), new(ClaimTypes.Role, "test-admin")], "test"));
            Actor = new HttpContextCurrentUserAccessor(Http);
            Row = new() { CaseId = "support:" + new string('a', 64), Kind = 보호지원종류Codes.분쟁,
                SourceKind = 보호지원출처Codes.음식주문, SourceId = orderNo, OwnerUserId = "customer-1",
                PartyUserIds = ["customer-1", "restaurant-41"], StatusCode = "closed", PreparedResult = "resolution",
                Revision = 5, ReviewCycle = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Row.PreparedResult, new JsonSerializerOptions(JsonSerializerDefaults.Web))))).ToLowerInvariant();
            foreach (var id in Row.PartyUserIds) Row.Notifications.Add(new("result", id, "counterparty", "synthetic-notice", DateTime.UtcNow, DateTime.UtcNow, 1, fingerprint));
            var options = Options.Create(new 보호지원Options());
            Service = new(new R76SupportStore(Row), new R76Source(), new(options), options, TimeProvider.System, Http, new R76Authorization());
        }
    }
    private sealed class R76Authorization : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(user.IsInRole("test-admin") ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            => AuthorizeAsync(user, resource, Array.Empty<IAuthorizationRequirement>());
    }
    private sealed class R76Activation : ISsalddelBackgroundJobActivationPolicy
    { public SsalddelBackgroundWorkloadActivation Evaluate(string key) => new(true, "SyntheticEnabled", "FoodDeliveryWorkflow"); }
    public class R76JobContext : DispatchProxy
    { protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_CancellationToken" ? CancellationToken.None : throw new NotSupportedException(); }
    private sealed class R76Source : I보호지원SourceResolver
    { public Task<보호지원Source?> 조회Async(string sourceKind, string sourceId, string actorUserId, CancellationToken cancellationToken = default) => Task.FromResult<보호지원Source?>(null); }
    private sealed class R76SupportStore(보호지원Record row) : I보호지원Store
    {
        public Task<보호지원Record?> 조회Async(string caseId, CancellationToken ct = default) => Task.FromResult<보호지원Record?>(caseId == row.CaseId ? row : null);
        public Task<bool> 생성Async(보호지원Record record, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 교체Async(보호지원Record record, long expectedRevision, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 목록Async(string? userId, string? kind, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 파기후보Async(DateTime nowUtc, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 보존조정후보Async(int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 파기Async(보호지원Record claimedRecord, DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class R76Transition : I배차대기원장전환Service
    {
        public int Calls { get; private set; }
        public bool ThrowAfterRead { get; set; }
        public Task<배차대기원장전환결과> 추천대기처리Async(string id, CancellationToken ct = default)
        { Calls++; if (ThrowAfterRead) throw new InvalidOperationException("synthetic retry failure"); return Result(id); }
        private static Task<배차대기원장전환결과> Result(string id) => Task.FromResult(배차대기원장전환결과.전환안됨(id, "후보없음", "합성 후보 없음"));
        public Task<배차대기원장전환결과> 계획배차에서추천으로전환Async(string id, CancellationToken ct = default) => Result(id);
        public Task<배차대기원장전환결과> 추천시작Async(string id, string driver, int? ttl = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 추천거절처리Async(string id, string driver, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 추천거절처리Async(string id, string driver, string? reason, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 추천만료처리Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 공개배차로전환Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 실행주체확정결과동기화Async(string id, DispatchConfirmationBoundaryRequest confirmation, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차대기원장전환결과> 배차수락취소처리Async(string id, string driver, string? reason = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
