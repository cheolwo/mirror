using System.Net;
using Ssalddel.Contracts.Admin.Management;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using SsalddelAdmin.Services;

namespace RoleWorkspacePreview;

/// <summary>검토 호스트 전용 합성 입력. 실제 계정·측정·주문·저장소를 사용하지 않는다.</summary>
public sealed class CargoInputRefreshR77PreviewFixture : IDisposable
{
    public 운송의뢰작성ViewModel Shipper { get; } = new();
    public NeighborhoodDeliveryRequest Package { get; } = new();
    public 기사실차적재확인ViewModel? Actual { get; private set; }
    public async Task PrepareAsync(string area, string scenario)
    {
        Shipper.상차작업시간분 = scenario == "empty" ? null : scenario == "negative" ? -1m : 0m;
        Shipper.하차작업시간분 = scenario == "empty" ? null : 6.5m;
        Shipper.상차시간창시작일시 = new(2026, 10, 20, 10, 0, 0);
        Shipper.상차시간창종료일시 = new(2026, 10, 20, 11, 0, 0);
        Shipper.하차시간창시작일시 = new(2026, 10, 20, 11, 0, 0);
        Shipper.하차시간창종료일시 = new(2026, 10, 20, 13, 0, 0);
        Package.Quantity = 2; Package.WeightKg = 8m;
        Package.PackageLengthMm = scenario == "empty" ? null : 400;
        Package.PackageWidthMm = scenario == "empty" ? null : 300;
        Package.PackageHeightMm = scenario == "empty" ? null : 200;
        Package.PalletCount = scenario == "empty" ? null : 0;
        Package.FloorRotationAllowed = scenario == "empty" ? null : false;
        Package.StackingAllowed = scenario == "empty" ? null : false;
        if (area == "vehicle")
        {
            Actual?.Dispose();
            Actual = new(new ActualClient(scenario), "r77-synthetic-driver");
            await Actual.LoadAsync();
        }
    }
    public void Dispose() => Actual?.Dispose();
    private sealed class ActualClient(string scenario) : I기사실차적재확인Client
    {
        private 기사실차적재제원응답 record = new("r77-synthetic-driver", 1,
            scenario == "empty" ? null : new()
            {
                ActualVehicleReference = "r77-synthetic-vehicle", VehicleProfileValue = "가상 검토 차량",
                PrivateEvidenceReference = "synthetic-r77-no-real-measurement", ReviewerId = "r77-synthetic-reviewer",
                ConfirmedAtUtc = DateTime.UtcNow.AddDays(-1), ValidUntilUtc = DateTime.UtcNow.AddDays(1),
                적재함길이Mm = 1200, 적재함폭Mm = 1000, 적재함높이Mm = 1000, 최대적재중량Kg = 300,
                최대부피Cbm = 1.2m, 팔레트적재개수 = 0, EntranceKind = "Enclosed", 비눈보호가능 = true,
                냉장가능 = false, 냉동가능 = false, 리프트가능 = false
            }) { 현재실차참조 = "r77-synthetic-vehicle", 현재차량종류 = "가상 검토 차량" };
        public Task<기사실차적재제원응답> 실차조회Async(string driverId, CancellationToken ct = default)
            => scenario == "error" ? throw new HttpRequestException("synthetic", null, HttpStatusCode.ServiceUnavailable) : Task.FromResult(record);
        public Task<기사실차적재제원응답> 실차수정Async(string driverId, 기사실차적재제원수정요청 payload, CancellationToken ct = default)
        {
            if (scenario == "conflict" || payload.ExpectedRevision != record.판본)
                throw new HttpRequestException("synthetic", null, HttpStatusCode.Conflict);
            record = record with { 판본 = (record.판본 ?? 0) + 1, 제원 = payload.제원 with { ReviewerId = "r77-synthetic-reviewer" } };
            return Task.FromResult(record);
        }
    }
}
