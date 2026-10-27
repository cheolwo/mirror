using FluentResults;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

public sealed class 화주운송의뢰일괄미리보기CommandHandler : IRequestHandler<화주운송의뢰일괄미리보기Command, Result<화주운송의뢰일괄미리보기응답>>
{
    private readonly I화주운송의뢰추천Service _recommendationService;
    private readonly I차량추천Service _vehicleRecommendationService;

    public 화주운송의뢰일괄미리보기CommandHandler(I화주운송의뢰추천Service recommendationService, I차량추천Service vehicleRecommendationService)
    {
        _recommendationService = recommendationService;
        _vehicleRecommendationService = vehicleRecommendationService;
    }

    public async Task<Result<화주운송의뢰일괄미리보기응답>> Handle(화주운송의뢰일괄미리보기Command request, CancellationToken cancellationToken)
    {
        if (request.행목록.Count == 0)
        {
            return Result.Fail<화주운송의뢰일괄미리보기응답>("미리보기할 행이 없습니다.");
        }

        var rowErrors = 화주운송의뢰일괄등록지원.행오류사전만들기(request.파싱오류목록);
        var rows = new List<화주운송의뢰일괄미리보기행응답>();

        foreach (var row in request.행목록)
        {
            var errors = new List<string>();
            if (rowErrors.TryGetValue(row.행번호, out var parseErrors))
            {
                errors.AddRange(parseErrors);
            }

            errors.AddRange(화주운송의뢰일괄등록지원.ValidateRow(row));

            var baseRecommendation = _recommendationService.추천(row);
            var vehicleInput = 화주운송의뢰일괄등록지원.To차량추천요청(row, baseRecommendation.운송방식);
            var vehicleRecommendation = await _vehicleRecommendationService.추천Async(vehicleInput, cancellationToken);
            var mergedRecommendation = 화주운송의뢰일괄등록지원.To통합추천결과(baseRecommendation, vehicleRecommendation, row.차량종류);
            errors.AddRange(화주운송의뢰일괄등록지원.ValidateVehicleRecommendation(row.행번호, vehicleRecommendation, mergedRecommendation.차량종류, vehicleInput));

            if (string.IsNullOrWhiteSpace(mergedRecommendation.차량종류))
            {
                errors.Add($"{row.행번호}행: 추천 가능한 차량을 찾지 못했습니다.");
            }

            var warnings = mergedRecommendation.경고목록.ToList();
            if (string.IsNullOrWhiteSpace(row.차량종류))
            {
                warnings.Add("차량종류가 비어 있어 추천 차량을 기본 선택값으로 사용합니다.");
            }

            warnings.Add("입력한 연락처와 시간창을 확인한 뒤 등록해 주세요. 시간은 CSV에 명시된 시간대를 기준으로 보존합니다.");

            rows.Add(new 화주운송의뢰일괄미리보기행응답
            {
                행번호 = row.행번호,
                유효함 = errors.Count == 0,
                등록대상여부 = errors.Count == 0,
                최종선택차량종류 = mergedRecommendation.차량종류,
                원본행 = row,
                추천결과 = mergedRecommendation,
                오류목록 = errors,
                경고목록 = warnings.Distinct(StringComparer.Ordinal).ToArray()
            });
        }

        var response = new 화주운송의뢰일괄미리보기응답
        {
            전체행수 = rows.Count,
            유효행수 = rows.Count(x => x.유효함),
            오류행수 = rows.Count(x => !x.유효함),
            행목록 = rows
        };

        return Result.Ok(response);
    }
}
