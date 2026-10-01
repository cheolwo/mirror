using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.Food;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Application.WorkflowRules;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
    "운영 서버 계약·정책과 Simulation 공통 업무 규칙의 상태·전이·제외 효과 차이를 검사한다.",
    Boundary = "순수 규칙 비교이며 운영 DB 호출, 실제 배차, Unity 화면 증거가 아니다.")]
public sealed class 업무흐름규칙ParityTests
{
    [Fact]
    public void Catalog는_다섯_업무와_규칙별개정_운영효과제외경계를_보존한다()
    {
        var rules = 업무흐름규칙Catalog.전체조회();

        Assert.Equal(
            [업무흐름코드.음식배달, 업무흐름코드.화물운송, 업무흐름코드.같이주문, 업무흐름코드.개별주문, 업무흐름코드.창고입고],
            rules.Select(x => x.업무흐름코드).OrderBy(x => x, StringComparer.Ordinal));
        Assert.All(rules, rule =>
        {
            Assert.NotEmpty(rule.SourceCapabilityKey);
            Assert.NotEmpty(rule.SourceContractRevision);
            Assert.False(string.IsNullOrWhiteSpace(rule.RuleRevision));
            Assert.NotEmpty(rule.SourceStableIds);
            Assert.NotEmpty(rule.Simulation제외운영효과코드목록);
        });
        Assert.All(
            rules.Where(rule => rule.업무흐름코드 is not (업무흐름코드.음식배달 or 업무흐름코드.창고입고)),
            rule => Assert.Equal("workflow-rules.v1", rule.RuleRevision));
        Assert.Equal(
            "food-delivery.v5",
            rules.Single(rule => rule.업무흐름코드 == 업무흐름코드.음식배달).RuleRevision);
        Assert.Equal(
            "warehouse-inbound.v1",
            rules.Single(rule => rule.업무흐름코드 == 업무흐름코드.창고입고).RuleRevision);
    }

    [Fact]
    public void 창고입고는_수령_검수_적재를_분리하고_운영상태를_정규화한다()
    {
        var rule = 업무흐름규칙Catalog.조회(업무흐름코드.창고입고);

        Assert.Equal(
            [
                창고입고상태코드.입고예정,
                창고입고상태코드.검수대기,
                창고입고상태코드.적재대기,
                창고입고상태코드.적재완료,
            ],
            rule.상태코드목록);
        Assert.True(업무상태전이Policy.판정(
            업무흐름코드.창고입고,
            창고입고상태코드.입고예정,
            창고입고상태코드.검수대기).허용여부);
        Assert.False(업무상태전이Policy.판정(
            업무흐름코드.창고입고,
            창고입고상태코드.검수대기,
            창고입고상태코드.적재완료).허용여부);
        Assert.Equal(창고입고상태코드.검수대기, 창고입고업무상태Adapter.정규화("보관중"));
        Assert.Equal(창고입고상태코드.적재대기, 창고입고업무상태Adapter.정규화("검수완료-불량포함"));
        Assert.Equal(창고입고상태코드.적재완료, 창고입고업무상태Adapter.정규화("적재완료"));
    }

    [Fact]
    public void 음식배달_상태코드는_현재_운영Contract와_일치한다()
    {
        var rule = 업무흐름규칙Catalog.조회(업무흐름코드.음식배달);

        Assert.Equal(음식주문상태코드.전체, rule.상태코드목록);
        Assert.True(업무상태전이Policy.판정(
            업무흐름코드.음식배달,
            음식주문상태코드.조리중,
            음식주문상태코드.픽업대기).허용여부);
        Assert.True(업무상태전이Policy.판정(
            업무흐름코드.음식배달,
            음식주문상태코드.조리중,
            음식주문상태코드.기사배정).허용여부);
        Assert.True(업무상태전이Policy.판정(
            업무흐름코드.음식배달,
            음식주문상태코드.주문대기,
            음식주문상태코드.취소).허용여부);
        Assert.False(업무상태전이Policy.판정(
            업무흐름코드.음식배달,
            음식주문상태코드.수령확인,
            음식주문상태코드.조리중).허용여부);
    }

    [Fact]
    public void 운영서버_음식배달Guard는_공통규칙판정과_모든상태쌍에서_같다()
    {
        foreach (var current in 음식주문상태코드.전체)
        {
            foreach (var target in 음식주문상태코드.전체)
            {
                var common = 업무상태전이Policy.판정(업무흐름코드.음식배달, current, target);
                var server = 음식배달업무상태전이Guard.판정(current, target);

                Assert.Equal(common.허용여부, server.허용여부);
                Assert.Equal(common.멱등재시도여부, server.멱등재시도여부);
                Assert.Equal(common.RuleRevision, server.RuleRevision);
                Assert.Equal(common.차단사유코드목록, server.차단사유코드목록);
                Assert.Equal(common.SourceStableIds, server.SourceStableIds);
            }
        }
    }

    [Fact]
    public void 운영서버_음식배달Guard는_주문대기에서_기사배정으로_건너뛰지않는다()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            음식배달업무상태전이Guard.허용확인(
                음식주문상태코드.주문대기,
                음식주문상태코드.기사배정));

        Assert.Contains("food-delivery.v5", error.Message, StringComparison.Ordinal);
        Assert.Contains(업무규칙차단사유코드.허용되지않은상태전이, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 음식점_거절과픽업준비는_운영Policy와_공통전이에서_같은의미를낸다()
    {
        var rule = 업무흐름규칙Catalog.조회(업무흐름코드.음식배달);
        var reject = 음식점주문진행Policy.판정(
            음식주문상태코드.주문대기,
            new 음식점주문진행변경요청
            {
                작업 = 음식점주문진행작업코드.거절,
                사유 = "비교 시험",
            });
        var pickupReady = 음식점주문진행Policy.판정(
            음식주문상태코드.조리중,
            new 음식점주문진행변경요청
            {
                작업 = 음식점주문진행작업코드.픽업준비,
            });

        Assert.Equal(음식주문상태코드.거절, reject.다음상태);
        Assert.Equal(음식주문상태코드.픽업대기, pickupReady.다음상태);
        Assert.Contains(rule.허용전이목록, transition =>
            transition.현재상태코드 == 음식주문상태코드.주문대기
            && transition.목표상태코드 == reject.다음상태);
        Assert.Contains(rule.허용전이목록, transition =>
            transition.현재상태코드 == 음식주문상태코드.조리중
            && transition.목표상태코드 == pickupReady.다음상태);
    }

    [Fact]
    public void 화물운송_상태전이는_현재_운영Policy와_일치한다()
    {
        var rule = 업무흐름규칙Catalog.조회(업무흐름코드.화물운송);

        foreach (var current in rule.상태코드목록)
        {
            foreach (var target in rule.상태코드목록)
            {
                var expected = 기사운송상태전이Policy.가능한가(current, target);
                var actual = 업무상태전이Policy.판정(
                    업무흐름코드.화물운송,
                    current,
                    target).허용여부;
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void Simulation에_이관하지않는_운영효과는_업무별로_명시되어있다()
    {
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [업무흐름코드.음식배달] =
                ["OperationalOrderWrite", "RealDriverDispatch", "PersonalAddress", "RealTimeNotification"],
            [업무흐름코드.화물운송] =
                ["RealDriverAssignment", "GpsLocationWrite", "OperationalFreightSettlement", "CarrierNotification"],
            [업무흐름코드.창고입고] =
                ["OperationalInventoryWrite", "OperationalAuditLog", "OperationalWarehouseEvent", "OperationalEmployeeAuthorization"],
        };

        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value,
                업무흐름규칙Catalog.조회(pair.Key).Simulation제외운영효과코드목록);
        }
    }

    [Fact]
    public void 같이주문_상태코드는_현재_집단화Contract와_일치한다()
    {
        var rule = 업무흐름규칙Catalog.조회(업무흐름코드.같이주문);

        Assert.Contains(공동구매자동집단상태코드.수요수집중, rule.상태코드목록);
        Assert.Contains(공동구매자동집단상태코드.확정대기, rule.상태코드목록);
        Assert.Contains(공동구매자동집단상태코드.확정, rule.상태코드목록);
        Assert.Contains(공동구매자동집단상태코드.모집종료목표미달, rule.상태코드목록);
    }

    [Theory]
    [InlineData(1, 20, 3, 60)]
    [InlineData(2, 30, 3, 60)]
    [InlineData(3, 60, 3, 60)]
    public void 같이주문_목표상태판정은_예약결제없는_운영계획과_일치한다(
        int 참여자수,
        int 총희망수량,
        int 목표참여자수,
        int 목표수량)
    {
        var expected = 공동구매자동집단화계획기.상태제안(
            참여자수,
            0,
            총희망수량,
            목표참여자수,
            목표수량);

        var actual = 같이주문상태Policy.판정(new 같이주문상태판정요청
        {
            참여자수 = 참여자수,
            총희망수량 = 총희망수량,
            목표참여자수 = 목표참여자수,
            목표수량 = 목표수량,
        });

        Assert.Equal(expected, actual.제안상태코드);
        Assert.Equal(
            expected == 같이주문상태코드.확정대기
                ? 같이주문상태코드.확정
                : 같이주문상태코드.모집종료목표미달,
            actual.모집종료결과상태코드);
    }

    [Fact]
    public void 개별주문_상태코드는_현재_커뮤니티원장Contract와_일치한다()
    {
        var rule = 업무흐름규칙Catalog.조회(업무흐름코드.개별주문);

        Assert.Equal(
            [커뮤니티원장상태.초안, 커뮤니티원장상태.진행중, 커뮤니티원장상태.완료],
            rule.상태코드목록);
    }

    [Fact]
    public void 같은상태_재시도는_멱등으로_허용한다()
    {
        var result = 업무상태전이Policy.판정(
            업무흐름코드.개별주문,
            개별주문상태코드.진행중,
            개별주문상태코드.진행중);

        Assert.True(result.허용여부);
        Assert.True(result.멱등재시도여부);
    }

    [Fact]
    public void 수량은_결과와_명시적손실의_합으로_보존한다()
    {
        var conserved = 업무수량보존Policy.판정(new 업무수량보존요청
        {
            입력수량 = 300m,
            결과수량 = 288m,
            손실수량 = 12m,
            단위코드 = "kg",
        });
        var broken = 업무수량보존Policy.판정(new 업무수량보존요청
        {
            입력수량 = 300m,
            결과수량 = 280m,
            손실수량 = 12m,
            단위코드 = "kg",
        });

        Assert.True(conserved.보존여부);
        Assert.Empty(conserved.차단사유코드목록);
        Assert.False(broken.보존여부);
        Assert.Equal(8m, broken.차이수량);
        Assert.Equal([업무규칙차단사유코드.수량불일치], broken.차단사유코드목록);
    }
}
