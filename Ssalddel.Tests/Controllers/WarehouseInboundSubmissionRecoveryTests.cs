using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Hr;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Controllers.Common;
using Ssalddel.Security;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using Ssalddel.Services.Privacy;
using 살뜰.Services.Audit;

namespace Ssalddel.Tests.Controllers;

public sealed class WarehouseInboundSubmissionRecoveryTests
{
    [Fact]
    public void 원요청조회는_기존운영인증과입고역할의읽기경로다()
    {
        var type = typeof(창고작업Controller);
        var action = type.GetMethod(nameof(창고작업Controller.입고원요청조회))!;
        Assert.Equal("운영사용자전용", type.GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        Assert.Equal("inbounds/by-client-request/{clientRequestId:guid}", action.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal(new[] { HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseInboundOperator },
            action.GetCustomAttribute<RequireHrRoleAttribute>()!.RoleCodes);
    }

    [Fact]
    public async Task 원요청조회는_정확한요청Id를전달하고_응답을캐시하지않는다()
    {
        var id = Guid.NewGuid();
        var (service, spy) = Service();
        spy.Lookup = requestId => Task.FromResult<입고요청항목응답?>(new() { Id = 71, ClientRequestId = requestId });
        var controller = Controller(new(service, new Audit(), null!));

        var response = Assert.IsType<OkObjectResult>(await controller.입고원요청조회(id, default));

        Assert.Equal(id, Assert.Single(spy.LookupIds));
        Assert.Equal(id, Assert.IsType<입고요청항목응답>(response.Value).ClientRequestId);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl);
        Assert.Equal(0, spy.CreateCalls);
    }

    [Fact]
    public async Task 조회404는_아직모르는결과이며_미적용이나재전송권한이아니다()
    {
        var (service, _) = Service();
        var controller = Controller(new(service, new Audit(), null!));

        var problem = Problem(await controller.입고원요청조회(Guid.NewGuid(), default), 404,
            "InboundSubmissionNotFound", 업무처리결과Codes.조회);

        Assert.Equal(new[] { 업무복구행동Ids.상태전체재조회 }, problem.Extensions["availableRecoveryActions"]);
        Assert.Equal(업무재시도정책Codes.자동재시도안함, problem.Extensions["retryPolicyCode"]);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task 빈조회Id는_DB나생성경로를호출하지않는다()
    {
        var (service, spy) = Service();
        var response = await Controller(new(service, new Audit(), null!)).입고원요청조회(Guid.Empty, default);

        Problem(response, 400, "InboundRequestIdInvalid", 업무처리결과Codes.조회);
        Assert.Empty(spy.LookupIds);
        Assert.Equal(0, spy.CreateCalls);
    }

    [Fact]
    public async Task 조회장애는_내부원문없이_결과확인업무로남는다()
    {
        var (service, spy) = Service();
        spy.Lookup = _ => Task.FromException<입고요청항목응답?>(new IOException("private path and SQL"));

        var problem = Problem(await Controller(new(service, new Audit(), null!)).입고원요청조회(Guid.NewGuid(), default),
            503, "InboundSubmissionLookupUnavailable", 업무처리결과Codes.조회);

        Assert.DoesNotContain("private", problem.Title!);
    }

    [Fact]
    public async Task 같은요청Id의다른내용은_기존신청이있으므로_미확정충돌이다()
    {
        var (service, spy) = Service();
        spy.Create = _ => Task.FromException<입고요청항목응답>(new InboundSubmissionConflictException());
        var audit = new Audit();

        Problem(await Controller(new(service, audit, null!)).입고생성(Request(), default),
            409, "InboundSubmissionRequestConflict", 업무처리결과Codes.미확정);

        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task 빈생성요청Id는_실행전명확한거절로만분류한다()
    {
        var (service, spy) = Service();
        spy.Create = _ => Task.FromException<입고요청항목응답>(new InboundSubmissionInvalidRequestException());
        var request = Request();
        request.ClientRequestId = Guid.Empty;

        Problem(await Controller(new(service, new Audit(), null!)).입고생성(request, default),
            400, "InboundRequestIdInvalid", 업무처리결과Codes.미적용);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 생성저장응답실패와취소는_원요청결과확인으로남고_새로운생성을하지않는다(bool cancelled)
    {
        var (service, spy) = Service();
        spy.Create = _ => Task.FromException<입고요청항목응답>(cancelled
            ? new OperationCanceledException("private cancellation") : new IOException("private server path"));

        var problem = Problem(await Controller(new(service, new Audit(), null!)).입고생성(Request(), default),
            503, "InboundSubmissionOutcomeUnknown", 업무처리결과Codes.미확정);

        Assert.Equal(1, spy.CreateCalls);
        Assert.Empty(spy.LookupIds);
        Assert.DoesNotContain("private", problem.Title!);
    }

    [Fact]
    public async Task 원요청Id가없는구형생성은_기존예외처리계약을보존한다()
    {
        var (service, spy) = Service();
        spy.Create = _ => Task.FromException<입고요청항목응답>(new IOException("legacy exception"));
        var request = Request();
        request.ClientRequestId = null;

        await Assert.ThrowsAsync<IOException>(() => Controller(new(service, new Audit(), null!)).입고생성(request, default));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task 최초생성만감사기록을추가하고_같은신청재조회는중복기록하지않는다(bool replay, int expectedLogs)
    {
        var (service, spy) = Service();
        spy.Create = request => Task.FromResult(new 입고요청항목응답
            { Id = 71, 창고Id = 7, ClientRequestId = request.ClientRequestId, 멱등재시도여부 = replay });
        var audit = new Audit();
        var request = Request();

        var response = Assert.IsType<OkObjectResult>(await Controller(new(service, audit, null!)).입고생성(request, default));

        Assert.Equal(request.ClientRequestId, Assert.IsType<입고요청항목응답>(response.Value).ClientRequestId);
        Assert.Equal(expectedLogs, audit.Entries.Count);
    }

    [Fact]
    public async Task 원장저장후감사실패는_적용됨으로보고하고_정본확인을요구한다()
    {
        var (service, spy) = Service();
        spy.Create = request => Task.FromResult(new 입고요청항목응답 { Id = 71, ClientRequestId = request.ClientRequestId });
        var audit = new Audit { Failure = new IOException("private audit path") };

        var problem = Problem(await Controller(new(service, audit, null!)).입고생성(Request(), default),
            503, "InboundSubmissionFollowUpUnavailable", 업무처리결과Codes.적용);

        Assert.DoesNotContain("private", problem.Title!);
        Assert.Equal(1, spy.CreateCalls);
    }

    [Fact]
    public async Task 개인정보동의가거절되면_새원요청생성서비스에진입하지않는다()
    {
        var (service, spy) = Service();
        var controller = Controller(new(service, new Audit(), null!), consentRejected: true);

        Assert.IsType<BadRequestObjectResult>(await controller.입고생성(Request(), default));

        Assert.Equal(0, spy.CreateCalls);
    }

    private static 입고요청저장요청 Request() => new() { ClientRequestId = Guid.NewGuid(), 창고Id = 7 };

    private static ProblemDetails Problem(IActionResult response, int status, string code, string outcome)
    {
        var result = Assert.IsType<ObjectResult>(response);
        Assert.Equal(status, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(code, problem.Extensions["errorCode"]);
        var context = Assert.IsType<업무실패맥락Dto>(problem.Extensions["workflowContext"]);
        Assert.Equal(OperatingSystemIds.WarehouseCommerceFulfillment, context.OperatingSystemId);
        Assert.Equal(OperatingSystemLifecycleStageIds.WarehouseInboundPlan, context.StageId);
        Assert.Equal(outcome, context.OutcomeCode);
        return problem;
    }

    private static (IWarehouseOperationService Service, WarehouseProxy Spy) Service()
    {
        var service = DispatchProxy.Create<IWarehouseOperationService, WarehouseProxy>();
        return (service, (WarehouseProxy)(object)service);
    }

    private static 창고작업Controller Controller(창고작업UseCase useCase, bool consentRejected = false)
    {
        var privacy = DispatchProxy.Create<I신청개인정보동의증적Service, PrivacyProxy>();
        ((PrivacyProxy)(object)privacy).Rejected = consentRejected;
        return new(useCase, null!, null!, null!, null!, null!, null!, null!, privacy)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "fixture-owner")], "fixture")) } }
        };
    }

    public class WarehouseProxy : DispatchProxy
    {
        public int CreateCalls;
        public List<Guid> LookupIds { get; } = [];
        public Func<Guid, Task<입고요청항목응답?>> Lookup { get; set; } = _ => Task.FromResult<입고요청항목응답?>(null);
        public Func<입고요청저장요청, Task<입고요청항목응답>> Create { get; set; } = _ => throw new NotSupportedException();
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IWarehouseOperationService.GetInboundByClientRequestIdAsync))
            {
                var id = (Guid)args![0]!;
                LookupIds.Add(id);
                return Lookup(id);
            }
            if (method.Name == nameof(IWarehouseOperationService.CreateInboundAsync))
            {
                CreateCalls++;
                return Create((입고요청저장요청)args![0]!);
            }
            throw new NotSupportedException(method.Name);
        }
    }

    public class PrivacyProxy : DispatchProxy
    {
        public bool Rejected;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method!.Name == nameof(I신청개인정보동의증적Service.유효한동의요구Async)
                ? Rejected ? Task.FromException(new InvalidOperationException("fixture consent rejected")) : Task.CompletedTask
                : throw new NotSupportedException();
    }

    private sealed class Audit : I사용자행위로그Service
    {
        public Exception? Failure { get; init; }
        public List<사용자행위로그기록> Entries { get; } = [];
        public Task 기록Async(사용자행위로그기록 entry, CancellationToken cancellationToken = default)
        {
            if (Failure is not null) return Task.FromException(Failure);
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
