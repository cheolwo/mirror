using Hongdal.Services.LogisticsProcessing.VehicleLoading;
using 홍달.도메인.화물;
using 홍달.도메인.차량;
using 홍달.도메인.화주;

namespace 홍달.Services.Dispatch.Recommendation
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

            if (차량 is null)
            {
                return new 차량화물적합성결과(
                    true,
                    ["기사 차량 제원이 등록되지 않아 적재 조건을 검증하지 못했습니다."],
                    []);
            }

            var loadingRequirement = BuildLoadingRequirement(request, 요구조건);
            var evaluation = _engine.평가(차량, loadingRequirement);
            var warnings = evaluation.검증경고.ToList();
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
                reasons);
        }

        private static 차량적재추천요구사항 BuildLoadingRequirement(
            화주운송의뢰 request,
            화물요구조건? cargo)
        {
            var text = string.Join(' ', new[]
                {
                    request.운송방식,
                    request.서비스레벨,
                    request.요청사항,
                    request.화물종류,
                    request.화물설명
                }
                .Where(x => !string.IsNullOrWhiteSpace(x))!);

            var length = cargo?.화물길이Mm ?? request.화물길이Mm;
            var width = cargo?.화물폭Mm ?? request.화물폭Mm;
            var height = cargo?.화물높이Mm ?? request.화물높이Mm;
            var packages = new List<차량적재포장요구사항>();
            if (length.HasValue || width.HasValue || height.HasValue)
            {
                packages.Add(new 차량적재포장요구사항
                {
                    항목키 = request.의뢰Id,
                    항목명 = string.IsNullOrWhiteSpace(request.화물종류) ? "화물" : request.화물종류,
                    포장개수 = Math.Max(1, request.화물수량.GetValueOrDefault(1)),
                    포장길이Mm = length.GetValueOrDefault(),
                    포장폭Mm = width.GetValueOrDefault(),
                    포장높이Mm = height.GetValueOrDefault(),
                    바닥회전가능여부 = true,
                    적층가능여부 = true
                });
            }

            var temperature = request.화물온도조건;
            if (cargo?.냉동필요 == true)
            {
                temperature = "냉동";
            }
            else if (cargo?.냉장필요 == true
                     && !string.Equals(temperature, "냉동", StringComparison.OrdinalIgnoreCase))
            {
                temperature = "냉장";
            }

            return new 차량적재추천요구사항
            {
                총중량Kg = cargo?.화물무게Kg ?? request.화물중량Kg,
                총부피Cbm = request.화물부피Cbm,
                총팔레트개수 = cargo?.팔레트개수 ?? request.화물팔레트개수,
                온도조건 = temperature,
                비눈보호필요 = cargo?.비맞으면안됨 == true
                                   || text.Contains("비", StringComparison.OrdinalIgnoreCase)
                                   || text.Contains("방수", StringComparison.OrdinalIgnoreCase),
                리프트필요 = cargo?.리프트필요 == true
                               || text.Contains("리프트", StringComparison.OrdinalIgnoreCase),
                측면상하차필요 = cargo?.측면상하차필요 == true
                                     || text.Contains("측면", StringComparison.OrdinalIgnoreCase),
                장재물 = cargo?.장재물 == true
                          || text.Contains("장재물", StringComparison.OrdinalIgnoreCase),
                분할운송허용 = false,
                포장목록 = packages
            };
        }

        private static bool ContainsDispatchKeyword(화주운송의뢰 request, string keyword)
            => new[] { request.운송방식, request.서비스레벨, request.요청사항, request.화물설명 }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Any(x => x.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    public sealed record 차량화물적합성결과(bool 적합여부, string[] 경고, string[] 부적합사유);
}
