namespace 살뜰.Services.Dispatch.Coordination;

public interface I국내화물배차조율실행Service
{
    Task<(국내화물배차조율입력 Input, 국내화물배차조율결과 Result, 국내화물배차조율적용결과 ApplyResult)> 실행Async(
        국내화물배차조율입력요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class 국내화물배차조율실행Service : I국내화물배차조율실행Service
{
    private readonly I국내화물배차조율입력Factory _inputFactory;
    private readonly I국내화물배차조율Service _coordinationService;
    private readonly I국내화물배차조율적용Service _applyService;
    private readonly I국내화물배차계획검증Service? _planValidation;

    public 국내화물배차조율실행Service(
        I국내화물배차조율입력Factory inputFactory,
        I국내화물배차조율Service coordinationService,
        I국내화물배차조율적용Service applyService,
        I국내화물배차계획검증Service? planValidation = null)
    {
        _inputFactory = inputFactory;
        _coordinationService = coordinationService;
        _applyService = applyService;
        _planValidation = planValidation;
    }

    public async Task<(국내화물배차조율입력 Input, 국내화물배차조율결과 Result, 국내화물배차조율적용결과 ApplyResult)> 실행Async(
        국내화물배차조율입력요청 request,
        CancellationToken cancellationToken = default)
    {
        var input = await _inputFactory.생성Async(request, cancellationToken);
        var result = _coordinationService.조율(input);
        if (_planValidation is not null)
            result = await _planValidation.검증Async(input, result, cancellationToken);
        var applyResult = await _applyService.추천잠금적용Async(
            result,
            기사동시추천잠금건수: Math.Max(1, request.기사당최대추천건수),
            cancellationToken: cancellationToken);
        return (input, result, applyResult);
    }
}
