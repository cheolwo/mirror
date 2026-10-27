using System.Globalization;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

public static class CargoNoticeNavigation
{
    public const string Public = "/community/cargo-notices";
    public const string Mine = "/shipper/cargo-notices";
    public static string Login => RoleWorkspaceNavigation.WithReturn("/workspace-login/shipper", Mine)!;
}

public static class CargoNoticePresentation
{
    private static readonly Regex PrivateText = new(
        @"(?:\+?82[-.\s]?)?0?1[016789][-.\s]?\d{3,4}[-.\s]?\d{4}|0[2-6]\d?[-.\s]?\d{3,4}[-.\s]?\d{4}|\d{6}[-\s]?[1-4]\d{6}|[^\s@]+@[^\s@]+\.[^\s@]+|https?://|www\.|[가-힣A-Za-z0-9]+(?:대로|로|길)\s*\d+|\d+\s*(?:동|호|층)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    // Same representative-format preflight as the server, not a guarantee to detect all personal data.
    public static string PublicText(string value)
    {
        var text = value.Normalize(NormalizationForm.FormKC).Trim();
        if (text.Length is < 1 or > 80 || text.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format)
            || PrivateText.IsMatch(text))
            throw new ArgumentException("제목·화물명은 80자 이내로 입력하고 연락처·상세주소·식별번호·링크를 빼 주세요.");
        return text;
    }
    public static string Status(string status) => status switch
    { CargoNoticeStatus.Draft => "초안", CargoNoticeStatus.Published => "게시 중", CargoNoticeStatus.Withdrawn => "철회", CargoNoticeStatus.Expired => "만료", _ => "상태 확인 필요" };
    public static string Vehicle(string value) => value switch
    { CargoNoticeVehicleKinds.Motorcycle => "오토바이", CargoNoticeVehicleKinds.Van => "승합·소형 밴", CargoNoticeVehicleKinds.Truck => "화물차", _ => "선택 안 함" };
    public static string Time(DateTimeOffset? value) => value?.ToOffset(TimeSpan.FromHours(9)).ToString("MM.dd HH:mm", CultureInfo.InvariantCulture) ?? "지정 안 함";
    public static string InputTime(DateTimeOffset? value) => value?.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) ?? "";
    public static DateTimeOffset? ParseTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            throw new ArgumentException("날짜와 시간을 다시 확인해 주세요.");
        return new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).ToUniversalTime();
    }
    public static bool Disabled(Exception error) => error is SsalddelApiException { ErrorCode: "CargoNotice.FeatureDisabled" };
    public static string Error(Exception error) => error is SsalddelApiException api ? api.StatusCode switch
    {
        401 => "로그인이 만료됐습니다. 다시 로그인한 뒤 확인해 주세요.",
        403 => "이 공고를 관리할 권한이 없습니다.",
        404 => "공고를 찾을 수 없거나 제공되지 않는 기능입니다.",
        409 => "공고가 다른 화면에서 변경됐습니다. 최신 내용을 다시 확인해 주세요.",
        400 or 422 => "입력 내용과 게시 동의·만료 시간을 확인해 주세요.",
        _ => "서버와 연결하지 못했습니다. 잠시 후 다시 확인해 주세요."
    } : "처리 결과를 확인하지 못했습니다. 연결을 확인하고 다시 조회해 주세요.";
    public static CargoNoticeContent Copy(CargoNoticeContent source) => new()
    {
        Title = source.Title, GoodsName = source.GoodsName, Quantity = source.Quantity, WeightKg = source.WeightKg,
        VehicleKind = source.VehicleKind, PickupRegionKey = source.PickupRegionKey, DropoffRegionKey = source.DropoffRegionKey,
        DesiredFromUtc = source.DesiredFromUtc, DesiredUntilUtc = source.DesiredUntilUtc
    };
    public static string Key(CargoNoticeContent content) => JsonSerializer.Serialize(content);
}

/// <summary>현재 입력값만 보존하며 게시·소유권·완료 상태는 서버가 소유합니다.</summary>
public sealed class CargoNoticeForm
{
    public string Title { get; set; } = "";
    public string GoodsName { get; set; } = "";
    public string Quantity { get; set; } = "1";
    public string WeightKg { get; set; } = "";
    public string VehicleKind { get; set; } = CargoNoticeVehicleKinds.Unspecified;
    public string PickupRegionKey { get; set; } = "";
    public string DropoffRegionKey { get; set; } = "";
    public string DesiredFromLocal { get; set; } = "";
    public string DesiredUntilLocal { get; set; } = "";
    public CargoNoticeContent Content()
    {
        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(GoodsName)
            || string.IsNullOrWhiteSpace(PickupRegionKey) || string.IsNullOrWhiteSpace(DropoffRegionKey)
            || !int.TryParse(Quantity, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count <= 0
            || !CargoNoticeVehicleKinds.IsSupported(VehicleKind))
            throw new ArgumentException("제목·화물명·수량·출발 및 도착 지역을 확인해 주세요.");
        decimal? weight = null;
        if (!string.IsNullOrWhiteSpace(WeightKg))
        {
            if (!decimal.TryParse(WeightKg, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
                throw new ArgumentException("중량은 0보다 큰 숫자로 입력해 주세요.");
            weight = parsed;
        }
        var from = CargoNoticePresentation.ParseTime(DesiredFromLocal);
        var until = CargoNoticePresentation.ParseTime(DesiredUntilLocal);
        if (from.HasValue != until.HasValue || from.HasValue && from >= until)
            throw new ArgumentException("희망 시간을 입력한다면 시작과 끝을 모두 확인해 주세요.");
        return new() { Title = CargoNoticePresentation.PublicText(Title), GoodsName = CargoNoticePresentation.PublicText(GoodsName), Quantity = count, WeightKg = weight,
            VehicleKind = VehicleKind, PickupRegionKey = PickupRegionKey, DropoffRegionKey = DropoffRegionKey,
            DesiredFromUtc = from, DesiredUntilUtc = until };
    }
    public static CargoNoticeForm From(CargoNoticeContent c) => new()
    { Title = c.Title, GoodsName = c.GoodsName, Quantity = c.Quantity.ToString(CultureInfo.InvariantCulture),
      WeightKg = c.WeightKg?.ToString(CultureInfo.InvariantCulture) ?? "", VehicleKind = c.VehicleKind,
      PickupRegionKey = c.PickupRegionKey, DropoffRegionKey = c.DropoffRegionKey,
      DesiredFromLocal = CargoNoticePresentation.InputTime(c.DesiredFromUtc), DesiredUntilLocal = CargoNoticePresentation.InputTime(c.DesiredUntilUtc) };
}

public sealed class CargoNoticeDraftSession
{
    internal string? OwnerScope { get; set; }
    internal CargoNoticeForm Form { get; set; } = new();
    internal string? SelectedNoticeId { get; set; }
}
