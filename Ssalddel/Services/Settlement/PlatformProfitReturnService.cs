using Ssalddel.Contracts.Common.PlatformProfit;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.정산;

namespace 살뜰.Services.Settlement;

public interface IPlatformProfitReturnService
{
    Task<PlatformRevenueEntryResponse> RecordRevenueAsync(PlatformRevenueEntryRequest request, CancellationToken cancellationToken);
    Task<PlatformProfitReturnPolicyResponse> CreatePolicyAsync(PlatformProfitReturnPolicyRequest request, CancellationToken cancellationToken);
    Task<PlatformProfitReturnPlanResponse> CreateReturnSchedulesAsync(PlatformProfitReturnScheduleCreateRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformProfitReturnScheduleResponse>> ListSchedulesAsync(string? participantUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
}

public sealed class PlatformProfitReturnService : IPlatformProfitReturnService
{
    private const string RetainedSurplusRevenueSource = "NeighborhoodRetainedSurplus";
    private readonly SsalddelContext _db;

    public PlatformProfitReturnService(SsalddelContext db)
    {
        _db = db;
    }

    public async Task<PlatformRevenueEntryResponse> RecordRevenueAsync(PlatformRevenueEntryRequest request, CancellationToken cancellationToken)
    {
        // 주문 성과금과 함께 발생한 잔여금은 전용 transaction만 기록합니다. 수동 입력 우회는 허용하지 않습니다.
        if (string.Equals(request.RevenueSource?.Trim(), RetainedSurplusRevenueSource, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("주문 잔여금의 플랫폼 몫은 주문별 정산에서만 기록할 수 있습니다.", nameof(request.RevenueSource));
        }
        if (request.PlatformRevenueAmount < 0 || request.GrossAmount < 0)
        {
            throw new ArgumentException("수익 금액은 음수일 수 없습니다.");
        }

        var record = new PlatformRevenueEntryRecord
        {
            Id = Guid.NewGuid(),
            RevenueSource = NormalizeRevenueSource(request.RevenueSource),
            SourceReferenceType = NormalizeOptional(request.SourceReferenceType),
            SourceReferenceId = NormalizeOptional(request.SourceReferenceId),
            PayerUserId = NormalizeOptional(request.PayerUserId),
            RelatedParticipantUserId = NormalizeOptional(request.RelatedParticipantUserId),
            GrossAmount = request.GrossAmount,
            PlatformRevenueAmount = request.PlatformRevenueAmount,
            CurrencyCode = NormalizeOptional(request.CurrencyCode, "KRW"),
            OccurredAtUtc = request.OccurredAtUtc == default ? DateTime.UtcNow : request.OccurredAtUtc,
            Memo = NormalizeOptional(request.Memo),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.PlatformRevenueEntries.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(record);
    }

    public async Task<PlatformProfitReturnPolicyResponse> CreatePolicyAsync(PlatformProfitReturnPolicyRequest request, CancellationToken cancellationToken)
    {
        var record = new PlatformProfitReturnPolicyRecord
        {
            Id = Guid.NewGuid(),
            PolicyName = NormalizeRequired(request.PolicyName, nameof(request.PolicyName)),
            TargetParticipantCategory = NormalizeParticipantCategory(request.TargetParticipantCategory),
            ReturnRatePercent = Math.Clamp(request.ReturnRatePercent, 0m, 100m),
            CompanyReserveAmount = Math.Max(0m, request.CompanyReserveAmount),
            MinimumProfitThreshold = Math.Max(0m, request.MinimumProfitThreshold),
            EffectiveStartDate = request.EffectiveStartDate == default ? DateOnly.FromDateTime(DateTime.Today) : request.EffectiveStartDate,
            EffectiveEndDate = request.EffectiveEndDate,
            IsActive = true,
            Memo = NormalizeOptional(request.Memo),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.PlatformProfitReturnPolicies.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(record);
    }

    public async Task<PlatformProfitReturnPlanResponse> CreateReturnSchedulesAsync(PlatformProfitReturnScheduleCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.PeriodEndDate < request.PeriodStartDate)
        {
            throw new ArgumentException("정산 종료일은 시작일보다 빠를 수 없습니다.", nameof(request.PeriodEndDate));
        }

        var policy = await _db.PlatformProfitReturnPolicies
            .FirstOrDefaultAsync(x => x.Id == request.PolicyId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("활성 환원 정책을 찾을 수 없습니다.");

        var fromUtc = request.PeriodStartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = request.PeriodEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var revenueAmount = await _db.PlatformRevenueEntries
            // 이 주문의 참여자 성과금은 이미 배분했습니다. 남긴 플랫폼 몫을 기간 환원으로 다시 나누지 않습니다.
            .Where(x => x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc
                && x.RevenueSource != RetainedSurplusRevenueSource)
            .SumAsync(x => x.PlatformRevenueAmount, cancellationToken);

        var operatingCost = Math.Max(0m, request.OperatingCostAmount);
        var estimatedProfit = Math.Max(0m, revenueAmount - operatingCost);
        var returnBase = estimatedProfit >= policy.MinimumProfitThreshold
            ? Math.Max(0m, estimatedProfit - policy.CompanyReserveAmount)
            : 0m;
        var returnPool = decimal.Round(returnBase * policy.ReturnRatePercent / 100m, 2, MidpointRounding.AwayFromZero);

        var participants = request.Participants
            .Where(x => !string.IsNullOrWhiteSpace(x.ParticipantUserId) && x.Weight > 0)
            .ToArray();
        var totalWeight = participants.Sum(x => x.Weight);
        if (returnPool > 0 && totalWeight <= 0)
        {
            throw new InvalidOperationException("환원 대상 참여자 가중치가 필요합니다.");
        }

        var created = new List<PlatformProfitReturnScheduleRecord>();
        foreach (var participant in participants)
        {
            var plannedAmount = totalWeight <= 0
                ? 0m
                : decimal.Round(returnPool * participant.Weight / totalWeight, 2, MidpointRounding.AwayFromZero);

            var record = new PlatformProfitReturnScheduleRecord
            {
                Id = Guid.NewGuid(),
                PolicyId = policy.Id,
                ParticipantUserId = participant.ParticipantUserId.Trim(),
                ParticipantName = NormalizeOptional(participant.ParticipantName),
                ParticipantCategory = policy.TargetParticipantCategory,
                PeriodStartDate = request.PeriodStartDate,
                PeriodEndDate = request.PeriodEndDate,
                ScheduledPaymentDate = request.ScheduledPaymentDate,
                TotalPlatformRevenueAmount = revenueAmount,
                OperatingCostAmount = operatingCost,
                EstimatedProfitAmount = estimatedProfit,
                ReturnPoolAmount = returnPool,
                ParticipantWeight = participant.Weight,
                PlannedReturnAmount = plannedAmount,
                Status = ProfitReturnScheduleStatuses.Planned,
                Memo = returnPool <= 0 ? "환원 가능 이익이 없어 0원 예정으로 생성되었습니다." : "플랫폼 이익 환원 예정 지급입니다.",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            created.Add(record);
            _db.PlatformProfitReturnSchedules.Add(record);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new PlatformProfitReturnPlanResponse
        {
            PolicyId = policy.Id,
            PeriodStartDate = request.PeriodStartDate,
            PeriodEndDate = request.PeriodEndDate,
            TotalPlatformRevenueAmount = revenueAmount,
            OperatingCostAmount = operatingCost,
            EstimatedProfitAmount = estimatedProfit,
            ReturnPoolAmount = returnPool,
            Schedules = created.Select(ToResponse).ToArray()
        };
    }

    public async Task<IReadOnlyList<PlatformProfitReturnScheduleResponse>> ListSchedulesAsync(string? participantUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = _db.PlatformProfitReturnSchedules.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(participantUserId))
        {
            var normalizedParticipantUserId = participantUserId.Trim();
            query = query.Where(x => x.ParticipantUserId == normalizedParticipantUserId);
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.ScheduledPaymentDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.ScheduledPaymentDate <= to.Value);
        }

        var records = await query
            .OrderByDescending(x => x.ScheduledPaymentDate)
            .Take(300)
            .ToArrayAsync(cancellationToken);

        return records.Select(ToResponse).ToArray();
    }

    private static PlatformRevenueEntryResponse ToResponse(PlatformRevenueEntryRecord record)
        => new()
        {
            Id = record.Id,
            RevenueSource = record.RevenueSource,
            SourceReferenceType = record.SourceReferenceType,
            SourceReferenceId = record.SourceReferenceId,
            PayerUserId = record.PayerUserId,
            RelatedParticipantUserId = record.RelatedParticipantUserId,
            GrossAmount = record.GrossAmount,
            PlatformRevenueAmount = record.PlatformRevenueAmount,
            CurrencyCode = record.CurrencyCode,
            OccurredAtUtc = record.OccurredAtUtc,
            Memo = record.Memo
        };

    private static PlatformProfitReturnPolicyResponse ToResponse(PlatformProfitReturnPolicyRecord record)
        => new()
        {
            Id = record.Id,
            PolicyName = record.PolicyName,
            TargetParticipantCategory = record.TargetParticipantCategory,
            ReturnRatePercent = record.ReturnRatePercent,
            CompanyReserveAmount = record.CompanyReserveAmount,
            MinimumProfitThreshold = record.MinimumProfitThreshold,
            EffectiveStartDate = record.EffectiveStartDate,
            EffectiveEndDate = record.EffectiveEndDate,
            IsActive = record.IsActive,
            Memo = record.Memo
        };

    private static PlatformProfitReturnScheduleResponse ToResponse(PlatformProfitReturnScheduleRecord record)
        => new()
        {
            Id = record.Id,
            PolicyId = record.PolicyId,
            ParticipantUserId = record.ParticipantUserId,
            ParticipantName = record.ParticipantName,
            ParticipantCategory = record.ParticipantCategory,
            PeriodStartDate = record.PeriodStartDate,
            PeriodEndDate = record.PeriodEndDate,
            ScheduledPaymentDate = record.ScheduledPaymentDate,
            TotalPlatformRevenueAmount = record.TotalPlatformRevenueAmount,
            OperatingCostAmount = record.OperatingCostAmount,
            EstimatedProfitAmount = record.EstimatedProfitAmount,
            ReturnPoolAmount = record.ReturnPoolAmount,
            ParticipantWeight = record.ParticipantWeight,
            PlannedReturnAmount = record.PlannedReturnAmount,
            Status = record.Status,
            Memo = record.Memo
        };

    private static string NormalizeRevenueSource(string? value)
        => value?.Trim() switch
        {
            PlatformRevenueSourceCodes.DriverUsageFee => PlatformRevenueSourceCodes.DriverUsageFee,
            PlatformRevenueSourceCodes.WarehouseSalesCommission => PlatformRevenueSourceCodes.WarehouseSalesCommission,
            PlatformRevenueSourceCodes.FoodDeliveryCommission => PlatformRevenueSourceCodes.FoodDeliveryCommission,
            PlatformRevenueSourceCodes.LogisticsAgencyFee => PlatformRevenueSourceCodes.LogisticsAgencyFee,
            PlatformRevenueSourceCodes.PlatformSubscription => PlatformRevenueSourceCodes.PlatformSubscription,
            _ => PlatformRevenueSourceCodes.TransportRecommendationCommission
        };

    private static string NormalizeParticipantCategory(string? value)
        => value?.Trim() switch
        {
            ProfitReturnParticipantCategoryCodes.DeliveryRider => ProfitReturnParticipantCategoryCodes.DeliveryRider,
            ProfitReturnParticipantCategoryCodes.WarehouseWorker => ProfitReturnParticipantCategoryCodes.WarehouseWorker,
            ProfitReturnParticipantCategoryCodes.RestaurantPartner => ProfitReturnParticipantCategoryCodes.RestaurantPartner,
            ProfitReturnParticipantCategoryCodes.PlatformContributor => ProfitReturnParticipantCategoryCodes.PlatformContributor,
            _ => ProfitReturnParticipantCategoryCodes.Driver
        };

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("필수값이 비어 있습니다.", parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeOptional(string? value, string fallback = "")
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
