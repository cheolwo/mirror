using DriverApp.Models.Driver;
using DriverApp.Services.Samples;
using Ssalddel.Contracts.Driver.Recommendation;

namespace Ssalddel.Tests.Clients;

public sealed class CargoFeasibleRoutePresentationR20Tests
{
    [Fact]
    public void 권장방문순서는_별도복사하고_검증표시와수락전대조에보존한다()
    {
        var source = new 기사배차추천항목응답
        {
            의뢰Id = "B", 일정삽입가능여부 = true, 전체일정완수가능여부 = true,
            최적삽입인덱스 = 0, 권장경로순서 = ["신규 상차 B", "신규 하차 B", "기존 하차 A"]
        };
        var item = ServerBackedDriverSampleDataService.ToRequestItem(source);
        Assert.Equal(source.권장경로순서, item.권장경로순서);
        Assert.NotSame(source.권장경로순서, item.권장경로순서);
        Assert.Equal(0, item.최적삽입인덱스);
        Assert.Contains("1. 신규 상차 B\n2. 신규 하차 B\n3. 기존 하차 A", item.권장방문순서표시);
        var reviewed = item.CaptureCargoConditions();
        source.권장경로순서[0] = "server-array-mutation";
        Assert.Equal(reviewed, item.CaptureCargoConditions());
        Assert.Equal("신규 상차 B", item.권장경로순서[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void 같은차수의방문순서나검증조건이바뀌면_다시검토해야한다(int change)
    {
        var order = new[] { "신규 상차 B", "신규 하차 B", "기존 하차 A" };
        var item = new DriverRequestItem
        {
            의뢰Id = "B", 추천라운드 = 4, 일정삽입가능여부 = true,
            전체일정완수가능여부 = true, 최적삽입인덱스 = 0, 권장경로순서 = order
        };
        var reviewed = item.CaptureCargoConditions();
        switch (change)
        {
            case 0: (order[1], order[2]) = (order[2], order[1]); break;
            case 1: item.최적삽입인덱스 = 1; break;
            case 2: item.일정삽입가능여부 = false; break;
            case 3: item.전체일정완수가능여부 = false; break;
        }
        Assert.NotEqual(reviewed, item.CaptureCargoConditions());
        Assert.Equal("B", item.의뢰Id);
        Assert.Equal(4, item.추천라운드);
    }

    [Fact]
    public void 미검증순서를_확정방문안내처럼보이지않고_빈구판도호환한다()
    {
        var item = new DriverRequestItem { 권장경로순서 = ["숨겨야 할 미검증 순서"] };
        Assert.DoesNotContain("숨겨야", item.권장방문순서표시);
        Assert.Contains("재검증", item.권장방문순서표시);
        Assert.Equal(new DriverRequestItem().CaptureCargoConditions(), new DriverRequestItem().CaptureCargoConditions());
        item.일정삽입가능여부 = true;
        Assert.DoesNotContain("숨겨야", item.권장방문순서표시);
    }
}
