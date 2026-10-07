using System.Globalization;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

public sealed class ShipperWorkspaceAdapter(ShipperWorkspaceApiClient client) : IRoleWorkspaceAdapter
{
    public string RoleKey => RoleWorkspaceCatalog.Shipper;

    public async Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
    {
        var source = (await client.ListAsync(cancellationToken)).ToList();
        var selected = string.IsNullOrWhiteSpace(selectedId)
            ? source.FirstOrDefault(item => !CargoWorkspacePresentation.IsClosed(item.운송상태)
                && !CargoWorkspacePresentation.IsClosed(item.의뢰상태))?.의뢰Id ?? source.FirstOrDefault()?.의뢰Id
            : selectedId.Trim();
        // URL의 선택 ID도 서버의 진행조회 권한을 새로 확인한다.
        if (selected is not null)
        {
            var detail = await client.GetAsync(selected, cancellationToken);
            if (!string.Equals(detail.의뢰Id, selected, StringComparison.Ordinal))
                throw new InvalidOperationException("조회한 운송 의뢰가 선택한 업무와 일치하지 않습니다.");
            source.RemoveAll(item => item.의뢰Id == selected);
            source.Insert(0, detail);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(RoleKey, source.Select(item => Map(item, item.의뢰Id == selected)).ToArray(),
            source.Count == 200 ? "최근 운송 의뢰 200건입니다. 이전 의뢰는 운송 내역에서 확인해 주세요." : null,
            selected, [new("create", "운송 의뢰 등록", "/shipper/request?from=%2Fworkspace%2Fshipper", IsPrimary: true, RequiresConfirmation: false)]);
    }

    public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
        => throw new InvalidOperationException("운송 의뢰의 입력과 지급 확인은 해당 업무 화면에서 진행해 주세요.");
    public void Clear() { } // 개인 DTO를 adapter에 보관하지 않는다.

    public static RoleWorkspaceItem Map(화주운송의뢰응답 source, bool current = false)
    {
        var presentation = ShipperRequestDetailSnapshot.FromContract(source);
        var markers = CargoWorkspacePresentation.LocationMarkers(source.의뢰Id,
            source.픽업위도 ?? source.픽업?.주소?.위도, source.픽업경도 ?? source.픽업?.주소?.경도,
            source.하차위도 ?? source.하차?.주소?.위도, source.하차경도 ?? source.하차?.주소?.경도);
        var ended = CargoWorkspacePresentation.IsClosed(source.운송상태) || CargoWorkspacePresentation.IsClosed(source.의뢰상태);
        var locationText = ended ? "운송 종료 후 비공개" : ShipperRequestDetailPresentation.ResolveDriverLocationFreshness(presentation);
        var now = DateTimeOffset.UtcNow;
        if (!ended
            && !string.IsNullOrWhiteSpace(source.확정기사Id)
            && source.기사최근위치시각Utc is { } measured
            && measured.ToUniversalTime() <= now.UtcDateTime
            && !ShipperRequestDetailPresentation.IsDriverLocationStale(presentation)
            && CargoWorkspacePresentation.Point(source.기사최근위도, source.기사최근경도) is { } driver)
        {
            var measuredAt = new DateTimeOffset(measured.ToUniversalTime());
            markers.Add(new(source.의뢰Id + "|driver", "운송 기사", NeighborhoodMapMarkerKinds.Driver,
                driver.Latitude, driver.Longitude, MeasuredAt: measuredAt,
                ExpiresAt: measuredAt.Add(ShipperRequestDetailPresentation.DriverLocationFreshnessWindow)));
            locationText += " · " + measuredAt.ToOffset(TimeSpan.FromHours(9)).ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
        }
        else if (!ended && source.기사최근위치시각Utc?.ToUniversalTime() > now.UtcDateTime)
            locationText = "위치 시각 확인 필요";
        var sections = new List<RoleWorkspaceSection>
        {
            CargoWorkspacePresentation.Section("운송", ("상차지", source.픽업지), ("하차지", source.하차지),
                ("배차", source.배차상태), ("현재 운송", source.운송상태)),
            CargoWorkspacePresentation.Section("화물과 운임", ("화물", source.화물?.화물종류 ?? source.요약?.화물종류),
                ("운임", CargoWorkspacePresentation.Money(source.최종운임 ?? source.결제예정금액)),
                ("결제", source.결제상태), ("정산", source.정산상태))
        };
        if (!string.IsNullOrWhiteSpace(source.확정기사명))
            sections.Add(CargoWorkspacePresentation.Section("배정 기사", ("기사", source.확정기사명),
                ("차량", source.확정기사차량), ("위치 확인", locationText)));
        if (source.비정상운송검토보류중)
            sections.Add(CargoWorkspacePresentation.Section("확인 필요", ("운송", "운송 예외를 확인하는 중입니다. 운송 내역에서 진행 상황을 확인해 주세요.")));
        if (markers.Count == 0)
            sections.Add(CargoWorkspacePresentation.Section("지도", ("위치", "확인된 위치가 없어 목록으로 표시합니다.")));
        var context = new ShipperRequestDetailNavigationContext { ReturnPath = CargoWorkspaceRoutes.ReturnTo(RoleWorkspaceCatalog.Shipper, source.의뢰Id) };
        return new(source.의뢰Id, CargoWorkspacePresentation.Display(source.화물?.화물종류 ?? source.요약?.화물종류, "운송 의뢰"),
            CargoWorkspacePresentation.Display(source.운송상태, source.의뢰상태), source.의뢰Id,
            sections,
            [new("timeline", "운송 내역", context.PathFor(ShipperRequestDetailScreenKind.Timeline, source.의뢰Id), IsPrimary: true, RequiresConfirmation: false),
             new("payment", "결제·정산 확인", context.PathFor(ShipperRequestDetailScreenKind.Payment, source.의뢰Id), RequiresConfirmation: false),
             new("proofs", "인수증·증빙", context.PathFor(ShipperRequestDetailScreenKind.Proofs, source.의뢰Id), RequiresConfirmation: false)],
            markers, CargoWorkspacePresentation.CandidateRoutes(source.의뢰Id, markers), !ended,
            new(ended ? "하차지" : CargoWorkspacePresentation.CanArriveDropoff(source.운송상태) || source.운송상태 == "하차지도착" ? "현재 하차지" : "상차지",
                CargoWorkspacePresentation.Display(CargoWorkspacePresentation.CanArriveDropoff(source.운송상태) || source.운송상태 == "하차지도착" || ended ? source.하차지 : source.픽업지),
                [new("물품", CargoWorkspacePresentation.Display(source.화물?.화물종류 ?? source.요약?.화물종류)),
                 new("수량", source.화물?.수량 is { } quantity ? quantity.ToString("N0", CultureInfo.GetCultureInfo("ko-KR")) + "개" : "미확인"),
                 new("운임", CargoWorkspacePresentation.Money(source.최종운임 ?? source.결제예정금액))],
                CargoIncidentPresentation.Notice(source) ?? new("기사 위치", locationText)));
    }
}

public sealed class CargoDriverWorkspaceAdapter(CargoDriverWorkspaceApiClient client) : IRoleWorkspaceAdapter
{
    public string RoleKey => RoleWorkspaceCatalog.CargoDriver;

    public async Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
    {
        var workspace = await client.WorkspaceAsync(cancellationToken);
        var source = workspace.활성운송목록.ToList();
        var selected = string.IsNullOrWhiteSpace(selectedId)
            ? CurrentTransportId(workspace, source)
            : selectedId.Trim();
        if (selected?.StartsWith("offer:", StringComparison.Ordinal) == true && CurrentTransportId(workspace, source) is { } activeId)
            selected = activeId;
        if (selected is not null && !selected.StartsWith("offer:", StringComparison.Ordinal))
        {
            var id = long.Parse(CargoWorkspaceRoutes.PositiveNumber(selected), CultureInfo.InvariantCulture);
            var detail = await client.TransportAsync(id, cancellationToken);
            CargoWorkspacePresentation.RequireTransport(detail, id);
            source.RemoveAll(item => item.Id == id);
            source.Insert(0, detail);
            // 완료 이력은 남기되 현재 수행할 운송이 있으면 그 상세와 지도 좌표를 같은 조회에 함께 가져온다.
            if (CargoWorkspacePresentation.IsClosed(detail.상태)
                && CurrentTransportId(workspace, source) is { } next && next != selected)
            {
                selected = next;
                var nextId = long.Parse(CargoWorkspaceRoutes.PositiveNumber(next), CultureInfo.InvariantCulture);
                var nextDetail = await client.TransportAsync(nextId, cancellationToken);
                CargoWorkspacePresentation.RequireTransport(nextDetail, nextId);
                source.RemoveAll(item => item.Id == nextId);
                source.Insert(0, nextDetail);
            }
        }
        var items = source.Select(item => Map(item, item.Id.ToString(CultureInfo.InvariantCulture) == selected)).ToList();
        // 진행 업무가 있으면 현재 운송에 집중한다. 추천은 대기 상태에서만 기본 표시한다.
        if (source.All(item => CargoWorkspacePresentation.IsClosed(item.상태)) || selected?.StartsWith("offer:", StringComparison.Ordinal) == true)
        {
            var offers = await client.RecommendationsAsync(cancellationToken);
            items.AddRange(offers.Select(MapOffer));
            if (selected?.StartsWith("offer:", StringComparison.Ordinal) == true && items.All(item => item.Id != selected))
                throw new InvalidOperationException("추천 운송이 만료되었거나 현재 계정에 제공된 후보가 아닙니다.");
            selected ??= items.FirstOrDefault()?.Id;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(RoleKey, items, items.Count == 0 ? "현재 운송과 추천 운송이 없습니다." : null, selected);
    }

    private static string? CurrentTransportId(기사화물운송작업공간응답 workspace, IReadOnlyList<기사운송요약응답> items)
    {
        var preferred = workspace.다음행동운송 is { } next
            ? items.FirstOrDefault(item => item.Id == next.Id && !CargoWorkspacePresentation.IsClosed(item.상태)) : null;
        return (preferred ?? items.FirstOrDefault(item => !CargoWorkspacePresentation.IsClosed(item.상태)))?.Id.ToString(CultureInfo.InvariantCulture);
    }

    public async Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("업무 확인 식별자가 없습니다.", nameof(requestId));
        var id = long.Parse(CargoWorkspaceRoutes.PositiveNumber(itemId), CultureInfo.InvariantCulture);
        var current = await client.TransportAsync(id, cancellationToken);
        CargoWorkspacePresentation.RequireTransport(current, id);
        if (current.개인정보제공보류 || !CargoWorkspacePresentation.CanPerform(current, actionKey))
            throw new InvalidOperationException("운송 예외와 업무 권한을 먼저 확인해 주세요.");
        var allowed = actionKey switch
        {
            "arrive-pickup" => CargoWorkspacePresentation.CanArrivePickup(current.상태),
            "arrive-dropoff" => CargoWorkspacePresentation.CanArriveDropoff(current.상태),
            _ => throw new InvalidOperationException("입력이 필요한 업무는 해당 화면에서 진행해 주세요.")
        };
        if (!allowed) throw new InvalidOperationException("현재 운송 단계에서는 이 행동을 처리할 수 없습니다. 새로고침해 주세요.");
        var response = await client.ArriveAsync(id, actionKey == "arrive-pickup", cancellationToken)
            ?? throw new InvalidOperationException("도착 처리 응답이 없습니다. 운송 상태를 다시 확인해 주세요.");
        if (response.Id != id) throw new InvalidOperationException("도착 처리 대상이 선택한 운송과 일치하지 않습니다.");
    }
    public void Clear() { }

    public static RoleWorkspaceItem Map(기사운송요약응답 source, bool current = false)
    {
        var id = CargoWorkspaceRoutes.Number(source.Id);
        if (source.개인정보제공보류)
            return new(id, "화물 운송", "접근 확인 필요", Sections:
                [CargoWorkspacePresentation.Section("업무 정보", ("안내", "현재 계정과 운송 정보의 제공 조건을 다시 확인해 주세요."))], IsCurrent: current);
        var pickup = CargoWorkspacePresentation.CanArrivePickup(source.상태) || source.상태 == "상차지도착";
        var ended = CargoWorkspacePresentation.IsClosed(source.상태);
        List<NeighborhoodMapMarker> markers = source is 기사운송상세응답 detail && !ended
            ? CargoWorkspacePresentation.LocationMarkers(id, detail.픽업위도, detail.픽업경도, detail.하차위도, detail.하차경도) : [];
        var sections = new List<RoleWorkspaceSection>
        {
            CargoWorkspacePresentation.Section(pickup ? "상차지" : "하차지",
                ("주소", pickup ? source.출발지 : source.도착지),
                ("시간", CargoWorkspacePresentation.TimeWindow(pickup ? source.상차시간창시작일시 : source.하차시간창시작일시,
                    pickup ? source.상차시간창종료일시 : source.하차시간창종료일시)),
                ("담당자", pickup ? source.상차담당자명 : source.수령자명),
                ("연락처", pickup ? source.상차연락처 : source.수령자연락처)),
            CargoWorkspacePresentation.Section("운송 정보", ("운임", CargoWorkspacePresentation.Money(source.운임)),
                ("거리", CargoWorkspacePresentation.Distance(source.예상거리Km)), ("거리 기준", source.거리계산방식),
                ("전달 요청", source.전달요청))
        };
        sections.Add(CargoWorkspacePresentation.Section("지도", ("위치", ended ? "종료된 운송은 지도에 표시하지 않습니다."
            : markers.Count == 0 ? "현재 운송의 확인된 좌표가 없어 주소로 확인해 주세요."
            : "확인된 상하차 장소 · 직선 후보선은 실제 주행 경로가 아닙니다.")));
        if (source.예외신고됨 || source.관리자확인필요 || source.운송진행보류)
            sections.Add(CargoWorkspacePresentation.Section("확인 필요", ("안내", source.다음행동안내),
                ("운송 예외", source.최근예외메시지),
                ("진행", source.운송진행보류 ? "전체 운송 보류" : "현재 허용된 업무 진행"),
                ("정산", source.정산보류 ? "운영 검토로 보류" : null)));
        foreach (var incident in source.예외검토목록)
            sections.Add(CargoWorkspacePresentation.Section("예외 처리 상태",
                ("대상", incident.예외Code), ("검토", CargoWorkspacePresentation.IncidentStatus(incident.상태Code)),
                ("담당", incident.현재담당Code == "PlatformOperationsReview" ? "운영 담당자" : "담당 확인 필요"),
                ("범위", CargoWorkspacePresentation.HoldScope(incident.업무통제상태Code)),
                ("최근 변경", CargoWorkspacePresentation.Time(incident.UpdatedAt))));
        var actions = new List<RoleWorkspaceAction>();
        var blocked = source.운송진행보류 || (source.가능한행동 is null && source.관리자확인필요);
        if (CargoWorkspacePresentation.CanArrivePickup(source.상태))
            actions.Add(new("arrive-pickup", "상차지 도착", IsPrimary: true, Enabled: CargoWorkspacePresentation.CanPerform(source, "arrive-pickup"), DisabledReason: "현재 운송 단계와 예외 처리 상태를 확인해 주세요."));
        else if (source.상태 == "상차지도착")
            actions.Add(new("pickup", "상차 확인", CargoWorkspaceRoutes.DriverInput(source.Id, "pickup"), IsPrimary: true,
                Enabled: CargoWorkspacePresentation.CanPerform(source, "pickup"), DisabledReason: "현재 운송 단계와 예외 처리 상태를 확인해 주세요.", RequiresConfirmation: false));
        else if (CargoWorkspacePresentation.CanArriveDropoff(source.상태))
            actions.Add(new("arrive-dropoff", "하차지 도착", IsPrimary: true, Enabled: CargoWorkspacePresentation.CanPerform(source, "arrive-dropoff"), DisabledReason: "현재 운송 단계와 예외 처리 상태를 확인해 주세요."));
        else if (source.상태 == "하차지도착")
            actions.Add(new("dropoff", "하차 확인", CargoWorkspaceRoutes.DriverInput(source.Id, "dropoff"), IsPrimary: true,
                Enabled: CargoWorkspacePresentation.CanPerform(source, "dropoff"), DisabledReason: "현재 운송 단계와 예외 처리 상태를 확인해 주세요.", RequiresConfirmation: false));
        if (!CargoWorkspacePresentation.IsClosed(source.상태))
            actions.Add(new("issue", "문제 신고", CargoWorkspaceRoutes.DriverInput(source.Id, "issue"),
                Enabled: CargoWorkspacePresentation.CanPerform(source, "issue"), DisabledReason: "현재 신고 권한을 확인해 주세요.", RequiresConfirmation: false));
        return new(id, "운송 " + CargoWorkspacePresentation.Display(source.운송번호, id), source.상태,
            ended ? "운송 종료" : pickup ? "상차 준비" : "전달 진행", sections, actions,
            Markers: markers.Count == 0 ? null : markers,
            Routes: CargoWorkspacePresentation.CandidateRoutes(id, markers), IsCurrent: current,
            Summary: new(pickup ? "다음 상차지" : ended ? "하차지" : "다음 하차지",
                CargoWorkspacePresentation.Display(pickup ? source.출발지 : source.도착지),
                new RoleWorkspaceField[] { new("운임", CargoWorkspacePresentation.Money(source.운임)),
                 new("운송 거리", CargoWorkspacePresentation.Distance(source.예상거리Km)),
                 new("거리 기준", CargoWorkspacePresentation.Display(source.거리계산방식)) }
                    .Concat(CargoWorkspacePresentation.TaskRequirements(source, pickup, ended)).ToArray(),
                source.예외신고됨 || source.관리자확인필요 || blocked ? new("확인 필요", CargoWorkspacePresentation.Display(source.다음행동안내, "운송 예외 처리 상태를 확인해 주세요."))
                    : !pickup && !ended ? new("전달 요청", CargoWorkspacePresentation.Display(source.전달요청)) : null));
    }

    public static RoleWorkspaceItem MapOffer(기사배차추천항목응답 source)
    {
        var id = "offer:" + source.의뢰Id;
        var markers = CargoWorkspacePresentation.LocationMarkers(id, source.픽업_위도, source.픽업_경도, source.하차_위도, source.하차_경도);
        var expiry = source.추천만료시각 is { } expires ? new DateTimeOffset(expires.ToUniversalTime()) : (DateTimeOffset?)null;
        if (expiry.HasValue) markers = markers.Select(marker => marker with { ExpiresAt = expiry }).ToList();
        return new(id, CargoWorkspacePresentation.Display(source.화물종류, "추천 운송"), "추천", source.운송의뢰유형표시,
            [CargoWorkspacePresentation.Section("운송 조건", ("상차지", source.픽업지), ("하차지", source.하차지),
                ("운송 거리", CargoWorkspacePresentation.Distance(source.운송거리Km ?? source.주행거리Km ?? source.직선거리Km))),
             CargoWorkspacePresentation.Section("참여 전 확인", ("차량", source.차량적합여부 ? "적합" : string.Join(" · ", source.차량부적합사유)),
                ("안내", string.Join(" · ", source.경고.Concat(source.일정위반사유)))),
             CargoWorkspacePresentation.Section("지도", ("경로", "직선 후보선이며 실제 주행 경로가 아닙니다."))],
            [new("offer", "추천 조건 확인", CargoWorkspaceRoutes.Offer(source.의뢰Id), IsPrimary: true, RequiresConfirmation: false)],
            markers, CargoWorkspacePresentation.CandidateRoutes(id, markers).Select(route => route with { ExpiresAt = expiry }).ToArray(),
            Summary: new("상차지", CargoWorkspacePresentation.Display(source.픽업지),
                [new("운임", "미확정"),
                 new("운송 거리", CargoWorkspacePresentation.Distance(source.운송거리Km ?? source.주행거리Km ?? source.직선거리Km)),
                 new("거리 기준", source.운송거리Km.HasValue ? "운송 거리" : source.주행거리Km.HasValue ? "주행 거리" : source.직선거리Km.HasValue ? "직선 거리" : "미확인")],
                new("하차지", CargoWorkspacePresentation.Display(source.하차지))));
    }
}

public static class CargoWorkspacePresentation
{
    public static string Display(string? value, string? fallback = "미확인")
        => string.IsNullOrWhiteSpace(value) ? string.IsNullOrWhiteSpace(fallback) ? "미확인" : fallback : value.Trim();
    public static string Money(decimal? value) => value.HasValue ? value.Value.ToString("N0", CultureInfo.GetCultureInfo("ko-KR")) + "원" : "미확정";
    public static string Distance(decimal? value) => value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) + " km" : "미확인";
    public static RoleWorkspaceSection Section(string title, params (string Label, string? Value)[] fields)
        => new(title, fields.Where(field => !string.IsNullOrWhiteSpace(field.Value)).Select(field => new RoleWorkspaceField(field.Label, field.Value!)).ToArray());
    public static bool IsClosed(string? value) => value is "완료" or "운송완료" or "인수완료" or "하차완료" or "배송완료" or "취소" or "운송취소" or "의뢰취소";
    public static bool CanArrivePickup(string? value) => value is "배차대기" or "배차확정" or "확정" or "매칭중" or "이동중";
    public static bool CanArriveDropoff(string? value) => value is "상차완료" or "운송중";
    public static bool CanPerform(기사운송요약응답 source, string key)
        => !source.개인정보제공보류 && (key == "issue"
            ? !IsClosed(source.상태) && (source.가능한행동?.Contains(key, StringComparer.Ordinal) ?? true)
            : !source.운송진행보류 && (source.가능한행동?.Contains(key, StringComparer.Ordinal)
                ?? (!source.관리자확인필요 && (key switch
                {
                    "arrive-pickup" => CanArrivePickup(source.상태), "pickup" => source.상태 == "상차지도착",
                    "arrive-dropoff" => CanArriveDropoff(source.상태), "dropoff" => source.상태 == "하차지도착", _ => false
                }))));
    public static string? TimeWindow(DateTime? start, DateTime? end)
    {
        return start.HasValue ? Time(start.Value) + (end.HasValue ? " ~ " + Time(end.Value) : " 이후")
            : end.HasValue ? Time(end.Value) + "까지" : null;
    }
    public static string Time(DateTime value) => (value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value)
        .AddHours(9).ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
    public static IEnumerable<RoleWorkspaceField> TaskRequirements(기사운송요약응답 source, bool pickup, bool ended)
    {
        if (ended) yield break;
        if (TimeWindow(pickup ? source.상차시간창시작일시 : source.하차시간창시작일시,
                pickup ? source.상차시간창종료일시 : source.하차시간창종료일시) is { } window)
            yield return new(pickup ? "상차 시간" : "하차 시간", window);
        if (pickup && source.상태 == "상차지도착")
            yield return new("필수 확인", source.인수증서명필수 ? "상차 사진 · 인수증 · 서명" : source.인수증필요 ? "상차 사진 · 인수증" : "상차 사진");
        else if (source.상태 == "하차지도착") yield return new("필수 확인", "하차 사진 · 수령 확인");
    }
    public static string IncidentStatus(string code) => code switch
    { "OperationsReviewPending" => "운영 검토 중", "ActionDecided" => "조치 결정", "Closed" => "검토 종료", _ => "처리 상태 확인 필요" };
    public static string HoldScope(string code) => code switch
    { "ContinueWithCaution" => "현장 조건 확인 후 진행", "PartiallyHeld" => "영향 수량 보류", "FullyHeld" => "전체 운송 보류", "Resumed" => "운송 재개", "Closed" => "종료", _ => "통제 범위 확인 필요" };
    public static void RequireTransport(기사운송요약응답? source, long id)
    {
        if (source is null || source.Id != id) throw new InvalidOperationException("운송 정보가 선택한 업무와 일치하지 않습니다.");
    }
    public static NeighborhoodMapPoint? Point(decimal? latitude, decimal? longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180
            ? new((double)latitude.Value, (double)longitude.Value) : null;
    public static List<NeighborhoodMapMarker> LocationMarkers(string id, decimal? pickupLat, decimal? pickupLng, decimal? dropoffLat, decimal? dropoffLng)
    {
        var markers = new List<NeighborhoodMapMarker>();
        if (Point(pickupLat, pickupLng) is { } pickup)
            markers.Add(new(id + "|pickup", "상차지", NeighborhoodMapMarkerKinds.Pickup, pickup.Latitude, pickup.Longitude));
        if (Point(dropoffLat, dropoffLng) is { } dropoff)
            markers.Add(new(id + "|dropoff", "하차지", NeighborhoodMapMarkerKinds.Dropoff, dropoff.Latitude, dropoff.Longitude));
        return markers;
    }
    public static IReadOnlyList<NeighborhoodMapRoute> CandidateRoutes(string id, IReadOnlyList<NeighborhoodMapMarker> markers)
    {
        var pickup = markers.FirstOrDefault(item => item.Kind == NeighborhoodMapMarkerKinds.Pickup);
        var dropoff = markers.FirstOrDefault(item => item.Kind == NeighborhoodMapMarkerKinds.Dropoff);
        return pickup is null || dropoff is null ? [] :
            [new(id + "|candidate", [new(pickup.Latitude, pickup.Longitude), new(dropoff.Latitude, dropoff.Longitude)],
                "#64748b", "직선 후보선 · 실제 주행 경로 아님", "cargo-straight-candidate")];
    }
}
