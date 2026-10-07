using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoWorkspaceProjectionR16Tests
{
    [Theory]
    [InlineData("배차확정", "다음 상차지", "상차 주소", "arrive-pickup")]
    [InlineData("상차지도착", "다음 상차지", "상차 주소", "pickup")]
    [InlineData("운송중", "다음 하차지", "하차 주소", "arrive-dropoff")]
    [InlineData("하차지도착", "다음 하차지", "하차 주소", "dropoff")]
    public void 현재_운송의_목적지_운임_거리와_행동은_동일_단계를_표시한다(string state, string label, string address, string action)
    {
        var source = Detail(12, state);
        var item = CargoDriverWorkspaceAdapter.Map(source, true);

        Assert.NotNull(item.Summary);
        Assert.Equal(label, item.Summary.DestinationLabel);
        Assert.Equal(address, item.Summary.Destination);
        Assert.Contains(item.Summary.Metrics!, field => field.Label == "운임" && field.Value == "45,000원");
        Assert.Contains(item.Summary.Metrics!, field => field.Label == "운송 거리" && field.Value == "8.25 km");
        Assert.Contains(item.Summary.Metrics!, field => field.Label == "거리 기준" && field.Value == "확인된 견적 거리");
        Assert.Equal(action, Assert.Single(item.Actions!.Where(x => x.IsPrimary)).Key);
        Assert.Equal(2, item.Markers!.Count);
        Assert.Contains("실제 주행 경로 아님", Assert.Single(item.Routes!).Notice);
        Assert.DoesNotContain(item.Summary.Metrics!, field => field.Value.Contains("010-", StringComparison.Ordinal));
        Assert.DoesNotContain("담당자", item.Summary.Destination, StringComparison.Ordinal);
        if (state is "운송중" or "하차지도착") Assert.Equal("1층에서 확인", item.Summary.Request!.Value);
    }

    [Fact]
    public void 현재_운송_좌표가_없어도_주소와_미확인_거리를_표시하며_핀을_만들지_않는다()
    {
        var item = CargoDriverWorkspaceAdapter.Map(new 기사운송상세응답 { Id = 1, 상태 = "상차지도착", 출발지 = "상차 주소" });
        Assert.Null(item.Markers);
        Assert.Equal("상차 주소", item.Summary!.Destination);
        Assert.Contains(item.Summary.Metrics!, field => field.Label == "운송 거리" && field.Value == "미확인");
        Assert.Contains(item.Summary.Metrics!, field => field.Label == "운임" && field.Value == "미확정");
    }

    [Theory]
    [InlineData("운송완료")]
    [InlineData("취소")]
    public void 종료된_운송은_지도_핀을_남기지_않고_상세_정보는_보존한다(string state)
    {
        var item = CargoDriverWorkspaceAdapter.Map(Detail(12, state));
        Assert.Null(item.Markers); Assert.Empty(item.Routes!);
        Assert.NotNull(item.Summary); Assert.NotEmpty(item.Sections!); Assert.Empty(item.Actions!);
    }

    [Fact]
    public void 정보_제공_보류는_기본_카드와_핀과_연락처를_모두_제외한다()
    {
        var source = Detail(12, "운송중"); source.개인정보제공보류 = true;
        var item = CargoDriverWorkspaceAdapter.Map(source);
        Assert.Null(item.Summary); Assert.Null(item.Markers); Assert.Null(item.Actions);
        Assert.DoesNotContain(item.Sections!.SelectMany(section => section.Fields), field => field.Value.Contains("주소", StringComparison.Ordinal) || field.Value.Contains("010-", StringComparison.Ordinal));
    }

    [Fact]
    public void 화주_기본_카드는_물품_수량_운임과_위치_시각을_보여주고_기존_10분_만료를_보존한다()
    {
        var measured = DateTime.UtcNow.AddMinutes(-2);
        var item = ShipperWorkspaceAdapter.Map(new()
        {
            의뢰Id = "r", 운송상태 = "운송중", 픽업지 = "상차 주소", 하차지 = "하차 주소",
            화물 = new() { 화물종류 = "식품 상자", 수량 = 4 }, 최종운임 = 45000m,
            확정기사Id = "driver", 기사최근위도 = 37.58m, 기사최근경도 = 127.02m, 기사최근위치시각Utc = measured
        });
        Assert.Equal("하차 주소", item.Summary!.Destination);
        Assert.Contains(item.Summary.Metrics!, field => field.Value == "4개");
        Assert.Contains("최신 위치", item.Summary.Request!.Value);
        var marker = Assert.Single(item.Markers!);
        Assert.Equal(new DateTimeOffset(measured), marker.MeasuredAt);
        Assert.Equal(new DateTimeOffset(measured.AddMinutes(10)), marker.ExpiresAt);
        Assert.Null(marker.ReceivedAt); // 클라이언트의 변환 시각을 원천 위치 수신 시각으로 만들지 않는다.
    }

    [Theory]
    [InlineData(-11, "운송중", "위치 갱신 지연")]
    [InlineData(5, "운송중", "위치 시각 확인 필요")]
    [InlineData(0, "인수완료", "운송 종료 후 비공개")]
    public void 지연_미래_종료_기사_위치는_핀으로_표시하지_않는다(int minutes, string state, string message)
    {
        var item = ShipperWorkspaceAdapter.Map(new()
        {
            의뢰Id = "r", 운송상태 = state, 확정기사Id = "driver", 기사최근위도 = 37m, 기사최근경도 = 127m,
            기사최근위치시각Utc = DateTime.UtcNow.AddMinutes(minutes)
        });
        Assert.Empty(item.Markers!); Assert.Contains(message, item.Summary!.Request!.Value);
    }

    [Fact]
    public void 화주_위치의_작은_미래시각도_현재_기사_위치로_승격하지_않는다()
    {
        var item = ShipperWorkspaceAdapter.Map(new()
        {
            의뢰Id = "r", 운송상태 = "운송중", 확정기사Id = "driver", 기사최근위도 = 37m, 기사최근경도 = 127m,
            기사최근위치시각Utc = DateTime.UtcNow.AddSeconds(5)
        });
        Assert.Empty(item.Markers!);
        Assert.Contains("위치 시각 확인 필요", item.Summary!.Request!.Value);
    }

    [Fact]
    public void 추천_거리는_실제_응답의_기준을_따르고_후보선_만료_시각을_전달한다()
    {
        var expiry = DateTime.UtcNow.AddMinutes(5);
        var item = CargoDriverWorkspaceAdapter.MapOffer(new()
        {
            의뢰Id = "r", 픽업지 = "상차 주소", 하차지 = "하차 주소", 직선거리Km = 4.2m,
            픽업_위도 = 37m, 픽업_경도 = 127m, 하차_위도 = 38m, 하차_경도 = 128m, 추천만료시각 = expiry
        });
        Assert.Contains(item.Summary!.Metrics!, field => field.Label == "거리 기준" && field.Value == "직선 거리");
        Assert.Contains(item.Summary.Metrics!, field => field.Label == "운임" && field.Value == "미확정");
        Assert.All(item.Markers!, marker => Assert.Equal(new DateTimeOffset(expiry), marker.ExpiresAt));
        Assert.Equal(new DateTimeOffset(expiry), Assert.Single(item.Routes!).ExpiresAt);
        Assert.Equal("cargo-straight-candidate", Assert.Single(item.Routes!).SourceCode);
    }

    [Fact]
    public async Task 완료_선택에서_다음_운송으로_바뀔_때_첫_조회부터_상세_카드와_좌표를_맞춘다()
    {
        var api = new Api
        {
            Read = path => path.EndsWith("workspace", StringComparison.Ordinal)
                ? new 기사화물운송작업공간응답 { 활성운송목록 = [new() { Id = 2, 상태 = "운송중" }], 다음행동운송 = new() { Id = 2, 상태 = "운송중" } }
                : path.EndsWith("/1", StringComparison.Ordinal) ? Detail(1, "운송완료") : Detail(2, "운송중")
        };
        var result = await new CargoDriverWorkspaceAdapter(new(api)).LoadAsync("1", default);
        Assert.Equal("2", result.SelectedId);
        var current = Assert.Single(result.Items.Where(item => item.IsCurrent));
        Assert.Equal("2", current.Id); Assert.Equal(2, current.Markers!.Count);
        Assert.Equal("하차 주소", current.Summary!.Destination);
        Assert.Contains(result.Items, item => item.Id == "1" && item.Status == "운송완료" && !item.IsCurrent);
        Assert.Contains(api.Reads, path => path.EndsWith("/2", StringComparison.Ordinal));
        Assert.DoesNotContain(api.Reads, path => path.Contains("/driver/requests/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 완료_이력_보기는_다음_운송이_없을_때_선택을_보존한다()
    {
        var api = new Api { Read = path => path.EndsWith("workspace", StringComparison.Ordinal) ? new 기사화물운송작업공간응답()
            : path.EndsWith("recommendations", StringComparison.Ordinal) ? Array.Empty<기사배차추천항목응답>() : Detail(1, "운송완료") };
        var result = await new CargoDriverWorkspaceAdapter(new(api)).LoadAsync("1", default);
        Assert.Equal("1", result.SelectedId); Assert.Single(result.Items);
    }

    [Fact]
    public async Task 창고_기본_카드는_야외_창고와_실내_보관위치를_구분한다()
    {
        var api = WarehouseApi();
        var result = await new WarehouseWorkspaceAdapter(new(api)).LoadAsync("inbound:2", default);
        var item = Assert.Single(result.Items);
        Assert.Equal("동네 창고 · 창고 주소", item.Summary!.Destination);
        Assert.Contains(item.Summary.Metrics!, field => field.Label == "예정 수량" && field.Value == "3개");
        Assert.Contains(item.Summary.Metrics!, field => field.Label == "보관 위치" && field.Value == "미확인");
        Assert.Equal("작업 창고", item.Summary.DestinationLabel);
        Assert.Single(item.Markers!); Assert.Null(item.Routes);
        Assert.Contains("/tasks/inbound/2", Assert.Single(item.Actions!).Route);
    }

    [Fact]
    public void 피킹_기본_카드는_선반과_수량을_표시하고_지도상_실내_경로를_만들지_않는다()
    {
        var item = WarehouseWorkspaceAdapter.MapPicking(new() { TaskKey = "batch-7", ProductName = "상자", Quantity = 5, RackCode = "A-3", Status = 피킹작업조회상태코드.진행중 });
        Assert.Contains(item.Summary!.Metrics!, field => field.Label == "보관 위치" && field.Value == "A-3");
        Assert.Contains(item.Summary.Metrics!, field => field.Label == "수량" && field.Value == "5개");
        Assert.Null(item.Routes);
    }

    [Fact]
    public async Task 창고_완료_선택은_다음_활성_작업_상세로_전환하고_이력을_보존한다()
    {
        var api = WarehouseApi();
        var previous = api.Read;
        api.Read = path => path.EndsWith("/1", StringComparison.Ordinal)
            ? new 입고요청항목응답 { Id = 1, 창고Id = 9, 상태 = 입고상태코드.완료, 예정상품명 = "완료 물품" } : previous(path);
        var result = await new WarehouseWorkspaceAdapter(new(api)).LoadAsync("inbound:1", default);
        Assert.Equal("inbound:2", result.SelectedId);
        var current = Assert.Single(result.Items.Where(item => item.IsCurrent));
        Assert.Equal("inbound:2", current.Id); Assert.Equal("동네 창고 · 창고 주소", current.Summary!.Destination);
        Assert.Contains(result.Items, item => item.Id == "inbound:1" && !item.IsCurrent);
        Assert.Contains(api.Reads, path => path.EndsWith("/2", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("put-away", true)]
    [InlineData("put-away", false)]
    [InlineData("packing", true)]
    [InlineData("packing", false)]
    [InlineData("handoff", true)]
    [InlineData("handoff", false)]
    public async Task 창고_공정의_행동이_비활성이어도_완료_근거가_있을_때만_다음_업무로_전환한다(string kind, bool completed)
    {
        var stamp = completed ? DateTime.UtcNow : (DateTime?)null;
        var api = new Api
        {
            AllowedProcess = _ => true,
            Read = path => path.EndsWith("warehouses", StringComparison.Ordinal) ? new 창고목록응답()
                : path.EndsWith("/1", StringComparison.Ordinal) ? kind switch
                {
                    "put-away" => new 적재작업상세응답 { InboundItemId = 1, InventoryStatus = "입고완료", CanPutAway = false, PutAwayAtUtc = stamp },
                    "packing" => new 포장작업상세응답 { InboundItemId = 1, InventoryStatus = "적재완료", CanPack = false, PackedAtUtc = stamp },
                    _ => new 출고인계준비상세응답 { InboundItemId = 1, OutboundStatus = "준비중", CanConfirmHandoff = false, HandoffReadyAtUtc = stamp }
                }
                : path.EndsWith("/2", StringComparison.Ordinal) ? new 입고요청항목응답 { Id = 2, 상태 = 입고상태코드.예정 }
                : path.EndsWith("inbounds", StringComparison.Ordinal) ? new 입고요청목록응답 { Items = [new() { Id = 2, 상태 = 입고상태코드.예정 }] }
                : path.Contains("put-away-tasks?", StringComparison.Ordinal) ? new 적재작업목록페이지응답()
                : path.Contains("picking-tasks?", StringComparison.Ordinal) ? new 피킹작업목록페이지응답()
                : path.Contains("packing-tasks?", StringComparison.Ordinal) ? new 포장작업목록페이지응답()
                : path.Contains("inspection-targets?", StringComparison.Ordinal) ? new 입고검수대상페이지응답()
                : path.Contains("outbound-plan-reviews?", StringComparison.Ordinal) ? new 출고예정검토목록페이지응답()
                : new 출고인계준비목록페이지응답()
        };
        var result = await new WarehouseWorkspaceAdapter(new(api)).LoadAsync(kind + ":1", default);
        Assert.Equal(completed ? "inbound:2" : kind + ":1", result.SelectedId);
        Assert.Contains(result.Items, item => item.Id == kind + ":1");
        Assert.Equal(completed, api.Reads.Any(path => path.EndsWith("/2", StringComparison.Ordinal)));
    }

    private static 기사운송상세응답 Detail(long id, string state) => new()
    {
        Id = id, 상태 = state, 출발지 = "상차 주소", 도착지 = "하차 주소", 운임 = 45000m, 예상거리Km = 8.25m,
        거리계산방식 = "확인된 견적 거리", 상차담당자명 = "담당자", 상차연락처 = "010-0000-1001", 전달요청 = "1층에서 확인",
        픽업위도 = 37.57m, 픽업경도 = 127.01m, 하차위도 = 37.59m, 하차경도 = 127.04m
    };

    private static Api WarehouseApi() => new()
    {
        Read = path => path.EndsWith("warehouses", StringComparison.Ordinal) ? new 창고목록응답 { Items = [new() { Id = 9, IsActive = true, 창고명 = "동네 창고", 주소 = "창고 주소", 위도 = 37m, 경도 = 127m }] }
            : path.Contains("inspection-targets?", StringComparison.Ordinal) ? new 입고검수대상페이지응답()
            : path.EndsWith("/2", StringComparison.Ordinal) ? new 입고요청항목응답 { Id = 2, 창고Id = 9, 상태 = 입고상태코드.예정, 예정상품명 = "상자", 예정수량 = 3 }
            : new 입고요청목록응답 { Items = [new() { Id = 2, 창고Id = 9, 상태 = 입고상태코드.예정, 예정상품명 = "상자", 예정수량 = 3 }] }
    };

    private sealed class Api : IRoleWorkspaceApi
    {
        public Func<string, object> Read { get; set; } = _ => throw new InvalidOperationException("예상하지 않은 조회");
        public Func<string, bool> AllowedProcess { get; set; } = process => process == "inbound";
        public List<string> Reads { get; } = [];
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
        { Reads.Add(path); return Task.FromResult((T)Read(path)); }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken ct)
        {
            if (body is not 창고작업진입확인요청 gate) throw new InvalidOperationException("예상하지 않은 변경");
            return Task.FromResult((T?)(object)new 창고작업진입확인응답 { IsAllowed = AllowedProcess(gate.ProcessCode) });
        }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken ct) => throw new NotSupportedException();
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken ct) => throw new NotSupportedException();
    }
}
