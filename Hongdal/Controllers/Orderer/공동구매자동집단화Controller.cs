using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Filters;
using Hongdal.Services.Orderer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using 홍달.Services.Versioning;

namespace Hongdal.Controllers.Orderer;

[ApiController]
[Authorize]
[HongdalApiVersion(HongdalProductVersion.V2_5, FeatureKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow, WorkflowKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
[HongdalApiWorkflow(HongdalWorkflow.GroupPurchaseImport)]
[RequireVersionFeature(VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
[Route("api/v1/orderer/group-purchase-auto-groups")]
public sealed class 공동구매자동집단화Controller : ControllerBase
{
    private readonly I공동구매자동집단화UseCase _useCase;

    public 공동구매자동집단화Controller(I공동구매자동집단화UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<공동구매자동집단요약응답>), StatusCodes.Status200OK)]
    public async Task<IActionResult> 목록(
        [FromQuery(Name = "productKey")] string? 상품키,
        [FromQuery(Name = "deliveryScopeKey")] string? 배송권키,
        [FromQuery(Name = "currentStatus")] string? 현재상태,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.목록조회Async(new 공동구매자동집단조회조건
        {
            상품키 = 상품키,
            배송권키 = 배송권키,
            현재상태 = 현재상태
        }, cancellationToken);

        if (!result.성공)
        {
            return this.ToProblemActionResult(result.메시지, result.상태코드);
        }

        var response = (result.값 ?? [])
            .Select(요약응답으로)
            .ToArray();
        return Ok(response);
    }

    [HttpPost("demands")]
    [ProducesResponseType(typeof(공동구매자동집단사용자응답), StatusCodes.Status200OK)]
    public async Task<IActionResult> 수요등록(
        [FromBody] 공동구매자동수요등록Command command,
        CancellationToken cancellationToken)
    {
        var currentUserId = CurrentUserId();
        command.주문자키 = currentUserId;
        if (string.IsNullOrWhiteSpace(command.주문자표시명))
        {
            command.주문자표시명 = User.Identity?.Name ?? command.주문자키;
        }

        var result = await _useCase.수요등록Async(command, cancellationToken);
        if (!result.성공)
        {
            return this.ToProblemActionResult(result.메시지, result.상태코드);
        }

        if (result.값 is null)
        {
            return this.ToNotFoundProblem("등록된 공동구매 자동집단을 찾을 수 없습니다.");
        }

        return Ok(사용자응답으로(result.값, currentUserId, command.수요출처키));
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? throw new UnauthorizedAccessException("사용자 식별자를 찾을 수 없습니다.");

    private static 공동구매자동집단요약응답 요약응답으로(공동구매자동집단응답 source)
        => 요약응답으로<공동구매자동집단요약응답>(source);

    private static 공동구매자동집단사용자응답 사용자응답으로(
        공동구매자동집단응답 source,
        string currentUserId,
        string demandSourceKey)
    {
        var response = 요약응답으로<공동구매자동집단사용자응답>(source);
        var ownDemand = source.수요목록.FirstOrDefault(item =>
                string.Equals(item.주문자키, currentUserId, StringComparison.Ordinal)
                && string.Equals(item.수요출처키, demandSourceKey, StringComparison.Ordinal))
            ?? source.수요목록.FirstOrDefault(item =>
                string.Equals(item.주문자키, currentUserId, StringComparison.Ordinal));

        if (ownDemand is null)
        {
            return response;
        }

        response.공동구매주문집계원장Id = source.공동구매주문집계원장Id;
        response.수요목록 = [본인수요응답으로(ownDemand)];
        return response;
    }

    private static T 요약응답으로<T>(공동구매자동집단응답 source)
        where T : 공동구매자동집단요약응답, new()
        => new()
        {
            자동집단Id = source.자동집단Id,
            상품키 = source.상품키,
            상품명 = source.상품명,
            HS코드 = source.HS코드,
            온도코드 = source.온도코드,
            물류방식 = source.물류방식,
            배송권키 = source.배송권키,
            배송권명 = source.배송권명,
            현재상태 = source.현재상태,
            수요건수 = source.수요건수,
            예약결제건수 = source.예약결제건수,
            총희망수량 = source.총희망수량,
            수량단위 = source.수량단위,
            목표참여자수 = source.목표참여자수,
            목표수량 = source.목표수량,
            생성시각Utc = source.생성시각Utc,
            수정시각Utc = source.수정시각Utc
        };

    private static 공동구매자동본인수요응답 본인수요응답으로(공동구매자동수요응답 source)
        => new()
        {
            수요Id = source.수요Id,
            수요출처키 = source.수요출처키,
            커뮤니티게시글Id = source.커뮤니티게시글Id,
            자동집단Id = source.자동집단Id,
            상품키 = source.상품키,
            상품명 = source.상품명,
            주문자키 = source.주문자키,
            배송권키 = source.배송권키,
            배송권명 = source.배송권명,
            입고의미상태 = source.입고의미상태,
            공동구매주문집계원장Id = source.공동구매주문집계원장Id,
            개별주문원장Id = source.개별주문원장Id,
            입고예정원장Id = source.입고예정원장Id,
            수요유형 = source.수요유형,
            결제상태 = source.결제상태,
            희망수량 = source.희망수량,
            수량단위 = source.수량단위,
            예약결제금액 = source.예약결제금액,
            생성시각Utc = source.생성시각Utc
        };
}
