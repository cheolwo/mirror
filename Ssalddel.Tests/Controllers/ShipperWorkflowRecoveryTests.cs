using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Controllers.Shipper.Request01;

namespace Ssalddel.Tests.Controllers;

public sealed class ShipperWorkflowRecoveryTests
{
    [Theory]
    [InlineData(false, OperatingSystemLifecycleStageIds.ShipperProgressChange)]
    [InlineData(true, OperatingSystemLifecycleStageIds.ShipperRequestCommitment)]
    public async Task MissingReadKeepsItsStageAndDoesNotClaimSubmissionWasRejected(bool byClientRequest, string stage)
    {
        var (controller, fake) = Create(null);
        var result = byClientRequest
            ? await controller.접수요청조회("synthetic-request", default)
            : await controller.의뢰단건조회("synthetic-request");

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal("ShipperRequestNotFound", problem.Extensions["errorCode"]);
        var context = Assert.IsType<업무실패맥락Dto>(problem.Extensions["workflowContext"]);
        Assert.Equal(OperatingSystemIds.ShipperTransportManagement, context.OperatingSystemId);
        Assert.Equal(stage, context.StageId);
        Assert.Equal(업무처리결과Codes.조회, context.OutcomeCode);
        Assert.True(context.유효한가());
        Assert.Single(fake.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FoundReadReturnsTheExistingResponseWithoutChangingItsContract(bool byClientRequest)
    {
        var item = new 화주운송의뢰응답();
        var (controller, fake) = Create(item);
        var result = byClientRequest
            ? await controller.접수요청조회("synthetic-request", default)
            : await controller.의뢰단건조회("synthetic-request");
        Assert.Same(item, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Single(fake.Calls);
    }

    private static (화주운송의뢰Controller Controller, ReadOnlyUseCase Fake) Create(화주운송의뢰응답? item)
    {
        var useCase = DispatchProxy.Create<I화주운송의뢰UseCase, ReadOnlyUseCase>();
        var fake = (ReadOnlyUseCase)useCase;
        fake.Item = item;
        var controller = new 화주운송의뢰Controller(useCase, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return (controller, fake);
    }

    public class ReadOnlyUseCase : DispatchProxy
    {
        public 화주운송의뢰응답? Item { get; set; }
        public List<string> Calls { get; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var name = targetMethod?.Name ?? throw new InvalidOperationException();
            if (name is not (nameof(I화주운송의뢰UseCase.접수요청조회Async) or nameof(I화주운송의뢰UseCase.의뢰단건조회Async)))
                throw new InvalidOperationException("The read probe must not send a business command.");
            Calls.Add(name);
            return Task.FromResult(Item);
        }
    }
}
