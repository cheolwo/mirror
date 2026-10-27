using Ssalddel.Contracts.Admin.BusinessPackages;
using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Driver.Action;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace RoleWorkspacePreview;

/// <summary>실제 제품 adapter/화면의 비교 검토용 합성 자료. 제품 HTTP·DB·외부 배차·지급을 호출하지 않는다.</summary>
public sealed class CargoConditionsPreviewFixture : IRoleWorkspaceApi, IDisposable
{
    private readonly DateTime expires = DateTime.UtcNow.AddHours(1);
    private string? accepted;
    private static readonly string[] Ids = ["demo-mixed", "demo-exclusive", "demo-unknown"];
    public int AcceptPosts { get; private set; }
    public RoleWorkspaceViewModel Model { get; }

    public CargoConditionsPreviewFixture(PreviewAccess access)
    {
        var state = new RoleWorkspaceState();
        state.BindOwner(access.GetIdentity(RoleWorkspaceCatalog.CargoDriver).OwnerId);
        foreach (var role in new[] { RoleWorkspaceCatalog.CargoDriver, RoleWorkspaceCatalog.Shipper, RoleWorkspaceCatalog.Operator })
            state.Save(role, new(null, false, null, ListOnly: true), BusinessPackageCatalog.FreightDelivery);
        Model = new([
            new CargoDriverWorkspaceAdapter(new(this)), new ShipperWorkspaceAdapter(new(this)),
            new CargoOperatorWorkspaceAdapter(this)
        ], access, state);
    }

    public Task<T> GetAsync<T>(string role, string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        object value = path switch
        {
            "api/v1/driver/work/status" => new 기사운행상태응답 { DriverId = "preview-owner", Status = "운행중", ShiftId = 101, 운송실행유형 = Ssalddel.Contracts.Common.Transport.운송실행유형코드.화물운송 },
            "api/v1/driver/operational-dispatch/availability" => new 운영배차수신상태Dto
                { 주체Id = "preview-owner", 수신의사Code = 운영배차수신의사Code.On, 실효상태Code = 운영배차실효상태Code.배차가능, 서버관측시각Utc = DateTimeOffset.UtcNow },
            "api/v1/driver/transports/workspace" => new 기사화물운송작업공간응답
                { 활성운송목록 = accepted is null ? [] : [Transport(accepted)], 다음행동운송 = accepted is null ? null : Transport(accepted) },
            "api/v1/driver/recommendations" => Ids.Where(id => id != accepted).Select(Offer).ToArray(),
            "api/v1/shipper/requests?page=1&pageSize=200" => Ids.Select(Shipper).ToArray(),
            "api/v1/admin/transports?business=freight-delivery" => Ids.Select((id, index) => Admin(id, index)).ToArray(),
            _ when path.StartsWith("api/v1/driver/requests/", StringComparison.Ordinal) => Detail(Id(path)),
            _ when path.StartsWith("api/v1/shipper/requests/", StringComparison.Ordinal) => Shipper(Id(path)),
            _ when path == "api/v1/driver/transports/301" && accepted is not null => Transport(accepted),
            _ => throw new InvalidOperationException("이 화면 예시에서 제공하지 않는 업무입니다.")
        };
        return Task.FromResult((T)value);
    }

    private 기사배차추천항목응답 Offer(string id)
    {
        var index = Array.IndexOf(Ids, id);
        if (index < 0) throw new InvalidOperationException("예시 의뢰를 확인해 주세요.");
        return new()
        {
            의뢰Id = id, 화물종류 = index == 0 ? "포장 상자" : index == 1 ? "가구" : "조건 미확인 물품",
            운송방식 = index == 0 ? "혼적" : index == 1 ? "단독" : null,
            차량종류 = index < 2 ? "1톤 카고" : null, 화물중량Kg = index < 2 ? 150m + index * 400m : null,
            화물수량 = index < 2 ? 12 - index * 10 : null,
            픽업지 = "서울 중랑구 사가정로 · 예시 상차지", 하차지 = index == 1 ? "서울 광진구 능동로 · 예시 하차지" : "서울 동대문구 전농로 · 예시 하차지",
            상차시간창시작일시 = index < 2 ? Utc(index == 0 ? 3 : 14, 0) : null,
            상차시간창종료일시 = index < 2 ? Utc(index == 0 ? 4 : 15, 0) : null,
            하차시간창시작일시 = index < 2 ? Utc(index == 0 ? 4 : 15, 30) : null,
            하차시간창종료일시 = index < 2 ? Utc(index == 0 ? 6 : 17, 0) : null,
            기사지급예정운임 = index == 1 ? 60000 : 30000, 기사지급예정운임근거 = "운임구성.기사지급예정운임",
            운송거리Km = index == 1 ? 12.5m : 6.8m, 추천만료시각 = expires, 추천시작시각 = expires.AddHours(-1),
            차량적합여부 = true, 배차상태 = "추천중"
        };
    }

    private 기사운송의뢰상세응답 Detail(string id)
    {
        var o = Offer(id);
        return new() { 의뢰Id = id, 화물종류 = o.화물종류, 픽업지 = o.픽업지, 하차지 = o.하차지,
            운송방식 = o.운송방식, 차량종류 = o.차량종류, 화물중량Kg = o.화물중량Kg, 화물수량 = o.화물수량,
            상차시간창시작일시 = o.상차시간창시작일시, 상차시간창종료일시 = o.상차시간창종료일시,
            하차시간창시작일시 = o.하차시간창시작일시, 하차시간창종료일시 = o.하차시간창종료일시,
            결제예정금액 = (int)o.기사지급예정운임!.Value + 4000, 차량적합여부 = true, 배차상태 = "추천중" };
    }

    private 화주운송의뢰응답 Shipper(string id)
    {
        var o = Offer(id);
        return new() { 의뢰Id = id, 화물 = new() { 화물종류 = o.화물종류, 수량 = o.화물수량, 중량Kg = o.화물중량Kg },
            운송방식 = o.운송방식 ?? "", 차량종류 = o.차량종류 ?? "", 픽업지 = o.픽업지, 하차지 = o.하차지,
            픽업 = new() { 시간창 = o.상차시간창시작일시 is { } ps && o.상차시간창종료일시 is { } pe ? new() { 시작일시 = ps, 종료일시 = pe } : null },
            하차 = new() { 시간창 = o.하차시간창시작일시 is { } ds && o.하차시간창종료일시 is { } de ? new() { 시작일시 = ds, 종료일시 = de } : null },
            최종운임 = o.기사지급예정운임 + 4000, 의뢰상태 = "접수", 운송상태 = "배차대기", 배차상태 = "배차대기" };
    }

    private 운송진행응답 Admin(string id, int index)
    {
        var o = Offer(id);
        return new() { Id = 101 + index, 운송번호 = id, 상태 = "운송중", 기사_운송자 = "예시 기사", 출발지 = o.픽업지, 도착지 = o.하차지,
            운임 = o.기사지급예정운임 + 4000, 운송방식 = o.운송방식, 차량종류 = o.차량종류, 화물중량Kg = o.화물중량Kg, 화물수량 = o.화물수량,
            상차시간창시작일시 = o.상차시간창시작일시, 상차시간창종료일시 = o.상차시간창종료일시,
            하차시간창시작일시 = o.하차시간창시작일시, 하차시간창종료일시 = o.하차시간창종료일시, UpdatedAt = DateTime.UtcNow };
    }

    private 기사운송상세응답 Transport(string id)
    {
        var o = Offer(id);
        return new() { Id = 301, 운송번호 = id, 상태 = "상차지도착", 출발지 = o.픽업지, 도착지 = o.하차지,
            기사지급예정액 = o.기사지급예정운임, 기사지급예정액근거 = "운송원장.기사지급예정액",
            운송방식 = o.운송방식, 차량종류 = o.차량종류, 화물중량Kg = o.화물중량Kg, 화물수량 = o.화물수량,
            상차시간창시작일시 = o.상차시간창시작일시, 상차시간창종료일시 = o.상차시간창종료일시,
            하차시간창시작일시 = o.하차시간창시작일시, 하차시간창종료일시 = o.하차시간창종료일시 };
    }

    public Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (role != RoleWorkspaceCatalog.CargoDriver || !path.StartsWith("api/v1/driver/dispatch-actions/", StringComparison.Ordinal) || !path.EndsWith("/accept", StringComparison.Ordinal))
            throw new InvalidOperationException("이 화면 예시는 실제 업무를 저장하지 않습니다.");
        accepted = path.Split('/')[4];
        _ = Offer(accepted); AcceptPosts++;
        return Task.FromResult<T?>((T)(object)new 기사배차처리응답 { RequestId = accepted });
    }
    public Task<T?> PutAsync<T>(string role, string path, object body, CancellationToken ct) => throw new InvalidOperationException("실제 업무 저장 없음");
    public Task<T?> UploadAsync<T>(string role, string path, HttpContent body, CancellationToken ct) => throw new InvalidOperationException("실제 사진 저장 없음");
    private static string Id(string path) => path[(path.LastIndexOf('/') + 1)..];
    private static DateTime Utc(int hour, int minute) => new(2026, 10, 24, hour, minute, 0, DateTimeKind.Utc);
    public void Dispose() => Model.Dispose();
}
