using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Admin.Restaurants;
using 살뜰.Data;
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
}

public sealed class 음식운영관리UseCase(SsalddelContext db) : I음식운영관리UseCase
{
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
        ApplyAudit(policy, 수정자UserId);
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
        policy.기사거리단위지급액 = request.DriverDistanceUnitPayout;
        policy.기사최소지급액 = request.DriverMinimumPayout;
        policy.기사기상할증활성화여부 = request.DriverWeatherSurchargeEnabled;
        policy.기사기상할증액 = request.DriverWeatherSurcharge;
        policy.기사기상할증정책판본 = request.DriverWeatherSurchargePolicyRevision.Trim();
        ApplyAudit(policy, 수정자UserId);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok(ToPricingPolicy(policy));
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
            DriverDistanceUnitPayout = policy.기사거리단위지급액,
            DriverMinimumPayout = policy.기사최소지급액,
            DriverWeatherSurchargeEnabled = policy.기사기상할증활성화여부,
            DriverWeatherSurcharge = policy.기사기상할증액,
            DriverWeatherSurchargePolicyRevision = policy.기사기상할증정책판본,
            UpdatedAtUtc = policy.UpdatedAtUtc,
            UpdatedByUserId = policy.수정자UserId
        };

    private static string? ValidatePricing(음식배달요금정책응답 request)
    {
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

    private static void ApplyAudit(음식운영정책 policy, string 수정자UserId)
    {
        policy.수정자UserId = string.IsNullOrWhiteSpace(수정자UserId)
            ? "unknown-admin"
            : 수정자UserId.Trim();
        policy.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static Result<T> BadRequest<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", 400));
}
