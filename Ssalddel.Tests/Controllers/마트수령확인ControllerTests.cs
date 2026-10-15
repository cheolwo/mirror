using System.Reflection;
using System.Security.Claims;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.Mart;
using Ssalddel.Contracts.Mart;
using Ssalddel.Controllers.Orderer;

namespace Ssalddel.Tests.Controllers;

public sealed class 마트수령확인ControllerTests
{
    [Fact]
    public async Task 인증식별자가없으면조회와변경과결과조회모두UseCase를호출하지않는다()
    {
        var service = new Stub();
        var controller = Create(service, null);
        Assert.IsType<UnauthorizedResult>(await controller.조회(701, default));
        Assert.IsType<UnauthorizedResult>(await controller.확인(701, new(), default));
        Assert.IsType<UnauthorizedResult>(await controller.문제신고(701, new(), default));
        Assert.IsType<UnauthorizedResult>(await controller.요청결과조회(701, Guid.NewGuid(), default));
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task 확인은원본요청과주문ID와취소토큰을UseCase에전달한다()
    {
        using var cancel = new CancellationTokenSource();
        var request = new 마트수령확인요청();
        var service = new Stub { Confirm = (orderId, input, token) =>
        {
            Assert.Equal(701, orderId); Assert.Same(request, input); Assert.Equal(cancel.Token, token);
            return Task.FromResult(Result.Ok(new 마트수령확인Dto()));
        } };
        Assert.IsType<OkObjectResult>(await Create(service, "buyer").확인(701, request, cancel.Token));
        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public async Task 확정충돌과요청결과부재는재전송판단을구분한다()
    {
        var service = new Stub
        {
            Confirm = (_, _, _) => Task.FromResult(Failure(409)),
            Request = (_, _, _) => Task.FromResult(Failure(404))
        };
        var controller = Create(service, "buyer");
        var conflict = Assert.IsType<ObjectResult>(await controller.확인(701, new(), default));
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal(true, Assert.IsType<ProblemDetails>(conflict.Value).Extensions["commandRejected"]);
        var missing = Assert.IsType<ObjectResult>(await controller.요청결과조회(701, Guid.NewGuid(), default));
        Assert.Equal(404, missing.StatusCode);
        Assert.False(Assert.IsType<ProblemDetails>(missing.Value).Extensions.ContainsKey("commandRejected"));
    }

    [Fact]
    public async Task 수령문제신고는수령확인과다른명령으로전달한다()
    {
        var request = new 마트수령문제신고요청();
        var service = new Stub { Issue = (id, input, _) =>
        {
            Assert.Equal(702, id); Assert.Same(request, input);
            return Task.FromResult(Result.Ok(new 마트수령확인Dto()));
        } };
        Assert.IsType<OkObjectResult>(await Create(service, "buyer").문제신고(702, request, default));
        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public void 수령API는로그인과비공개응답을요구하며지급이나보류해제명령이없다()
    {
        var type = typeof(마트수령확인Controller);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/v1/orderer/mart/orders/{orderId:long}/receipt", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.True(type.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
        Assert.Null(type.GetMethod("지급")); Assert.Null(type.GetMethod("보류해제"));
    }

    private static Result<마트수령확인Dto> Failure(int status)
        => Result.Fail<마트수령확인Dto>(new Error("현재 조건을 다시 확인해 주세요.")
            .WithMetadata("StatusCode", status).WithMetadata("ErrorCode", "MartReceiptConflict"));

    private static 마트수령확인Controller Create(Stub service, string? actor)
    {
        var context = new DefaultHttpContext();
        if (actor is not null) context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor)], "test"));
        return new(service) { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private sealed class Stub : I마트수령확인UseCase
    {
        public Task<Result<마트수령문제해결Dto>> 해결조회Async(long id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<마트수령문제해결Dto>> 해결제안Async(long id, 마트수령해결제안요청 r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<마트수령문제해결Dto>> 해결동의Async(long id, 마트수령해결동의요청 r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<마트수령문제해결Dto>> 해결요청결과조회Async(long id, Guid requestId, CancellationToken ct = default) => throw new NotSupportedException();
        public int Calls { get; private set; }
        public Func<long, 마트수령확인요청, CancellationToken, Task<Result<마트수령확인Dto>>>? Confirm { get; init; }
        public Func<long, 마트수령문제신고요청, CancellationToken, Task<Result<마트수령확인Dto>>>? Issue { get; init; }
        public Func<long, Guid, CancellationToken, Task<Result<마트수령확인Dto>>>? Request { get; init; }
        public Task<Result<마트수령확인Dto>> 조회Async(long id, CancellationToken ct = default) { Calls++; return Task.FromResult(Result.Ok(new 마트수령확인Dto())); }
        public Task<Result<마트수령확인Dto>> 확인Async(long id, 마트수령확인요청 r, CancellationToken ct = default) { Calls++; return Confirm!(id, r, ct); }
        public Task<Result<마트수령확인Dto>> 문제신고Async(long id, 마트수령문제신고요청 r, CancellationToken ct = default) { Calls++; return Issue!(id, r, ct); }
        public Task<Result<마트수령확인Dto>> 요청결과조회Async(long id, Guid requestId, CancellationToken ct = default) { Calls++; return Request!(id, requestId, ct); }
    }
}
