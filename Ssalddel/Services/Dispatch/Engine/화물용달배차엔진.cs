using Ssalddel.Contracts.Common.Versioning;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.Services.Dispatch.Queue;

namespace 살뜰.Services.Dispatch.Engine;

public sealed class 화물용달배차엔진 : 정책기반배차엔진
{
    private readonly I운송의뢰배차원천분류Service _sourceClassifier;
    private readonly I화물용달배차흐름Resolver _flowResolver;
    private readonly ILogger<화물용달배차엔진> _logger;

    public 화물용달배차엔진(
        IEnumerable<I배차업무정책> policies,
        I운송의뢰배차원천분류Service sourceClassifier,
        I화물용달배차흐름Resolver flowResolver,
        ILogger<화물용달배차엔진> logger)
        : base(policies)
    {
        _sourceClassifier = sourceClassifier;
        _flowResolver = flowResolver;
        _logger = logger;
    }

    public override string 엔진코드 => EngineImplementationIds.CargoYongdalDispatch;

    public override string 운영체제Id => OperatingSystemIds.DomesticCargoTransport;

    public override string 표시명 => "화물/용달 배차 엔진";

    public override int 배차업무유형 => 상태값.배차업무유형.용달운송;

    public override async Task<배차추천후보선정결과> 다음후보선정Async(
        운송원장 queue,
        운송의뢰배차Engine입력Context? context = null,
        string? 제외기사Id = null,
        CancellationToken cancellationToken = default)
    {
        if (queue is null)
        {
            return 배차추천후보선정결과.잘못된입력("배차대기 원장이 제공되지 않았습니다.");
        }

        var source = _sourceClassifier.분류(queue);
        var flow = _flowResolver.Resolve(queue, context?.화물운송방식);

        _logger.LogDebug(
            "화물/용달 운송의뢰 배차 흐름을 확인했습니다. QueueId={QueueId} RequestId={RequestId} SourceType={SourceType} SourceFlow={SourceFlow} Flow={Flow} Unit={Unit}",
            queue.Id,
            queue.의뢰Id,
            queue.원본의뢰유형,
            source.상위흐름,
            flow.표시명,
            flow.운송단위);

        return await base.다음후보선정Async(queue, context, 제외기사Id, cancellationToken);
    }
}
