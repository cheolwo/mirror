using System.Security.Cryptography;
using System.Text.Json;
using Ssalddel.Contracts.Common.Inbound;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.LogisticsProcessing.Warehouse;

/// <summary>최초 신청의 정규화된 업무 입력을 비교하며 원문 사본이나 새 원장을 만들지 않는다.</summary>
internal static class InboundSubmissionIdentity
{
    public static string Compute(입고요청 inbound, 입고요청저장요청 request)
    {
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Schema = "ordinary-inbound-submission.v1",
            inbound.창고Id,
            inbound.입고흐름유형,
            inbound.입고생성경로,
            inbound.계약선행여부,
            inbound.자동생성여부,
            inbound.주문Id,
            inbound.주문참조번호,
            inbound.주문자UserId,
            inbound.판매자UserId,
            inbound.출고예정Id,
            inbound.운송의뢰Id,
            inbound.공급처코드,
            inbound.공급처명,
            inbound.원주문참조번호,
            inbound.예정도착일,
            inbound.비고,
            inbound.계약번호,
            inbound.계약유형,
            inbound.계약상대방명,
            inbound.정산방식,
            inbound.판매수수료율,
            inbound.보관료일단가,
            inbound.통관필요여부,
            inbound.계약시작일,
            inbound.계약종료일,
            inbound.계약메모,
            request.신청개인정보동의증적Id,
            신청출처Code = request.신청출처Code?.Trim() ?? string.Empty
        });
        if (request.가입고사진신청Id is { } photoRequestId)
        {
            // 사진 없는 기존 요청의 v1 입력 바이트와 지문을 그대로 보존한다.
            canonical = JsonSerializer.SerializeToUtf8Bytes(new
            {
                Schema = "ordinary-inbound-photo-submission.v1",
                OrdinaryFingerprint = Convert.ToHexString(SHA256.HashData(canonical)),
                PhotoRequestId = photoRequestId
            });
        }
        return Convert.ToHexString(SHA256.HashData(canonical));
    }
}

public sealed class InboundSubmissionConflictException()
    : InvalidOperationException("이미 사용한 입고 신청 요청 ID를 다른 내용에 사용할 수 없습니다.");

public sealed class InboundSubmissionInvalidRequestException()
    : InvalidOperationException("입고 신청 요청 ID를 확인해 주세요.");
