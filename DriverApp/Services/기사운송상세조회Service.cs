using System.Net;
using DriverApp.Models.Driver.Samples;
using Ssalddel.Contracts.Driver.Transport;

namespace DriverApp.Services;

/// <summary>경로의 운송 ID를 서버에서 재조회하고 이전 인증 세션의 응답을 차단합니다.</summary>
public sealed class 기사운송상세조회Service(IDriverTransportApiService api, IAuthSession auth)
{
    public event Action? 인증변경
    {
        add => auth.Changed += value;
        remove => auth.Changed -= value;
    }

    public bool 로그인됨 => auth.IsAuthenticated;
    public long SessionRevision => auth.SessionRevision;

    public Task 상차지도착Async(long transportId, CancellationToken cancellationToken)
        => 도착기록Async(transportId, api.상차지도착Async, cancellationToken);

    public Task 하차지도착Async(long transportId, CancellationToken cancellationToken)
        => 도착기록Async(transportId, api.하차지도착Async, cancellationToken);

    private async Task 도착기록Async(
        long transportId,
        Func<long, CancellationToken, Task<기사운송상태변경응답?>> arrive,
        CancellationToken cancellationToken)
    {
        await auth.RestoreAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!auth.IsAuthenticated)
        {
            throw new HttpRequestException("로그인이 필요합니다.", null, HttpStatusCode.Unauthorized);
        }

        var version = auth.SessionRevision;
        var result = await arrive(transportId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (version != auth.SessionRevision || !auth.IsAuthenticated)
        {
            throw new OperationCanceledException("인증 세션이 변경되었습니다.", cancellationToken);
        }
        if (result is null || result.Id != transportId)
        {
            throw new InvalidOperationException("요청한 운송의 도착 처리 결과를 확인할 수 없습니다.");
        }
    }

    public async Task<기사운송샘플항목?> 조회Async(long transportId, CancellationToken cancellationToken)
    {
        await auth.RestoreAsync(cancellationToken);
        if (!auth.IsAuthenticated)
        {
            throw new HttpRequestException("로그인이 필요합니다.", null, HttpStatusCode.Unauthorized);
        }

        var version = auth.SessionRevision;
        var detail = await api.상세조회Async(transportId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (version != auth.SessionRevision || !auth.IsAuthenticated)
        {
            throw new OperationCanceledException("인증 세션이 변경되었습니다.", cancellationToken);
        }

        if (detail is null)
        {
            return null;
        }

        if (detail.Id != transportId)
        {
            throw new InvalidOperationException("요청한 운송과 조회 결과가 일치하지 않습니다.");
        }

        return 기사운송표시Mapper.Map(detail);
    }
}
