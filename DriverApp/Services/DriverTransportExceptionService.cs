using Hongdal.Contracts.Driver.Transport;

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
    기사운송요약응답? Transport = null);

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
        catch (HttpRequestException ex)
        {
            return new DriverTransportExceptionReportResult(false, $"서버 연결 실패로 문제 신고가 저장되지 않았습니다. 네트워크 확인 후 다시 시도해 주세요. {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DriverTransportExceptionReportResult(false, "서버 응답 지연으로 문제 신고가 저장되지 않았습니다. 잠시 후 다시 시도해 주세요.");
        }
        catch (InvalidOperationException ex)
        {
            return new DriverTransportExceptionReportResult(false, ex.Message);
        }

        if (transport is null)
        {
            return new DriverTransportExceptionReportResult(false, "운송 예외 신고 응답을 받지 못했습니다.");
        }

        return new DriverTransportExceptionReportResult(
            true,
            transport.관리자확인필요
                ? "현장 문제가 서버에 신고되었고 관리자 확인 대상으로 표시되었습니다."
                : "현장 문제가 서버에 신고되었습니다.",
            transport);
    }
}
