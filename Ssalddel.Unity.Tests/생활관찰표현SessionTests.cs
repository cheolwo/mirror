using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests;

[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E3,
    "음식 사본의 복제·집계·만료와 세 관찰 단계의 안정 대기·상세 해제 정책을 검증한다.",
    Boundary = "가짜 시계 순수 C# 시험이며 실제 Unity 객체, Game View, 서버 업무 완료 증거가 아니다.")]
public sealed class 생활관찰표현SessionTests
{
    private const string 지역 = "region:kr:bjd:1126010100";
    private static readonly DateTime 기준시각 = new(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void 최신요청만_안정뒤적용하고_같은대기요청은_안정시간을초기화하지않는다()
    {
        var session = new 생활관찰표현Session(지역);
        Assert.Equal(생활관찰단계.Neighborhood, session.CurrentStage);
        Assert.True(session.Request(생활관찰단계.Street, 0));
        Assert.True(session.Request(생활관찰단계.District, .25));
        Assert.True(session.Request(생활관찰단계.District, .6));
        Assert.Equal(2, session.RequestGeneration);
        Assert.True(session.Advance(.74, 기준시각));
        Assert.Equal(생활관찰단계.Neighborhood, session.CurrentStage);
        Assert.True(session.Advance(.75, 기준시각));
        Assert.Equal(생활관찰단계.District, session.CurrentStage);
        Assert.Equal(session.RequestedStage, session.CurrentStage);
        Assert.False(session.DetailVisible);
        Assert.False(session.DetailRetained);
    }

    [Fact]
    public void 대기중현재단계로돌아오면_이전거리요청을적용하지않는다()
    {
        var session = new 생활관찰표현Session(지역);
        session.Request(생활관찰단계.Street, 0);
        session.Request(생활관찰단계.Neighborhood, .25);
        session.Advance(1, 기준시각);
        Assert.Equal(2, session.RequestGeneration);
        Assert.Equal(생활관찰단계.Neighborhood, session.CurrentStage);
        Assert.False(session.DetailRetained);
    }

    [Fact]
    public void 거리이탈은즉시비표시하고_5초후해제하며_사본은유지한다()
    {
        var session = 거리Session();
        session.Request(생활관찰단계.Neighborhood, 1);
        session.Advance(1.5, 기준시각);
        Assert.False(session.DetailVisible);
        Assert.True(session.DetailRetained);
        session.Advance(6.49, 기준시각);
        Assert.True(session.DetailRetained);
        session.Advance(6.5, 기준시각);
        Assert.False(session.DetailRetained);
        Assert.Single(session.CurrentItems);
        Assert.Equal(1, session.WorkCount);
    }

    [Fact]
    public void 유예중거리복귀는해제를취소하고_다시이탈하면새유예를시작한다()
    {
        var session = 거리Session();
        session.Request(생활관찰단계.District, 1);
        session.Advance(1.5, 기준시각);
        session.Request(생활관찰단계.Street, 6);
        session.Advance(6.5, 기준시각);
        Assert.True(session.DetailRetained);
        Assert.True(session.DetailVisible);
        session.Request(생활관찰단계.Neighborhood, 7);
        session.Advance(7.5, 기준시각);
        session.Advance(11, 기준시각);
        Assert.True(session.DetailRetained);
        session.Advance(12.5, 기준시각);
        Assert.False(session.DetailRetained);
    }

    [Fact]
    public void 거리해제중최신판본을받고_복귀하면같은최신사본으로읽는다()
    {
        var session = 거리Session();
        session.Request(생활관찰단계.District, 1);
        session.Advance(6.5, 기준시각);
        session.Advance(11.5, 기준시각);
        Assert.False(session.DetailRetained);
        var 최신 = 사본(revision: 2);
        최신.ActivityCode = 최신.LifecycleStageCode = "픽업완료";
        Assert.True(session.Apply(계획(최신), 기준시각));
        session.Request(생활관찰단계.Street, 12);
        session.Advance(12.5, 기준시각);
        Assert.True(session.DetailVisible);
        Assert.Equal(2, Assert.Single(session.CurrentItems).Revision);
        Assert.Equal("픽업완료", Assert.Single(session.CurrentItems).LifecycleStageCode);
    }

    [Fact]
    public void 구와동을오가는동안_거리해제유예를연장하지않는다()
    {
        var session = 거리Session();
        session.Request(생활관찰단계.Neighborhood, 1);
        session.Advance(1.5, 기준시각);
        session.Request(생활관찰단계.District, 5);
        session.Advance(5.5, 기준시각);
        session.Advance(6.5, 기준시각);
        Assert.False(session.DetailRetained);
    }

    [Fact]
    public void 만료경계와해제후거리복귀에서_과거사본이되살아나지않는다()
    {
        var session = 거리Session();
        session.Expire(기준시각.AddMinutes(1));
        Assert.Empty(session.CurrentItems);
        session.Request(생활관찰단계.District, 1);
        session.Advance(1.5, 기준시각.AddMinutes(1));
        session.Request(생활관찰단계.Street, 10);
        session.Advance(10.5, 기준시각.AddMinutes(1));
        Assert.Empty(session.CurrentItems);
        Assert.True(session.DetailVisible);
        Assert.Equal(0, session.WorkCount);
    }

    [Fact]
    public void 만료입력은제외하고_Advance도만료항목을제거한다()
    {
        var session = new 생활관찰표현Session(지역);
        var expired = 사본("expired");
        expired.ExpiresAtUtc = 기준시각;
        Assert.True(session.Apply(계획(expired, 사본()), 기준시각));
        Assert.Single(session.CurrentItems);
        session.Advance(0, 기준시각.AddMinutes(1));
        Assert.Empty(session.CurrentItems);
    }

    [Fact]
    public void Clear는사본과표현과요청시계를즉시지우고기본단계로돌린다()
    {
        var session = 거리Session();
        session.Request(생활관찰단계.District, 500);
        session.Expire(기준시각.AddMinutes(2));
        session.Clear();
        Assert.Empty(session.CurrentItems);
        Assert.Equal(0, session.WorkCount);
        Assert.Equal(0, session.RequestGeneration);
        Assert.Equal(생활관찰단계.Neighborhood, session.CurrentStage);
        Assert.Equal(생활관찰단계.Neighborhood, session.RequestedStage);
        Assert.False(session.DetailVisible);
        Assert.False(session.DetailRetained);
        Assert.Empty(session.DiagnosticCode);
        Assert.True(session.Request(생활관찰단계.Street, 0));
        Assert.True(session.Apply(계획(사본()), 기준시각));
    }

    [Fact]
    public void 입력과반환사본수정은내부상태나이전반환값에영향을주지않는다()
    {
        var session = new 생활관찰표현Session(지역);
        var input = 사본();
        var plan = 계획(input);
        session.Apply(plan, 기준시각);
        var original = Assert.Single(session.CurrentItems);
        input.Revision = 900;
        input.WorkStableId = "mutated-input";
        plan.Instructions = [];
        var returned = Assert.Single(session.CurrentItems);
        returned.Revision = 999;
        returned.WorkStableId = "mutated-output";
        var current = Assert.Single(session.CurrentItems);
        Assert.Equal(1, current.Revision);
        Assert.Equal("work:1", current.WorkStableId);
        Assert.Equal(1, original.Revision);
        Assert.Equal(input.ObjectStableId, current.ObjectStableId);
        Assert.Equal(input.AreaStableId, current.AreaStableId);
        Assert.Equal(input.OperatingSystemId, current.OperatingSystemId);
        Assert.Equal(input.AnchorKey, current.AnchorKey);
        Assert.Equal(input.VisualKey, current.VisualKey);
        Assert.Equal(input.ActivityCode, current.ActivityCode);
        Assert.Equal(input.LifecycleStageCode, current.LifecycleStageCode);
        Assert.Equal(input.AttentionStateCode, current.AttentionStateCode);
        Assert.Equal(input.ObjectKindCode, current.ObjectKindCode);
        Assert.Equal(input.SemanticPlaceStableId, current.SemanticPlaceStableId);
        Assert.Equal(input.SourceKindCode, current.SourceKindCode);
        Assert.Equal(input.ScenarioRunStableId, current.ScenarioRunStableId);
        Assert.Equal(input.ExpiresAtUtc, current.ExpiresAtUtc);
    }

    [Theory]
    [InlineData("rejected", "ApplyResultRejected")]
    [InlineData("missing-id", "ObjectStableIdRequired")]
    [InlineData("missing-work", "WorkStableIdRequired")]
    [InlineData("unknown-source", "SourceKindUnsupported")]
    [InlineData("missing-run", "ScenarioRunStableIdRequired")]
    [InlineData("negative-revision", "RevisionInvalid")]
    [InlineData("invalid-expiry", "ExpiresAtUtcRequired")]
    [InlineData("duplicate", "DuplicateObjectStableId")]
    [InlineData("null-item", "PlacementInstructionRequired")]
    [InlineData("null-instructions", "PlacementInstructionsRequired")]
    public void 잘못된전체계획은진단을남기고이전사본을보존한다(string mutation, string diagnostic)
    {
        var session = new 생활관찰표현Session(지역);
        session.Apply(계획(사본()), 기준시각);
        var incoming = 사본("incoming");
        var plan = 계획(incoming);
        switch (mutation)
        {
            case "rejected": plan.Accepted = false; break;
            case "missing-id": incoming.ObjectStableId = " "; break;
            case "missing-work": incoming.WorkStableId = " "; break;
            case "unknown-source": incoming.SourceKindCode = "Statistics"; break;
            case "missing-run": incoming.ScenarioRunStableId = ""; break;
            case "negative-revision": incoming.Revision = -1; break;
            case "invalid-expiry": incoming.ExpiresAtUtc = DateTime.SpecifyKind(incoming.ExpiresAtUtc, DateTimeKind.Unspecified); break;
            case "duplicate": plan.Instructions = [incoming, incoming]; break;
            case "null-item": plan.Instructions = [null!]; break;
            case "null-instructions": plan.Instructions = null!; break;
        }
        Assert.False(session.Apply(plan, 기준시각));
        Assert.Equal(diagnostic, session.DiagnosticCode);
        Assert.Equal("object:1", Assert.Single(session.CurrentItems).ObjectStableId);
        // 매 프레임 Advance가 자료 실패 진단을 성공으로 지우지 않습니다.
        session.Advance(0, 기준시각);
        Assert.Equal(diagnostic, session.DiagnosticCode);
    }

    [Fact]
    public void 낮은판본은현재사본을보존하지만만료를연장하거나누락ID를유지하지않는다()
    {
        var session = new 생활관찰표현Session(지역);
        session.Apply(계획(사본(revision: 3), 사본("missing")), 기준시각);
        var stale = 사본(revision: 2);
        stale.ExpiresAtUtc = 기준시각.AddHours(1);
        Assert.True(session.Apply(계획(stale), 기준시각.AddSeconds(1)));
        Assert.Equal("LowerRevision", session.DiagnosticCode);
        Assert.Equal(3, Assert.Single(session.CurrentItems).Revision);
        Assert.Equal(기준시각.AddMinutes(1), Assert.Single(session.CurrentItems).ExpiresAtUtc);
        Assert.True(session.Apply(계획(stale), 기준시각.AddMinutes(1)));
        Assert.Empty(session.CurrentItems);
    }

    [Fact]
    public void 동일판본내용충돌은거부하고_정상재조회는표시임대만갱신한다()
    {
        var session = new 생활관찰표현Session(지역);
        session.Apply(계획(사본()), 기준시각);
        var conflict = 사본();
        conflict.ActivityCode = "전달완료";
        Assert.False(session.Apply(계획(conflict), 기준시각));
        Assert.Equal("RevisionConflict", session.DiagnosticCode);
        var refresh = 사본();
        refresh.ExpiresAtUtc = 기준시각.AddMinutes(2);
        Assert.True(session.Apply(계획(refresh), 기준시각));
        Assert.Equal(기준시각.AddMinutes(2), Assert.Single(session.CurrentItems).ExpiresAtUtc);
    }

    [Fact]
    public void 전체계획에서누락된ID는삭제하고_단계왕복으로되살리지않는다()
    {
        var session = 거리Session();
        session.Apply(계획(), 기준시각);
        session.Request(생활관찰단계.District, 1);
        session.Advance(1.5, 기준시각);
        session.Request(생활관찰단계.Street, 2);
        session.Advance(2.5, 기준시각);
        Assert.Empty(session.CurrentItems);
    }

    [Fact]
    public void 삭제후새입력판본의권위는상위해석기가소유하고_세션은ID이력을축적하지않는다()
    {
        var session = new 생활관찰표현Session(지역, maxObjects: 1);
        for (var index = 0; index < 500; index++)
        {
            Assert.True(session.Apply(계획(사본("object:" + index, revision: 10)), 기준시각));
            Assert.Single(session.CurrentItems);
        }
        Assert.True(session.Apply(계획(), 기준시각));
        // 삭제 ID의 응답 권위 검사는 이 정책 밖의 Interpreter 책임이며 별도 고수위 캐시는 없습니다.
        Assert.True(session.Apply(계획(사본("object:0", revision: 1)), 기준시각));
        Assert.Equal(1, Assert.Single(session.CurrentItems).Revision);
    }

    [Fact]
    public void 정확한지역과FoodDelivery만수용하며_다른세션과분리한다()
    {
        var one = new 생활관찰표현Session(지역, maxObjects: 1);
        var two = new 생활관찰표현Session("region:other");
        var otherArea = 사본("other-area");
        otherArea.AreaStableId = "region:other";
        var otherOs = 사본("other-os");
        otherOs.OperatingSystemId = OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment;
        var plan = 계획(사본(), otherArea, otherOs);
        Assert.True(one.Apply(plan, 기준시각));
        Assert.True(two.Apply(plan, 기준시각));
        Assert.Equal("object:1", Assert.Single(one.CurrentItems).ObjectStableId);
        Assert.Equal("other-area", Assert.Single(two.CurrentItems).ObjectStableId);
        one.Clear();
        Assert.Single(two.CurrentItems);
    }

    [Fact]
    public void 동일업무복수객체는한번만세고_출처와실행이다르면별개로센다()
    {
        var session = new 생활관찰표현Session(지역);
        var first = 사본("actor");
        var second = 사본("parcel");
        var differentRun = 사본("another-run");
        differentRun.ScenarioRunStableId = "scenario:2";
        var operational = 사본("operational");
        operational.SourceKindCode = OperationalWorldSceneSourceKinds.OperationalProjection;
        Assert.True(session.Apply(계획(first, second, differentRun, operational), 기준시각));
        Assert.Equal(4, session.CurrentItems.Count);
        Assert.Equal(3, session.WorkCount);
    }

    [Fact]
    public void 입력예산초과는부분적용하지않고_만료중복ID도거부한다()
    {
        var session = new 생활관찰표현Session(지역, maxObjects: 2);
        session.Apply(계획(사본()), 기준시각);
        Assert.False(session.Apply(계획(사본("a"), 사본("b"), 사본("c")), 기준시각));
        Assert.Equal("ObjectBudgetExceeded", session.DiagnosticCode);
        Assert.Equal("object:1", Assert.Single(session.CurrentItems).ObjectStableId);
        var expired = 사본("expired");
        expired.ExpiresAtUtc = 기준시각;
        Assert.False(session.Apply(계획(expired, expired), 기준시각));
        Assert.Equal("DuplicateObjectStableId", session.DiagnosticCode);
        Assert.Single(session.CurrentItems);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void 유효하지않은단조시계는상태와요청을변경하지않는다(double invalid)
    {
        var session = 거리Session();
        Assert.False(session.Request(생활관찰단계.District, invalid));
        Assert.Equal("MonotonicClockInvalid", session.DiagnosticCode);
        Assert.False(session.Advance(invalid, 기준시각.AddMinutes(1)));
        Assert.Equal(생활관찰단계.Street, session.CurrentStage);
        Assert.Equal(생활관찰단계.Street, session.RequestedStage);
        Assert.Single(session.CurrentItems);
        Assert.True(session.Advance(.5, 기준시각));
    }

    [Fact]
    public void 역행한단조시계와UTC시계는각자거부하며정상시각으로회복한다()
    {
        var session = 거리Session();
        Assert.False(session.Request(생활관찰단계.District, .4));
        Assert.Equal("MonotonicClockReversed", session.DiagnosticCode);
        Assert.False(session.Advance(.6, 기준시각.AddSeconds(-1)));
        Assert.Equal("UtcClockReversed", session.DiagnosticCode);
        Assert.True(session.Request(생활관찰단계.District, .5));
        Assert.False(session.Apply(계획(), DateTime.SpecifyKind(기준시각, DateTimeKind.Local)));
        Assert.Equal("UtcClockRequired", session.DiagnosticCode);
        session.Expire(기준시각.AddSeconds(-1));
        Assert.Single(session.CurrentItems);
        Assert.True(session.Advance(1, 기준시각));
        Assert.Equal(생활관찰단계.District, session.CurrentStage);
        Assert.Empty(session.DiagnosticCode);
    }

    [Fact]
    public void 유효하지않은단계와생성설정은거부하고_영초설정은즉시전환해제한다()
    {
        Assert.Throws<ArgumentException>(() => new 생활관찰표현Session(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new 생활관찰표현Session(지역, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new 생활관찰표현Session(지역, releaseSeconds: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new 생활관찰표현Session(지역, maxObjects: 0));
        var session = new 생활관찰표현Session(지역, 0, 0);
        Assert.False(session.Request((생활관찰단계)99, 0));
        Assert.Equal("ObservationStageUnsupported", session.DiagnosticCode);
        Assert.Equal(0, session.RequestGeneration);
        session.Request(생활관찰단계.Street, 0);
        session.Advance(0, 기준시각);
        Assert.True(session.DetailVisible);
        session.Request(생활관찰단계.District, 0);
        session.Advance(0, 기준시각);
        Assert.False(session.DetailRetained);
        Assert.False(session.DetailVisible);
    }

    private static 생활관찰표현Session 거리Session()
    {
        var session = new 생활관찰표현Session(지역);
        session.Apply(계획(사본()), 기준시각);
        session.Request(생활관찰단계.Street, 0);
        session.Advance(.5, 기준시각);
        return session;
    }

    private static OperationalWorldPlacementPlan 계획(params OperationalWorldPlacementInstruction[] items)
        => new() { Accepted = true, Instructions = items };

    private static OperationalWorldPlacementInstruction 사본(string id = "object:1", long revision = 1)
        => new()
        {
            ObjectStableId = id,
            AreaStableId = 지역,
            OperatingSystemId = OperationalWorldOperatingSystemIds.FoodDelivery,
            AnchorKey = "world.area.food-delivery",
            VisualKey = "operational.active-lifecycle",
            ActivityCode = "조리중",
            WorkStableId = "work:1",
            LifecycleStageCode = "조리중",
            AttentionStateCode = OperationalWorldAttentionStateCodes.Active,
            ObjectKindCode = "DeliveryActor",
            SemanticPlaceStableId = "place:1",
            SourceKindCode = OperationalWorldSceneSourceKinds.VerificationSample,
            ScenarioRunStableId = "scenario:1",
            Revision = revision,
            ExpiresAtUtc = 기준시각.AddMinutes(1)
        };
}
