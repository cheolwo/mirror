using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Handlers;
using Ssalddel.Contracts.Food;
using Ssalddel.Controllers.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Food;

public sealed class 음식주문제출확인Tests
{
    [Theory]
    [InlineData("menu-hidden")]
    [InlineData("menu-deleted")]
    [InlineData("menu-sold-out")]
    [InlineData("restaurant-hidden")]
    [InlineData("restaurant-closed")]
    public async Task 최신메뉴를주문할수없으면_실제등록경로는_수정가능한400을반환하고원장을만들지않는다(string change)
    {
        await using var db = await CreateSeededContextAsync();
        await ApplyMenuChangeAsync(db, change);
        var publisher = new RecordingPublisher();
        var result = await Controller(db, publisher).등록(Request(), default);
        var problem = Problem(result, FoodOrderSubmissionErrorCodes.MenuUnavailable);
        Assert.NotEmpty(problem.Title!);
        Assert.Empty(db.음식주문);
        Assert.Empty(db.운영배차활동사건);
        Assert.Empty(publisher.Notifications);
    }

    [Theory]
    [InlineData(4_000)]
    [InlineData(5_000)]
    public async Task 선택가격이바뀌었으면_실제등록경로는_가격재확인400과무저장을보장한다(int newPrice)
    {
        await using var db = await CreateSeededContextAsync();
        db.음식점메뉴.Single().판매가 = newPrice;
        await db.SaveChangesAsync();
        var publisher = new RecordingPublisher();
        var result = await Controller(db, publisher).등록(Request(), default);
        Problem(result, FoodOrderSubmissionErrorCodes.MenuPriceChanged);
        Assert.Empty(db.음식주문);
        Assert.Empty(db.운영배차활동사건);
        Assert.Empty(publisher.Notifications);
    }

    [Fact]
    public async Task 수령입력이누락되면_실제등록경로는_입력400을반환하고원장을만들지않는다()
    {
        await using var db = await CreateSeededContextAsync();
        var request = Request();
        request.수령인정보.연락처 = "";
        var publisher = new RecordingPublisher();
        Problem(await Controller(db, publisher).등록(request, default), FoodOrderSubmissionErrorCodes.InputInvalid);
        Assert.Empty(db.음식주문);
        Assert.Empty(publisher.Notifications);
    }

    [Theory]
    [InlineData("menu-hidden")]
    [InlineData("menu-deleted")]
    [InlineData("menu-sold-out")]
    [InlineData("restaurant-hidden")]
    [InlineData("price-changed")]
    public async Task 이미접수한동일요청은_메뉴변경뒤에도_원래가격과원장을반환하고알림을중복발행하지않는다(string change)
    {
        await using var db = await CreateSeededContextAsync();
        var publisher = new RecordingPublisher();
        var controller = Controller(db, publisher);
        var request = Request();
        var first = Assert.IsType<음식주문응답>(Assert.IsType<OkObjectResult>((await controller.등록(request, default)).Result).Value);
        await ApplyMenuChangeAsync(db, change);
        var retry = Assert.IsType<음식주문응답>(Assert.IsType<OkObjectResult>((await controller.등록(request, default)).Result).Value);
        Assert.Equal(first.주문번호, retry.주문번호);
        Assert.Equal(9_000m, retry.총주문금액);
        Assert.Equal(4_500m, Assert.Single(retry.상품목록).단가);
        Assert.Single(db.음식주문);
        Assert.Single(db.운영배차활동사건);
        Assert.Single(publisher.Notifications);
    }

    [Fact]
    public async Task 저장후알림의일반ArgumentException은_확정입력400으로변환하지않고_재시도는기존주문을반환한다()
    {
        await using var db = await CreateSeededContextAsync();
        var publisher = new RecordingPublisher { Failure = new ArgumentException("후속 처리 실패") };
        var controller = Controller(db, publisher);
        var request = Request();
        await Assert.ThrowsAsync<ArgumentException>(() => controller.등록(request, default));
        var saved = Assert.Single(db.음식주문);
        var retry = Assert.IsType<음식주문응답>(Assert.IsType<OkObjectResult>((await controller.등록(request, default)).Result).Value);
        Assert.Equal(saved.주문번호, retry.주문번호);
        Assert.Single(db.음식주문);
        Assert.Single(publisher.Notifications);
    }

    [Fact]
    public async Task 접수주문조회는_소유자와요청Id를함께검사하고_최근200건밖의원장도찾는다()
    {
        await using var db = await CreateSeededContextAsync();
        var store = new EfSsalddelFoodOrderStore(db);
        var request = Request();
        request.주문자UserId = "owner";
        var first = store.AddOrder(request);
        db.음식주문.Single().CreatedAt = DateTime.UtcNow.AddDays(-10);
        db.음식주문.AddRange(Enumerable.Range(0, 201).Select(i => new 음식주문
        {
            주문번호 = $"other-{i}", 주문자UserId = "other", 음식점Id = 101, CreatedAt = DateTime.UtcNow
        }));
        await db.SaveChangesAsync();
        Assert.DoesNotContain(store.GetOrders().Items, item => item.주문번호 == first.주문번호);
        Assert.Equal(first.주문번호, store.접수주문조회("owner", request.클라이언트요청Id)?.주문번호);
        Assert.Null(store.접수주문조회("other", request.클라이언트요청Id));
        Assert.Null(store.접수주문조회("owner", Guid.Empty));
    }

    private static ProblemDetails Problem(ActionResult<음식주문응답> result, string expectedCode)
    {
        var response = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal(expectedCode, problem.Extensions["errorCode"]);
        return problem;
    }

    private static 음식주문Controller Controller(SsalddelContext db, RecordingPublisher publisher)
        => new(new RegistrationUseCase(new 음식주문등록CommandHandler(
            new EfSsalddelFoodOrderStore(db), new 음식주문메뉴검증Service(db), publisher, db)), null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "owner")], "test"))
                }
            }
        };

    private static 음식주문등록요청 Request() => new()
    {
        클라이언트요청Id = Guid.NewGuid(), 음식점Id = 101, 메뉴가격확인필요 = true,
        수령인정보 = new() { 수령인명 = "수령인", 연락처 = "test-contact", 주소 = "test-address" },
        상품목록 = [new() { 메뉴Id = 1001, 상품명 = "선택 김밥", 단가 = 4_500m, 수량 = 2 }]
    };

    private static async Task ApplyMenuChangeAsync(SsalddelContext db, string change)
    {
        var restaurant = db.음식점공개프로필.Single();
        var menu = db.음식점메뉴.Single();
        switch (change)
        {
            case "menu-hidden": menu.공개여부 = false; break;
            case "menu-deleted": db.음식점메뉴.Remove(menu); break;
            case "menu-sold-out": menu.품절여부 = true; break;
            case "restaurant-hidden": restaurant.공개여부 = false; break;
            case "restaurant-closed": restaurant.주문가능여부 = false; break;
            case "price-changed": menu.판매가 = 6_000m; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        await db.SaveChangesAsync();
    }

    private static async Task<SsalddelContext> CreateSeededContextAsync()
    {
        var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"food-submission-confirmation-{Guid.NewGuid():N}").Options, new Encryption());
        db.음식점공개프로필.Add(new 음식점공개프로필
        {
            Id = 101, 상호명 = "검토 분식", 공개여부 = true, 주문가능여부 = true, 최소주문금액 = 8_000,
            메뉴목록 = [new 음식점메뉴 { Id = 1001, 메뉴명 = "현재 김밥", 판매가 = 4_500m, 공개여부 = true }]
        });
        await db.SaveChangesAsync();
        return db;
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class RecordingPublisher : IPublisher
    {
        public List<object> Notifications { get; } = [];
        public Exception? Failure { get; init; }
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Publish((object)notification, cancellationToken);
    }

    private sealed class RegistrationUseCase(음식주문등록CommandHandler handler) : I음식주문접수UseCase
    {
        public Task<음식주문응답> 등록Async(음식주문등록요청 request, CancellationToken token)
            => handler.Handle(new 음식주문등록Command(request), token);
        public Task<음식주문응답?> 음식점수락Async(string n, 음식점주문수락요청 r, string? u, CancellationToken t) => throw new NotSupportedException();
        public Task<음식주문응답?> 음식점진행변경Async(string n, 음식점주문진행변경요청 r, string u, CancellationToken t) => throw new NotSupportedException();
        public Task<음식주문응답?> 주문자수령확인Async(string n, 주문자음식주문수령확인요청 r, string u, CancellationToken t) => throw new NotSupportedException();
        public Task<음식주문응답?> 주문자취소Async(string n, 주문자음식주문취소요청 r, string u, CancellationToken t) => throw new NotSupportedException();
    }
}
