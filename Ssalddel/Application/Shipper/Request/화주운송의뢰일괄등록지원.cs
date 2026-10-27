using System.Globalization;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

internal static class 화주운송의뢰일괄등록지원
{
    internal static Dictionary<int, List<string>> 행오류사전만들기(IReadOnlyList<string> errors)
    {
        var map = new Dictionary<int, List<string>>();
        foreach (var error in errors)
        {
            var rowNumber = ParseRowNumber(error);
            if (!rowNumber.HasValue)
            {
                continue;
            }

            if (!map.TryGetValue(rowNumber.Value, out var list))
            {
                list = [];
                map[rowNumber.Value] = list;
            }

            list.Add(error);
        }

        return map;
    }

    internal static 차량추천요청 To차량추천요청(화주운송의뢰일괄등록행입력 row, string? transportMethod = null)
    {
        return new 차량추천요청
        {
            화물종류 = row.화물종류,
            화물설명 = row.화물설명,
            운송방식 = transportMethod ?? row.운송방식,
            서비스레벨 = row.서비스레벨,
            요청사항 = row.요청사항,
            화물수량 = row.화물수량,
            화물길이Mm = row.화물길이Mm,
            화물폭Mm = row.화물폭Mm,
            화물높이Mm = row.화물높이Mm,
            화물중량Kg = row.화물중량Kg,
            화물부피Cbm = row.화물부피Cbm,
            팔레트개수 = row.팔레트개수,
            화물온도조건 = row.화물온도조건,
            화물파손주의여부 = row.화물파손주의여부,
            화물바닥회전가능여부 = row.화물바닥회전가능여부,
            화물적층가능여부 = row.화물적층가능여부
        };
    }

    internal static 화주운송의뢰추천결과 To통합추천결과(화주운송의뢰추천결과 baseRecommendation, 차량추천응답 vehicleRecommendation, string? selectedVehicleType = null)
    {
        var vehicleType = string.IsNullOrWhiteSpace(selectedVehicleType)
            ? vehicleRecommendation.추천차량종류
            : selectedVehicleType.Trim();

        var reasonList = vehicleRecommendation.추천사유.Any()
            ? vehicleRecommendation.추천사유.ToArray()
            : string.IsNullOrWhiteSpace(baseRecommendation.추천사유)
                ? Array.Empty<string>()
                : [baseRecommendation.추천사유];

        var warnings = baseRecommendation.경고목록
            .Concat(vehicleRecommendation.경고목록)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new 화주운송의뢰추천결과
        {
            운송방식 = baseRecommendation.운송방식,
            차량종류 = vehicleType,
            결제수단 = baseRecommendation.결제수단,
            정산시점 = baseRecommendation.정산시점,
            증빙방식 = baseRecommendation.증빙방식,
            수납주체 = baseRecommendation.수납주체,
            추천사유 = string.Join("; ", reasonList),
            추정화물부피Cbm = vehicleRecommendation.추정화물부피Cbm,
            적재조건 = vehicleRecommendation.적재조건,
            추천사유목록 = reasonList,
            경고목록 = warnings,
            후보차량목록 = vehicleRecommendation.후보목록
        };
    }

    internal static 의뢰생성Command To의뢰생성Command(화주운송의뢰일괄등록행입력 row, 화주운송의뢰추천결과 recommendation)
    {
        return new 의뢰생성Command(
            row.화주Id,
            recommendation.운송방식,
            recommendation.차량종류,
            recommendation.결제수단,
            null,
            new 화주운송정산조건DTO
            {
                정산시점 = ParseSettlementTime(recommendation.정산시점),
                결제수단 = ParsePaymentMethod(recommendation.결제수단),
                증빙방식 = ParseEvidenceMethod(recommendation.증빙방식),
                수납주체 = ParseCollector(recommendation.수납주체),
                정산메모 = recommendation.추천사유,
                세금계산서필요 = string.Equals(recommendation.증빙방식, "세금계산서", StringComparison.OrdinalIgnoreCase),
                현금영수증필요 = string.Equals(recommendation.증빙방식, "현금영수증", StringComparison.OrdinalIgnoreCase)
            },
            row.화물종류,
            row.화물설명,
            row.화물수량,
            row.화물길이Mm,
            row.화물폭Mm,
            row.화물높이Mm,
            row.화물중량Kg,
            row.화물부피Cbm,
            row.팔레트개수,
            row.화물파손주의여부,
            row.화물온도조건,
            row.픽업도로명주소 ?? string.Empty,
            row.픽업상세주소,
            null,
            null,
            row.픽업연락처이름 ?? string.Empty,
            row.픽업연락처전화번호 ?? string.Empty,
            row.픽업시간창시작일시.GetValueOrDefault(),
            row.픽업시간창종료일시.GetValueOrDefault(),
            row.하차도로명주소 ?? string.Empty,
            row.하차상세주소,
            null,
            null,
            row.하차연락처이름 ?? string.Empty,
            row.하차연락처전화번호 ?? string.Empty,
            row.하차시간창시작일시,
            row.하차시간창종료일시,
            row.서비스레벨,
            row.요청사항,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            row.클라이언트행Id,
            null)
        {
            화물바닥회전가능여부 = row.화물바닥회전가능여부,
            화물적층가능여부 = row.화물적층가능여부
        };
    }

    internal static IReadOnlyList<string> ValidateRow(화주운송의뢰일괄등록행입력 row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.화물종류))
        {
            errors.Add($"{row.행번호}행: 화물종류는 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(row.픽업도로명주소))
        {
            errors.Add($"{row.행번호}행: 픽업도로명주소는 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(row.하차도로명주소))
        {
            errors.Add($"{row.행번호}행: 하차도로명주소는 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(row.픽업연락처이름) || string.IsNullOrWhiteSpace(row.픽업연락처전화번호))
            errors.Add($"{row.행번호}행: 상차 담당자 이름과 연락처를 확인해 주세요.");
        if (string.IsNullOrWhiteSpace(row.하차연락처이름) || string.IsNullOrWhiteSpace(row.하차연락처전화번호))
            errors.Add($"{row.행번호}행: 하차 담당자 이름과 연락처를 확인해 주세요.");
        if (!row.픽업시간창시작일시.HasValue || !row.픽업시간창종료일시.HasValue)
            errors.Add($"{row.행번호}행: 상차 가능한 시작·종료 시각을 확인해 주세요.");
        else if (row.픽업시간창시작일시 >= row.픽업시간창종료일시)
            errors.Add($"{row.행번호}행: 상차 종료 시각은 시작 시각보다 늦어야 합니다.");
        if (row.하차시간창시작일시.HasValue != row.하차시간창종료일시.HasValue
            || row.하차시간창시작일시.HasValue && row.하차시간창시작일시 >= row.하차시간창종료일시)
            errors.Add($"{row.행번호}행: 하차 시작·종료 시각을 함께 확인해 주세요.");
        if (row.하차시간창종료일시.HasValue && row.픽업시간창시작일시.HasValue
            && row.하차시간창종료일시 <= row.픽업시간창시작일시)
            errors.Add($"{row.행번호}행: 하차 약속이 상차 시작보다 늦어야 합니다.");

        var loadingInput = new 화물적재입력
        {
            화물종류 = row.화물종류, 설명 = row.화물설명, 수량 = row.화물수량,
            길이Mm = row.화물길이Mm, 폭Mm = row.화물폭Mm, 높이Mm = row.화물높이Mm,
            총중량Kg = row.화물중량Kg, 입력부피Cbm = row.화물부피Cbm, 팔레트개수 = row.팔레트개수,
            온도조건 = row.화물온도조건, 파손주의여부 = row.화물파손주의여부,
            바닥회전가능여부 = row.화물바닥회전가능여부, 적층가능여부 = row.화물적층가능여부,
            운송방식 = row.운송방식, 서비스레벨 = row.서비스레벨, 요청사항 = row.요청사항
        };
        errors.AddRange(화물적재입력Policy.계산(loadingInput).오류.Select(error => $"{row.행번호}행: {error}"));
        return errors;
    }

    internal static IReadOnlyList<string> ValidateVehicleRecommendation(int rowNumber, 차량추천응답 recommendation, string selectedVehicle, 차량추천요청 input)
    {
        var expected = 화물적재입력Policy.계산(화물적재입력Policy.입력생성(input));
        if (recommendation.적재조건 is not { 유효여부: true } evidence
            || evidence.계산판본 != 화물적재입력Policy.CurrentRevision || evidence.입력지문 != expected.입력지문
            || !recommendation.후보목록.Any(candidate => candidate.단일운송가능여부
                && string.Equals(candidate.차량종류, selectedVehicle, StringComparison.Ordinal)))
            return [$"{rowNumber}행: 현재 화물 조건에 맞는 추천 차량을 다시 확인해 주세요."];
        return [];
    }

    private static int? ParseRowNumber(string error)
    {
        var index = error.IndexOf('행');
        if (index <= 0)
        {
            return null;
        }

        var raw = error[..index].Trim();
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rowNumber) ? rowNumber : null;
    }

    private static 정산시점 ParseSettlementTime(string? value)
    {
        return Enum.TryParse<정산시점>(value, true, out var parsed) ? parsed : 정산시점.선결제;
    }

    private static 결제수단 ParsePaymentMethod(string? value)
    {
        return Enum.TryParse<결제수단>(value, true, out var parsed) ? parsed : 결제수단.카드;
    }

    private static 증빙방식 ParseEvidenceMethod(string? value)
    {
        return Enum.TryParse<증빙방식>(value, true, out var parsed) ? parsed : 증빙방식.없음;
    }

    private static 수납주체 ParseCollector(string? value)
    {
        return Enum.TryParse<수납주체>(value, true, out var parsed) ? parsed : 수납주체.플랫폼;
    }
}
