using Ssalddel.Contracts.Driver.Transport;

namespace DriverApp.Services;

public sealed record DriverTransportExceptionReport(
    long TransportId,
    string Stage,
    string ExceptionCode,
    string Reason,
    string? Memo,
    bool RequestAdminReview = true);

public sealed record DriverTransportExceptionReportResult(
    bool Reported,
    string Message,
    기사운송요약응답? Transport = null,
    bool OutcomeUnknown = false);

public interface IDriverTransportExceptionService
{
    Task<DriverTransportExceptionReportResult> ReportAsync(
        DriverTransportExceptionReport report,
        CancellationToken cancellationToken = default);
}

public sealed class HttpDriverTransportExceptionService : IDriverTransportExceptionService
{
    private readonly IDriverTransportApiService _transportApi;

    public HttpDriverTransportExceptionService(IDriverTransportApiService transportApi)
    {
        _transportApi = transportApi;
    }

    public async Task<DriverTransportExceptionReportResult> ReportAsync(
        DriverTransportExceptionReport report,
        CancellationToken cancellationToken = default)
    {
        기사운송요약응답? transport;
        try
        {
            transport = await _transportApi.예외신고Async(
                report.TransportId,
                new 기사운송문제신고요청
                {
                    단계 = report.Stage,
                    예외코드 = report.ExceptionCode,
                    사유 = report.Reason,
                    메모 = report.Memo,
                    관리자확인요청 = report.RequestAdminReview
                },
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Unknown();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unknown();
        }
        catch (InvalidOperationException)
        {
            return Unknown();
        }

        if (transport is null || transport.Id != report.TransportId || transport.개인정보제공보류
            || !transport.예외신고됨 || transport.최근예외단계 != report.Stage
            || transport.최근예외메시지 != report.Reason.Trim())
        {
            return Unknown();
        }

        return new DriverTransportExceptionReportResult(
            true,
            transport.관리자확인필요
                ? "현장 문제가 서버에 신고되었고 관리자 확인 대상으로 표시되었습니다."
                : "현장 문제가 서버에 신고되었습니다.",
            transport);
    }

    private static DriverTransportExceptionReportResult Unknown()
        => new(false, "신고 처리 결과를 확인하지 못했습니다. 중복 신고하지 말고 운영자에게 확인해 주세요.",
            OutcomeUnknown: true);
}
