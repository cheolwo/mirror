using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Controllers.Orderer;
using Hongdal.Services.Orderer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Tests.Controllers.Orderer;

public sealed class 공동구매자동집단화ControllerTests
{
    [Fact]
    public void Controller는인증사용자만허용한다()
        => Assert.NotNull(typeof(공동구매자동집단화Controller)
            .GetCustomAttribute<AuthorizeAttribute>());

    [Fact]
    public async Task 목록은참여자와결제정보가없는요약만반환한다()
    {
        var useCase = new RecordingUseCase
        {
            Groups =
            [
                Group(
                    Demand("orderer-a", "참여자 A", "address:a", 10_001m),
                    Demand("orderer-b", "참여자 B", "address:b", 20_002m))
            ]
        };
        var controller = Controller(useCase, "viewer");

        var result = await controller.목록(null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var groups = Assert.IsType<공동구매자동집단요약응답[]>(ok.Value);
        var group = Assert.Single(groups);
        Assert.Equal("auto-group-1", group.자동집단Id);
        Assert.Equal(2, group.수요건수);

        var json = JsonSerializer.Serialize(ok.Value);
        Assert.DoesNotContain(nameof(공동구매자동집단응답.수요목록), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(공동구매자동집단응답.공동구매주문집계원장Id), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(공동구매자동집단응답.예약결제합계), json, StringComparison.Ordinal);
        Assert.DoesNotContain("orderer-a", json, StringComparison.Ordinal);
        Assert.DoesNotContain("참여자 A", json, StringComparison.Ordinal);
        Assert.DoesNotContain("address:a", json, StringComparison.Ordinal);
        Assert.DoesNotContain("10001", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 수요등록은요청의주문자키를로그인사용자로교체한다()
    {
        var useCase = new RecordingUseCase
        {
            RegisterResult = Group(
                Demand("authenticated-orderer", "인증 주문자", "address:mine", 30_003m, "source-mine"),
                Demand("other-orderer", "다른 참여자", "address:other", 40_004m, "source-other"))
        };
        var controller = Controller(useCase, "authenticated-orderer", "인증 주문자");
        var command = new 공동구매자동수요등록Command
        {
            수요출처키 = "source-mine",
            주문자키 = "spoofed-orderer",
            주문자표시명 = string.Empty
        };

        var result = await controller.수요등록(command, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Same(command, useCase.LastCommand);
        Assert.Equal("authenticated-orderer", useCase.LastCommand!.주문자키);
        Assert.Equal("인증 주문자", useCase.LastCommand.주문자표시명);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<공동구매자동집단사용자응답>(ok.Value);
        var ownDemand = Assert.Single(response.수요목록);
        Assert.Equal("authenticated-orderer", ownDemand.주문자키);
        Assert.Equal(30_003m, ownDemand.예약결제금액);
        Assert.Equal("aggregation-ledger-1", response.공동구매주문집계원장Id);

        var json = JsonSerializer.Serialize(ok.Value);
        Assert.DoesNotContain(nameof(공동구매자동수요응답.주문자표시명), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(공동구매자동수요응답.수령지주소참조키), json, StringComparison.Ordinal);
        Assert.DoesNotContain("other-orderer", json, StringComparison.Ordinal);
        Assert.DoesNotContain("다른 참여자", json, StringComparison.Ordinal);
        Assert.DoesNotContain("address:other", json, StringComparison.Ordinal);
        Assert.DoesNotContain("40004", json, StringComparison.Ordinal);
    }

    [Fact]
    public void 공개응답은기존클라이언트의핵심필드와역직렬화호환된다()
    {
        var response = new 공동구매자동집단사용자응답
        {
            자동집단Id = "auto-group-1",
            상품키 = "product-1",
            상품명 = "테스트 상품",
            공동구매주문집계원장Id = "aggregation-ledger-1",
            수요목록 =
            [
                new 공동구매자동본인수요응답
                {
                    수요Id = "demand-1",
                    수요출처키 = "source-mine",
                    주문자키 = "authenticated-orderer",
                    개별주문원장Id = "individual-ledger-1"
                }
            ]
        };

        var legacyResponse = JsonSerializer.Deserialize<공동구매자동집단응답>(
            JsonSerializer.Serialize(response));

        Assert.NotNull(legacyResponse);
        Assert.Equal(response.자동집단Id, legacyResponse.자동집단Id);
        Assert.Equal(response.공동구매주문집계원장Id, legacyResponse.공동구매주문집계원장Id);
        Assert.Equal("individual-ledger-1", Assert.Single(legacyResponse.수요목록).개별주문원장Id);
    }

    private static 공동구매자동집단화Controller Controller(
        I공동구매자동집단화UseCase useCase,
        string userId,
        string? userName = null)
        => new(useCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId),
                        new Claim(ClaimTypes.Name, userName ?? userId)
                    ], "test"))
                }
            }
        };

    private static 공동구매자동집단응답 Group(params 공동구매자동수요응답[] demands)
        => new()
        {
            자동집단Id = "auto-group-1",
            공동구매주문집계원장Id = "aggregation-ledger-1",
            상품키 = "product-1",
            상품명 = "테스트 상품",
            배송권키 = "delivery-scope-1",
            배송권명 = "테스트 배송권",
            수요건수 = demands.Length,
            예약결제건수 = demands.Length,
            총희망수량 = demands.Sum(item => item.희망수량),
            수량단위 = "개",
            예약결제합계 = demands.Sum(item => item.예약결제금액 ?? 0),
            수요목록 = demands
        };

    private static 공동구매자동수요응답 Demand(
        string ordererId,
        string displayName,
        string addressReference,
        decimal reservationAmount,
        string? sourceKey = null)
        => new()
        {
            수요Id = $"demand-{ordererId}",
            수요출처키 = sourceKey ?? $"source-{ordererId}",
            자동집단Id = "auto-group-1",
            상품키 = "product-1",
            상품명 = "테스트 상품",
            주문자키 = ordererId,
            주문자표시명 = displayName,
            수령지주소참조키 = addressReference,
            공동구매주문집계원장Id = "aggregation-ledger-1",
            개별주문원장Id = $"individual-ledger-{ordererId}",
            희망수량 = 1,
            수량단위 = "개",
            예약결제금액 = reservationAmount
        };

    private sealed class RecordingUseCase : I공동구매자동집단화UseCase
    {
        public 공동구매자동수요등록Command? LastCommand { get; private set; }
        public IReadOnlyList<공동구매자동집단응답> Groups { get; set; } = [];
        public 공동구매자동집단응답 RegisterResult { get; set; } = new();

        public Task<공동구매처리결과<IReadOnlyList<공동구매자동집단응답>>> 목록조회Async(
            공동구매자동집단조회조건 조건,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                공동구매처리결과<IReadOnlyList<공동구매자동집단응답>>.성공결과(Groups));

        public Task<공동구매처리결과<공동구매자동집단응답>> 수요등록Async(
            공동구매자동수요등록Command command,
            CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return Task.FromResult(
                공동구매처리결과<공동구매자동집단응답>.성공결과(RegisterResult));
        }
    }
}
