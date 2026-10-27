using MudBlazor;
using SellerApp.Services;
using Ssalddel.Contracts.Common.Orderer;

namespace Ssalddel.Tests.Ui.Seller;

public sealed class 주문자집단수요ProjectionTests
{
    [Theory]
    [InlineData("id")]
    [InlineData("name")]
    [InlineData("key")]
    [InlineData("area")]
    [InlineData("status")]
    public void 검색은_기존다섯필드에서대소문자를무시한다(string field)
    {
        var item = new 공동구매자동집단응답();
        switch (field)
        {
            case "id": item.자동집단Id = "Needle"; break;
            case "name": item.상품명 = "Needle"; break;
            case "key": item.상품키 = "Needle"; break;
            case "area": item.배송권명 = "Needle"; break;
            case "status": item.현재상태 = "Needle"; break;
        }
        Assert.Same(item, Assert.Single(주문자집단수요Projection.Filter([item, new()], "nEEdle")));
    }

    [Fact]
    public void 공백뿐인검색은전체이며_다른검색어를임의로Trim하지않는다()
    {
        var items = new 공동구매자동집단응답[] { new() { 상품명 = "needle" }, new() { 상품명 = " needle " } };
        Assert.Equal(2, 주문자집단수요Projection.Filter(items, " ").Count);
        Assert.Same(items[1], Assert.Single(주문자집단수요Projection.Filter(items, " needle ")));
    }

    [Fact]
    public void 정렬은_모집중충족수정시각순서이며_동률의입력순서를보존한다()
    {
        var now = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
        var items = new 공동구매자동집단응답[]
        {
            new() { 자동집단Id = "ended", 모집종료여부 = true, 모집조건충족여부 = true, 수정시각Utc = now },
            new() { 자동집단Id = "collecting-old", 수정시각Utc = now.AddDays(-1) },
            new() { 자동집단Id = "first-fulfilled", 모집조건충족여부 = true, 수정시각Utc = now },
            new() { 자동집단Id = "collecting-new", 수정시각Utc = now },
            new() { 자동집단Id = "second-fulfilled", 모집조건충족여부 = true, 수정시각Utc = now }
        };
        Assert.Equal(new[] { "first-fulfilled", "second-fulfilled", "collecting-new", "collecting-old", "ended" },
            주문자집단수요Projection.Filter(items, "").Select(item => item.자동집단Id));
        Assert.Equal("ended", items[0].자동집단Id);
    }

    public static TheoryData<decimal?, decimal, bool, double> 진행률 => new()
    {
        { null, 50m, false, 0d }, { null, 50m, true, 100d },
        { 0m, 50m, false, 0d }, { -1m, 50m, true, 100d },
        { 100m, -1m, true, 0d }, { 100m, 25m, true, 25d }, { 100m, 150m, false, 100d }
    };

    [Theory]
    [MemberData(nameof(진행률))]
    public void 수량목표가없으면_현재충족여부로0이나100을표시하고_있으면수량비율을제한한다(
        decimal? target, decimal quantity, bool reached, double expected)
        => Assert.Equal(expected, 주문자집단수요Projection.DemandProgress(new()
        { 목표수량 = target, 총희망수량 = quantity, 모집조건충족여부 = reached, 목표참여자수 = 5, 참여자수 = 5 }));

    [Fact]
    public void 목표문구는_수량을먼저보고_참여자목표와미확인단위를구분한다()
    {
        var item = new 공동구매자동집단응답 { 목표수량 = 100m, 총희망수량 = 50m, 수량단위 = " 박스 ", 목표참여자수 = 4, 참여자수 = 2 };
        Assert.Equal("목표까지 50 박스 필요", 주문자집단수요Projection.DemandProgressText(item));
        item.총희망수량 = 101m;
        Assert.Equal("목표 100 박스 충족", 주문자집단수요Projection.DemandProgressText(item));
        item.목표수량 = null;
        Assert.Equal("목표까지 2명 필요", 주문자집단수요Projection.DemandProgressText(item));
        item.참여자수 = 5;
        Assert.Equal("목표 4명 충족", 주문자집단수요Projection.DemandProgressText(item));
        item.목표참여자수 = null;
        Assert.Equal("목표 수량은 모집 원장에서 협의 중입니다.", 주문자집단수요Projection.DemandProgressText(item));
        Assert.Equal("1 단위 미정", 주문자집단수요Projection.Quantity(1m, " "));
    }

    [Theory]
    [InlineData(공동구매자동집단상태코드.수요수집중, "수요 수집 중", Color.Primary)]
    [InlineData(공동구매자동집단상태코드.확정대기, "조건 충족 · 확인 대기", Color.Info)]
    [InlineData(공동구매자동집단상태코드.확정, "집단 확정", Color.Success)]
    [InlineData(공동구매자동집단상태코드.모집종료목표미달, "모집 종료 · 목표 미달", Color.Warning)]
    [InlineData(" unknown ", "unknown", Color.Primary)]
    [InlineData(" ", "상태 확인 중", Color.Primary)]
    public void 상태별라벨과색을_기존값으로표시한다(string status, string label, Color color)
    {
        var item = new 공동구매자동집단응답 { 현재상태 = status };
        Assert.Equal(label, 주문자집단수요Projection.DemandStatus(item));
        Assert.Equal(color, 주문자집단수요Projection.DemandStatusColor(item));
    }

    [Fact]
    public void 마감물류방식과다음대응은_현재문구를유지한다()
    {
        var item = new 공동구매자동집단응답();
        Assert.Equal("협의 중", 주문자집단수요Projection.RecruitmentDeadline(item));
        item.모집종료시각Utc = new DateTime(2026, 10, 20, 12, 30, 0, DateTimeKind.Local);
        Assert.Equal("10.20 12:30까지", 주문자집단수요Projection.RecruitmentDeadline(item));
        item.모집종료여부 = true;
        Assert.Equal("10.20 12:30 종료", 주문자집단수요Projection.RecruitmentDeadline(item));
        Assert.Equal("수요 집계 후 검토", 주문자집단수요Projection.LogisticsMode(공동구매자동수요물류방식코드.후속검토.ToLowerInvariant()));
        Assert.Equal("후속 검토", 주문자집단수요Projection.LogisticsMode(" "));
        Assert.Equal("다음 대응: 수요 추이를 살펴보고 최소 공급 수량과 가격 구간을 미리 검토합니다.", 주문자집단수요Projection.DemandNextAction(item));
        item.모집조건충족여부 = true;
        Assert.Equal("다음 대응: 공급 가능 수량·포장 단위·가격 구간을 확인해 판매 조건 초안을 준비합니다.", 주문자집단수요Projection.DemandNextAction(item));
    }
}
