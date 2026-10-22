using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Admin.Progress;
using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Controllers.Admin.Progress03;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Admin.Progress;

public sealed class AdminTransportBusinessFilterR9Tests
{
    [Theory]
    [InlineData("freight-delivery", "cargo-active,cargo-completed")]
    [InlineData(" FOOD-DELIVERY ", "food-active")]
    [InlineData(null, "cargo-active,cargo-completed,food-active,unknown")]
    public async Task 업무구분은혼합원장에서서버조회하며미지정은구판전체조회를보존한다(string? business, string expected)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Database(connection);
        await db.Database.EnsureCreatedAsync();
        db.운송원장.AddRange(
            Item(1, "cargo-active", 상태값.배차업무유형.용달운송, "상차완료"),
            Item(2, "food-active", 상태값.배차업무유형.음식배달, "상차완료"),
            Item(3, "cargo-completed", 상태값.배차업무유형.용달운송, "인수완료"),
            Item(4, "unknown", 99, "상차완료"));
        await db.SaveChangesAsync();

        var result = await new 관리자운송목록조회QueryHandler(db).Handle(new(null, business), default);

        Assert.Equal(expected.Split(','), result.Select(item => item.운송번호).OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public async Task 상태필터도업무필터와함께SQL에적용한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Database(connection);
        await db.Database.EnsureCreatedAsync();
        db.운송원장.AddRange(
            Item(1, "cargo-active", 상태값.배차업무유형.용달운송, "상차완료"),
            Item(2, "food-active", 상태값.배차업무유형.음식배달, "상차완료"),
            Item(3, "cargo-completed", 상태값.배차업무유형.용달운송, "인수완료"));
        await db.SaveChangesAsync();
        var result = await new 관리자운송목록조회QueryHandler(db).Handle(new(" 상차완료 ", "freight-delivery"), default);
        Assert.Equal("cargo-active", Assert.Single(result).운송번호);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("order-warehouse")]
    [InlineData("freight")]
    public async Task 잘못된업무구분은전체조회로떨어지지않고API에서400을반환한다(string business)
    {
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, new Encryption());
        var handler = new 관리자운송목록조회QueryHandler(db);
        var sender = new Sender(handler);
        var controller = new 운송진행관리Controller(sender, new Incidents())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var result = Assert.IsType<ObjectResult>(await controller.운송목록조회(null, business));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(business, sender.Request?.BusinessCode);
    }

    [Theory]
    [InlineData("?business=", null)]
    [InlineData("?business=%20%20", null)]
    [InlineData("?business=freight-delivery&business=food-delivery", "freight-delivery")]
    [InlineData("?business=freight-delivery&business=freight-delivery", "freight-delivery")]
    public async Task MVC가명시적빈값을null로바인딩하거나업무값이중복돼도전체조회하지않는다(string query, string? boundBusiness)
    {
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, new Encryption());
        var sender = new Sender(new 관리자운송목록조회QueryHandler(db));
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);
        var controller = new 운송진행관리Controller(sender, new Incidents())
        { ControllerContext = new ControllerContext { HttpContext = context } };

        var result = Assert.IsType<ObjectResult>(await controller.운송목록조회(null, boundBusiness));

        Assert.Equal(400, result.StatusCode);
        Assert.Null(sender.Request);
    }

    [Fact]
    public async Task HttpContext가없는기존직접호출은미지정전체조회를유지한다()
    {
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, new Encryption());
        var sender = new Sender(new 관리자운송목록조회QueryHandler(db));
        var controller = new 운송진행관리Controller(sender, new Incidents());

        var result = Assert.IsType<OkObjectResult>(await controller.운송목록조회(null));

        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<운송진행응답>>(result.Value));
        Assert.Null(sender.Request!.BusinessCode);
    }

    private static 운송원장 Item(long id, string number, int business, string status) => new()
    {
        Id = id, 운송번호 = number, 의뢰Id = number, 배차업무유형 = business, 상태 = status,
        UpdatedAt = new DateTime(2026, 10, 22, 1, 0, 0, DateTimeKind.Utc).AddSeconds(id)
    };
    private static SsalddelContext Database(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Incidents : I비정상운송사건운영UseCase
    {
        public Task<IReadOnlyList<비정상운송사건운영응답>> 목록조회Async(string? 상태Code, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<비정상운송사건운영응답?> 검토Async(string 사건StableId, 비정상운송사건검토요청 request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
    private sealed class Sender(관리자운송목록조회QueryHandler handler) : ISender
    {
        public 관리자운송목록조회Query? Request { get; private set; }
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        { Request = Assert.IsType<관리자운송목록조회Query>(request); return (TResponse)(object)await handler.Handle(Request, cancellationToken); }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
