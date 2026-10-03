using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace SsalddelApp.Models.Shipper;

/// <summary>작성 화면의 한국시간을 원장 UTC 시간창으로 바꾸고 개인정보 객체의 편집 사본을 만듭니다.</summary>
public static class ShipperRequestHandoffMapper
{
    public static LocationContactDTO CreatePickup(운송모델작성Draft draft)
        => Create(draft.픽업도로명주소, draft.픽업상세주소, draft.픽업연락처이름,
            draft.픽업연락처전화번호, draft.픽업시간창시작일시, draft.픽업시간창종료일시);

    public static LocationContactDTO CreateDropoff(운송모델작성Draft draft)
        => Create(draft.하차도로명주소, draft.하차상세주소, draft.하차연락처이름,
            draft.하차연락처전화번호, draft.하차시간창시작일시, draft.하차시간창종료일시);

    public static LocationContactDTO? Copy(LocationContactDTO? source, string? roadAddress = null)
    {
        if (source is null && roadAddress is null) return null;
        return new LocationContactDTO
        {
            주소 = new AddressDTO
            {
                도로명주소 = roadAddress ?? source?.주소?.도로명주소 ?? string.Empty,
                상세주소 = source?.주소?.상세주소,
                위도 = source?.주소?.위도,
                경도 = source?.주소?.경도
            },
            연락처 = new ContactDTO
            {
                이름 = source?.연락처?.이름 ?? string.Empty,
                전화번호 = source?.연락처?.전화번호 ?? string.Empty
            },
            시간창 = source?.시간창 is { } window
                ? new TimeWindowDTO { 시작일시 = window.시작일시, 종료일시 = window.종료일시 }
                : null
        };
    }

    private static LocationContactDTO Create(string address, string? detail, string name, string phone, DateTime? start, DateTime? end)
        => new()
        {
            주소 = new AddressDTO { 도로명주소 = address, 상세주소 = detail },
            연락처 = new ContactDTO { 이름 = name, 전화번호 = phone },
            시간창 = ToUtcWindow(start, end)
        };

    private static TimeWindowDTO? ToUtcWindow(DateTime? start, DateTime? end)
    {
        if (!start.HasValue && !end.HasValue) return null;
        if (!start.HasValue || !end.HasValue || end.Value <= start.Value)
            throw new ArgumentException("시간창의 시작과 종료 일시를 함께 입력하고 종료를 시작보다 늦게 설정해 주세요.");
        return new TimeWindowDTO { 시작일시 = ToUtc(start.Value), 종료일시 = ToUtc(end.Value) };
    }

    private static DateTime ToUtc(DateTime koreanTime)
        => new DateTimeOffset(DateTime.SpecifyKind(koreanTime, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).UtcDateTime;
}
