using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.DeliveryZones;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.Services.DeliveryZones;

namespace Ssalddel.Controllers.Admin.DeliveryZones;

[ApiController]
[Authorize(Policy = "서버관리자전용")]
[SsalddelApiVersion(SsalddelProductVersion.V3_5)]
[Route(배달운영권역Routes.AdminBase)]
[SsalddelApiContractName("AdministrativeDongDeliveryOperatingTerritoriesAdminController")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.PlatformDeliveryZoneLedger,
    SsalddelCodeLayer.Api,
    "서버 관리자가 공식 행정동 후보를 조회하고 Draft 배달운영권역의 행정동 집합을 원자적으로 교체한다.",
    FlowOrder = 33,
    Boundary = "관리자 인증을 요구하며 운영 활성·주문·배차·정산·Unity 효과를 제공하지 않는다.")]
public sealed class 배달운영권역Controller(
    I배달운영권역관리Service service,
    ILogger<배달운영권역Controller> logger) : ControllerBase
{
    [HttpGet("source-scopes/{sourceScopeStableId}/administrative-dong-modules")]
    [SsalddelApiContractName("ListAdministrativeDongModules")]
    public async Task<IActionResult> 행정동Module목록(
        string sourceScopeStableId,
        CancellationToken cancellationToken)
        => await ExecuteAsync(
            () => service.행정동Module목록Async(sourceScopeStableId, cancellationToken));

    [HttpGet]
    [SsalddelApiContractName("ListDeliveryOperatingTerritories")]
    public async Task<IActionResult> 목록(CancellationToken cancellationToken)
        => Ok(await service.목록Async(cancellationToken));

    [HttpGet("{deliveryTerritoryStableId}")]
    [SsalddelApiContractName("GetDeliveryOperatingTerritory")]
    public async Task<IActionResult> 조회(
        string deliveryTerritoryStableId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.조회Async(deliveryTerritoryStableId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "DeliveryTerritoryRequestInvalid",
                detail: exception.Message);
        }
    }

    [HttpPost]
    [SsalddelApiContractName("CreateDeliveryOperatingTerritoryDraft")]
    public async Task<IActionResult> Draft생성(
        [FromBody] 배달운영권역Draft생성Request request,
        CancellationToken cancellationToken)
        => await ExecuteAsync(
            () => service.Draft생성Async(request, ResolveActorUserStableId(), cancellationToken),
            value => CreatedAtAction(
                nameof(조회),
                new { deliveryTerritoryStableId = value.DeliveryTerritoryStableId },
                value));

    [HttpPut("{deliveryTerritoryStableId}/administrative-dongs")]
    [SsalddelApiContractName("ReplaceDeliveryOperatingTerritoryAdministrativeDongs")]
    public async Task<IActionResult> 행정동교체(
        string deliveryTerritoryStableId,
        [FromBody] 배달운영권역행정동교체Request request,
        CancellationToken cancellationToken)
        => await ExecuteAsync(() => service.행정동교체Async(
            deliveryTerritoryStableId,
            request,
            ResolveActorUserStableId(),
            cancellationToken));

    private string ResolveActorUserStableId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.Identity?.Name
           ?? throw new UnauthorizedAccessException("AuthenticatedAdministratorIdentityRequired");

    private async Task<IActionResult> ExecuteAsync<T>(
        Func<Task<T>> action,
        Func<T, IActionResult>? success = null)
    {
        try
        {
            var result = await action();
            return success?.Invoke(result) ?? Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "DeliveryTerritoryRequestInvalid",
                detail: exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "DeliveryTerritoryNotFound",
                detail: exception.Message);
        }
        catch (배달운영권역ConcurrencyException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "DeliveryTerritoryRevisionConflict",
                detail: exception.Message);
        }
        catch (배달운영권역ConflictException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "DeliveryTerritoryConflict",
                detail: exception.Message);
        }
        catch (배달운영권역SourceUnavailableException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "AdministrativeDongReferenceUnavailable",
                detail: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "AdministratorIdentityRequired",
                detail: exception.Message);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "배달운영권역 저장 충돌");
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "DeliveryTerritoryPersistenceConflict",
                detail: "DeliveryTerritoryPersistenceConflict");
        }
    }
}
