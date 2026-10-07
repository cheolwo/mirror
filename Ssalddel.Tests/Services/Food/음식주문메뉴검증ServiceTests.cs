using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.Food;

public sealed class 음식주문메뉴검증ServiceTests
{
    [Fact]
    public async Task 공개메뉴Id를기준으로_클라이언트메뉴명과단가를서버값으로교체한다()
    {
        await using var db = CreateContext();
        await SeedRestaurantAsync(db);
        var service = new 음식주문메뉴검증Service(db);

        var result = await service.서버기준요청생성Async(
            CreateRequest(101, 1001, quantity: 2, name: "변조 메뉴", price: 1),
            CancellationToken.None);

        var item = Assert.Single(result.상품목록);
        Assert.Equal(1001, item.메뉴Id);
        Assert.Equal("살뜰김밥", item.상품명);
        Assert.Equal(4_500m, item.단가);
        Assert.Equal(2, item.수량);
    }

    [Fact]
    public async Task 다른음식점메뉴나품절메뉴는_주문스냅샷으로만들지않는다()
    {
        await using var db = CreateContext();
        await SeedRestaurantAsync(db);
        var service = new 음식주문메뉴검증Service(db);

        var wrongRestaurant = await Assert.ThrowsAsync<음식주문입력확인Exception>(() =>
            service.서버기준요청생성Async(
                CreateRequest(101, 2001, quantity: 1),
                CancellationToken.None));
        var soldOut = await Assert.ThrowsAsync<음식주문입력확인Exception>(() =>
            service.서버기준요청생성Async(
                CreateRequest(101, 1002, quantity: 1),
                CancellationToken.None));

        Assert.Contains("선택한 음식점", wrongRestaurant.Message);
        Assert.Contains("품절", soldOut.Message);
        Assert.Equal(FoodOrderSubmissionErrorCodes.MenuUnavailable, wrongRestaurant.ErrorCode);
        Assert.Equal(FoodOrderSubmissionErrorCodes.MenuUnavailable, soldOut.ErrorCode);
    }

    [Fact]
    public async Task 서버가격으로계산한금액이최소주문보다작으면_등록을거절한다()
    {
        await using var db = CreateContext();
        await SeedRestaurantAsync(db);
        var service = new 음식주문메뉴검증Service(db);

        var exception = await Assert.ThrowsAsync<음식주문입력확인Exception>(() =>
            service.서버기준요청생성Async(
                CreateRequest(101, 1001, quantity: 1, price: 99_999),
                CancellationToken.None));

        Assert.Contains("최소 주문 금액", exception.Message);
        Assert.Contains("4,500", exception.Message);
        Assert.Equal(FoodOrderSubmissionErrorCodes.InputInvalid, exception.ErrorCode);
    }

    [Theory]
    [InlineData(4_000)]
    [InlineData(5_000)]
    public async Task 선택가격확인이필요하면_가격인상과인하모두_등록전에재확인을요구한다(int selectedPrice)
    {
        await using var db = CreateContext();
        await SeedRestaurantAsync(db);
        var request = CreateRequest(101, 1001, 2, price: selectedPrice);
        request.메뉴가격확인필요 = true;
        await Assert.ThrowsAsync<음식주문메뉴가격변경Exception>(() =>
            new 음식주문메뉴검증Service(db).서버기준요청생성Async(request, default));
        Assert.Empty(db.음식주문);
    }

    [Fact]
    public async Task 확인한가격이최신과같으면_서버메뉴명으로요청을만든다()
    {
        await using var db = CreateContext();
        await SeedRestaurantAsync(db);
        var request = CreateRequest(101, 1001, 2, name: "오래된 표시명");
        request.메뉴가격확인필요 = true;
        var canonical = await new 음식주문메뉴검증Service(db).서버기준요청생성Async(request, default);
        Assert.True(canonical.메뉴가격확인필요);
        Assert.Equal("살뜰김밥", Assert.Single(canonical.상품목록).상품명);
        Assert.Equal(4_500m, Assert.Single(canonical.상품목록).단가);
    }

    private static 음식주문등록요청 CreateRequest(
        long restaurantId,
        long menuId,
        int quantity,
        string name = "김밥",
        decimal price = 4_500)
        => new()
        {
            음식점Id = restaurantId,
            주문자UserId = "orderer-1",
            수령인정보 = new 음식주문수령인정보Dto
            {
                수령인명 = "주문자",
                연락처 = "010-1234-5678",
                주소 = "서울특별시 중구 세종대로 1"
            },
            상품목록 =
            [
                new 음식주문상품Dto
                {
                    메뉴Id = menuId,
                    상품명 = name,
                    수량 = quantity,
                    단가 = price
                }
            ],
            결제수단 = "현장결제"
        };

    private static async Task SeedRestaurantAsync(SsalddelContext db)
    {
        db.음식점공개프로필.AddRange(
            new 음식점공개프로필
            {
                Id = 101,
                상호명 = "살뜰분식",
                공개여부 = true,
                주문가능여부 = true,
                최소주문금액 = 8_000,
                메뉴목록 =
                [
                    new 음식점메뉴
                    {
                        Id = 1001,
                        메뉴명 = "살뜰김밥",
                        판매가 = 4_500,
                        공개여부 = true
                    },
                    new 음식점메뉴
                    {
                        Id = 1002,
                        메뉴명 = "품절라면",
                        판매가 = 5_000,
                        공개여부 = true,
                        품절여부 = true
                    }
                ]
            },
            new 음식점공개프로필
            {
                Id = 202,
                상호명 = "다른분식",
                공개여부 = true,
                주문가능여부 = true,
                메뉴목록 =
                [
                    new 음식점메뉴
                    {
                        Id = 2001,
                        메뉴명 = "다른김밥",
                        판매가 = 4_000,
                        공개여부 = true
                    }
                ]
            });
        await db.SaveChangesAsync();
    }

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"food-menu-validation-{Guid.NewGuid():N}")
                .Options,
            new PassThroughEncryptionService());

    private sealed class PassThroughEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
