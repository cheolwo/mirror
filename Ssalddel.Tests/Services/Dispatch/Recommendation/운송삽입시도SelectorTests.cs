using 살뜰.Services.Dispatch.Recommendation;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class 운송삽입시도SelectorTests
{
    [Fact]
    public void 완수가능한양수절감시도는_일반시도보다우선하며_절감과소요시간순서로선택한다()
    {
        var attempts = new List<운송삽입시도결과>
        {
            Attempt(0),
            Attempt(1, routeChange: true, feasible: false, saved: 100),
            Attempt(2, routeChange: true, saved: 0),
            Attempt(3, routeChange: true, saved: null),
            Attempt(4, routeChange: true, saved: 4, minutes: 1),
            Attempt(5, routeChange: true, saved: 5, minutes: null),
            Attempt(6, routeChange: true, saved: 5, minutes: 20),
            Attempt(7, routeChange: true, saved: 5, minutes: 10)
        };

        var result = 운송삽입시도Selector.Select(attempts, (null, false, null));

        Assert.Equal(7, result.최적삽입인덱스);
        Assert.True(result.삽입가능여부);
        Assert.True(result.전체완수가능여부);
        Assert.True(result.경로변경이점여부);
        Assert.Equal(5m, result.경로변경절감분);
        Assert.Same(attempts, result.시도목록);
        Assert.Equal(Enumerable.Range(0, 8), attempts.Select(x => x.삽입인덱스));
    }

    [Fact]
    public void 일반완수가능시도는_추가지연오름차순후_거리내림차순을유지한다()
    {
        var attempts = new List<운송삽입시도결과>
        {
            Attempt(0, routeChange: true, saved: 0, delay: 0, distance: 100),
            Attempt(1, feasible: false, delay: 0, distance: 100),
            Attempt(2, delay: null, distance: 100),
            Attempt(3, delay: 1, distance: 100),
            Attempt(4, delay: 0, distance: null),
            Attempt(5, delay: 0, distance: 3),
            Attempt(6, delay: 0, distance: 8)
        };

        var result = 운송삽입시도Selector.Select(attempts, (null, false, null));

        Assert.Equal(6, result.최적삽입인덱스);
        Assert.True(result.삽입가능여부);
        Assert.False(result.경로변경이점여부);
        Assert.Null(result.경로변경절감분);
    }

    [Fact]
    public void 실패시도도_최대위반과추가지연오름차순의fallback으로근거를반환한다()
    {
        var attempts = new List<운송삽입시도결과>
        {
            Attempt(0, feasible: false, violation: null, delay: 0),
            Attempt(1, feasible: false, violation: 2, delay: 0),
            Attempt(2, feasible: false, violation: 1, delay: null),
            Attempt(3, feasible: false, violation: 1, delay: 4),
            Attempt(4, feasible: false, violation: 1, delay: 2)
        };

        var result = 운송삽입시도Selector.Select(attempts, (null, false, null));

        Assert.Equal(4, result.최적삽입인덱스);
        Assert.False(result.삽입가능여부);
        Assert.False(result.전체완수가능여부);
        Assert.False(result.경로변경이점여부);
        Assert.Equal(1m, result.최대시간위반분);
        Assert.Same(attempts[4].위반사유, result.위반사유);
        Assert.Same(attempts[4].경로순서, result.권장경로순서);
        Assert.Same(attempts[4].도착예상목록, result.도착예상목록);
        Assert.Same(attempts, result.시도목록);
    }

    [Fact]
    public void 절감없는경로변경만있으면_완수가능결과도fallback으로구분한다()
    {
        var attempt = Attempt(1, routeChange: true, saved: 0);

        var result = 운송삽입시도Selector.Select([attempt], (null, false, null));

        Assert.Equal(1, result.최적삽입인덱스);
        Assert.False(result.삽입가능여부);
        Assert.True(result.전체완수가능여부);
        Assert.False(result.경로변경이점여부);
    }

    [Theory]
    [InlineData("beneficial")]
    [InlineData("feasible")]
    [InlineData("fallback")]
    public void 모든선택분기의완전동률은_입력순서를유지한다(string branch)
    {
        var first = Attempt(9, routeChange: branch == "beneficial", feasible: branch != "fallback", saved: 1);
        var second = first with { 삽입인덱스 = 1 };
        var attempts = new List<운송삽입시도결과> { first, second };

        var result = 운송삽입시도Selector.Select(attempts, (null, false, null));

        Assert.Equal(9, result.최적삽입인덱스);
        Assert.Same(first, attempts[0]);
        Assert.Same(second, attempts[1]);
    }

    [Theory]
    [InlineData(null, "same", true, true, "same")]
    [InlineData("same", null, true, true, "same")]
    [InlineData(null, null, true, true, null)]
    [InlineData("same", "same", true, true, "same")]
    [InlineData("same", "SAME", true, false, "SAME")]
    [InlineData("same", "other", true, false, "other")]
    [InlineData("same", "same", false, true, "same")]
    public void 종료좌표와실경로근거는_기준과선택을구분하고_옵션은ordinal로대조한다(
        string? baselineOption,
        string? selectedOption,
        bool baselineActual,
        bool expectedSelectedActual,
        string? expectedOption)
    {
        var baselineEnd = new 배차경로좌표(37, 127);
        var selectedEnd = new 배차경로좌표(38, 128);
        var attempt = Attempt(1) with
        {
            종료좌표 = selectedEnd,
            전체실제경로확인됨 = true,
            경로옵션 = selectedOption
        };

        var result = 운송삽입시도Selector.Select([attempt], (baselineEnd, baselineActual, baselineOption));

        Assert.Same(baselineEnd, result.기존일정종료좌표);
        Assert.Same(selectedEnd, result.후보포함종료좌표);
        Assert.Equal(baselineActual, result.기존일정실제경로확인됨);
        Assert.Equal(expectedSelectedActual, result.후보포함실제경로확인됨);
        Assert.Equal(expectedOption, result.복귀비교경로옵션);
    }

    [Fact]
    public void 선택경로가미확인이면_같은옵션만으로실경로가되지않는다()
    {
        var attempt = Attempt(1) with { 전체실제경로확인됨 = false, 경로옵션 = "same" };

        var result = 운송삽입시도Selector.Select([attempt], (null, true, "same"));

        Assert.False(result.후보포함실제경로확인됨);
        Assert.True(result.기존일정실제경로확인됨);
    }

    [Fact]
    public void 빈시도는_원래사유와기준근거를반환하고_목록참조를보존한다()
    {
        var attempts = new List<운송삽입시도결과>();
        var baselineEnd = new 배차경로좌표(37, 127);

        var result = 운송삽입시도Selector.Select(attempts, (baselineEnd, true, "same"));

        Assert.False(result.삽입가능여부);
        Assert.False(result.전체완수가능여부);
        Assert.Null(result.최적삽입인덱스);
        Assert.Equal("삽입 시뮬레이션 결과가 없습니다.", Assert.Single(result.위반사유));
        Assert.Same(baselineEnd, result.기존일정종료좌표);
        Assert.True(result.기존일정실제경로확인됨);
        Assert.Null(result.후보포함종료좌표);
        Assert.False(result.후보포함실제경로확인됨);
        Assert.Null(result.복귀비교경로옵션);
        Assert.Empty(result.권장경로순서);
        Assert.Empty(result.도착예상목록);
        Assert.Same(attempts, result.시도목록);
    }

    private static 운송삽입시도결과 Attempt(int index,
        bool routeChange = false,
        bool feasible = true,
        decimal? saved = null,
        decimal? minutes = 10m,
        decimal? delay = 0m,
        decimal? distance = 1m,
        decimal? violation = 0m)
        => new(index, routeChange ? "분리삽입" : "하차후이어가기", routeChange, feasible,
            minutes, distance, delay, saved, violation,
            [$"시도 {index} 근거"], [$"시도 {index} 경로"],
            [new 운송일정도착예상항목($"request-{index}", "dropoff", 0, "시험 주소", null, null, false, null)]);
}
