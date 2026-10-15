using FluentResults;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

/// <summary>화주 제안은 서버에 저장된 제안 금액이며, 기사 수락이나 결제 완료를 의미하지 않습니다.</summary>
public static class 화주직접운임제안Policy
{
    public const string SourceCode = "ShipperDirectOffer";

    public static Result<PricingDTO?> 준비(화주운송의뢰생성요청 request)
    {
        var source = request.요금옵션;
        if (source?.화주직접운임제안 != true) return Result.Ok(source);
        if (request.결제예정금액 is not > 0)
            return Result.Fail<PricingDTO?>("화주가 제안할 배송료를 원 단위로 입력해 주세요.");
        var amount = request.결제예정금액.Value;
        if (source.기사지급예정운임.HasValue && source.기사지급예정운임 != amount)
            return Result.Fail<PricingDTO?>("직접 제안에서는 배송료와 기사 제안 보수가 같아야 합니다.");
        if (source.플랫폼수수료 is not null and not 0m || source.AgreedRateTable is not null)
            return Result.Fail<PricingDTO?>("직접 제안은 별도 수수료나 다른 요금표와 함께 확정할 수 없습니다.");
        if (source.알선정책 is { 알선단계: > 1 } || !string.IsNullOrWhiteSpace(source.알선정책?.알선소Id))
            return Result.Fail<PricingDTO?>("직접 제안에는 알선소나 재알선 단계를 지정할 수 없습니다.");
        if (new[] { source.대기료, source.수작업비, source.할증 }.Any(value => value is not null and not 0m))
            return Result.Fail<PricingDTO?>("직접 제안 배송료에 추가 작업 비용을 포함해 하나의 금액으로 입력해 주세요.");
        return Result.Ok<PricingDTO?>(new PricingDTO
        {
            화주직접운임제안 = true, 단가출처 = SourceCode,
            서비스레벨 = source.서비스레벨, 요청사항 = source.요청사항,
            최종운임 = amount, 기사지급예정운임 = amount, 플랫폼수수료 = 0m,
            알선정책 = new 화주운송알선정책DTO { 알선단계 = 1, 재알선금지 = true }
        });
    }
}
