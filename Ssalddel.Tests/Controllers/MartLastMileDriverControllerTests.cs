using System.Reflection;
using System.Security.Claims;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Mart;
using Ssalddel.Controllers.Common;
using Ssalddel.Controllers.Driver.Food;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Controllers;

/// <summary>직접 Controller 호출의 인증 주체/계약 시험입니다. 실제 인증 HTTP 미들웨어 시험과 구별합니다.</summary>
public sealed class MartLastMileDriverControllerTests
{
    [Fact]
    public async Task 조회와명령은_인증기사만전달하고_요청판본과입력을보존한다()
    {
        var service = new 기록Service();
        var controller = Driver(service);
        var accept = new 마트기사수락요청 { ClientRequestId = Guid.NewGuid(), ExpectedRecommendationRound = 3,
            ExpectedQueueUpdatedAtUtc = DateTime.UtcNow };
        var reject = new 마트기사거절요청 { ClientRequestId = Guid.NewGuid(), ExpectedRecommendationRound = 3,
            ExpectedQueueUpdatedAtUtc = accept.ExpectedQueueUpdatedAtUtc, ReasonCode = "DriverChoice" };
        var pickup = new 마트기사픽업요청 { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 4,
            ExpectedLabelRevision = 2, Code = "1234", ByNumber = true };
        var deliver = new 마트기사전달요청 { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 5 };

        Assert.IsType<OkObjectResult>(await controller.조회(default));
        Assert.IsType<OkObjectResult>(await controller.업무조회("offer-1", default));
        Assert.IsType<OkObjectResult>(await controller.수락("offer-1", accept, default));
        Assert.IsType<OkObjectResult>(await controller.거절("offer-1", reject, default));
        Assert.IsType<OkObjectResult>(await controller.픽업("offer-1", pickup, default));
        Assert.IsType<OkObjectResult>(await controller.전달("offer-1", deliver, default));
        Assert.IsType<OkObjectResult>(await controller.요청결과조회(deliver.ClientRequestId, default));

        Assert.Equal(6, service.Actors.Count);
        Assert.All(service.Actors, actor => Assert.Equal("authenticated-driver", actor));
        Assert.Same(accept, service.Accept);
        Assert.Same(reject, service.Reject);
        Assert.Same(pickup, service.Pickup);
        Assert.Same(deliver, service.Deliver);
        Assert.Equal(deliver.ClientRequestId, service.RequestId);
        Assert.All(service.Offers, id => Assert.Equal("offer-1", id));
        Assert.Equal(0, service.SlipIssues);
        Assert.DoesNotContain(typeof(마트기사업무Controller).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.DeclaringType == typeof(마트기사업무Controller)).SelectMany(method => method.GetParameters()),
            parameter => parameter.Name!.Contains("driver", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task 로그인정보없으면_조회와모든변경이Service에도달하지않는다()
    {
        var service = new 기록Service();
        var controller = Driver(service, authenticated: false);
        Assert.IsType<UnauthorizedResult>(await controller.조회(default));
        Assert.IsType<UnauthorizedResult>(await controller.업무조회("offer-1", default));
        Assert.IsType<UnauthorizedResult>(await controller.수락("offer-1", new(), default));
        Assert.IsType<UnauthorizedResult>(await controller.거절("offer-1", new(), default));
        Assert.IsType<UnauthorizedResult>(await controller.픽업("offer-1", new(), default));
        Assert.IsType<UnauthorizedResult>(await controller.전달("offer-1", new(), default));
        Assert.IsType<UnauthorizedResult>(await controller.요청결과조회(Guid.NewGuid(), default));
        Assert.Empty(service.Actors);
        Assert.Null(service.RequestId);
    }

    [Fact]
    public async Task 전표GET은발급하지않으며_발급은별도POST와요청ID를요구한다()
    {
        var service = new 기록Service();
        var controller = new 마트전표Controller(service) { ControllerContext = Context("warehouse-owner", 역할명.창고관리자) };
        Assert.IsType<OkObjectResult>(await controller.조회("mart-order", default));
        Assert.Equal(0, service.SlipIssues);
        var request = new 마트포장전표발급요청 { ClientRequestId = Guid.NewGuid() };
        Assert.IsType<OkObjectResult>(await controller.발급("mart-order", request, default));
        Assert.Equal(1, service.SlipIssues);
        Assert.Same(request, service.SlipRequest);
        Assert.Equal("mart-order", service.SlipOrder);

        var read = typeof(마트전표Controller).GetMethod(nameof(controller.조회))!;
        var write = typeof(마트전표Controller).GetMethod(nameof(controller.발급))!;
        Assert.NotNull(read.GetCustomAttribute<HttpGetAttribute>());
        Assert.NotNull(write.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(typeof(마트전표Controller).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task 판본충돌은_409와기계판독오류코드와현재판본을유지한다()
    {
        var service = new 기록Service { Failure = new Error("현재 업무를 다시 조회해 주세요.")
            .WithMetadata("StatusCode", 409).WithMetadata("ErrorCode", "MartDriverRevisionConflict")
            .WithMetadata("CurrentRevision", 7L) };
        var result = Assert.IsType<ObjectResult>(await Driver(service).전달("offer-1", new(), default));
        Assert.Equal(409, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("MartDriverRevisionConflict", problem.Extensions["errorCode"]);
        Assert.Equal(7L, problem.Extensions["currentRevision"]);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(429)]
    public async Task 서버가확정거절한업무실패만_거절표시와대체오류코드를제공한다(int status)
    {
        var service = new 기록Service { Failure = new Error("확정 거절").WithMetadata("StatusCode", status) };
        var result = Assert.IsType<ObjectResult>(await Driver(service).픽업("offer-1", new(), default));
        Assert.Equal(status, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(true, problem.Extensions["commandRejected"]);
        Assert.Equal("MartDriverCommandRejected", problem.Extensions["errorCode"]);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task 인증이나서버장애는_업무가확정거절되었다고표시하지않는다(int status)
    {
        var service = new 기록Service { Failure = new Error("결과 확인 필요").WithMetadata("StatusCode", status) };
        var result = Assert.IsType<ObjectResult>(await Driver(service).전달("offer-1", new(), default));
        Assert.Equal(status, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.False(problem.Extensions.ContainsKey("commandRejected"));
    }

    [Theory]
    [InlineData(409, true)]
    [InlineData(429, true)]
    [InlineData(404, false)]
    public async Task 불확실한요청결과조회는_저장된실패만확정거절하고접수없음은구분한다(int status, bool rejected)
    {
        var requestId = Guid.NewGuid();
        var service = new 기록Service { Failure = new Error("요청 결과 조회").WithMetadata("StatusCode", status)
            .WithMetadata("ErrorCode", status == 404 ? "MartDriverRequestNotFound" : "MartDriverStoredFailure") };

        var result = Assert.IsType<ObjectResult>(await Driver(service).요청결과조회(requestId, default));

        Assert.Equal(requestId, service.RequestId);
        Assert.Equal(status, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(rejected, problem.Extensions.ContainsKey("commandRejected"));
        if (rejected) Assert.Equal(true, problem.Extensions["commandRejected"]);
    }

    [Fact]
    public void 기사전용route와캐시금지경계를유지한다()
    {
        var type = typeof(마트기사업무Controller);
        Assert.Equal(역할명.기사, type.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("api/v1/driver/mart-deliveries", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.True(type.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
        Assert.True(typeof(마트전표Controller).GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
    }

    private static 마트기사업무Controller Driver(기록Service service, bool authenticated = true)
        => new(service) { ControllerContext = authenticated ? Context("authenticated-driver", 역할명.기사)
            : new ControllerContext { HttpContext = new DefaultHttpContext() } };
    private static ControllerContext Context(string actor, string role) => new() { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor), new Claim(ClaimTypes.Role, role)], "test")) } };

    private sealed class 기록Service : I마트기사업무Service
    {
        public List<string> Actors { get; } = [];
        public List<string> Offers { get; } = [];
        public 마트기사수락요청? Accept { get; private set; }
        public 마트기사거절요청? Reject { get; private set; }
        public 마트기사픽업요청? Pickup { get; private set; }
        public 마트기사전달요청? Deliver { get; private set; }
        public Guid? RequestId { get; private set; }
        public int SlipIssues { get; private set; }
        public string? SlipOrder { get; private set; }
        public 마트포장전표발급요청? SlipRequest { get; private set; }
        public Error? Failure { get; init; }
        private Task<Result<마트기사업무항목Dto>> Item(string? actor = null, string? offer = null)
        {
            if (actor is not null) Actors.Add(actor);
            if (offer is not null) Offers.Add(offer);
            return Task.FromResult(Failure is null ? Result.Ok(new 마트기사업무항목Dto { OfferId = offer ?? "offer-1" })
                : Result.Fail<마트기사업무항목Dto>(Failure));
        }
        public Task<Result<마트기사업무목록Dto>> 조회Async(string actor, CancellationToken ct = default)
        { Actors.Add(actor); return Task.FromResult(Result.Ok(new 마트기사업무목록Dto())); }
        public Task<Result<마트기사업무항목Dto>> 업무조회Async(string actor, string offer, CancellationToken ct = default) => Item(actor, offer);
        public Task<Result<마트기사업무항목Dto>> 수락Async(string actor, string offer, 마트기사수락요청 request, CancellationToken ct = default)
        { Accept = request; return Item(actor, offer); }
        public Task<Result<마트기사업무항목Dto>> 거절Async(string actor, string offer, 마트기사거절요청 request, CancellationToken ct = default)
        { Reject = request; return Item(actor, offer); }
        public Task<Result<마트기사업무항목Dto>> 픽업Async(string actor, string offer, 마트기사픽업요청 request, CancellationToken ct = default)
        { Pickup = request; return Item(actor, offer); }
        public Task<Result<마트기사업무항목Dto>> 전달Async(string actor, string offer, 마트기사전달요청 request, CancellationToken ct = default)
        { Deliver = request; return Item(actor, offer); }
        public Task<Result<마트기사업무항목Dto>> 요청결과조회Async(Guid requestId, CancellationToken ct = default)
        { RequestId = requestId; return Item(); }
        public Task<Result<마트포장전표Dto>> 전표조회Async(string orderReference, CancellationToken ct = default)
        { SlipOrder = orderReference; return Task.FromResult(Result.Ok(new 마트포장전표Dto { OrderReference = orderReference })); }
        public Task<Result<마트포장전표Dto>> 전표발급Async(string orderReference, 마트포장전표발급요청 request, CancellationToken ct = default)
        { SlipIssues++; SlipRequest = request; return 전표조회Async(orderReference, ct); }
    }
}
