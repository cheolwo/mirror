using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Food;
using Ssalddel.Controllers.Food;
using Ssalddel.Filters;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.Controllers;

public sealed class 음식주문ControllerTests
{
    [Fact]
    public void Controller는_기존음식주문경로와음식배달기능경계를유지한다()
    {
        var type = typeof(음식주문Controller);

        Assert.Equal("api/v1/food-orders", type.GetCustomAttribute<RouteAttribute>()?.Template);
        var feature = Assert.Single(type.GetCustomAttributes<RequireVersionFeatureAttribute>());
        Assert.Equal(VersionFeatureFlagKeys.FoodDeliveryWorkflow, Assert.Single(feature.Arguments!));
        var version = Assert.Single(type.GetCustomAttributes<SsalddelApiVersionAttribute>());
        Assert.Equal(SsalddelProductVersion.V3_0, version.Version);
        Assert.Equal(VersionFeatureFlagKeys.FoodDeliveryWorkflow, version.FeatureKey);
    }

    [Theory]
    [InlineData(nameof(음식주문Controller.목록조회), null)]
    [InlineData(nameof(음식주문Controller.상세조회), "{orderNo}")]
    public void 주문자조회는_로그인과정확한기존Get경로를요구한다(string methodName, string? route)
    {
        var method = typeof(음식주문Controller).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal(route, method.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }

    [Fact]
    public void 주문등록수령확인취소는_로그인을요구하고_음식점업무는음식점정책을요구한다()
    {
        var register = typeof(음식주문Controller).GetMethod(nameof(음식주문Controller.등록));
        var receipt = typeof(음식주문Controller).GetMethod(nameof(음식주문Controller.주문자수령확인));
        var cancellation = typeof(음식주문Controller).GetMethod(nameof(음식주문Controller.주문자취소));
        var inbox = typeof(음식주문Controller).GetMethod(nameof(음식주문Controller.음식점수신함));
        var detail = typeof(음식주문Controller).GetMethod(nameof(음식주문Controller.음식점상세));
        var accept = typeof(음식주문Controller).GetMethod(nameof(음식주문Controller.음식점수락));
        var progress = typeof(음식주문Controller).GetMethod(nameof(음식주문Controller.음식점진행변경));

        Assert.NotNull(register?.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(receipt?.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal(
            "{orderNo}/receipt-confirmation",
            receipt?.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.NotNull(cancellation?.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal(
            "{orderNo}/cancellation",
            cancellation?.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.Equal("restaurant/inbox", inbox?.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal("음식점운영자전용", inbox?.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("restaurant/inbox/{orderNo}", detail?.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal("음식점운영자전용", detail?.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("음식점운영자전용", accept?.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("{orderNo}/restaurant-progress", progress?.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.Equal("음식점운영자전용", progress?.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public async Task 주문등록은_본문의주문자Id대신로그인사용자Id를사용한다()
    {
        var command = new RecordingCommandUseCase();
        var controller = new 음식주문Controller(command, null!, null!)
        {
            ControllerContext = Context(
                new Claim(ClaimTypes.NameIdentifier, "signed-in-orderer"))
        };
        var request = new 음식주문등록요청
        {
            음식점Id = 101,
            주문자UserId = "spoofed-orderer",
            수령인정보 = new() { 주소 = "서울시" },
            상품목록 = [new() { 상품명 = "비빔밥", 수량 = 1, 단가 = 9000 }]
        };

        await controller.등록(request, CancellationToken.None);

        Assert.Equal("signed-in-orderer", command.Registered?.주문자UserId);
    }

    [Fact]
    public async Task 수령확인은_로그인사용자Id를Command에전달한다()
    {
        var command = new RecordingCommandUseCase();
        var controller = new 음식주문Controller(command, null!, null!)
        {
            ControllerContext = Context(
                new Claim(ClaimTypes.NameIdentifier, "signed-in-orderer"))
        };
        var request = new 주문자음식주문수령확인요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            확인메모 = "정상 수령"
        };

        var result = await controller.주문자수령확인(
            "FOOD-RECEIPT",
            request,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("signed-in-orderer", command.ReceiptOrdererUserId);
        Assert.Equal("FOOD-RECEIPT", command.ReceiptOrderNo);
        Assert.Same(request, command.ReceiptRequest);
    }

    [Fact]
    public async Task 주문자취소는_로그인사용자Id를Command에전달한다()
    {
        var command = new RecordingCommandUseCase();
        var controller = new 음식주문Controller(command, null!, null!)
        {
            ControllerContext = Context(
                new Claim(ClaimTypes.NameIdentifier, "signed-in-orderer"))
        };
        var request = new 주문자음식주문취소요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            사유Code = "ChangedMind"
        };

        var result = await controller.주문자취소(
            "FOOD-CANCEL",
            request,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("signed-in-orderer", command.CancellationOrdererUserId);
        Assert.Equal("FOOD-CANCEL", command.CancellationOrderNo);
        Assert.Same(request, command.CancellationRequest);
    }

    [Fact]
    public async Task 음식점수락은_클레임범위밖주문을404로숨기고Command를실행하지않는다()
    {
        var command = new RecordingCommandUseCase();
        var restaurantRead = new StubRestaurantReadUseCase();
        var controller = new 음식주문Controller(command, null!, restaurantRead)
        {
            ControllerContext = Context(
                new Claim(ClaimTypes.NameIdentifier, "restaurant-user"),
                new Claim(음식점접근ClaimTypes.음식점Id, "101"))
        };

        var result = await controller.음식점수락(
            "OTHER-RESTAURANT-ORDER",
            new 음식점주문수락요청(),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.False(command.AcceptCalled);
    }

    [Fact]
    public void 음식점수신함은_지원하지않는처리상태를400으로거부한다()
    {
        var controller = new 음식주문Controller(null!, null!, new StubRestaurantReadUseCase())
        {
            ControllerContext = Context(
                new Claim(ClaimTypes.NameIdentifier, "restaurant-user"),
                new Claim(음식점접근ClaimTypes.음식점Id, "101"))
        };

        var result = controller.음식점수신함(new 음식점주문수신함조회요청
        {
            처리상태 = "알수없음"
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task 음식점역할이있어도_음식점범위클레임이없으면수신함과수락을403으로거부한다()
    {
        var command = new RecordingCommandUseCase();
        var controller = new 음식주문Controller(command, null!, new StubRestaurantReadUseCase())
        {
            ControllerContext = Context(
                new Claim(ClaimTypes.NameIdentifier, "restaurant-user"),
                new Claim(ClaimTypes.Role, "음식점"))
        };

        var inbox = controller.음식점수신함(new 음식점주문수신함조회요청());
        var acceptance = await controller.음식점수락(
            "FOOD-TEST",
            new 음식점주문수락요청(),
            CancellationToken.None);

        Assert.IsType<ForbidResult>(inbox.Result);
        Assert.IsType<ForbidResult>(acceptance.Result);
        Assert.False(command.AcceptCalled);
    }

    [Fact]
    public async Task 음식점진행판본충돌은_정본재조회행동과현재Revision을반환한다()
    {
        var command = new RecordingCommandUseCase
        {
            ProgressException = new DbUpdateConcurrencyException("주문 판본이 변경되었습니다.")
        };
        var restaurantRead = new StubRestaurantReadUseCase
        {
            Detail = new 음식주문응답
            {
                주문번호 = "FOOD-CONFLICT",
                음식점Id = 101,
                Revision = 7
            }
        };
        var controller = new 음식주문Controller(command, null!, restaurantRead)
        {
            ControllerContext = Context(
                new Claim(ClaimTypes.NameIdentifier, "restaurant-user"),
                new Claim(음식점접근ClaimTypes.음식점Id, "101"))
        };

        var result = await controller.음식점진행변경(
            "FOOD-CONFLICT",
            new 음식점주문진행변경요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                예상Revision = 6,
                작업 = 음식점주문진행작업코드.픽업준비
            },
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(업무실패분류Codes.판본충돌, problem.Extensions["failureClassCode"]);
        Assert.Equal(true, problem.Extensions["requiresStateRefresh"]);
        Assert.Equal(7L, problem.Extensions["currentRevision"]);
        var actions = Assert.IsType<string[]>(problem.Extensions["availableRecoveryActions"]);
        Assert.Contains(업무복구행동Ids.상태전체재조회, actions);
    }

    private static ControllerContext Context(params Claim[] claims)
        => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        };

    private sealed class RecordingCommandUseCase : I음식주문접수UseCase
    {
        public 음식주문등록요청? Registered { get; private set; }
        public bool AcceptCalled { get; private set; }
        public string? ReceiptOrderNo { get; private set; }
        public string? ReceiptOrdererUserId { get; private set; }
        public 주문자음식주문수령확인요청? ReceiptRequest { get; private set; }
        public string? CancellationOrderNo { get; private set; }
        public string? CancellationOrdererUserId { get; private set; }
        public 주문자음식주문취소요청? CancellationRequest { get; private set; }
        public Exception? ProgressException { get; init; }

        public Task<음식주문응답> 등록Async(
            음식주문등록요청 request,
            CancellationToken cancellationToken)
        {
            Registered = request;
            return Task.FromResult(new 음식주문응답
            {
                주문번호 = "FOOD-TEST",
                음식점Id = request.음식점Id,
                주문자UserId = request.주문자UserId
            });
        }

        public Task<음식주문응답?> 음식점수락Async(
            string orderNo,
            음식점주문수락요청 request,
            string? 처리UserId,
            CancellationToken cancellationToken)
        {
            AcceptCalled = true;
            return Task.FromResult<음식주문응답?>(new 음식주문응답
            {
                주문번호 = orderNo,
                음식점Id = 101
            });
        }

        public Task<음식주문응답?> 음식점진행변경Async(
            string orderNo,
            음식점주문진행변경요청 request,
            string 처리UserId,
            CancellationToken cancellationToken)
        {
            if (ProgressException is not null)
            {
                throw ProgressException;
            }

            return Task.FromResult<음식주문응답?>(new 음식주문응답
            {
                주문번호 = orderNo,
                음식점Id = 101,
                상태 = request.작업
            });
        }

        public Task<음식주문응답?> 주문자수령확인Async(
            string orderNo,
            주문자음식주문수령확인요청 request,
            string 주문자UserId,
            CancellationToken cancellationToken)
        {
            ReceiptOrderNo = orderNo;
            ReceiptOrdererUserId = 주문자UserId;
            ReceiptRequest = request;
            return Task.FromResult<음식주문응답?>(new 음식주문응답
            {
                주문번호 = orderNo,
                주문자UserId = 주문자UserId,
                상태 = 음식주문상태코드.수령확인
            });
        }

        public Task<음식주문응답?> 주문자취소Async(
            string orderNo,
            주문자음식주문취소요청 request,
            string 주문자UserId,
            CancellationToken cancellationToken)
        {
            CancellationOrderNo = orderNo;
            CancellationOrdererUserId = 주문자UserId;
            CancellationRequest = request;
            return Task.FromResult<음식주문응답?>(new 음식주문응답
            {
                주문번호 = orderNo,
                주문자UserId = 주문자UserId,
                상태 = 음식주문상태코드.취소
            });
        }
    }

    private sealed class StubRestaurantReadUseCase : I음식점음식주문조회UseCase
    {
        public 음식주문응답? Detail { get; init; }

        public 음식점주문수신함응답 목록(음식점주문수신함조회요청 request, long 음식점Id) => new();

        public 음식주문응답? 상세(string 주문번호, long 음식점Id) => Detail;
    }
}
