using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.도메인.화물;
using 살뜰.도메인.차량;
using 살뜰.도메인.화주;

namespace 살뜰.Services.Dispatch.Recommendation
{
    public interface I차량화물적합성Service
    {
        차량화물적합성결과 판정(차량제원? 차량, 화주운송의뢰 request, 화물요구조건? 요구조건);
    }

    public sealed class 차량화물적합성Service : I차량화물적합성Service
    {
        private readonly 차량적재추천Engine _engine;

        public 차량화물적합성Service(차량적재추천Engine engine)
        {
            _engine = engine;
        }

        public 차량화물적합성결과 판정(
            차량제원? 차량,
            화주운송의뢰 request,
            화물요구조건? 요구조건)
        {
            ArgumentNullException.ThrowIfNull(request);

            var normalized = NormalizeLoadingInput(request, 요구조건);
            if (차량 is null)
            {
                return new 차량화물적합성결과(
                    false,
                    normalized.경고.Append("기사 차량 제원이 등록되지 않아 적재 조건을 검증하지 못했습니다.").ToArray(),
                    normalized.오류.Append("검증 불가: 기사 차량 제원을 등록한 뒤 적재 조건을 확인해야 합니다.").ToArray())
                { 적재조건 = normalized };
            }
            if (!normalized.유효여부)
                return new 차량화물적합성결과(false, normalized.경고.ToArray(), normalized.오류.ToArray())
                { 적재조건 = normalized };

            var loadingRequirement = BuildLoadingRequirement(request, 요구조건, normalized);
            var evaluation = _engine.평가(차량, loadingRequirement);
            var warnings = normalized.경고.Concat(evaluation.검증경고).ToList();
            if (요구조건 is { 혼적허용: false }
                || 요구조건?.독차필수 == true
                || ContainsDispatchKeyword(request, "단독"))
            {
                warnings.Add("단독 배차 선호 조건입니다.");
            }

            var reasons = evaluation.하드부적합사유
                .Concat(evaluation.단일운송불가사유)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return new 차량화물적합성결과(
                evaluation.단일운송가능여부,
                warnings.Distinct(StringComparer.Ordinal).ToArray(),
                reasons) { 적재조건 = normalized };
        }

        internal static 화물적재입력결과 NormalizeLoadingInput(화주운송의뢰 request, 화물요구조건? cargo)
        {
            var input = new 화물적재입력
            {
                화물종류 = request.화물종류, 설명 = request.화물설명, 수량 = request.화물수량,
                길이Mm = cargo?.화물길이Mm ?? request.화물길이Mm,
                폭Mm = cargo?.화물폭Mm ?? request.화물폭Mm,
                높이Mm = cargo?.화물높이Mm ?? request.화물높이Mm,
                // 전문 원장의 정수 kg는 접수 총중량을 올림한 투영값이므로 원장 decimal을 우선한다.
                총중량Kg = request.화물중량Kg ?? cargo?.화물무게Kg,
                입력부피Cbm = request.화물입력부피Cbm,
                팔레트개수 = cargo?.팔레트개수 ?? request.화물팔레트개수,
                온도조건 = cargo?.냉동필요 == true ? "냉동"
                    : cargo?.냉장필요 == true && !string.Equals(request.화물온도조건, "냉동", StringComparison.OrdinalIgnoreCase)
                        ? "냉장" : request.화물온도조건,
                바닥회전가능여부 = request.화물바닥회전가능여부,
                적층가능여부 = request.화물적층가능여부,
                파손주의여부 = request.화물파손주의여부,
                운송방식 = request.운송방식, 서비스레벨 = request.서비스레벨, 요청사항 = request.요청사항
            };
            return 화물적재입력Policy.저장값적용(화물적재입력Policy.계산(input), request.화물부피Cbm,
                request.화물부피근거Code, request.화물적재계산판본);
        }

        internal static 차량적재추천요구사항 BuildLoadingRequirement(
            화주운송의뢰 request,
            화물요구조건? cargo,
            화물적재입력결과 normalized)
        {
            var input = normalized.입력;
            var length = input.길이Mm;
            var width = input.폭Mm;
            var height = input.높이Mm;
            var packages = new List<차량적재포장요구사항>();
            if (length.HasValue || width.HasValue || height.HasValue)
            {
                packages.Add(new 차량적재포장요구사항
                {
                    항목키 = request.의뢰Id,
                    항목명 = string.IsNullOrWhiteSpace(request.화물종류) ? "화물" : request.화물종류,
                    포장개수 = normalized.적용수량,
                    포장길이Mm = length.GetValueOrDefault(),
                    포장폭Mm = width.GetValueOrDefault(),
                    포장높이Mm = height.GetValueOrDefault(),
                    바닥회전가능여부 = normalized.바닥회전가능여부,
                    적층가능여부 = normalized.적층가능여부
                });
            }

            return new 차량적재추천요구사항
            {
                총중량Kg = input.총중량Kg,
                총부피Cbm = normalized.유효부피Cbm,
                적층불가바닥면적M2 = normalized.비적층바닥면적M2,
                총팔레트개수 = input.팔레트개수,
                온도조건 = input.온도조건,
                비눈보호필요 = cargo?.비맞으면안됨 == true
                                   || 화물적재입력Policy.요청문구포함(input, "비")
                                   || 화물적재입력Policy.요청문구포함(input, "방수"),
                리프트필요 = cargo?.리프트필요 == true
                               || 화물적재입력Policy.요청문구포함(input, "리프트"),
                측면상하차필요 = cargo?.측면상하차필요 == true
                                     || 화물적재입력Policy.요청문구포함(input, "측면"),
                장재물 = cargo?.장재물 == true
                          || 화물적재입력Policy.요청문구포함(input, "장재물"),
                분할운송허용 = false,
                포장목록 = packages
            };
        }

        private static bool ContainsDispatchKeyword(화주운송의뢰 request, string keyword)
            => new[] { request.운송방식, request.서비스레벨, request.요청사항, request.화물설명 }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Any(x => x.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    public sealed record 차량화물적합성결과(bool 적합여부, string[] 경고, string[] 부적합사유)
    {
        public 화물적재입력결과? 적재조건 { get; init; }
    }
}
