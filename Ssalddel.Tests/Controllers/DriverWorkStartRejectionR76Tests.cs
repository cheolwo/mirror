using System.Reflection;
using System.Security.Claims;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Controllers.Driver.Work01;

namespace Ssalddel.Tests.Controllers;

public sealed class DriverWorkStartRejectionR76Tests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 서버거절결과에만_원요청번호를표시하고_성공이나구형요청에는추정하지않는다(bool failed, bool noId)
    {
        var sender = DispatchProxy.Create<ISender, Sender>(); ((Sender)(object)sender).Failed = failed;
        var controller = Controller(sender);
        var id = noId ? (Guid?)null : Guid.NewGuid();
        var response = await controller.시작(new() { ClientRequestId = id, 시작위치 = "fixture" });
        if (failed && !noId)
        {
            Assert.Equal("Rejected", controller.Response.Headers["X-Mirror-Work-Start-Outcome"]);
            Assert.Equal(id!.Value.ToString("D"), controller.Response.Headers["X-Mirror-Work-Start-Request-Id"]);
            Assert.Equal(400, Assert.IsType<ObjectResult>(response).StatusCode);
        }
        else Assert.False(controller.Response.Headers.ContainsKey("X-Mirror-Work-Start-Outcome"));
    }

    [Fact]
    public async Task 예외나응답유실은_명확한거절로표시하지않는다()
    {
        var sender = DispatchProxy.Create<ISender, Sender>(); ((Sender)(object)sender).Throw = true;
        var controller = Controller(sender);
        await Assert.ThrowsAsync<IOException>(() => controller.시작(new() { ClientRequestId = Guid.NewGuid() }));
        Assert.False(controller.Response.Headers.ContainsKey("X-Mirror-Work-Start-Outcome"));
    }
    private static 기사운행Controller Controller(ISender sender) => new(sender)
    {
        ControllerContext = new() { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "driver")], "fixture")) } }
    };
    public class Sender : DispatchProxy
    {
        public bool Failed, Throw;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => Throw ? Task.FromException<Result<기사운행시작응답>>(new IOException("fixture unavailable"))
                : Task.FromResult(Failed ? Result.Fail<기사운행시작응답>(new Error("시작 조건 미확인").WithMetadata("StatusCode", 400))
                    : Result.Ok(new 기사운행시작응답 { DriverId = "driver", ShiftId = 76, Status = "운행중" }));
    }
}
