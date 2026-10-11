using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace Ssalddel.Tests.Domain.Dispatch;

public sealed class 음식배달수요참여PolicyTests
{
    private static readonly DateTimeOffset 현재 = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);
    private readonly 음식배달수요참여Policy _policy = new();

    [Fact]
    public void 시간당35건은_3점5건목표의_기사10명을_권고한다()
    {
        var 입력 = 수요(35, Enumerable.Range(1, 12).Select(i => 기사($"driver-{i:00}")));
        var 결과 = _policy.평가(입력);

        Assert.Equal(10, 결과.목표활동인원);
        Assert.Equal(10, 결과.기사판정목록.Count(x => x.신규제안허용));
        Assert.Equal(2, 결과.기사판정목록.Count(x => x.상태Code == 음식배달수요참여상태Code.수요대기));
        Assert.Equal(음식배달수요참여Policy.현재정책판본, 결과.정책판본);
    }

    [Fact]
    public void 수요가_줄어도_기존활동은_제거하지_않고_신규만_대기한다()
    {
        var 결과 = _policy.평가(수요(0, [기사("existing-1", true), 기사("existing-2", true), 기사("new")]));

        Assert.Equal(2, 결과.보호활동기사수);
        Assert.Equal(2, 결과.목표활동인원);
        Assert.All(결과.기사판정목록.Where(x => x.기존활동여부), x =>
        {
            Assert.True(x.신규제안허용);
            Assert.False(x.승격여부);
        });
        Assert.False(결과.기사판정목록.Single(x => x.기사Id == "new").신규제안허용);
    }

    [Fact]
    public void 기존_기사가_60분간_수행중이면_그_인원을_가용_여력으로_세지_않는다()
    {
        var 기존 = 기사("existing", true);
        기존.남은수행시간분 = 60;
        var 결과 = _policy.평가(수요(7, [기존, 기사("new-1"), 기사("new-2"), 기사("new-3")]));

        Assert.Equal(0m, 결과.기존기사가용처리건수);
        Assert.Equal(3, 결과.목표활동인원);
        Assert.Equal(2, 결과.추가참여인원);
    }

    [Fact]
    public void 남은업무30분은_계획창의_절반_처리량으로_계산한다()
    {
        var 기존 = 기사("existing", true);
        기존.남은수행시간분 = 30;
        var 결과 = _policy.평가(수요(2, [기존, 기사("new")]));

        Assert.Equal(1.75m, 결과.기존기사가용처리건수);
        Assert.Equal(1, 결과.추가참여인원);
    }

    [Fact]
    public void 최신_운행이_아닌_먼저_대기한_기사를_추가참여시킨다()
    {
        var 먼저 = 기사("older");
        먼저.운행시작시각Utc = 먼저.대기시작시각Utc = 현재.AddMinutes(-20);
        var 결과 = _policy.평가(수요(1, [기사("newer"), 먼저]));

        Assert.True(결과.기사판정목록.Single(x => x.기사Id == "older").승격여부);
        Assert.False(결과.기사판정목록.Single(x => x.기사Id == "newer").신규제안허용);
    }

    [Fact]
    public void 주문_증가시_대기순서를_다시_계산해_추가_참여를_권고한다()
    {
        var 입력 = 수요(1, [기사("a"), 기사("b")]);
        var 이전 = _policy.평가(입력);
        입력.대기주문수 = 7;
        var 이후 = _policy.평가(입력);

        Assert.False(이전.기사판정목록.Single(x => x.기사Id == "b").신규제안허용);
        Assert.True(이후.기사판정목록.Single(x => x.기사Id == "b").승격여부);
        Assert.Equal(현재.AddMinutes(-10), 입력.기사목록[1].대기시작시각Utc);
    }

    [Fact]
    public void 장기대기_한명은_기존여력이_충분해도_기회를_받고_기존활동은_보호한다()
    {
        var 대기1 = 기사("wait-1");
        대기1.운행시작시각Utc = 대기1.대기시작시각Utc = 현재.AddMinutes(-40);
        var 대기2 = 기사("wait-2");
        대기2.운행시작시각Utc = 대기2.대기시작시각Utc = 현재.AddMinutes(-35);
        var 결과 = _policy.평가(수요(1, [기사("existing", true), 대기2, 대기1]));

        Assert.Equal("wait-1", 결과.기사판정목록[0].기사Id);
        Assert.Equal("LongWaitOpportunity", 결과.기사판정목록[0].사유Code);
        Assert.True(결과.기사판정목록.Single(x => x.기사Id == "existing").신규제안허용);
        Assert.False(결과.기사판정목록.Single(x => x.기사Id == "wait-2").신규제안허용);
        Assert.Equal(1, 결과.추가참여인원);
    }

    [Fact]
    public void 지연위험이_있으면_기존여력이_충분해도_추가_기사에게_제안한다()
    {
        var 입력 = 수요(1, [기사("existing", true), 기사("new")]);
        입력.지연위험주문존재 = true;
        var 결과 = _policy.평가(입력);

        var 신규 = 결과.기사판정목록.Single(x => x.기사Id == "new");
        Assert.True(신규.신규제안허용);
        Assert.Equal("OrderDelayCapacityException", 신규.사유Code);
    }

    [Fact]
    public void 적격_기존기사가_없고_주문이_있으면_새_기사로_처리를_이어간다()
    {
        var 기존 = 기사("existing", true);
        기존.적격여부 = false;
        var 입력 = 수요(1, [기존, 기사("new")]);
        입력.적격활동기사부재 = true;
        var 결과 = _policy.평가(입력);

        Assert.False(결과.기사판정목록.Single(x => x.기사Id == "existing").신규제안허용);
        Assert.True(결과.기사판정목록.Single(x => x.기사Id == "new").신규제안허용);
    }

    [Fact]
    public void 이미_많이_배차받은_기존기사보다_기회가_적은_기존기사를_먼저_둔다()
    {
        var 먼저시작 = 기사("early", true);
        먼저시작.운행시작시각Utc = 현재.AddHours(-1);
        먼저시작.최근시간창배차수 = 3;
        var 나중시작 = 기사("later", true);
        나중시작.최근시간창배차수 = 1;
        var 결과 = _policy.평가(수요(1, [먼저시작, 나중시작]));

        Assert.Equal("later", 결과.기사판정목록[0].기사Id);
        Assert.All(결과.기사판정목록, x => Assert.True(x.신규제안허용));
    }

    [Fact]
    public void 관측된15분_신규주문과_향후예측은_중복으로_더하지_않는다()
    {
        var 입력 = 수요(2, []);
        입력.최근시간창신규주문수 = 7;
        입력.향후수요예측건수 = 30;
        var 결과 = _policy.평가(입력);

        Assert.Equal(32m, 결과.예상업무량건수);
        Assert.Equal(10, 결과.목표활동인원);
    }

    [Fact]
    public void 낮은_AI예측은_확인된_관측수요를_지우지_않는다()
    {
        var 입력 = 수요(2, []);
        입력.최근시간창신규주문수 = 7;
        입력.향후수요예측건수 = 0;
        var 결과 = _policy.평가(입력);

        Assert.Equal(30m, 결과.예상업무량건수);
        Assert.Equal(9, 결과.목표활동인원);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 수요_미확인이나_만료를_0건으로_오인해_기사참여를_차단하지_않는다(bool 만료)
    {
        var 입력 = 수요(0, [기사("new")]);
        입력.수요관측시각Utc = 만료 ? 현재.AddMinutes(-6) : null;
        var 결과 = _policy.평가(입력);

        Assert.Equal(만료 ? 음식배달수요자료상태Code.만료 : 음식배달수요자료상태Code.미확인, 결과.데이터상태Code);
        Assert.Null(결과.목표활동인원);
        Assert.True(Assert.Single(결과.기사판정목록).신규제안허용);
        Assert.Equal(음식배달수요참여상태Code.기존관문재검사, 결과.기사판정목록[0].상태Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 기존기사_잔여업무가_미확인이거나_관측이_만료면_전원_기존관문으로_인계한다(bool 만료)
    {
        var 기존 = 기사("existing", true);
        if (만료) 기존.마지막관측시각Utc = 현재.AddMinutes(-6);
        else 기존.남은수행시간분 = null;
        var 결과 = _policy.평가(수요(0, [기존, 기사("new")]));

        Assert.NotEqual(음식배달수요자료상태Code.최신, 결과.데이터상태Code);
        Assert.All(결과.기사판정목록, x => Assert.True(x.신규제안허용));
        Assert.Null(결과.기존기사가용처리건수);
    }

    [Fact]
    public void unknown_인계도_독립_적격조건_실패를_허용하지_않는다()
    {
        var 부적격 = 기사("ineligible");
        부적격.적격여부 = false;
        var 입력 = 수요(0, [부적격]);
        입력.대기주문수 = null;
        Assert.False(Assert.Single(_policy.평가(입력).기사판정목록).신규제안허용);
    }

    [Fact]
    public void 미래_수요_관측을_최신_근거로_쓰지_않는다()
    {
        var 입력 = 수요(0, [기사("new")]);
        입력.수요관측시각Utc = 현재.AddSeconds(1);
        var 결과 = _policy.평가(입력);

        Assert.Equal(음식배달수요자료상태Code.만료, 결과.데이터상태Code);
        Assert.True(Assert.Single(결과.기사판정목록).신규제안허용);
    }

    [Fact]
    public void 중복_기사나_입력_순서를_바꿔도_같은_인원과_순위로_평가한다()
    {
        var a = 기사("a");
        var b = 기사("b");
        var 처음 = _policy.평가(수요(1, [a, b, a]));
        var 뒤집기 = _policy.평가(수요(1, [b, a]));

        Assert.Equal(2, 처음.기사판정목록.Count);
        Assert.Equal(처음.기사판정목록.Select(x => (x.기사Id, x.신규제안허용, x.우선순위)),
            뒤집기.기사판정목록.Select(x => (x.기사Id, x.신규제안허용, x.우선순위)));
    }

    [Fact]
    public void 같은시각_중복관측의_적격충돌은_더_제한적인_조건을_유지한다()
    {
        var 적격 = 기사("same");
        var 부적격 = 기사("same");
        부적격.적격여부 = false;
        var 결과 = _policy.평가(수요(1, [적격, 부적격]));

        Assert.False(Assert.Single(결과.기사판정목록).신규제안허용);
    }

    [Fact]
    public void 시간당목표_변경은_수요참여만_바꾸고_입력기사_업무를_수정하지_않는다()
    {
        var 기존 = 기사("existing", true);
        기존.최근시간창배차수 = 2;
        기존.남은수행시간분 = 40;
        var 입력 = 수요(5, [기존, 기사("new")]);
        var 낮음 = _policy.평가(입력, new() { 시간당목표처리건수 = 3m });
        var 높음 = _policy.평가(입력, new() { 시간당목표처리건수 = 4m });

        Assert.True(낮음.목표활동인원 > 높음.목표활동인원);
        Assert.Equal(40m, 기존.남은수행시간분);
        Assert.Equal(2, 기존.최근시간창배차수);
        Assert.True(기존.기존활동여부);
    }

    [Fact]
    public void 목표처리량이_0이면_설정_오류를_명시한다()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _policy.평가(수요(1, []), new() { 시간당목표처리건수 = 0m }));

    private static 음식배달수요참여입력 수요(int 대기, IEnumerable<음식배달수요기사입력> 기사목록)
        => new()
        {
            권역Key = "food-area-1", 판정시각Utc = 현재, 수요관측시각Utc = 현재,
            대기주문수 = 대기, 최근시간창신규주문수 = 0, 관측시간창분 = 15,
            기사목록 = 기사목록.ToArray()
        };

    private static 음식배달수요기사입력 기사(string id, bool 기존 = false)
        => new()
        {
            기사Id = id, 기존활동여부 = 기존, 적격여부 = true,
            운행시작시각Utc = 현재.AddMinutes(-10), 대기시작시각Utc = 현재.AddMinutes(-10),
            마지막관측시각Utc = 현재, 남은수행시간분 = 0
        };
}
