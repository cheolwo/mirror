using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Controllers.Common;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class 주문별원가보수ControllerTests
{
    [Fact]
    public async Task 인증식별자가없으면주문정보를조회하지않는다()
    {
        var query = new Query((_, _, _) => throw new InvalidOperationException("조회되면 안 됩니다."));
        var controller = Create(null);
        Assert.IsType<UnauthorizedResult>(await controller.OrderEconomics("root-a", query, default));
        Assert.Equal(0, query.Calls);
    }

    [Fact]
    public async Task 인증한본인과취소토큰으로만원주문을조회한다()
    {
        using var cancellation = new CancellationTokenSource();
        var expected = new 주문별원가보수Dto();
        var query = new Query((root, actor, ct) =>
        {
            Assert.Equal("root-a", root);
            Assert.Equal("worker-a", actor);
            Assert.Equal(cancellation.Token, ct);
            return Task.FromResult(expected);
        });
        var result = Assert.IsType<OkObjectResult>(await Create("worker-a")
            .OrderEconomics("root-a", query, cancellation.Token));
        Assert.Same(expected, result.Value);
        Assert.Equal(1, query.Calls);
    }

    [Fact]
    public async Task 접근불가응답에원장과다른당사자정보를담지않는다()
    {
        var query = new Query((_, _, _) => throw new NeighborhoodSettlementException("OrderEconomicsNotFound", 404));
        var result = Assert.IsType<ObjectResult>(await Create("worker-a").OrderEconomics("root-b", query, default));
        Assert.Equal(404, result.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("OrderEconomicsNotFound", json);
        Assert.DoesNotContain("root-b", json);
        Assert.DoesNotContain("worker-a", json);
    }

    private static NeighborhoodSettlementController Create(string? actor)
    {
        // 이 조회 경로는 별도 읽기 책임만 사용합니다. 기존 정산 변경 UseCase는 호출하지 않습니다.
        var controller = new NeighborhoodSettlementController(null!);
        var context = new DefaultHttpContext();
        if (actor is not null)
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor)], "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private sealed class Query(Func<string, string, CancellationToken, Task<주문별원가보수Dto>> execute)
        : I주문별원가보수조회Service
    {
        public int Calls { get; private set; }
        public Task<주문별원가보수Dto> GetAsync(string rootOrderLedgerId, string actorUserId, CancellationToken ct)
        {
            Calls++;
            return execute(rootOrderLedgerId, actorUserId, ct);
        }
    }
}
