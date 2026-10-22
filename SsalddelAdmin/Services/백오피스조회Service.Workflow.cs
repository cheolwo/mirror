using Ssalddel.Contracts.Admin.Settlement;
using System.Net.Http.Json;
using System.Text.Json;

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

    private async Task<기사운송료지급조회응답?> 운송료지급요청조회Async(
        string requestId, 운송진행응답? transport, CancellationToken cancellationToken)
    {
        if (transport?.Id is not > 0) return null;
        var nowUtc = DateTime.UtcNow;
        var result = new 기사운송료지급조회응답 { 조회년 = nowUtc.Year, 조회월 = nowUtc.Month };
        var query = BuildQuery("api/v1/admin/driver-payouts",
            ("year", nowUtc.Year.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("month", nowUtc.Month.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        try
        {
            ApplyAuthorizationHeader();
            var records = await _httpClient.GetFromJsonAsync<List<기사지급승인응답>>(query, cancellationToken)
                ?? throw new InvalidDataException("기사 지급요청 조회 본문이 없습니다.");
            if (records.Any(item => item is null)) throw new InvalidDataException("기사 지급요청 항목을 확인하지 못했습니다.");
            result.지급요청목록 = records.Where(item => item.TransportId == transport.Id
                    && string.Equals(item.RequestId, requestId, StringComparison.Ordinal))
                .OrderByDescending(item => item.ApprovedAtUtc).ToArray();
            result.조회완료 = true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result.조회실패 = true;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException or InvalidOperationException)
        {
            result.조회실패 = true;
        }
        return result;
    }
}
