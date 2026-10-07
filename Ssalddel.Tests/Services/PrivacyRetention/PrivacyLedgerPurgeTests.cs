using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacyRetention;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacyRetention;

namespace Ssalddel.Tests.Services.PrivacyRetention;

public sealed class PrivacyLedgerPurgeTests
{
    [Fact]
    public void 정확한업무원천과템플릿이일치해야파기한다()
    {
        var doc = new 커뮤니티원장문서 { 원장Id = "food-order:order", 원장템플릿Key = "food-order", 외부참조 = new() { ["음식주문번호"] = "order" } };
        Assert.True(배송원장개인정보파기Service.원천일치(doc, 개인정보파기원천Codes.FoodOrder, "order"));
        Assert.False(배송원장개인정보파기Service.원천일치(doc, 개인정보파기원천Codes.FoodOrder, "other"));
        doc.원장템플릿Key = "education";
        Assert.False(배송원장개인정보파기Service.원천일치(doc, 개인정보파기원천Codes.FoodOrder, "order"));
    }

    [Fact]
    public void 개인정보필드만제거하고_금액메뉴와공개매장장소는보존한다()
    {
        var doc = new 커뮤니티원장문서
        {
            블록목록 = [new() { BlockId = "restaurant", Data = new() { ["주소"] = "공개음식점", ["위도"] = "37", ["연락처전화번호"] = "010" } },
                new() { BlockId = "food-order", Data = new() { ["메뉴요약"] = "김밥", ["총주문금액"] = "10000", ["수령지주소"] = "개인주소", ["요청사항"] = "비밀번호" } }],
            상태이력 = [new() { 상태 = "완료", 메모 = "private" }],
            다이어그램스냅샷 = new() { Nodes = [new() { Data = new() { ["Phone"] = "010", ["amount"] = "4000" } }] }
        };
        배송원장개인정보파기Service.Scrub(doc, 개인정보파기원천Codes.FoodOrder);
        Assert.Equal("공개음식점", doc.블록목록[0].Data["주소"]);
        Assert.False(doc.블록목록[0].Data.ContainsKey("연락처전화번호"));
        Assert.False(doc.블록목록[1].Data.ContainsKey("수령지주소"));
        Assert.Equal("10000", doc.블록목록[1].Data["총주문금액"]); Assert.Equal("김밥", doc.블록목록[1].Data["메뉴요약"]);
        Assert.Null(doc.상태이력[0].메모);
        Assert.False(doc.다이어그램스냅샷!.Nodes[0].Data.ContainsKey("Phone"));
    }

    [Fact]
    public async Task 파기된원본의지연투영은처리기를다시실행하지않는다()
    {
        var handler = new Handler(); var barrier = new Barrier();
        var service = new 커뮤니티원장업무투영동기화Service([handler], NullLogger<커뮤니티원장업무투영동기화Service>.Instance, barrier);
        await service.갱신Async(new 커뮤니티원장Dto { 원장Id = "transport:delivery", 외부참조 = new Dictionary<string, string> { ["원천유형"] = "RestaurantFoodOrder", ["원천Id"] = "order" } });
        Assert.Equal(0, handler.Calls);
        Assert.Equal(개인정보파기원천Codes.FoodOrder, barrier.Source);
    }

    private sealed class Handler : I원장업무투영동기화Handler
    { public int Calls; public bool 처리대상인가(커뮤니티원장Dto 원장) => true; public Task 동기화Async(커뮤니티원장Dto 원장, CancellationToken cancellationToken = default) { Calls++; return Task.CompletedTask; } }
    private sealed class Barrier : I개인정보복원차단Service
    { public string? Source; public Task<bool> 복원허용Async(string sourceCode, string recordId, CancellationToken ct = default) { Source = sourceCode; return Task.FromResult(false); } }
}
