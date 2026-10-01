using Hongdal.Contracts.Common.AgriculturalFisheries;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public interface I미국농어업경영체정보원천Service
{
    미국농어업경영체정보원천조회응답 Search(
        미국농어업경영체정보원천조회요청 request);
}

public sealed class 미국농어업경영체정보원천Service
    : I미국농어업경영체정보원천Service
{
    public 미국농어업경영체정보원천조회응답 Search(
        미국농어업경영체정보원천조회요청 request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var searchText = NullIfWhiteSpace(request.SearchText);
        var sectorCode = NullIfWhiteSpace(request.SectorCode);
        var recordTypeCode = NullIfWhiteSpace(request.RecordTypeCode);
        var publicAccessCode = NullIfWhiteSpace(request.PublicAccessCode);
        var integrationStatusCode = NullIfWhiteSpace(request.IntegrationStatusCode);

        var allSources = UnitedStatesAgriculturalFisheriesOperatorSourceCatalog.Sources;
        var filtered = allSources
            .Where(item => MatchesSearch(item, searchText))
            .Where(item => HasCode(item.SectorCodes, sectorCode))
            .Where(item => Matches(item.RecordTypeCode, recordTypeCode))
            .Where(item => Matches(item.PublicAccessCode, publicAccessCode))
            .Where(item => Matches(item.IntegrationStatusCode, integrationStatusCode))
            .ToArray();

        var skip = (long)(page - 1) * pageSize;
        var items = skip >= filtered.LongLength
            ? Array.Empty<미국농어업경영체정보원천항목>()
            : filtered.Skip((int)skip).Take(pageSize).ToArray();

        return new 미국농어업경영체정보원천조회응답
        {
            SnapshotReviewedOn =
                UnitedStatesAgriculturalFisheriesOperatorSourceCatalog.SnapshotReviewedOn,
            HasUnifiedPublicOperatorRegistry = false,
            Summary = "미국에는 한국의 농어업경영체 등록정보와 같은 단일 공개 명부가 없습니다. 개별 행정·통계 기록은 대체로 비공개이고, 인증·검사·자발적 등재·지역 허가 목적별 공개 원천만 구분해 사용할 수 있습니다.",
            Page = page,
            PageSize = pageSize,
            TotalCount = filtered.Length,
            AvailableSectorCodes = DistinctCodes(allSources.SelectMany(item => item.SectorCodes)),
            AvailableRecordTypeCodes = DistinctCodes(allSources.Select(item => item.RecordTypeCode)),
            AvailablePublicAccessCodes = DistinctCodes(allSources.Select(item => item.PublicAccessCode)),
            AvailableIntegrationStatusCodes = DistinctCodes(allSources.Select(item => item.IntegrationStatusCode)),
            Notices =
            [
                "공개 명부의 등재는 해당 프로그램의 인증·검사·허가 또는 자발적 등록 상태만 뜻하며 거래 수행 권한이나 플랫폼 제휴를 보증하지 않습니다.",
                "사업자 후보는 최신 공식 원천에서 다시 확인하고, 당사자의 동의를 받은 뒤 플랫폼 계정과 연결해야 합니다.",
                "개인 이름·자택 주소·연락처·농장 좌표·선박 위치는 공개 페이지에 있더라도 목적 없이 복제하거나 자동 초대에 사용하지 않습니다.",
                "주별 농업·양식·어업 허가는 별도 체계이므로 실제 업무 전 해당 주와 품목의 규정을 추가 확인해야 합니다."
            ],
            Items = Array.AsReadOnly(items)
        };
    }

    private static bool MatchesSearch(
        미국농어업경영체정보원천항목 item,
        string? searchText)
        => searchText is null
           || item.SourceKey.Contains(searchText, StringComparison.OrdinalIgnoreCase)
           || item.AgencyName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
           || item.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
           || item.GeographicScope.Contains(searchText, StringComparison.OrdinalIgnoreCase)
           || item.SectorCodes.Any(code =>
               code.Contains(searchText, StringComparison.OrdinalIgnoreCase))
           || item.PublicFieldExamples.Any(field =>
               field.Contains(searchText, StringComparison.OrdinalIgnoreCase));

    private static bool HasCode(IReadOnlyList<string> codes, string? requiredCode)
        => requiredCode is null
           || codes.Contains(requiredCode, StringComparer.OrdinalIgnoreCase);

    private static bool Matches(string value, string? requiredValue)
        => requiredValue is null
           || string.Equals(value, requiredValue, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> DistinctCodes(IEnumerable<string> codes)
        => Array.AsReadOnly(codes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray());

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
