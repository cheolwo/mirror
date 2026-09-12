using System.Diagnostics;
using Ssalddel.Contracts.Driver.Transport;
using FluentResults;
using Ssalddel.Application.CommandProcessing;
using MediatR;
using Ssalddel.Services.Operations;

namespace Ssalddel.Application.Driver.Transport;

public sealed class 운송문제신고CommandHandler : IRequestHandler<운송문제신고Command, Result<기사운송요약응답>>
{
    private readonly SsalddelContext _db;
    private readonly IPublisher _publisher;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I참여자실행권한검사 _권한검사;
    private readonly I운송증빙첨부JsonWriter _attachmentWriter;
    private readonly I비정상운송사건Service _비정상운송사건Service;
    private readonly ILogger<운송문제신고CommandHandler> _logger;

    public 운송문제신고CommandHandler(
        SsalddelContext db,
        IPublisher publisher,
        ICurrentUserAccessor currentUserAccessor,
        I참여자실행권한검사 권한검사,
        I운송증빙첨부JsonWriter attachmentWriter,
        I비정상운송사건Service 비정상운송사건Service,
        ILogger<운송문제신고CommandHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _currentUserAccessor = currentUserAccessor;
        _권한검사 = 권한검사;
        _attachmentWriter = attachmentWriter;
        _비정상운송사건Service = 비정상운송사건Service;
        _logger = logger;
    }

    public async Task<Result<기사운송요약응답>> Handle(운송문제신고Command request, CancellationToken cancellationToken)
    {
        if (!_권한검사.Try검증(_currentUserAccessor.UserId, _currentUserAccessor.Role, request.참여자Id, request.실행역할, out var 권한오류))
        {
            return Result.Fail<기사운송요약응답>(권한오류);
        }

        var entity = await _db.운송원장
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.기사_운송자 == request.기사Id, cancellationToken);

        if (entity is null)
        {
            return Result.Fail<기사운송요약응답>("운송을 찾을 수 없습니다.");
        }

        var now = DateTime.UtcNow;
        var 예외 = 운송현장예외정책.정리(
            request.단계,
            request.예외코드,
            request.사유,
            request.관리자확인요청);
        var memo = BuildMemoLine(예외, request.메모);
        entity.메모 = string.IsNullOrWhiteSpace(entity.메모) ? memo : $"{entity.메모}\n{memo}";
        _attachmentWriter.추가(
            entity,
            new 운송증빙첨부(
                "transport-field-exception",
                request.증빙ObjectName,
                request.증빙Url,
                request.기사Id,
                now,
                new Dictionary<string, object?>
                {
                    ["stage"] = 예외.단계,
                    ["exceptionCode"] = 예외.예외코드,
                    ["reason"] = 예외.사유,
                    ["memo"] = request.메모?.Trim(),
                    ["nextAction"] = 예외.다음행동안내,
                    ["adminReviewRequired"] = 예외.관리자확인필요,
                    ["adminReviewRequested"] = request.관리자확인요청,
                    ["transportStatus"] = entity.상태,
                    ["traceId"] = Activity.Current?.TraceId.ToString()
                }));
        entity.UpdatedAt = now;

        var requestId = string.IsNullOrWhiteSpace(entity.의뢰Id)
            ? entity.운송번호
            : entity.의뢰Id;
        비정상운송사건접수결과 incidentResult;
        try
        {
            incidentResult = await _비정상운송사건Service.접수Async(
                new 비정상운송사건접수요청(
                    entity.Id,
                    requestId,
                    예외.단계,
                    예외.예외코드,
                    !string.IsNullOrWhiteSpace(request.증빙ObjectName)
                    || !string.IsNullOrWhiteSpace(request.증빙Url),
                    now,
                    request.정상확인수량,
                    request.영향수량,
                    request.현장진행불가),
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Result.Fail<기사운송요약응답>(ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await PublishAfterCommitAsync(entity, 예외, request, incidentResult.사건, now, cancellationToken);

        return Result.Ok(new 기사운송요약응답
        {
            Id = entity.Id,
            운송번호 = entity.운송번호,
            상태 = entity.상태,
            출발지 = entity.출발지,
            도착지 = entity.도착지,
            기사_운송자 = entity.기사_운송자,
            출발_픽업 = entity.출발_픽업,
            도착 = entity.도착,
            운임 = entity.운임,
            예외신고됨 = true,
            최근예외단계 = 예외.단계,
            최근예외코드 = 예외.예외코드,
            최근예외메시지 = 예외.사유,
            다음행동안내 = 예외.다음행동안내,
            관리자확인필요 = 예외.관리자확인필요,
            UpdatedAt = entity.UpdatedAt
        });
    }

    private static string BuildMemoLine(운송현장예외정리결과 예외, string? requestMemo)
    {
        var memo = string.IsNullOrWhiteSpace(requestMemo) ? string.Empty : $" 메모={requestMemo.Trim()}";
        var admin = 예외.관리자확인필요 ? " 관리자확인필요" : string.Empty;
        return $"[운송예외][{예외.단계}][{예외.예외코드}] {예외.사유}{memo}{admin}";
    }

    private async Task PublishAfterCommitAsync(
        운송원장 entity,
        운송현장예외정리결과 예외,
        운송문제신고Command request,
        살뜰.도메인.운송.비정상운송사건? incident,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.Publish(
                new 운송문제신고됨Event(
                    request.기사Id,
                    entity.Id,
                    entity.운송번호,
                    예외.단계,
                    예외.예외코드,
                    예외.사유,
                    request.메모,
                    request.증빙ObjectName,
                    request.증빙Url,
                    예외.관리자확인필요,
                    now,
                    Activity.Current?.TraceId.ToString() ?? string.Empty)
                {
                    전체수량 = incident?.전체수량,
                    정상확인수량 = incident?.정상확인수량,
                    영향수량 = incident?.영향수량,
                    현장진행불가 = incident?.현장진행불가 ?? request.현장진행불가,
                    업무통제상태Code = incident?.업무통제상태Code ?? string.Empty,
                    보류범위Code = incident?.보류범위Code ?? string.Empty
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "운송 문제 신고 사후처리 이벤트 발행 중 예외가 발생했습니다. TransportId={TransportId}",
                entity.Id);
        }
    }
}
