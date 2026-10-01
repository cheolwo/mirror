using Hongdal.ApiMetadata;
using Hongdal.Application.Shipper.ImportFood;
using Hongdal.Contracts.Shipper.ImportFood;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Shipper;

[HongdalApiVersion(HongdalProductVersion.V2_0)]
[ApiController]
[Route("api/v1/shipper/import-food/korean-labels")]
[Authorize(Roles = 역할명.화주)]
public sealed class 수입식품한글표시사항Controller : ControllerBase
{
    private readonly ISender _sender;

    public 수입식품한글표시사항Controller(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType(typeof(수입식품한글표시사항조회응답), StatusCodes.Status200OK)]
    public async Task<ActionResult<수입식품한글표시사항조회응답>> 조회(
        [FromQuery(Name = "pageNo")] int 페이지번호 = 1,
        [FromQuery(Name = "numOfRows")] int 한페이지결과수 = 10,
        [FromQuery(Name = "type")] string 데이터형식 = "xml",
        [FromQuery(Name = "dclPrductSeCdNm")] string? 제품구분 = null,
        [FromQuery(Name = "bsnOfcName")] string? 수입업체명 = null,
        [FromQuery(Name = "prductKoreanNm")] string? 한글제품명 = null,
        [FromQuery(Name = "prductNm")] string? 영문제품명 = null,
        [FromQuery(Name = "ovsmnfstNm")] string? 해외제조업소명 = null,
        [FromQuery(Name = "itmNm")] string? 품목명 = null,
        [FromQuery(Name = "xportNtncdNm")] string? 수출국명 = null,
        [FromQuery(Name = "mnfNtncdNm")] string? 제조국명 = null,
        [FromQuery(Name = "korlabel")] string? 한글표시사항검색어 = null,
        [FromQuery(Name = "irdntNm")] string? 원재료명 = null,
        [FromQuery(Name = "expirdeBeginDtmStart")] string? 유통기한시작일자검색시작 = null,
        [FromQuery(Name = "expirdeBeginDtmEnd")] string? 유통기한시작일자검색종료 = null,
        [FromQuery(Name = "expirdeEndDtmStart")] string? 유통기한종료일자검색시작 = null,
        [FromQuery(Name = "expirdeEndDtmEnd")] string? 유통기한종료일자검색종료 = null,
        [FromQuery(Name = "procsDtmStart")] string? 처리일자검색시작 = null,
        [FromQuery(Name = "procsDtmEnd")] string? 처리일자검색종료 = null,
        CancellationToken cancellationToken = default)
    {
        var 응답 = await _sender.Send(new 수입식품한글표시사항조회Query
        {
            페이지번호 = 페이지번호,
            한페이지결과수 = 한페이지결과수,
            데이터형식 = 데이터형식,
            제품구분 = 제품구분,
            수입업체명 = 수입업체명,
            한글제품명 = 한글제품명,
            영문제품명 = 영문제품명,
            해외제조업소명 = 해외제조업소명,
            품목명 = 품목명,
            수출국명 = 수출국명,
            제조국명 = 제조국명,
            한글표시사항검색어 = 한글표시사항검색어,
            원재료명 = 원재료명,
            유통기한시작일자검색시작 = 유통기한시작일자검색시작,
            유통기한시작일자검색종료 = 유통기한시작일자검색종료,
            유통기한종료일자검색시작 = 유통기한종료일자검색시작,
            유통기한종료일자검색종료 = 유통기한종료일자검색종료,
            처리일자검색시작 = 처리일자검색시작,
            처리일자검색종료 = 처리일자검색종료
        }, cancellationToken);

        return Ok(응답);
    }
}
