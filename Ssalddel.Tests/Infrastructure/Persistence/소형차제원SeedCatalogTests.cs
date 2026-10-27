using 살뜰.Data;

namespace Ssalddel.Tests.Infrastructure.Persistence;

public sealed class 소형차제원SeedCatalogTests
{
    [Fact]
    public void 미확인소형차는_치수나중량을꾸며넣지않고_추천을비활성화한다()
    {
        var templates = 소형차제원SeedCatalog.CreateUnverifiedTemplates();

        Assert.Equal(new[] { "오토바이", "다마스" }, templates.Select(x => x.차량코드).ToArray());
        Assert.All(templates, item =>
        {
            Assert.False(item.추천사용여부);
            Assert.Equal(0, item.적재함길이Mm);
            Assert.Equal(0, item.적재함폭Mm);
            Assert.Null(item.적재함높이Mm);
            Assert.Equal(0, item.최대적재중량Kg);
            Assert.Null(item.운영권장중량Kg);
            Assert.Null(item.권장최대CBM);
            Assert.Null(item.팔레트적재개수);
            Assert.Empty(item.제조사);
            Assert.Empty(item.모델명);
            Assert.Contains("실차", item.장점메모);
        });
    }

    [Fact]
    public void 실측설정을적용해도_다음호출의미확인템플릿을바꾸지않는다()
    {
        var first = 소형차제원SeedCatalog.CreateUnverifiedTemplates();
        first[0].적재함길이Mm = 600;
        first[0].적재함폭Mm = 400;
        first[0].적재함높이Mm = 400;
        first[0].최대적재중량Kg = 5;
        first[0].추천사용여부 = true;

        var second = 소형차제원SeedCatalog.CreateUnverifiedTemplates();

        Assert.NotSame(first[0], second[0]);
        Assert.False(second[0].추천사용여부);
        Assert.Equal(0, second[0].적재함길이Mm);
        Assert.Equal(0, second[0].최대적재중량Kg);
        Assert.Null(second[0].적재함높이Mm);
    }
}
