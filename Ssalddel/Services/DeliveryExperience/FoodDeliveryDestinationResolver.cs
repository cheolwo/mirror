using System.Text.RegularExpressions;
using Ssalddel.Contracts.Common.PublicData;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Services.External.PublicData;
using 살뜰.도메인.음식;

namespace Ssalddel.Services.DeliveryExperience;

public sealed class 음식배달전달지역Resolver(IRoadAddressLookupService roads, IApartmentComplexLookupService apartments,
    TimeProvider clock, ILogger<음식배달전달지역Resolver> logger)
{
    public async Task<음식배달전달지역Snapshot> 확인Async(string baseAddress,
        FoodDeliveryDestinationIdentityRequest? selected, CancellationToken ct)
    {
        var snapshot = new 음식배달전달지역Snapshot { VerifiedAtUtc = clock.GetUtcNow().UtcDateTime };
        if (selected is null || string.IsNullOrWhiteSpace(baseAddress)) return snapshot;
        if (string.IsNullOrWhiteSpace(selected.BuildingManagementNumber) || string.IsNullOrWhiteSpace(selected.RoadNameCode)
            || selected.BuildingManagementNumber.Length > 80 || selected.RoadNameCode.Length > 80
            || selected.ApartmentComplexCode?.Length > 80) throw new 음식주문입력확인Exception("선택한 주소 식별자를 확인해 주세요.", "DestinationIdentityInvalid");
        PublicDataLookupResponse<RoadAddressItem> response;
        try { response = await roads.SearchAsync(new() { Keyword = baseAddress, PageSize = 30 }, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("도로 경험 식별자 확인 보류. ErrorType={ErrorType}", ex.GetType().Name);
            return snapshot;
        }
        if (!response.Success) return snapshot; // API 연결 실패는 미확인입니다. 주문은 기존처럼 진행합니다.
        var matched = response.Items.Where(x => x.BuildingManagementNo == selected.BuildingManagementNumber
            && x.RoadNameManagementNo == selected.RoadNameCode && 주소같음(x.RoadAddress, baseAddress)).ToArray();
        if (matched.Length != 1 || string.IsNullOrWhiteSpace(selected.RoadNameCode)
            || string.IsNullOrWhiteSpace(selected.BuildingManagementNumber))
            throw new 음식주문입력확인Exception("선택한 공식 주소와 수령지 기본주소가 달라 다시 선택해 주세요.", "DestinationIdentityMismatch");
        var roadName = 공개도로명(matched[0].RoadAddress);
        if (roadName is not null)
        {
            snapshot.도로Id = selected.RoadNameCode;
            snapshot.도로표시명 = roadName;
            snapshot.확인상태Code = "Confirmed";
        }
        // 단지와 도로는 서로 다른 공식 자료로 검증합니다. 단지 이름/코드를 주소 문자열에서 추정하지 않습니다.
        try
        {
            if (!string.IsNullOrWhiteSpace(selected.ApartmentComplexCode))
            {
                var basic = await apartments.GetBasicInfoAsync(new() { ComplexCode = selected.ApartmentComplexCode }, ct);
                var apt = basic.Success ? basic.Items.SingleOrDefault(x => x.ComplexCode == selected.ApartmentComplexCode
                    && !string.IsNullOrWhiteSpace(x.ComplexName) && 주소같음(x.RoadAddress, matched[0].RoadAddress)) : null;
                if (apt is not null) { snapshot.단지Id = apt.ComplexCode; snapshot.단지표시명 = apt.ComplexName; }
            }
            else if (snapshot.도로표시명 is not null)
            {
                var aptResponse = await apartments.SearchAsync(new()
                {
                    RoadName = snapshot.도로표시명.Split(' ').Last(),
                    SigunguCode = matched[0].AdministrativeCode is { Length: >= 5 } code ? code[..5] : null,
                    PageSize = 30
                }, ct);
                var exact = aptResponse.Success ? aptResponse.Items.Where(x => 주소같음(x.RoadAddress, matched[0].RoadAddress)
                    && !string.IsNullOrWhiteSpace(x.ComplexCode) && !string.IsNullOrWhiteSpace(x.ComplexName)).ToArray() : [];
                if (exact.Length == 1) { snapshot.단지Id = exact[0].ComplexCode; snapshot.단지표시명 = exact[0].ComplexName; }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        { logger.LogWarning("단지 경험 식별자 확인 보류. ErrorType={ErrorType}", ex.GetType().Name); }
        return snapshot;
    }

    public static string? 공개도로명(string officialAddress)
    {
        var tokens = Regex.Replace(officialAddress, @"\([^)]*\)", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var index = Array.FindIndex(tokens, x => Regex.IsMatch(x, @"(?:대로|로|길)$"));
        // 공식 도로명 토큰까지로 제한. 상세 번지나 고객 발화는 표시명에 넣지 않습니다.
        return index is >= 1 and <= 4 ? string.Join(' ', tokens.Take(index + 1)) : null;
    }
    private static bool 주소같음(string? first, string? second)
        => !string.IsNullOrWhiteSpace(first) && !string.IsNullOrWhiteSpace(second)
            && Regex.Replace(Regex.Replace(first, @"\([^)]*\)", ""), @"\s", "")
                == Regex.Replace(Regex.Replace(second, @"\([^)]*\)", ""), @"\s", "");
}
