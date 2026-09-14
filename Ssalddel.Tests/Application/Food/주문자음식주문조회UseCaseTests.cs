using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Food;
using System.Text.Json;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Food;

public sealed class 주문자음식주문조회UseCaseTests
{
    [Fact]
    public async Task 목록은_로그인주문자소유범위에서검색상태와페이징을적용한다()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var useCase = new 주문자음식주문조회UseCase(context);

        var result = await useCase.목록Async(new 주문자음식주문목록조회요청
        {
            검색어 = "김밥",
            상태 = 음식주문상태코드.조리중,
            Page = 1,
            PageSize = 10
        }, "user-a", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("FOOD-A-002", item.주문번호);
        Assert.Equal("김밥집", item.음식점명);
        Assert.Equal("김밥", item.상품요약);
        Assert.Equal(2, item.총수량);
        Assert.Equal(9000m, item.총주문금액);
    }

    [Fact]
    public async Task 상세는_정확한소유주문만반환하고다른사용자주문은404로숨긴다()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var useCase = new 주문자음식주문조회UseCase(context);

        var own = await useCase.상세Async("FOOD-A-001", "user-a", CancellationToken.None);
        var other = await useCase.상세Async("FOOD-B-001", "user-a", CancellationToken.None);

        Assert.True(own.IsSuccess);
        Assert.Equal("FOOD-A-001", own.Value.주문.주문번호);
        Assert.Equal("서울 강서구 수령로 1", own.Value.수령인정보.주소);
        Assert.Equal("010-1234-5678", own.Value.수령인정보.연락처);
        Assert.Equal("돈까스", Assert.Single(own.Value.상품목록).상품명);
        Assert.Equal(42L, Assert.Single(own.Value.상품목록).메뉴Id);
        Assert.True(other.IsFailed);
        Assert.Equal(404, other.Errors.Single().Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 로그인사용자가없으면401이고지원하지않는상태는거부한다()
    {
        await using var context = CreateContext();
        var useCase = new 주문자음식주문조회UseCase(context);

        var anonymous = await useCase.목록Async(new(), null, CancellationToken.None);
        var invalidStatus = await useCase.목록Async(
            new 주문자음식주문목록조회요청 { 상태 = "알수없음" },
            "user-a",
            CancellationToken.None);

        Assert.True(anonymous.IsFailed);
        Assert.Equal(401, anonymous.Errors.Single().Metadata["StatusCode"]);
        Assert.True(invalidStatus.IsFailed);
        Assert.Contains("상태를 확인", invalidStatus.Errors.Single().Message);
    }

    [Fact]
    public async Task 상세는_음식배달운송투영을_읽어_기사수락과진행상태를주문자에게돌려준다()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var updatedAt = new DateTime(2026, 7, 20, 1, 20, 0, DateTimeKind.Utc);
        context.운송원장.Add(new 운송원장
        {
            운송번호 = "FOOD-A-001",
            의뢰Id = "FOOD-A-001",
            원본의뢰유형 = "FoodOrder",
            원본의뢰Id = "FOOD-A-001",
            배차업무유형 = 상태값.배차업무유형.음식배달,
            상태 = "배차확정",
            확정기사Id = "driver-private",
            UpdatedAt = updatedAt
        });
        await context.SaveChangesAsync();
        var useCase = new 주문자음식주문조회UseCase(context);

        var result = await useCase.상세Async("FOOD-A-001", "user-a", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(음식주문배차상태코드.기사배정, result.Value.주문.배차상태);
        Assert.True(result.Value.배달진행.배차요청됨);
        Assert.True(result.Value.배달진행.기사배정됨);
        Assert.Equal("배차확정", result.Value.배달진행.현재운송상태);
        Assert.Equal(updatedAt, result.Value.배달진행.최근변경시각Utc);
        Assert.Contains("기사가 주문을 수락", result.Value.배달진행.안내);
        Assert.DoesNotContain("driver-private", JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task 목록은_음식배달운송투영의_완료상태를_기존주문배차상태보다우선한다()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        context.운송원장.Add(new 운송원장
        {
            운송번호 = "FOOD-A-002",
            의뢰Id = "FOOD-A-002",
            원본의뢰유형 = "FoodOrder",
            원본의뢰Id = "FOOD-A-002",
            배차업무유형 = 상태값.배차업무유형.음식배달,
            상태 = "인수완료",
            확정기사Id = "driver-private",
            UpdatedAt = new DateTime(2026, 7, 20, 2, 40, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();
        var useCase = new 주문자음식주문조회UseCase(context);

        var result = await useCase.목록Async(
            new 주문자음식주문목록조회요청 { PageSize = 10 },
            "user-a",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = Assert.Single(result.Value.Items, item => item.주문번호 == "FOOD-A-002");
        Assert.Equal(음식주문배차상태코드.배달완료, order.배차상태);
    }

    [Fact]
    public async Task 상세는_기사전달완료와주문자수령확인을_서로다른단계로반환한다()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var order = await context.음식주문
            .Include(item => item.상태이력)
            .SingleAsync(item => item.주문번호 == "FOOD-A-001");
        var deliveredAt = new DateTime(2026, 7, 20, 1, 45, 0, DateTimeKind.Utc);
        order.상태 = 음식주문상태코드.전달완료;
        order.배차상태 = 음식주문배차상태코드.배달완료;
        order.UpdatedAt = deliveredAt;
        order.상태이력.Add(new 음식주문상태이력
        {
            이전상태 = 음식주문상태코드.픽업완료,
            다음상태 = 음식주문상태코드.전달완료,
            사유 = "고객 전달 완료",
            전이시각Utc = deliveredAt
        });
        await context.SaveChangesAsync();
        var useCase = new 주문자음식주문조회UseCase(context);

        var delivered = await useCase.상세Async("FOOD-A-001", "user-a", CancellationToken.None);

        Assert.True(delivered.IsSuccess);
        Assert.True(delivered.Value.배달진행.기사전달완료);
        Assert.True(delivered.Value.배달진행.수령확인가능);
        Assert.Contains(
            delivered.Value.AvailableActions,
            action => action.ActionId == 음식배달가능행동Ids.주문수령확인);
        Assert.False(delivered.Value.배달진행.주문자수령확인됨);
        Assert.Contains("실제 수령 상태", delivered.Value.배달진행.안내);

        var confirmedAt = deliveredAt.AddMinutes(3);
        order.상태 = 음식주문상태코드.수령확인;
        order.UpdatedAt = confirmedAt;
        order.상태이력.Add(new 음식주문상태이력
        {
            이전상태 = 음식주문상태코드.전달완료,
            다음상태 = 음식주문상태코드.수령확인,
            사유 = "주문자 수령 확인",
            전이시각Utc = confirmedAt
        });
        await context.SaveChangesAsync();

        var confirmed = await useCase.상세Async("FOOD-A-001", "user-a", CancellationToken.None);

        Assert.True(confirmed.IsSuccess);
        Assert.True(confirmed.Value.배달진행.기사전달완료);
        Assert.True(confirmed.Value.배달진행.주문자수령확인됨);
        Assert.False(confirmed.Value.배달진행.수령확인가능);
        Assert.Empty(confirmed.Value.AvailableActions);
        Assert.Equal(confirmedAt, confirmed.Value.배달진행.수령확인시각Utc);
        Assert.Contains("모두 완료", confirmed.Value.배달진행.안내);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private static async Task SeedAsync(SsalddelContext context)
    {
        context.음식주문.AddRange(
            CreateOrder(
                "FOOD-A-001",
                "user-a",
                "돈까스집",
                음식주문상태코드.주문대기,
                new 음식주문상품 { 메뉴Id = 42, 상품명 = "돈까스", 수량 = 1, 단가 = 11000m },
                new DateTime(2026, 7, 20, 1, 0, 0, DateTimeKind.Utc)),
            CreateOrder(
                "FOOD-A-002",
                "user-a",
                "김밥집",
                음식주문상태코드.조리중,
                new 음식주문상품 { 상품명 = "김밥", 수량 = 2, 단가 = 4500m },
                new DateTime(2026, 7, 20, 2, 0, 0, DateTimeKind.Utc)),
            CreateOrder(
                "FOOD-B-001",
                "user-b",
                "비공개식당",
                음식주문상태코드.조리중,
                new 음식주문상품 { 상품명 = "김밥", 수량 = 10, 단가 = 1m },
                new DateTime(2026, 7, 20, 3, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();
    }

    private static 음식주문 CreateOrder(
        string orderNo,
        string userId,
        string restaurantName,
        string status,
        음식주문상품 product,
        DateTime createdAt)
        => new()
        {
            주문번호 = orderNo,
            음식점Id = 7,
            음식점명 = restaurantName,
            음식점주소 = "서울 강서구 음식로 2",
            주문자UserId = userId,
            수령인명 = "주문자",
            수령인연락처 = "010-1234-5678",
            수령지주소 = "서울 강서구 수령로 1",
            수령지상세주소 = "101호",
            총주문금액 = product.단가 * product.수량,
            상태 = status,
            배차상태 = 음식주문배차상태코드.미요청,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            상품목록 = [product],
            상태이력 =
            [
                new 음식주문상태이력
                {
                    이전상태 = string.Empty,
                    다음상태 = 음식주문상태코드.주문대기,
                    사유 = "주문 등록",
                    전이시각Utc = createdAt
                }
            ]
        };

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
