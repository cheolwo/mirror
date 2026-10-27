using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Shipper.Queries;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Shipper;
using Ssalddel.Controllers.Shipper;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Application.Shipper;

public sealed class ShipperWarehouseSummaryTests
{
    [Fact]
    public async Task 입고는본인주문_재고는본인소유만_창고와무관하게집계한다()
    {
        await using var db = Context();
        db.창고.Add(new 창고 { Id = 1, 소유자UserId = "owner" });
        db.입고요청.AddRange(
            new 입고요청 { Id = 1, 주문자UserId = "owner", 판매자UserId = "other", 창고Id = 99, 상태 = 입고상태코드.예정 },
            new 입고요청 { Id = 2, 주문자UserId = "owner", 창고Id = 1, 상태 = 입고상태코드.완료 },
            new 입고요청 { Id = 3, 주문자UserId = "other", 판매자UserId = "owner", 창고Id = 1, 상태 = 입고상태코드.예정 });
        db.입고상품.AddRange(
            new 입고상품 { Id = 1, 소유자UserId = "owner", 창고Id = 99, 가용수량 = 3 },
            new 입고상품 { Id = 2, 소유자UserId = "owner", 창고Id = 1, 가용수량 = 0 },
            new 입고상품 { Id = 3, 소유자UserId = "other", 판매자UserId = "owner", 창고Id = 1, 가용수량 = 8 });
        await db.SaveChangesAsync();

        var summary = await new ShipperWarehouseSummaryQueryHandler(db).Handle(new("owner"), default);

        Assert.Equal(1, summary.PendingInboundCount);
        Assert.Equal(1, summary.ActionableInventoryCount);
        Assert.Equal(["ActionableInventoryCount", "PendingInboundCount"],
            typeof(ShipperWarehouseSummaryResponse).GetProperties().Select(p => p.Name).Order().ToArray());
    }

    [Fact]
    public async Task 본인기록이없으면_타인창고에기록이있어도_빈요약을반환한다()
    {
        await using var db = Context();
        db.입고요청.Add(new 입고요청 { 주문자UserId = "another", 상태 = 입고상태코드.예정 });
        db.입고상품.Add(new 입고상품 { 소유자UserId = "another", 가용수량 = 4 });
        await db.SaveChangesAsync();

        var summary = await new ShipperWarehouseSummaryQueryHandler(db).Handle(new("owner"), default);

        Assert.Equal(new ShipperWarehouseSummaryResponse(), summary);
    }

    [Fact]
    public async Task 인증주체가없으면_범위없는조회를실행하지않는다()
    {
        await using var db = Context();
        await Assert.ThrowsAsync<ArgumentException>(() => new ShipperWarehouseSummaryQueryHandler(db).Handle(new(""), default));
        var sender = new RecordingSender();
        var controller = Controller(sender, null);

        Assert.IsType<UnauthorizedResult>(await controller.요약조회(default));
        Assert.Null(sender.Request);
    }

    [Fact]
    public async Task URL의화주나창고식별자로_인증주체범위를바꿀수없다()
    {
        var sender = new RecordingSender();
        var controller = Controller(sender, "owner");
        controller.HttpContext.Request.QueryString = new("?shipperId=other&warehouseId=99&ownerUserId=other");

        Assert.IsType<OkObjectResult>(await controller.요약조회(default));

        Assert.Equal("owner", Assert.IsType<ShipperWarehouseSummaryQuery>(sender.Request).ShipperUserId);
        var authorization = Assert.Single(typeof(ShipperWarehouseSummaryController).GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>());
        Assert.Equal("화주또는판매자전용", authorization.Policy);
    }

    private static ShipperWarehouseSummaryController Controller(RecordingSender sender, string? userId) => new(sender)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(userId is null ? [] : [new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
        } }
    };

    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("shipper-warehouse-summary-" + Guid.NewGuid()).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }

    private sealed class RecordingSender : ISender
    {
        public object? Request { get; private set; }
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        { Request = request; return Task.FromResult((TResponse)(object)new ShipperWarehouseSummaryResponse()); }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
