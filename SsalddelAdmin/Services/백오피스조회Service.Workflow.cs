namespace SsalddelAdmin.Services;

public sealed partial class 백오피스조회Service
{
    public async Task<운송워크플로우관제상세응답?> 운송워크플로우관제상세조회Async(string requestId, CancellationToken cancellationToken = default)
    {
        var normalized = requestId.Trim();
        var request = await 의뢰상세조회Async(normalized, cancellationToken);
        var payments = await 결제목록조회Async(null, normalized, cancellationToken);
        var dispatchWait = (await 배차대기목록조회Async(cancellationToken))
            .Where(x => string.Equals(x.의뢰Id, normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var transports = (await 운송진행목록조회Async(null, cancellationToken))
            .Where(x => string.Equals(x.운송번호, normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var events = await 운송이벤트조회Async(normalized, cancellationToken);
        var pods = await 파일POD목록조회Async(null, normalized, cancellationToken);
        var payoutQuery = await 운송료지급요청조회Async(normalized, transports.FirstOrDefault(), cancellationToken);

        return 운송워크플로우관제상세Factory.Build(
            normalized,
            request,
            payments,
            dispatchWait,
            transports,
            events,
            pods,
            [],
            payoutQuery);
    }
}
