using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Pricing;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Pricing;
using Ssalddel.Domain.Pricing;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace 살뜰.Services.Dispatch.Recommendation;

public interface I소형배송기사제안요금Service
{
    Task<Result> 준비Async(운송원장 queue, CancellationToken ct = default);
    Task<Result> 조회검증Async(운송원장 queue, CancellationToken ct = default);
    Task<Result> 수락검증Async(운송원장 queue, string driverId, CancellationToken ct = default);
}

/// <summary>생활 배송의 새 공통 기사 지급 제안만 동결합니다. 저장은 호출자의 상태 전이와 함께 수행합니다.</summary>
public sealed class 소형배송기사제안요금Service(SsalddelContext db, IOptions<StraightLineDriverPricingOptions> options)
    : I소형배송기사제안요금Service
{
    public const string EvidenceKind = "SmallGoodsStraightLineV1";
    public const string RevisionPrefix = "small-goods-straight-line:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result> 준비Async(운송원장 queue, CancellationToken ct = default)
    {
        if ((!options.Value.Enabled && !새정책근거가있는가(queue)) || !생활소형후보(queue)) return Result.Ok();
        var work = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == queue.의뢰Id, ct);
        if (work is null) return Fail("SmallGoodsPricingWorkMissing", "기사 배달료의 원의뢰를 확인해 주세요.");
        if (새정책근거가있는가(queue)) return ValidateFrozen(queue, work, out _);
        if (!소형차량(work.차량종류)) return Result.Ok();
        // 이미 제시된 금액·기존 약정은 새 정책으로 소급 계산하지 않습니다.
        if (기존제안인가(queue, options.Value.SmallGoodsEffectiveFromUtc) || !string.IsNullOrWhiteSpace(queue.기사제안요금계산근거Json)
            || !string.IsNullOrWhiteSpace(queue.기사제안요금정책판본)) return Result.Ok();
        var selected = await db.Set<AgreedRateTableSelectionRecord>().AsNoTracking().SingleOrDefaultAsync(
            x => x.BusinessKind == AgreedRateBusinessKinds.CargoTransport && x.WorkId == work.의뢰Id, ct);
        if (selected is not null) return await ValidateAgreementAsync(work, selected, ct);

        var fare = await db.운임구성.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == work.운임구성Id && x.의뢰Id == work.의뢰Id, ct);
        if (fare is null || !string.IsNullOrWhiteSpace(fare.합의요금표SnapshotJson))
            return Fail("SmallGoodsPricingFareEvidenceMissing", "기사 지급액을 보관할 운임 근거를 확인해 주세요.");
        var policy = options.Value;
        var clockNow = DateTime.UtcNow;
        var now = clockNow.AddTicks(-(clockNow.Ticks % 10)); // SQL datetime(6) 재조회에서도 같은 동결 시각입니다.
        if (string.IsNullOrWhiteSpace(policy.SmallGoodsPolicyRevision) || policy.SmallGoodsPolicyRevision.Length > 128
            || policy.SmallGoodsEffectiveFromUtc is not { Kind: DateTimeKind.Utc } from
            || policy.SmallGoodsEffectiveUntilUtc is not { Kind: DateTimeKind.Utc } until
            || from >= until || now < from || now >= until
            || !ValidMoney(policy.SmallGoodsBaseFeeKrw) || !ValidMoney(policy.SmallGoodsMinimumPayoutKrw)
            || !ValidMoney(policy.SmallGoodsWorkFeeKrw) || !ValidMoney(policy.SmallGoodsSurchargeKrw))
            return Fail("SmallGoodsDriverPricingPolicyUnavailable", "유효한 소형 배송 기사 기본비·최소 지급액 정책이 없어 제안을 보류합니다.");
        if (!CoordinatesMatch(queue, work))
            return Fail("SmallGoodsPricingCoordinatesInvalid", "픽업·전달 좌표를 확인한 뒤 기사 배달료를 계산할 수 있습니다.");

        decimal rawDistanceKm;
        try { rawDistanceKm = Distance(queue); }
        catch (ArgumentException) { return Fail("SmallGoodsPricingCoordinatesInvalid", "픽업·전달 좌표를 확인해 주세요."); }
        var distanceFee = StraightLineDeliveryPricingPolicy.CalculateDistanceFee(rawDistanceKm);
        var pricing = new DriverOfferPricingDto
        {
            DistanceBasisCode = StraightLineDeliveryPricingPolicy.DistanceBasisCode,
            RawDistanceMeters = rawDistanceKm * 1000m,
            RatePerKmKrw = StraightLineDriverPricingOptions.RatePerKmKrw,
            IncludedDistanceMeters = 0m,
            RoundingCode = StraightLineDeliveryPricingPolicy.RoundingCode,
            DistanceFeeKrw = distanceFee,
            BaseFeeKrw = policy.SmallGoodsBaseFeeKrw,
            MinimumPayoutKrw = policy.SmallGoodsMinimumPayoutKrw,
            WorkFeeKrw = policy.SmallGoodsWorkFeeKrw,
            SurchargeKrw = policy.SmallGoodsSurchargeKrw,
            TotalPayoutKrw = Math.Max(policy.SmallGoodsMinimumPayoutKrw!.Value,
                policy.SmallGoodsBaseFeeKrw!.Value + distanceFee + policy.SmallGoodsWorkFeeKrw + policy.SmallGoodsSurchargeKrw),
            PolicyRevision = RevisionPrefix + policy.SmallGoodsPolicyRevision
        };
        var evidence = new SmallGoodsPricingEvidence(EvidenceKind, queue.의뢰Id, work.차량종류,
            CoordinateDigest(queue), pricing, now, from, until);
        queue.기사제안요금계산근거Json = JsonSerializer.Serialize(evidence, JsonOptions);
        queue.기사제안요금정책판본 = pricing.PolicyRevision;
        queue.기사제안요금판정시각Utc = now;
        queue.기사기본거리지급액 = pricing.BaseFeeKrw + pricing.DistanceFeeKrw + pricing.WorkFeeKrw;
        queue.기사지급예정액 = pricing.TotalPayoutKrw;
        return Result.Ok();
    }

    public async Task<Result> 수락검증Async(운송원장 queue, string driverId, CancellationToken ct = default)
    {
        var readable = await 조회검증Async(queue, ct);
        if (readable.IsFailed) return readable;
        // OFF로 되돌려도 이미 제시한 새 정책 금액의 무결성은 확인하고 보존합니다.
        if (!새정책근거가있는가(queue)) return Result.Ok();
        var work = await db.화주운송의뢰.SingleOrDefaultAsync(x => x.의뢰Id == queue.의뢰Id, ct);
        if (work is null) return Fail("SmallGoodsPricingWorkMissing", "기사 배달료의 원의뢰를 확인해 주세요.");
        var validation = ValidateFrozen(queue, work, out var evidence);
        if (validation.IsFailed) return validation;
        if (string.IsNullOrWhiteSpace(driverId)) return Fail("SmallGoodsPricingActorMissing", "수락 기사를 확인해 주세요.");
        if (await db.Set<AgreedRateTableSelectionRecord>().AsNoTracking().AnyAsync(
            x => x.BusinessKind == AgreedRateBusinessKinds.CargoTransport && x.WorkId == queue.의뢰Id, ct))
            return Fail("SmallGoodsPricingAgreementChanged", "제안 이후 선택한 약정이 달라져 최신 배달료를 다시 확인해야 합니다.");
        var fare = await db.운임구성.SingleOrDefaultAsync(x => x.Id == work.운임구성Id && x.의뢰Id == work.의뢰Id, ct);
        if (fare is null || !string.IsNullOrWhiteSpace(fare.합의요금표SnapshotJson))
            return Fail("SmallGoodsPricingFareEvidenceMissing", "기사 지급액을 보관할 운임 근거를 확인해 주세요.");
        // 고객 청구의 최종운임·거리·단가를 변경하지 않습니다.
        fare.기사지급예정운임 = evidence!.Pricing.TotalPayoutKrw;
        return Result.Ok();
    }

    public async Task<Result> 조회검증Async(운송원장 queue, CancellationToken ct = default)
    {
        if (!새정책근거가있는가(queue) && (!options.Value.Enabled || !생활소형후보(queue))) return Result.Ok();
        var work = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == queue.의뢰Id, ct);
        if (work is null) return Fail("SmallGoodsPricingWorkMissing", "기사 배달료의 원의뢰를 확인해 주세요.");
        if (새정책근거가있는가(queue)) return ValidateFrozen(queue, work, out _);
        if (!소형차량(work.차량종류)) return Result.Ok();
        var selected = await db.Set<AgreedRateTableSelectionRecord>().AsNoTracking().SingleOrDefaultAsync(
            x => x.BusinessKind == AgreedRateBusinessKinds.CargoTransport && x.WorkId == work.의뢰Id, ct);
        if (selected is not null) return await ValidateAgreementAsync(work, selected, ct);
        if (기존제안인가(queue, options.Value.SmallGoodsEffectiveFromUtc)
            || !string.IsNullOrWhiteSpace(queue.기사제안요금계산근거Json)
            || !string.IsNullOrWhiteSpace(queue.기사제안요금정책판본) && !새정책근거가있는가(queue)) return Result.Ok();
        return Fail("SmallGoodsDriverPricingNotPrepared", "기사 배달료가 아직 준비되지 않아 제안을 보류합니다.");
    }

    /// <summary>저장된 제안만 읽습니다. GET에서 계산·쓰기·정책 소급 적용을 하지 않습니다.</summary>
    public static DriverOfferPricingDto? TryReadPricing(운송원장 queue)
    {
        if (!새정책근거가있는가(queue)) return null;
        try
        {
            var evidence = JsonSerializer.Deserialize<SmallGoodsPricingEvidence>(queue.기사제안요금계산근거Json ?? "null", JsonOptions);
            return evidence is not null && ValidateFrozenValues(queue, evidence) ? evidence.Pricing : null;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException) { return null; }
    }

    private static Result ValidateFrozen(운송원장 queue, 화주운송의뢰 work, out SmallGoodsPricingEvidence? evidence)
    {
        evidence = null;
        try { evidence = JsonSerializer.Deserialize<SmallGoodsPricingEvidence>(queue.기사제안요금계산근거Json ?? "null", JsonOptions); }
        catch (JsonException) { }
        try
        {
            if (evidence is not null && 생활소형후보(queue) && 소형차량(work.차량종류)
                && CoordinatesMatch(queue, work) && evidence.VehicleType == work.차량종류
                && ValidateFrozenValues(queue, evidence)) return Result.Ok();
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException) { }
        return Fail("SmallGoodsDriverPricingEvidenceInvalid", "저장된 기사 배달료와 픽업·전달 근거가 달라 최신 제안을 확인해 주세요.");
    }

    private static bool ValidateFrozenValues(운송원장 queue, SmallGoodsPricingEvidence evidence)
    {
        var p = evidence.Pricing;
        if (p is null || evidence.Kind != EvidenceKind || evidence.RequestId != queue.의뢰Id
            || evidence.CoordinateDigest != CoordinateDigest(queue) || !소형차량(evidence.VehicleType)
            || evidence.FrozenAtUtc.Kind != DateTimeKind.Utc || evidence.EffectiveFromUtc.Kind != DateTimeKind.Utc
            || evidence.EffectiveUntilUtc.Kind != DateTimeKind.Utc || evidence.EffectiveFromUtc >= evidence.EffectiveUntilUtc
            || evidence.FrozenAtUtc < evidence.EffectiveFromUtc || evidence.FrozenAtUtc >= evidence.EffectiveUntilUtc
            || queue.기사제안요금판정시각Utc != evidence.FrozenAtUtc
            || string.IsNullOrWhiteSpace(p.PolicyRevision) || p.PolicyRevision != queue.기사제안요금정책판본
            || !p.PolicyRevision.StartsWith(RevisionPrefix, StringComparison.Ordinal)
            || p.DistanceBasisCode != StraightLineDeliveryPricingPolicy.DistanceBasisCode
            || p.RoundingCode != StraightLineDeliveryPricingPolicy.RoundingCode || p.RatePerKmKrw != 1000m
            || p.IncludedDistanceMeters != 0m || !ValidMoney(p.BaseFeeKrw) || !ValidMoney(p.MinimumPayoutKrw)
            || !ValidMoney(p.WorkFeeKrw) || !ValidMoney(p.SurchargeKrw)
            || p.RawDistanceMeters != Distance(queue) * 1000m
            || p.DistanceFeeKrw != StraightLineDeliveryPricingPolicy.CalculateDistanceFee(p.RawDistanceMeters / 1000m)
            || p.TotalPayoutKrw != Math.Max(p.MinimumPayoutKrw!.Value,
                p.BaseFeeKrw!.Value + p.DistanceFeeKrw + p.WorkFeeKrw!.Value + p.SurchargeKrw!.Value)
            || queue.기사지급예정액 != p.TotalPayoutKrw
            || queue.기사기본거리지급액 != p.BaseFeeKrw + p.DistanceFeeKrw + p.WorkFeeKrw) return false;
        return true;
    }

    private async Task<Result> ValidateAgreementAsync(화주운송의뢰 work, AgreedRateTableSelectionRecord selected, CancellationToken ct)
    {
        AgreedRateTableSnapshotDto? snapshot;
        try { snapshot = JsonSerializer.Deserialize<AgreedRateTableSnapshotDto>(selected.SnapshotJson, JsonOptions); }
        catch (JsonException) { return Fail("RateTableSelectionEvidenceInvalid", "기존 기사 약정 근거를 확인해 주세요."); }
        if (!AgreedRateTableEvidencePolicy.IsValid(snapshot)) return Fail("RateTableSelectionEvidenceInvalid", "기존 기사 약정 근거를 확인해 주세요.");
        var driver = snapshot!.Proposer.PartyKind == AgreedRatePartyKinds.CargoDriver ? snapshot.Proposer
            : snapshot.Counterparty.PartyKind == AgreedRatePartyKinds.CargoDriver ? snapshot.Counterparty : null;
        if (driver is null) return Fail("RateTableSelectionEvidenceInvalid", "기존 기사 약정 당사자를 확인해 주세요.");
        return await AgreedCargoAcceptancePolicy.CheckAsync(db, work, driver.PartyId, ct) is { } error
            ? Fail(error, "기존 기사 약정의 동결 근거를 확인해 주세요.") : Result.Ok();
    }

    private static bool 생활소형후보(운송원장 queue) => queue.배차업무유형 == 상태값.배차업무유형.용달운송
        && queue.생활배송배차방식 is not null && !queue.일반회원배송전용;
    private static bool 소형차량(string vehicle) => vehicle is "오토바이" or "이륜차" or "Motorcycle" or "자동차" or "승용차" or "Car";
    private static bool 새정책근거가있는가(운송원장 queue) => queue.기사제안요금정책판본?.StartsWith(RevisionPrefix, StringComparison.Ordinal) == true
        || queue.기사제안요금계산근거Json?.Contains(EvidenceKind, StringComparison.Ordinal) == true;
    private static bool 기존제안인가(운송원장 queue, DateTime? policyFromUtc)
    {
        if (queue.배차큐단계 == 상태값.배차큐단계.확정) return true;
        var shownAt = queue.추천시작시각 ?? queue.공개전환시각;
        return shownAt.HasValue && policyFromUtc is { Kind: DateTimeKind.Utc } && shownAt < policyFromUtc;
    }
    private static bool ValidMoney(decimal? money) => money is >= 0m and <= 1000000000m && decimal.Truncate(money.Value) == money.Value;
    private static bool CoordinatesMatch(운송원장 queue, 화주운송의뢰 work) => queue.픽업_위도.HasValue && queue.픽업_경도.HasValue
        && queue.하차_위도.HasValue && queue.하차_경도.HasValue && queue.픽업_위도 == work.픽업_위도
        && queue.픽업_경도 == work.픽업_경도 && queue.하차_위도 == work.하차_위도 && queue.하차_경도 == work.하차_경도;
    private static decimal Distance(운송원장 queue) => StraightLineDeliveryPricingPolicy.CalculateDistanceKm(
        queue.픽업_위도 ?? throw new ArgumentException(), queue.픽업_경도 ?? throw new ArgumentException(),
        queue.하차_위도 ?? throw new ArgumentException(), queue.하차_경도 ?? throw new ArgumentException());
    private static string CoordinateDigest(운송원장 queue) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new
        {
            queue.의뢰Id, PickupLatitude = CanonicalCoordinate(queue.픽업_위도), PickupLongitude = CanonicalCoordinate(queue.픽업_경도),
            DropoffLatitude = CanonicalCoordinate(queue.하차_위도), DropoffLongitude = CanonicalCoordinate(queue.하차_경도)
        }, JsonOptions))));
    private static string? CanonicalCoordinate(decimal? coordinate) => coordinate?.ToString("G29", CultureInfo.InvariantCulture);
    private static Result Fail(string code, string message) => Result.Fail(new Error(message).WithMetadata("StatusCode", 409).WithMetadata("ErrorCode", code));

    private sealed record SmallGoodsPricingEvidence(string Kind, string RequestId, string VehicleType,
        string CoordinateDigest, DriverOfferPricingDto Pricing, DateTime FrozenAtUtc, DateTime EffectiveFromUtc, DateTime EffectiveUntilUtc);
}
