using FluentResults;
using Microsoft.AspNetCore.Http;
using Ssalddel.Contracts.Driver.Transport;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Driver.Transport;

public sealed class 운송상차완료CommandHandler : IRequestHandler<운송상차완료Command, Result<기사운송상태변경응답>>
{
    private readonly SsalddelContext _db;
    private readonly I기사운송상태변경CommandExecutor _executor;
    private readonly I운송증빙첨부JsonWriter _attachmentWriter;

    public 운송상차완료CommandHandler(
        SsalddelContext db,
        I기사운송상태변경CommandExecutor executor,
        I운송증빙첨부JsonWriter attachmentWriter)
    {
        _db = db;
        _executor = executor;
        _attachmentWriter = attachmentWriter;
    }

    public async Task<Result<기사운송상태변경응답>> Handle(운송상차완료Command request, CancellationToken cancellationToken)
    {
        var transport = await _db.운송원장
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.기사_운송자 == request.기사Id, cancellationToken);
        if (transport is null)
        {
            return Result.Fail<기사운송상태변경응답>("운송을 찾을 수 없습니다.");
        }

        var requestId = string.IsNullOrWhiteSpace(transport.의뢰Id) ? transport.운송번호 : transport.의뢰Id;
        // 창고의 출고 확정이 재고를 차감한다. 기사는 그 사실을 확인하며 재고를 다시 차감하지 않는다.
        if (!string.IsNullOrWhiteSpace(requestId)
            && await _db.출고예정.AsNoTracking().AnyAsync(
                x => x.운송의뢰Id == requestId && x.출고창고Id > 0
                     && x.상태 != 출고상태.취소 && x.상태 != 출고상태.출고완료,
                cancellationToken))
        {
            return Result.Fail<기사운송상태변경응답>(
                new Error("창고의 상품 인계 완료가 확인되어야 상차 완료 처리할 수 있습니다.")
                    .WithMetadata("StatusCode", StatusCodes.Status409Conflict));
        }

        if (string.IsNullOrWhiteSpace(request.상차사진ObjectName))
        {
            return Result.Fail<기사운송상태변경응답>("상차 완료 사진 업로드가 확인되어야 상차 완료 처리할 수 있습니다.");
        }

        var receiptRequired = await _db.화주운송의뢰
            .AsNoTracking()
            .Where(x => x.의뢰Id == requestId)
            .Select(x => new
            {
                필요 = x.증빙방식 == "인수증" || x.결제수단.Contains("인수증"),
                서명필수 = x.요청사항.Contains("서명필수")
                    || x.요청사항.Contains("서명 필수")
                    || x.정산메모.Contains("서명필수")
                    || x.정산메모.Contains("서명 필수")
            })
            .FirstOrDefaultAsync(cancellationToken);

        var receiptEvidenceResult = 운송상차인수증증빙정책.증빙생성(
            request,
            receiptRequired?.필요 == true,
            receiptRequired?.서명필수 == true);
        if (receiptEvidenceResult.IsFailed)
        {
            return Result.Fail<기사운송상태변경응답>(receiptEvidenceResult.Errors.Select(x => x.Message));
        }

        var receiptEvidence = receiptEvidenceResult.Value;

        return await _executor.실행Async(
            new 기사운송상태변경요청(
                request.기사Id,
                request.Id,
                request.참여자Id,
                request.실행역할,
                기사운송상태코드.상차완료,
                nameof(운송상차완료됨Event),
                context => new 운송상차완료됨Event(
                    request.기사Id,
                    context.운송.Id,
                    context.운송.운송번호,
                    context.운송.출발지,
                    context.운송.도착지,
                    context.이전상태,
                    context.운송.상태,
                    context.발생시각Utc,
                    context.TraceId,
                    receiptEvidence),
                (entity, changedAt) => _attachmentWriter.추가(
                    entity,
                    new 운송증빙첨부(
                        "pickup-complete-photo",
                        request.상차사진ObjectName,
                        request.상차사진Url,
                        request.기사Id,
                        changedAt,
                        new Dictionary<string, object?>
                        {
                            ["receiptEvidenceMethod"] = request.인수증증빙방식?.Trim(),
                            ["receiptConfirmed"] = request.인수증확인완료,
                            ["receiptSignatureOmitted"] = request.인수증서명생략확인
                        }))),
            cancellationToken);
    }

}
