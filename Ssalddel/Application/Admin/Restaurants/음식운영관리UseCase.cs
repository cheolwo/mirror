using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Admin.Restaurants;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.음식;

namespace Ssalddel.Application.Admin.Restaurants;

public interface I음식운영관리UseCase
{
    Task<Result<음식점리뷰관리목록응답>> 리뷰목록Async(CancellationToken cancellationToken);

    Task<Result<음식점리뷰운영정책응답>> 리뷰정책조회Async(CancellationToken cancellationToken);

    Task<Result<음식점리뷰운영정책응답>> 리뷰정책수정Async(
        음식점리뷰운영정책수정요청 request,
        string 수정자UserId,
        CancellationToken cancellationToken);

    Task<Result<음식배달요금정책응답>> 배달요금정책조회Async(CancellationToken cancellationToken);

    Task<Result<음식배달요금정책응답>> 배달요금정책수정Async(
        음식배달요금정책응답 request,
        string 수정자UserId,
        CancellationToken cancellationToken);

    Task<Result<음식배달한시수요할증응답>> 한시수요할증조회Async(
        CancellationToken cancellationToken);

    Task<Result<음식배달한시수요할증응답>> 한시수요할증적용Async(
        음식배달한시수요할증적용요청 request,
        string 수정자UserId,
        CancellationToken cancellationToken);
}

public sealed class 음식운영관리UseCase(
    SsalddelContext db,
    TimeProvider timeProvider,
    ISsalddelExecutionModePolicy executionMode) : I음식운영관리UseCase
{
    private static readonly decimal[] AllowedSurchargeAmounts = [500m, 1000m, 1500m];
    private static readonly int[] AllowedDurationMinutes = [15, 30, 60];

    public async Task<Result<음식점리뷰관리목록응답>> 리뷰목록Async(
        CancellationToken cancellationToken)
    {
        var reviews = await db.음식점리뷰
            .AsNoTracking()
            .Where(item => item.관리자검토필요여부)
            .OrderByDescending(item => item.관리자검토필요여부)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Take(500)
            .ToArrayAsync(cancellationToken);
        var restaurantIds = reviews.Select(item => item.음식점Id).Distinct().ToList();
        var names = await db.음식점공개프로필
            .AsNoTracking()
            .Where(item => restaurantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.상호명, cancellationToken);

        return Result.Ok(new 음식점리뷰관리목록응답
        {
            Items = reviews.Select(item =>
            {
                var photoUrls = 음식점리뷰UseCase.DeserializePhotoUrls(item.사진UrlsJson);
                return new 음식점리뷰관리항목응답
                {
                    리뷰Id = item.Id,
                    음식점Id = item.음식점Id,
                    음식점명 = names.GetValueOrDefault(item.음식점Id, $"음식점 {item.음식점Id}"),
                    주문자UserId = item.주문자UserId,
                    주문번호 = item.주문번호,
                    별점 = item.별점,
                    내용 = item.내용,
                    사진포함여부 = photoUrls.Count > 0,
                    같은음식점기준저평점3회연속여부 = item.같은음식점기준저평점3회연속여부,
                    사장노출허용여부 = item.사장노출허용여부,
                    관리자검토필요여부 = item.관리자검토필요여부,
                    관리자게시강제여부 = item.관리자게시강제여부,
                    현재노출여부 = item.현재노출여부,
                    CreatedAt = item.CreatedAtUtc,
                    게시종료일시Utc = item.게시종료일시Utc,
                    최근조치사유 = item.최근조치사유
                };
            }).ToArray()
        });
    }

    public async Task<Result<음식점리뷰운영정책응답>> 리뷰정책조회Async(
        CancellationToken cancellationToken)
    {
        var policy = await GetPolicyAsync(cancellationToken);
        return Result.Ok(ToReviewPolicy(policy));
    }

    public async Task<Result<음식점리뷰운영정책응답>> 리뷰정책수정Async(
        음식점리뷰운영정책수정요청 request,
        string 수정자UserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.기본저평점게시일수 is not (3 or 7))
        {
            return BadRequest<음식점리뷰운영정책응답>("저평점 게시일수는 3일 또는 7일이어야 합니다.");
        }

        var policy = await GetTrackedPolicyAsync(cancellationToken);
        policy.기본저평점게시일수 = request.기본저평점게시일수;
        ApplyAudit(policy, 수정자UserId, UtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok(ToReviewPolicy(policy));
    }

    public async Task<Result<음식배달요금정책응답>> 배달요금정책조회Async(
        CancellationToken cancellationToken)
    {
        var policy = await GetPolicyAsync(cancellationToken);
        return Result.Ok(ToPricingPolicy(policy));
    }

    public async Task<Result<음식배달요금정책응답>> 배달요금정책수정Async(
        음식배달요금정책응답 request,
        string 수정자UserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = ValidatePricing(request);
        if (validation is not null)
        {
            return BadRequest<음식배달요금정책응답>(validation);
        }

        var policy = await GetTrackedPolicyAsync(cancellationToken);
        policy.기본요금 = request.BaseFee;
        policy.포함거리Meters = request.IncludedDistanceMeters;
        policy.거리단위Meters = request.DistanceUnitMeters;
        policy.거리단위요금 = request.DistanceUnitFee;
        policy.최소요금 = request.MinimumFee;
        policy.기사기본지급액 = request.DriverBasePayout;
        policy.기사픽업지급액 = request.DriverPickupPayout;
        policy.기사거리단위지급액 = request.DriverDistanceUnitPayout;
        policy.기사최소지급액 = request.DriverMinimumPayout;
        policy.기사기상할증활성화여부 = request.DriverWeatherSurchargeEnabled;
        policy.기사기상할증액 = request.DriverWeatherSurcharge;
        policy.기사기상할증정책판본 = request.DriverWeatherSurchargePolicyRevision.Trim();
        ApplyAudit(policy, 수정자UserId, UtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok(ToPricingPolicy(policy));
    }

    public async Task<Result<음식배달한시수요할증응답>> 한시수요할증조회Async(
        CancellationToken cancellationToken)
    {
        var policy = await GetPolicyAsync(cancellationToken);
        return Result.Ok(ToTemporaryDemandSurcharge(policy, UtcNow()));
    }

    public async Task<Result<음식배달한시수요할증응답>> 한시수요할증적용Async(
        음식배달한시수요할증적용요청 request,
        string 수정자UserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!executionMode.IsSimulation)
        {
            return Forbidden<음식배달한시수요할증응답>(
                "한시 수요 할증은 현재 격리된 Simulation 운영 검증에서만 적용할 수 있습니다.");
        }

        var validation = ValidateTemporaryDemandSurcharge(request);
        if (validation is not null)
        {
            return BadRequest<음식배달한시수요할증응답>(validation);
        }

        var policy = await GetTrackedPolicyAsync(cancellationToken);
        var requestId = request.ClientRequestId.ToString("D");
        if (string.Equals(
                policy.기사한시수요할증ClientRequestId,
                requestId,
                StringComparison.OrdinalIgnoreCase))
        {
            if (!SameTemporaryDemandSurcharge(policy, request))
            {
                return Conflict<음식배달한시수요할증응답>(
                    "같은 요청 ID에 다른 한시 수요 할증 조건이 있습니다.");
            }

            return Result.Ok(ToTemporaryDemandSurcharge(policy, UtcNow()));
        }

        if (policy.기사한시수요할증Revision != request.ExpectedRevision)
        {
            return Conflict<음식배달한시수요할증응답>(
                "한시 수요 할증 상태가 변경되었습니다. 현재 상태를 다시 조회해 주세요.");
        }

        var now = UtcNow();
        policy.기사한시수요할증액 = request.SurchargeAmount;
        policy.기사한시수요할증시작일시Utc = now;
        policy.기사한시수요할증종료일시Utc = now.AddMinutes(request.DurationMinutes);
        policy.기사한시수요할증사유Code = request.ReasonCode.Trim();
        policy.기사한시수요할증범위Code = request.ScopeCode.Trim();
        policy.기사한시수요할증ClientRequestId = requestId;
        policy.기사한시수요할증Revision++;
        ApplyAudit(policy, 수정자UserId, now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<음식배달한시수요할증응답>(
                "다른 운영자가 한시 수요 할증을 먼저 변경했습니다. 현재 상태를 다시 조회해 주세요.");
        }

        return Result.Ok(ToTemporaryDemandSurcharge(policy, now));
    }

    private async Task<음식운영정책> GetPolicyAsync(CancellationToken cancellationToken)
        => await db.음식운영정책
               .AsNoTracking()
               .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken)
           ?? new 음식운영정책();

    private async Task<음식운영정책> GetTrackedPolicyAsync(CancellationToken cancellationToken)
    {
        var policy = await db.음식운영정책
            .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        if (policy is not null)
        {
            return policy;
        }

        policy = new 음식운영정책();
        db.음식운영정책.Add(policy);
        return policy;
    }

    private static 음식점리뷰운영정책응답 ToReviewPolicy(음식운영정책 policy)
        => new()
        {
            Id = policy.Id,
            기본저평점게시일수 = policy.기본저평점게시일수,
            허용게시일수옵션 = [3, 7],
            UpdatedAt = policy.UpdatedAtUtc
        };

    private static 음식배달요금정책응답 ToPricingPolicy(음식운영정책 policy)
        => new()
        {
            BaseFee = policy.기본요금,
            IncludedDistanceMeters = policy.포함거리Meters,
            DistanceUnitMeters = policy.거리단위Meters,
            DistanceUnitFee = policy.거리단위요금,
            MinimumFee = policy.최소요금,
            DriverBasePayout = policy.기사기본지급액,
            DriverPickupPayout = policy.기사픽업지급액,
            DriverDistanceUnitPayout = policy.기사거리단위지급액,
            DriverMinimumPayout = policy.기사최소지급액,
            DriverWeatherSurchargeEnabled = policy.기사기상할증활성화여부,
            DriverWeatherSurcharge = policy.기사기상할증액,
            DriverWeatherSurchargePolicyRevision = policy.기사기상할증정책판본,
            UpdatedAtUtc = policy.UpdatedAtUtc,
            UpdatedByUserId = policy.수정자UserId
        };

    private 음식배달한시수요할증응답 ToTemporaryDemandSurcharge(
        음식운영정책 policy,
        DateTime serverNowUtc)
    {
        var startedAtUtc = AsUtc(policy.기사한시수요할증시작일시Utc);
        var expiresAtUtc = AsUtc(policy.기사한시수요할증종료일시Utc);
        var active = executionMode.IsSimulation
                     && policy.기사한시수요할증액 > 0m
                     && startedAtUtc.HasValue
                     && expiresAtUtc.HasValue
                     && serverNowUtc >= startedAtUtc.Value
                     && serverNowUtc < expiresAtUtc.Value;
        var duration = startedAtUtc.HasValue && expiresAtUtc.HasValue
            ? (int?)Math.Round((expiresAtUtc.Value - startedAtUtc.Value).TotalMinutes)
            : null;

        return new 음식배달한시수요할증응답
        {
            SurchargeAmount = active ? policy.기사한시수요할증액 : 0m,
            DurationMinutes = duration,
            StartedAtUtc = startedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            ReasonCode = policy.기사한시수요할증사유Code,
            ScopeCode = string.IsNullOrWhiteSpace(policy.기사한시수요할증범위Code)
                ? 음식배달한시수요할증범위Codes.전체음식배달
                : policy.기사한시수요할증범위Code,
            PolicyRevision = $"food-demand-surcharge.r1:{policy.기사한시수요할증Revision}",
            Revision = policy.기사한시수요할증Revision,
            IsActive = active,
            CanApply = executionMode.IsSimulation,
            ExecutionModeCode = executionMode.Mode.ToString(),
            AllowedAmounts = AllowedSurchargeAmounts,
            AllowedDurationMinutes = AllowedDurationMinutes,
            ServerNowUtc = serverNowUtc,
            UpdatedAtUtc = AsUtc(policy.UpdatedAtUtc) ?? DateTime.UnixEpoch,
            UpdatedByUserId = policy.수정자UserId
        };
    }

    private static string? ValidatePricing(음식배달요금정책응답 request)
    {
        if (request.DriverPickupPayout is < 0m || request.DriverPickupPayout > request.DriverBasePayout)
            return "픽업 지급액은 총 기본 지급액 안에서 배분해야 합니다.";
        if (request.IncludedDistanceMeters < 0 || request.DistanceUnitMeters <= 0)
        {
            return "포함 거리는 0 이상이고 거리 계산 단위는 1m 이상이어야 합니다.";
        }

        if (string.IsNullOrWhiteSpace(request.DriverWeatherSurchargePolicyRevision)
            || request.DriverWeatherSurchargePolicyRevision.Trim().Length > 100)
        {
            return "기상 할증 정책 판본은 1~100자로 입력해야 합니다.";
        }

        return request.BaseFee < 0
               || request.DistanceUnitFee < 0
               || request.MinimumFee < 0
               || request.DriverBasePayout < 0
               || request.DriverDistanceUnitPayout < 0
               || request.DriverMinimumPayout < 0
               || request.DriverWeatherSurcharge < 0
            ? "요금과 기사 지급액은 0 이상이어야 합니다."
            : null;
    }

    private static string? ValidateTemporaryDemandSurcharge(
        음식배달한시수요할증적용요청 request)
    {
        if (!AllowedSurchargeAmounts.Contains(request.SurchargeAmount))
        {
            return "한시 수요 할증액은 500원, 1,000원 또는 1,500원이어야 합니다.";
        }

        if (!AllowedDurationMinutes.Contains(request.DurationMinutes))
        {
            return "한시 수요 할증 적용시간은 15분, 30분 또는 60분이어야 합니다.";
        }

        if (request.ClientRequestId == Guid.Empty)
        {
            return "멱등 처리를 위한 요청 ID가 필요합니다.";
        }

        if (request.ExpectedRevision < 0)
        {
            return "예상 revision은 0 이상이어야 합니다.";
        }

        if (request.ReasonCode != 음식배달한시수요할증사유Codes.제안가능기사부족)
        {
            return "첫 구현에서는 제안 가능한 기사 부족 사유만 지원합니다.";
        }

        return request.ScopeCode != 음식배달한시수요할증범위Codes.전체음식배달
            ? "첫 구현에서는 전체 음식 배달 범위만 지원합니다."
            : null;
    }

    private static bool SameTemporaryDemandSurcharge(
        음식운영정책 policy,
        음식배달한시수요할증적용요청 request)
    {
        var storedDuration = policy.기사한시수요할증시작일시Utc.HasValue
                             && policy.기사한시수요할증종료일시Utc.HasValue
            ? (int)Math.Round((policy.기사한시수요할증종료일시Utc.Value
                              - policy.기사한시수요할증시작일시Utc.Value).TotalMinutes)
            : 0;
        return policy.기사한시수요할증액 == request.SurchargeAmount
               && storedDuration == request.DurationMinutes
               && policy.기사한시수요할증사유Code == request.ReasonCode
               && policy.기사한시수요할증범위Code == request.ScopeCode;
    }

    private static void ApplyAudit(
        음식운영정책 policy,
        string 수정자UserId,
        DateTime updatedAtUtc)
    {
        policy.수정자UserId = string.IsNullOrWhiteSpace(수정자UserId)
            ? "unknown-admin"
            : 수정자UserId.Trim();
        policy.UpdatedAtUtc = updatedAtUtc;
    }

    private DateTime UtcNow()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static DateTime? AsUtc(DateTime? value)
        => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    private static Result<T> BadRequest<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", 400));

    private static Result<T> Forbidden<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", 403));

    private static Result<T> Conflict<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", 409));
}
