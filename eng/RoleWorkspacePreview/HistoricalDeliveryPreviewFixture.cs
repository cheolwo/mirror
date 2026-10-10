using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;

namespace RoleWorkspacePreview
{
    /// <summary>관측된 과거 자료의 읽기 전용 투영. 내부 생명주기 시각은 실제 완료 근거가 아닙니다.</summary>
    public sealed class HistoricalDeliveryProjection
    {
        public HistoricalDeliveryDataset Dataset { get; }
        public HistoricalDeliveryProjection(HistoricalDeliveryDataset dataset)
        {
            dataset.Validate();
            Dataset = dataset;
        }
        public HistoricalDeliveryRecord? Find(string? recordId) => Dataset.Records.SingleOrDefault(x => x.RecordId == recordId);
        public HistoricalDeliveryBranch? Branch(string? branchId) => Dataset.Branches.SingleOrDefault(x => x.BranchId == branchId);
        public IReadOnlyList<HistoricalDeliveryRecord> ForDate(DateOnly date) => Dataset.Records.Where(x => x.ServiceDate == date)
            .OrderByDescending(x => x.Sequence).ThenBy(x => x.RecordId, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<HistoricalDeliveryRecord> ForBranch(string branchId) => Dataset.Records.Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.ServiceDate).ThenByDescending(x => x.Sequence).ThenBy(x => x.RecordId, StringComparer.Ordinal).ToArray();

        // Native 목록 계약의 날짜 구간 검증을 위한 모의 시각입니다. 원본 ActualCompletedAtUtc에 복사하지 않습니다.
        public static DateTime SimulationCompletedAt(HistoricalDeliveryRecord row)
            => DateTime.SpecifyKind(row.ServiceDate.ToDateTime(new TimeOnly(12, 0)).AddHours(-9).AddSeconds(row.Sequence), DateTimeKind.Utc);

        public FoodDeliveryOrderSettlementDto Settlement(HistoricalDeliveryRecord row) => new()
        {
            SettlementId = row.RecordId, OrderNo = row.RecordId, DeliveryAttemptId = row.RecordId + "-simulation-attempt",
            DriverId = Dataset.DriverId, RestaurantName = Branch(row.BranchId)?.DisplayName ?? "지점 확인 필요",
            GrossAmount = row.DeliveryFeeKrw, DeductionAmount = null, NetAmount = null,
            PricingPolicyRevision = "historical-replay.r54", ExecutionModeCode = "Simulation", ServerExecutionModeCode = "Simulation",
            CompletedAtUtc = SimulationCompletedAt(row), ReceiptConfirmedAtUtc = null, SettlementStatusCode = "HistoricalReplay",
            PayoutStatusCode = "NotRequested", IsActualTransferCompleted = false,
            HoldReason = "관측된 배달료를 대조하는 로컬 자료입니다. 이 앱의 실제 완료·정산·입금 증거가 아닙니다.",
            PricingBreakdown = new() { EvidenceStatusCode = "MissingEvidence", EvidenceScopeCode = "HistoricalReplay" }
        };

        public FoodDeliveryDailySettlementDto Daily(DateOnly date, string driverId = "replay-driver")
        {
            if (driverId != Dataset.DriverId) throw new UnauthorizedAccessException("로컬 검증 기사만 조회할 수 있습니다.");
            var rows = ForDate(date);
            var start = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-9), DateTimeKind.Utc);
            return new()
            {
                DriverId = Dataset.DriverId, CompletionDateKst = date, PeriodStartAtUtc = start, PeriodEndAtUtc = start.AddDays(1),
                DateBasisCode = "HistoricalServiceDateSimulation", ServerExecutionModeCode = "Simulation", UpdatedAtUtc = start,
                CompletedOrderCount = rows.Count, AwaitingReceiptOrderCount = rows.Count,
                KnownGrossAmountTotal = rows.Sum(x => x.DeliveryFeeKrw), GrossAmountTotal = rows.Sum(x => x.DeliveryFeeKrw),
                UnconfirmedDeductionCount = rows.Count, UnconfirmedNetAmountCount = rows.Count,
                DeductionAmountTotal = null, NetAmountTotal = null, ActualTransferAmountTotal = null,
                OrderSettlements = rows.Select(Settlement).ToArray()
            };
        }
    }

    /// <summary>명시적으로 지정한 개인 자료만 읽는 루프백 화면 fixture. 네트워크·운영 저장 fallback이 없습니다.</summary>
    public sealed class HistoricalDeliveryPreviewFixture : IDisposable
    {
        private readonly PreviewReadApi? _api;
        public HistoricalDeliveryProjection? Projection { get; }
        public FDriverCompletedDeliveryListPageModel? DriverList { get; }
        public string? SelectedRecordId { get; private set; }
        public string Notice { get; }
        public bool HasData => Projection is not null;
        public IReadOnlyList<DateOnly> Dates => Projection?.Dataset.Records.Select(x => x.ServiceDate).Distinct().Order().ToArray() ?? [];

        public HistoricalDeliveryPreviewFixture(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var path = configuration["SSALDDEL_HISTORICAL_DELIVERY_DATASET"];
            if (string.IsNullOrWhiteSpace(path))
            {
                Notice = "로컬 배달 자료를 지정하지 않았습니다. 실행자가 SSALDDEL_HISTORICAL_DELIVERY_DATASET에 검토한 자료 경로를 지정해 주세요.";
                return;
            }
            try
            {
                var directory = new DirectoryInfo(environment.ContentRootPath);
                while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")) && !File.Exists(Path.Combine(directory.FullName, ".git")))
                    directory = directory.Parent;
                if (directory is null) throw new InvalidDataException();
                var allowed = Path.GetFullPath(Path.Combine(directory.FullName, "artifacts", "local")) + Path.DirectorySeparatorChar;
                var full = Path.GetFullPath(path);
                if (!Path.IsPathFullyQualified(path) || !full.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException();
                Projection = new(HistoricalDeliveryDataset.Read(full));
                var api = DispatchProxy.Create<IFoodDeliveryDriverApiService, PreviewReadApi>();
                _api = (PreviewReadApi)(object)api;
                _api.Projection = Projection;
                DriverList = new(api, new PreviewSession(), new PreviewNavigator(this));
                Notice = "실제 배달 기록을 읽는 로컬 검증입니다. 모의 계정·상태를 사용하며 실제 지급·현재 업무를 만들지 않습니다.";
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            {
                Notice = "로컬 배달 자료를 읽지 못했습니다. 파일 경로·형식·검토 상태를 확인해 주세요. 예시 자료로 대신 표시하지 않습니다.";
            }
        }

        public async Task InitializeAsync()
        {
            if (DriverList is null || Dates.Count == 0) return;
            DriverList.SetDate(Dates[0]);
            await DriverList.ActivateAsync();
        }

        public async Task SetDateAsync(DateOnly date)
        {
            if (DriverList is null || !Dates.Contains(date)) return;
            DriverList.Deactivate(); DriverList.SetDate(date); SelectedRecordId = null;
            await DriverList.ActivateAsync();
        }

        public void Select(string recordId)
        {
            if (Projection?.Find(recordId) is null) throw new ArgumentException("조회할 배달 기록을 확인해 주세요.");
            SelectedRecordId = recordId;
        }
        public void ClearSelection() => SelectedRecordId = null;
        public void Dispose() => DriverList?.Deactivate();

        // The only implemented port is the existing native financial-list read. Every write and other port fails closed.
        public class PreviewReadApi : DispatchProxy
        {
            public HistoricalDeliveryProjection? Projection { get; set; }
            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod?.Name == nameof(IFoodDeliveryDriverApiService.GetDailySettlementAsync)
                    && args is [DateOnly date, CancellationToken ct])
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult(Projection!.Daily(date));
                }
                throw new InvalidOperationException("과거 자료 화면은 읽기 전용이며 업무 요청을 실행하지 않습니다.");
            }
        }

        private sealed class PreviewNavigator(HistoricalDeliveryPreviewFixture fixture) : IFDriverCompletedDeliveryNavigator
        {
            public Task OpenDetailAsync(FDriverCompletedDeliveryNavigationTarget target) { fixture.Select(target.SettlementId); return Task.CompletedTask; }
            public Task OpenListAsync(DateOnly? date = null) => Task.CompletedTask;
            public Task BackAsync() { fixture.ClearSelection(); return Task.CompletedTask; }
            public Task ReturnToWorkspaceAsync() => throw new InvalidOperationException("현재 업무로 이동하지 않습니다.");
        }

        private sealed class PreviewSession : IFDriverAuthSession
        {
            public event EventHandler? SessionChanged { add { } remove { } }
            public string? AccessToken => null;
            public string? AuthenticationOwnerId => UserId;
            public DateTime AccessTokenExpiresAtUtc => default;
            public string? RefreshToken => null;
            public DateTime RefreshTokenExpiresAtUtc => default;
            public string? UserId => "replay-driver";
            public string? UserName => "로컬 검증 기사";
            public IReadOnlyList<string> Roles => ["Driver"];
            public bool IsAuthenticated => true;
            public ClientAuthSessionRestoreState CurrentState => ClientAuthSessionRestoreState.Authenticated;
            public Task<ClientAuthSessionRestoreState> RestoreAsync(CancellationToken ct = default) => Task.FromResult(CurrentState);
            public Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken ct = default) => throw new InvalidOperationException("인증 저장 금지");
            public Task ClearAsync(CancellationToken ct = default) => throw new InvalidOperationException("인증 저장 금지");
        }
    }
}

// Linked native sources require platform ports at compile time. The preview never uses device storage or dispatches work.
#if ROLE_WORKSPACE_PREVIEW_HOST
namespace FDriverApp.PageModels
{
    internal static class MainThread
    {
        public static Task InvokeOnMainThreadAsync(Action action) { action(); return Task.CompletedTask; }
    }
}
namespace FDriverApp.Services
{
    internal static class SecureStorage
    {
        public static Store Default { get; } = new();
        internal sealed class Store
        {
            public Task<string?> GetAsync(string key) => throw new InvalidOperationException("로컬 검증에서 기기 저장소를 읽지 않습니다.");
            public Task SetAsync(string key, string value) => throw new InvalidOperationException("로컬 검증에서 기기 저장소를 쓰지 않습니다.");
            public bool Remove(string key) => throw new InvalidOperationException("로컬 검증에서 기기 저장소를 쓰지 않습니다.");
        }
    }
}
#endif
