using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

public sealed class 공개화물요약조회QueryHandler : IRequestHandler<공개화물요약조회Query, IReadOnlyList<공개화물요약응답>>
{
    public 공개화물요약조회QueryHandler(SsalddelContext db)
    {
        // 기존 DI/호출 계약만 보존합니다. 실제 운송 의뢰는 공개 공고의 근거가 아닙니다.
    }

    public Task<IReadOnlyList<공개화물요약응답>> Handle(공개화물요약조회Query request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 명시적 공개 동의·만료가 없는 과거 운송 원장은 폐쇄 기본값을 유지합니다.
        // 독립 정보 공고는 CargoNoticeRoutes.Api의 별도 공개 수명 계약에서만 조회합니다.
        return Task.FromResult<IReadOnlyList<공개화물요약응답>>([]);
    }
}
