using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Controllers.Driver.Settings05;
using 살뜰.Services.Dispatch.Common;

namespace Ssalddel.Tests.Controllers;

public sealed class DispatchReceiptR62ControllerTests
{
    [Fact]
    public void 수신확인은_기존기사인증과명시PUT경로를사용한다()
    {
        Assert.NotNull(typeof(기사운영배차공통Controller).GetCustomAttribute<AuthorizeAttribute>()?.Roles);
        Assert.Equal("offers/{requestId}/receipt", typeof(기사운영배차공통Controller)
            .GetMethod(nameof(기사운영배차공통Controller.제안수신확인))!.GetCustomAttribute<HttpPutAttribute>()!.Template);
    }

    [Theory]
    [InlineData(true, StatusCodes.Status204NoContent)]
    [InlineData(false, StatusCodes.Status409Conflict)]
    public async Task 인증된본인의현재추천차수만서비스에전달한다(bool recorded, int status)
    {
        var service = new ReceiptService { Recorded = recorded };
        var result = await Controller(service).제안수신확인("offer-r62", new() { 추천라운드 = 4 }, default);
        Assert.Equal(("driver-r62", "offer-r62", 4), Assert.Single(service.Calls));
        if (recorded) Assert.IsType<NoContentResult>(result);
        else Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task 구형또는잘못된차수는미응답근거로저장하지않는다(int round)
    {
        var service = new ReceiptService();
        var result = await Controller(service).제안수신확인("offer", new() { 추천라운드 = round }, default);
        Assert.Equal(400, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(service.Calls);
    }

    [Fact]
    public async Task 수신확인서비스가없으면샘플성공으로처리하지않는다()
        => Assert.Equal(503, Assert.IsType<ObjectResult>(
            await Controller(null).제안수신확인("offer", new() { 추천라운드 = 1 }, default)).StatusCode);

    [Fact]
    public async Task OFF요청으로자동중지를해제할수없다()
    {
        var result = await Controller(null).수신의사변경(new()
        {
            클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.Off, 미응답자동중지해제 = true
        }, default);
        Assert.Equal(400, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task 같은요청의해제의사가달라지면409로안내하며저장실패와구별한다()
    {
        var controller = Controller(null);
        var result = await controller.수신의사변경(new()
        {
            클라이언트요청Id = Guid.NewGuid(), 수신의사Code = 운영배차수신의사Code.On
        }, default);
        Assert.Equal(409, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    private static 기사운영배차공통Controller Controller(I운영배차제안표시Service? receipt)
        => new(new UnusedUseCase(), receipt)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "driver-r62"), new Claim(ClaimTypes.Role, "기사")], "test"))
                }
            }
        };

    private sealed class ReceiptService : I운영배차제안표시Service
    {
        public bool Recorded { get; init; } = true;
        public List<(string Driver, string Offer, int Round)> Calls { get; } = [];
        public Task<bool> 표시확인Async(string 기사Id, string 의뢰Id, int 추천라운드, CancellationToken cancellationToken = default)
        {
            Calls.Add((기사Id, 의뢰Id, 추천라운드));
            return Task.FromResult(Recorded);
        }
    }

    private sealed class UnusedUseCase : I운영배차공통UseCase
    {
        public Task<운영배차수신상태Dto> 수신상태조회Async(string 주체Id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<운영배차수신상태Dto> 기사의사변경Async(string 기사Id, 운영배차수신의사변경요청 요청, CancellationToken cancellationToken = default) => throw new 운영배차수신요청충돌Exception();
        public Task<운영배차수신상태Dto> 서버실효상태변경Async(string 주체Id, Guid 요청Id, string 실효상태Code, string 실효사유Code, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<운영배차단기지표Dto> 단기지표조회Async(string 주체Id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> 활동사건기록Async(운영배차활동사건Dto 사건, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
