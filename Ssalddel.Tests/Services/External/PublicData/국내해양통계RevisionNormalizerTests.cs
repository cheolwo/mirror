using 살뜰.Services.External.PublicData.Korea;

namespace Ssalddel.Tests.Services.External.PublicData;

public sealed class 국내해양통계RevisionNormalizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private const string Item = "<item><year>2026.06</year><hsCd>030214</hsCd><statCd>NO</statCd><statKor>연어</statKor><impWgt>10</impWgt><impDlr>50</impDlr><expWgt>0</expWgt><expDlr>0</expDlr></item>";
    private static string Xml(string item) => "<response><header><resultCode>00</resultCode></header><body><items>" + item + "</items></body></response>";
    private static IReadOnlyList<Ssalddel.Domain.PublicData.외부데이터정규화Record> Parse(string xml, DateTimeOffset? at = null)
        => 국내해양통계RevisionNormalizer.Parse(xml, "202606", "030214", "NO", at ?? Now);

    [Fact] public void 수출수입중량금액을구분하고합계는제외한다()
    {
        var rows = Parse(Xml(Item + "<item><year>총계</year></item>"));
        Assert.Equal(4, rows.Count);
        Assert.Equal(10, rows.Single(x => x.MetricCode == "impWgt").NumericValue);
        Assert.Equal("USD", rows.Single(x => x.MetricCode == "expDlr").UnitCode);
        Assert.All(rows, x => Assert.Equal("PendingHumanReview", x.QualityCode));
    }
    [Fact] public void 재수집은같은판본이고수정값은새판본이다()
    {
        var original = Parse(Xml(Item));
        var repeat = Parse(Xml(Item), Now.AddDays(1));
        Assert.Equal(original.Select(x => x.RecordKey), repeat.Select(x => x.RecordKey));
        var changed = Parse(Xml(Item.Replace("<impDlr>50", "<impDlr>60")), Now.AddDays(1));
        Assert.Equal(original.Select(x => x.StableId), changed.Select(x => x.StableId));
        Assert.Single(changed, x => !original.Any(y => y.RecordKey == x.RecordKey));
        var latest = 국내해양통계RevisionNormalizer.Latest(original.Concat(changed));
        Assert.Equal(60, latest.Single(x => x.MetricCode == "impDlr").NumericValue);
        Assert.Equal(50, original.Single(x => x.MetricCode == "impDlr").NumericValue);
    }
    [Fact] public void 수치표기차이는새판본을만들지않는다()
        => Assert.Equal(Parse(Xml(Item)).Select(x => x.RecordKey), Parse(Xml(Item.Replace(">50<", ">50.0<"))).Select(x => x.RecordKey));
    [Fact] public void HS6조회에반환된HSK10은원래식별자를보존한다()
    {
        var rows = Parse(Xml(Item.Replace("030214", "0302140000")));
        Assert.Equal(4, rows.Count);
        Assert.All(rows, x => Assert.Contains(":0302140000:", x.StableId));
    }
    [Theory]
    [InlineData("2026.06", "2026.05")]
    [InlineData("030214", "030354")]
    [InlineData("NO", "RU")]
    [InlineData("<impWgt>10", "<impWgt>-1")]
    [InlineData("<impDlr>50", "<impDlr>NaN")]
    public void 다른범위또는비정상값은거절한다(string from, string to)
        => Assert.Throws<InvalidDataException>(() => Parse(Xml(Item.Replace(from, to))));
    [Fact] public void 실패빈응답중복은제로값이나삭제가아니다()
    {
        Assert.Throws<InvalidDataException>(() => Parse(Xml("")));
        Assert.Throws<InvalidDataException>(() => Parse(Xml(Item + Item)));
        Assert.Throws<InvalidDataException>(() => Parse(Xml(Item).Replace("<resultCode>00", "<resultCode>30")));
    }
}
