using System.Security.Claims;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Controllers.Driver.Progress05;

namespace Ssalddel.Tests.Controllers;

public sealed class CargoWorkflowFailureTests
{
    [Fact]
    public async Task 운송조회404는현재기사의조회맥락이며취소나완료를단정하지않는다()
    {
        var sender = new Sender { Response = null };
        var result = Assert.IsType<ObjectResult>(await Controller(sender).상세조회(42));
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("CargoTransportNotFound", problem.Extensions["errorCode"]);
        var context = Assert.IsType<업무실패맥락Dto>(problem.Extensions["workflowContext"]);
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, context.OperatingSystemId);
        Assert.Equal(OperatingSystemLifecycleStageIds.CargoTransport, context.StageId);
        Assert.Equal(업무처리결과Codes.조회, context.OutcomeCode);
        Assert.Equal("driver-1", Assert.IsType<Ssalddel.Application.Driver.Transport.운송상세조회Query>(sender.Request).기사Id);
    }

    [Theory]
    [InlineData(true, "cargo.pickup")]
    [InlineData(false, "cargo.dropoff")]
    public async Task 결과미확정의일반명령실패에미적용을추측하지않는다(bool pickup, string stage)
    {
        var controller = Controller(new Sender { Response = Result.Fail<기사운송상태변경응답>("처리 중 오류") });
        var result = pickup ? await controller.상차완료(42, null) : await controller.완료(42, null);
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        var context = Assert.IsType<업무실패맥락Dto>(problem.Extensions["workflowContext"]);
        Assert.Equal(stage, context.StageId);
        Assert.Equal(업무처리결과Codes.미확정, context.OutcomeCode);
    }

    private static 기사운송진행Controller Controller(Sender sender) => new(sender)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "driver-1")], "test"))
        } }
    };

    private sealed class Sender : ISender
    {
        public object? Response { get; init; }
        public object? Request { get; private set; }
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        { Request = request; return Task.FromResult((TResponse)Response!); }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
