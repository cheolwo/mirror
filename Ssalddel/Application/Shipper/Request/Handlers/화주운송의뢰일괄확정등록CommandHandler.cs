using FluentResults;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

public sealed class 화주운송의뢰일괄확정등록CommandHandler : IRequestHandler<화주운송의뢰일괄확정등록Command, Result<화주운송의뢰일괄등록결과응답>>
{
    private readonly ISender _sender;
    private readonly I화주운송의뢰추천Service _recommendationService;
    private readonly I차량추천Service _vehicleRecommendationService;

    public 화주운송의뢰일괄확정등록CommandHandler(ISender sender, I화주운송의뢰추천Service recommendationService, I차량추천Service vehicleRecommendationService)
    {
        _sender = sender;
        _recommendationService = recommendationService;
        _vehicleRecommendationService = vehicleRecommendationService;
    }

    public async Task<Result<화주운송의뢰일괄등록결과응답>> Handle(화주운송의뢰일괄확정등록Command request, CancellationToken cancellationToken)
    {
        if (request.행목록.Count == 0)
        {
            return Result.Fail<화주운송의뢰일괄등록결과응답>("등록할 행이 없습니다.");
        }

        var results = new List<화주운송의뢰일괄등록행결과>();

        foreach (var item in request.행목록)
        {
            var row = item.원본행;
            var rowResult = new 화주운송의뢰일괄등록행결과
            {
                행번호 = item.행번호,
                성공 = false
            };

            var errors = 화주운송의뢰일괄등록지원.ValidateRow(row).ToList();
            var baseRecommendation = _recommendationService.추천(row);
            var vehicleInput = 화주운송의뢰일괄등록지원.To차량추천요청(row, baseRecommendation.운송방식);
            var vehicleRecommendation = await _vehicleRecommendationService.추천Async(vehicleInput, cancellationToken);
            var mergedRecommendation = 화주운송의뢰일괄등록지원.To통합추천결과(baseRecommendation, vehicleRecommendation, item.최종선택차량종류);
            errors.AddRange(화주운송의뢰일괄등록지원.ValidateVehicleRecommendation(row.행번호, vehicleRecommendation, mergedRecommendation.차량종류, vehicleInput));
            rowResult.추천결과 = mergedRecommendation;

            if (!item.등록여부)
            {
                rowResult.오류.Add("사용자가 등록 대상에서 제외했습니다.");
                results.Add(rowResult);
                continue;
            }

            if (errors.Count > 0)
            {
                rowResult.오류.AddRange(errors);
                results.Add(rowResult);
                continue;
            }

            if (string.IsNullOrWhiteSpace(mergedRecommendation.차량종류))
            {
                rowResult.오류.Add("최종 선택 차량종류가 없습니다.");
                results.Add(rowResult);
                continue;
            }

            var createResult = await _sender.Send(화주운송의뢰일괄등록지원.To의뢰생성Command(row, mergedRecommendation), cancellationToken);
            if (createResult.IsFailed)
            {
                rowResult.오류.AddRange(createResult.Errors.Select(x => x.Message));
                results.Add(rowResult);
                continue;
            }

            rowResult.성공 = true;
            rowResult.의뢰Id = createResult.Value.의뢰Id;
            results.Add(rowResult);
        }

        return Result.Ok(new 화주운송의뢰일괄등록결과응답
        {
            전체행수 = results.Count,
            성공행수 = results.Count(x => x.성공),
            실패행수 = results.Count(x => !x.성공),
            행결과목록 = results
        });
    }
}
