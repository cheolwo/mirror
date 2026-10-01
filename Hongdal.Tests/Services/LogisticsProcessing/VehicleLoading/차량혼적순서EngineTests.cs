using Hongdal.Contracts.Common.VehicleLoading;
using Hongdal.Services.LogisticsProcessing.VehicleLoading;

namespace Hongdal.Tests.Services.LogisticsProcessing.VehicleLoading;

public sealed class 차량혼적순서EngineTests
{
    private readonly 차량혼적순서Engine _engine = new();

    [Fact]
    public void 계획_여러하차지가있으면_마지막하차화물을먼저안쪽에싣는다()
    {
        혼적화물순서요청항목[] items =
        [
            Item("first", 1, 10m),
            Item("second", 2, 20m),
            Item("last", 3, 30m)
        ];

        var result = _engine.계획(items);

        Assert.Equal(["last", "second", "first"], result.상차순서.Select(x => x.화물코드));
        Assert.Equal(["first", "second", "last"], result.하차순서.Select(x => x.화물코드));
        Assert.Equal("차량 전방 안쪽", result.상차순서[0].차량적재위치);
        Assert.Equal("후방 출입문 가까이", result.상차순서[^1].차량적재위치);
    }

    [Fact]
    public void 계획_같은하차지면_적층불가와무거운화물을먼저싣는다()
    {
        혼적화물순서요청항목[] items =
        [
            Item("light", 1, 5m),
            Item("heavy", 1, 30m),
            Item("floor", 1, 10m, stackable: false)
        ];

        var result = _engine.계획(items);

        Assert.Equal(["floor", "heavy", "light"], result.상차순서.Select(x => x.화물코드));
        Assert.Equal(["light", "heavy", "floor"], result.하차순서.Select(x => x.화물코드));
        Assert.All(result.상차순서, x => Assert.Equal("동일 하차지 적재 구역", x.차량적재위치));
        Assert.Contains("바닥 자리", result.상차순서[0].작업안내);
    }

    [Fact]
    public void 계획_빈목록이면_빈계획을반환한다()
    {
        var result = _engine.계획([]);

        Assert.Empty(result.상차순서);
        Assert.Empty(result.하차순서);
        Assert.NotEmpty(result.운영원칙);
    }

    [Fact]
    public void 계획_중복화물코드면_거부한다()
    {
        혼적화물순서요청항목[] items =
        [
            Item("same", 1, 10m),
            Item("same", 2, 20m)
        ];

        var exception = Assert.Throws<ArgumentException>(() => _engine.계획(items));

        Assert.Contains("중복", exception.Message);
    }

    private static 혼적화물순서요청항목 Item(
        string code,
        int dropoffSequence,
        decimal weightKg,
        bool stackable = true)
        => new(
            code,
            $"화물 {code}",
            $"하차지 {dropoffSequence}",
            dropoffSequence,
            weightKg,
            stackable);
}
