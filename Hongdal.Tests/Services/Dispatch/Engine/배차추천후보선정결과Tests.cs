using Hongdal.Contracts.Common.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Reflection;
using 홍달.Services.Dispatch.Engine;
using 홍달.Services.Dispatch.Queue;
using 홍달.도메인.공통;
using 홍달.도메인.배차;

namespace Hongdal.Tests.Services.Dispatch.Engine;

public sealed class 배차추천후보선정결과Tests
{
    [Fact]
    public void 적격후보없음만_공개배차전환을_허용한다()
    {
        var noCandidate = 배차추천후보선정결과.적격후보없음("후보 없음");
        var blockedResults = new[]
        {
            배차추천후보선정결과.선정됨(new 배차추천후보("DRIVER-1", 100m, "선정")),
            배차추천후보선정결과.준비안됨("준비 중"),
            배차추천후보선정결과.잘못된입력("입력 오류"),
            배차추천후보선정결과.구성오류("구성 오류")
        };

        Assert.True(noCandidate.공개배차전환허용);
        Assert.All(blockedResults, result => Assert.False(result.공개배차전환허용));
    }

    [Fact]
    public async Task 원장전환서비스는_준비안됨을_공개배차로_전환하지_않는다()
        => await Assert원장전환차단Async(
            배차추천후보선정결과.준비안됨("선행 작업 대기"),
            배차대기원장전환결과코드.추천준비안됨);

    [Fact]
    public async Task 원장전환서비스는_잘못된입력을_공개배차로_전환하지_않는다()
        => await Assert원장전환차단Async(
            배차추천후보선정결과.잘못된입력("입력 오류"),
            배차대기원장전환결과코드.후보선정입력오류);

    [Fact]
    public async Task 원장전환서비스는_구성오류를_공개배차로_전환하지_않는다()
        => await Assert원장전환차단Async(
            배차추천후보선정결과.구성오류("구성 오류"),
            배차대기원장전환결과코드.배차구성오류);

    [Fact]
    public async Task 정책이_후보를_반환하면_선정됨으로_구분한다()
    {
        var candidate = new 배차추천후보("DRIVER-1", 91m, "테스트 후보");
        var engine = new TestDispatchEngine([new StubPolicy(100, candidate)]);

        var result = await engine.다음후보선정Async(CreateQueue());

        Assert.Equal(배차추천후보선정상태.선정됨, result.상태값);
        Assert.Same(candidate, result.후보);
        Assert.False(result.공개배차전환허용);
    }

    [Fact]
    public async Task 정책이_후보를_반환하지_않으면_적격후보없음으로_구분한다()
    {
        var engine = new TestDispatchEngine([new StubPolicy(100, null)]);

        var result = await engine.다음후보선정Async(CreateQueue());

        Assert.Equal(배차추천후보선정상태.적격후보없음, result.상태값);
        Assert.Null(result.후보);
        Assert.True(result.공개배차전환허용);
    }

    [Fact]
    public async Task 필요한_정책이_없으면_구성오류로_구분한다()
    {
        var engine = new TestDispatchEngine([]);

        var result = await engine.다음후보선정Async(CreateQueue());

        Assert.Equal(배차추천후보선정상태.구성오류, result.상태값);
        Assert.False(result.공개배차전환허용);
    }

    [Fact]
    public async Task 픽업좌표가_없으면_준비안됨으로_구분한다()
    {
        var engine = new TestDispatchEngine([new StubPolicy(100, null)]);
        var queue = CreateQueue();
        queue.픽업_위도 = null;

        var result = await engine.다음후보선정Async(queue);

        Assert.Equal(배차추천후보선정상태.준비안됨, result.상태값);
        Assert.False(result.공개배차전환허용);
    }

    [Fact]
    public void 같은_업무유형의_정책이_중복되면_엔진생성을_거부한다()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new TestDispatchEngine(
            [
                new StubPolicy(100, null),
                new StubPolicy(100, null)
            ]));

        Assert.Contains("중복 등록", exception.Message);
    }

    [Fact]
    public void 같은_업무유형의_엔진이_중복되면_후보선정서비스생성을_거부한다()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new 배차추천후보선정Service(
                null!,
                [new StubDispatchEngine(100), new StubDispatchEngine(100)]));

        Assert.Contains("중복 등록", exception.Message);
    }

    [Fact]
    public async Task 음식배달_선행작업이_끝나지_않으면_준비안됨으로_구분한다()
    {
        var engine = new 음식배달배차엔진(
            [],
            new 운송의뢰배차원천분류Service(),
            new 음식배달배차흐름Resolver(),
            NullLogger<음식배달배차엔진>.Instance);
        var queue = CreateQueue(상태값.배차업무유형.음식배달);
        queue.원본의뢰유형 = 운송의뢰배차원천유형.홍달마트주문;

        var result = await engine.다음후보선정Async(queue);

        Assert.Equal(배차추천후보선정상태.준비안됨, result.상태값);
        Assert.Contains("포장 완료 전", result.사유);
        Assert.False(result.공개배차전환허용);
    }

    [Fact]
    public void 음식배달엔진은_논리패밀리와_구현코드를_각각_노출한다()
    {
        I운송의뢰배차엔진 engine = new 음식배달배차엔진(
            [],
            new 운송의뢰배차원천분류Service(),
            new 음식배달배차흐름Resolver(),
            NullLogger<음식배달배차엔진>.Instance);

        Assert.Equal(EngineFamilyIds.TransportRequestDispatch, engine.논리엔진코드);
        Assert.Equal(EngineImplementationIds.FoodDeliveryDispatch, engine.엔진코드);
        Assert.True(EngineImplementationCatalog.TryGetFamilyId(engine.엔진코드, out var familyId));
        Assert.Equal(engine.논리엔진코드, familyId);
    }

    private static 운송원장 CreateQueue(int dispatchType = 100)
        => new()
        {
            의뢰Id = "REQUEST-1",
            배차업무유형 = dispatchType,
            픽업_위도 = 37.5m,
            픽업_경도 = 127m
        };

    private static async Task Assert원장전환차단Async(
        배차추천후보선정결과 selection,
        string expectedCode)
    {
        var service = new 배차대기원장전환Service(
            null!,
            Options.Create(new 배차큐정책Options { 최대추천라운드 = 5 }),
            new StubCandidateSelectionService(selection),
            null!,
            null!,
            null!);
        var queue = CreateQueue();
        queue.상태 = 상태값.배차대기상태.대기;
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;

        var method = typeof(배차대기원장전환Service).GetMethod(
            "추천거절후다음후보로진행Async",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task<배차대기원장전환결과>>(
            method!.Invoke(service, [queue, null, CancellationToken.None]));
        var result = await task;

        Assert.False(result.전환여부);
        Assert.Equal(expectedCode, result.결과코드);
        Assert.Equal(상태값.배차큐단계.배차추천, queue.배차큐단계);
        Assert.NotEqual(상태값.배차노출상태.공개중, queue.배차노출상태);
    }

    private sealed class TestDispatchEngine(IEnumerable<I배차업무정책> policies) : 정책기반배차엔진(policies)
    {
        public override string 엔진코드 => "TestDispatchEngine";

        public override string 표시명 => "테스트 배차 엔진";

        public override int 배차업무유형 => 100;
    }

    private sealed class StubPolicy(int dispatchType, 배차추천후보? candidate) : I배차업무정책
    {
        public int 배차업무유형 => dispatchType;

        public Task<배차추천후보?> 다음후보선정Async(
            운송원장 queue,
            string? 제외기사Id = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(candidate);
    }

    private sealed class StubDispatchEngine(int dispatchType) : I운송의뢰배차엔진
    {
        public string 논리엔진코드 => EngineFamilyIds.TransportRequestDispatch;

        public string 엔진코드 => $"Stub-{dispatchType}";

        public string 표시명 => "테스트 엔진";

        public int 배차업무유형 => dispatchType;

        public Task<배차추천후보선정결과> 다음후보선정Async(
            운송원장 queue,
            string? 제외기사Id = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(배차추천후보선정결과.적격후보없음("후보 없음"));
    }

    private sealed class StubCandidateSelectionService(배차추천후보선정결과 result)
        : I배차추천후보선정Service
    {
        public Task<배차추천후보선정결과> 다음후보선정Async(
            string requestId,
            string? 제외기사Id = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }
}
