using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Application.Driver.Work;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Controllers.Driver.Food;
using Xunit;

namespace Ssalddel.Tests.Controllers;

public sealed class FoodDriverCurrentWorkContractTests
{
    [Fact]
    public async Task CurrentWork_uses_authenticated_owner_and_does_not_load_settlement()
    {
        var query = new Query();
        var controller = Controller(query);
        var response = Assert.IsType<OkObjectResult>(await controller.현재업무조회(default));
        var current = Assert.IsType<FoodDeliveryDriverCurrentWorkDto>(response.Value);
        Assert.Equal("driver-1", query.Owner);
        Assert.Equal("driver-1", current.DriverId);
        var json = JsonSerializer.Serialize(current);
        Assert.DoesNotContain("Settlement", json);
        Assert.DoesNotContain("OrderSettlements", json);
        Assert.False(query.LegacyCalled);
        Assert.DoesNotContain(typeof(음식배달기사업무Controller).GetMethod(nameof(controller.현재업무조회))!.GetParameters(),
            parameter => parameter.Name!.Contains("driver", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(typeof(음식배달기사업무Controller).GetCustomAttribute<AuthorizeAttribute>());
        Assert.True(typeof(음식배달기사업무Controller).GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
    }

    [Fact]
    public async Task CurrentWork_without_identity_does_not_call_query()
    {
        var query = new Query();
        var controller = Controller(query, authenticated: false);
        Assert.IsType<UnauthorizedResult>(await controller.현재업무조회(default));
        Assert.Null(query.Owner);
    }

    [Fact]
    public async Task Legacy_settlement_failure_returns_recoverable_503_instead_of_zero()
    {
        var query = new Query { SettlementUnavailable = true };
        var controller = Controller(query);
        var response = Assert.IsType<ObjectResult>(await controller.업무공간조회(default));
        Assert.Equal(503, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(FoodDeliverySettlementUnavailableException.Code, problem.Extensions["errorCode"]);
        Assert.Equal(업무실패분류Codes.일시기술장애, problem.Extensions["failureClassCode"]);
        Assert.IsType<OkObjectResult>(await controller.현재업무조회(default));
    }

    [Fact]
    public async Task Start_forwards_request_id_and_preserves_created_response()
    {
        var sender = new Sender();
        var controller = Controller(new Query(), sender);
        var requestId = Guid.NewGuid();
        var response = Assert.IsType<CreatedAtActionResult>(await controller.운행시작(
            new 기사운행시작요청 { 시작위치 = "37.5,127.0", ClientRequestId = requestId }, default));
        Assert.Equal(requestId, sender.Command!.ClientRequestId);
        Assert.Equal("driver-1", sender.Command.기사Id);
        Assert.Equal(42, Assert.IsType<기사운행시작응답>(response.Value).ShiftId);
    }

    [Fact]
    public async Task Start_conflict_keeps_machine_readable_recovery_and_revision()
    {
        var sender = new Sender { Result = Result.Fail<기사운행시작응답>(new Error("요청 내용이 다릅니다.")
            .WithMetadata("StatusCode", 409).WithMetadata("ErrorCode", "DriverWork.StartRequestConflict")
            .WithMetadata("CurrentRevision", 7L).WithMetadata("FailureClassCode", 업무실패분류Codes.판본충돌)) };
        var response = Assert.IsType<ObjectResult>(await Controller(new Query(), sender).운행시작(
            new 기사운행시작요청 { ClientRequestId = Guid.NewGuid() }, default));
        Assert.Equal(409, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal("DriverWork.StartRequestConflict", problem.Extensions["errorCode"]);
        Assert.Equal(7L, problem.Extensions["currentRevision"]);
        Assert.Equal(업무실패분류Codes.판본충돌, problem.Extensions["failureClassCode"]);
        Assert.Equal(업무재시도정책Codes.상태재조회후사용자재시도, problem.Extensions["retryPolicyCode"]);
    }

    private static 음식배달기사업무Controller Controller(Query query, Sender? sender = null, bool authenticated = true)
        => new(query, null!, null!, sender!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = authenticated ? new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, "driver-1"), new Claim(ClaimTypes.Role, "기사")
                ], "test")) : new ClaimsPrincipal()
            } }
        };

    private sealed class Query : IFoodDeliveryDriverWorkspaceUseCase
    {
        public string? Owner { get; private set; }
        public bool LegacyCalled { get; private set; }
        public bool SettlementUnavailable { get; init; }
        public Task<FoodDeliveryDriverCurrentWorkDto> GetCurrentWorkAsync(string driverId, CancellationToken token)
        { Owner = driverId; return Task.FromResult(new FoodDeliveryDriverCurrentWorkDto { DriverId = driverId }); }
        public Task<FoodDeliveryDriverWorkspaceDto> GetAsync(string driverId, CancellationToken token)
        {
            LegacyCalled = true;
            if (SettlementUnavailable) throw new FoodDeliverySettlementUnavailableException();
            throw new InvalidOperationException("Legacy workspace must not be loaded.");
        }
        public Task<FoodDeliveryDailySettlementDto> GetDailySettlementAsync(string driverId, DateOnly? date, CancellationToken token)
            => throw new InvalidOperationException("Settlement must not be loaded.");
        public Task<FoodDeliveryCompletedDeliveryDetailDto?> GetCompletedDeliveryDetailAsync(string driverId, string settlementId, CancellationToken token)
            => throw new InvalidOperationException("History must not be loaded.");
    }

    private sealed class Sender : ISender
    {
        public 운행시작Command? Command { get; private set; }
        public Result<기사운행시작응답> Result { get; set; } = FluentResults.Result.Ok(new 기사운행시작응답 { ShiftId = 42 });
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        { Command = Assert.IsType<운행시작Command>(request); return Task.FromResult((TResponse)(object)Result); }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
