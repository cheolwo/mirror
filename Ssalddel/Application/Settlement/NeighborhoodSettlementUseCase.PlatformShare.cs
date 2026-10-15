using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.Data;
using 살뜰.도메인.정산;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;
using R = Ssalddel.Contracts.Common.Settlement.NeighborhoodActivityRewardCodes;

namespace Ssalddel.Application.Settlement;

/// <summary>
/// 비용·예비금을 제외한 잔여금의 명시적 플랫폼 몫입니다. 실제 입금·영업이익 인증이 아니며
/// 기본 보수를 줄이거나 이미 참여자에게 배분한 잔여금을 기간 환원으로 다시 나누지 않습니다.
/// </summary>
public sealed partial class NeighborhoodSettlementUseCase
{
    private const string PlatformShareKind = "PlatformShare";
    private const string RetainedSurplusRevenueSource = "NeighborhoodRetainedSurplus";

    private async Task ValidatePlatformShareTerms(NeighborhoodSettlementAgreementRequest terms,
        NeighborhoodSettlementSourceSnapshot source, CancellationToken ct)
    {
        if (terms.ActivityRewardTerms?.Economics is not { } economics) return;
        Require(terms.SourceKind == C.DistributedOrder && source.SourceKind == C.DistributedOrder
            && terms.ActivityKind == C.StoredGoods, "PlatformShareOrderSourceRequired", 400);
        Require(terms.ActivityRewardTerms is { } reward && Party(source, reward.PayerUserId)
            && (Role(source, reward.PayerUserId, "Owner") || Role(source, reward.PayerUserId, "Seller")),
            "PlatformShareFundingPayerRequired", 403);
        Text(economics.PlatformPayeeUserId);
        Require(!string.IsNullOrWhiteSpace(options.Value.PlatformPayeeUserId)
            && economics.PlatformPayeeUserId == options.Value.PlatformPayeeUserId,
            "PlatformShareConfiguredPayeeRequired", 403);
        var administrator = await (from user in db.Users.AsNoTracking()
            join membership in db.UserRoles.AsNoTracking() on user.Id equals membership.UserId
            join role in db.Roles.AsNoTracking() on membership.RoleId equals role.Id
            where user.Id == economics.PlatformPayeeUserId && role.Name == 역할명.서버관리자
            select user.Id).AnyAsync(ct);
        Require(administrator, "PlatformShareAdministratorRequired", 403);
        Require(economics.BasisCode == "PostCostSurplus", "PlatformShareBasisInvalid", 400);
        Require(economics.PlatformRetentionRatePercent is 5m or 10m,
            "PlatformShareRetentionRateInvalid", 400);
        Require(terms.ActivityRewardTerms!.Policy.ReturnRatePercent == 100m - economics.PlatformRetentionRatePercent,
            "PlatformShareReturnRateMismatch", 400);
    }

    // 호출자는 성과금 발생 Execute transaction을 소유합니다. 여기서는 저장·commit하지 않습니다.
    private async Task<string?> AccruePlatformShare(NeighborhoodSettlementAgreementRecord row,
        NeighborhoodActivityRewardPreviewDto preview, CancellationToken ct)
    {
        var terms = Terms(row);
        if (terms.ActivityRewardTerms?.Economics is not { } economics) return null;
        await ValidatePlatformShareTerms(terms, Source(row), ct);
        Require(db.Database.CurrentTransaction is not null, "PlatformShareAtomicTransactionRequired");
        Require(preview.StatusCode == R.Ready && preview.CalculationDigest.Length > 0
            && preview.SurplusAmountKrw is { } surplus && surplus >= 0 && decimal.Floor(surplus) == surplus
            && preview.PoolAmountKrw is { } pool && pool >= 0 && decimal.Floor(pool) == pool && pool <= surplus
            && preview.Allocations.Sum(x => x.AmountKrw) == pool,
            "PlatformShareCalculationInvalid");
        var retained = preview.SurplusAmountKrw!.Value - preview.PoolAmountKrw!.Value;
        var revenueId = RetainedRevenueId(row.SourceKind, row.SourceId);
        Require(!await db.PlatformRevenueEntries.AsNoTracking().AnyAsync(x => x.Id == revenueId, ct),
            "PlatformShareAlreadyAccrued");
        var payer = terms.ActivityRewardTerms!.PayerUserId;
        var entry = new NeighborhoodSettlementEntryDto
        {
            EntryId = NewId("nse"), AgreementId = row.AgreementId, TermsVersion = row.TermsVersion,
            LineKey = "platform-share", Kind = PlatformShareKind, Billing = C.Fixed,
            AmountKrw = retained, PayerUserId = payer, PayeeUserId = economics.PlatformPayeeUserId,
            BeneficiaryRole = "Platform", OriginKind = row.SourceKind, OriginId = row.SourceId,
            OriginRevision = row.Revision, IsInternal = payer == economics.PlatformPayeeUserId,
            CalculationBasis = "비용·예비금 제외 잔여금 − 참여자 정수 원 성과금 풀 · 풀 내림 잔액 포함 · Simulation 계산 기록",
            ActivityRewardCalculationDigest = preview.CalculationDigest,
            WorkEvidenceReferences = preview.ContributionShares.SelectMany(x => x.Contribution.EvidenceReferences)
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            StatusCode = C.Accrued, Revision = 1, RecordedAtUtc = Now
        };
        db.Add(new NeighborhoodSettlementEntryRecord
        {
            EntryId = entry.EntryId, AgreementId = row.AgreementId, TermsVersion = row.TermsVersion,
            LineKey = entry.LineKey, Kind = PlatformShareKind, OriginKind = row.SourceKind, OriginId = row.SourceId,
            OriginRevision = row.Revision, AccrualKey = Digest(new { Kind = PlatformShareKind, row.SourceKind, row.SourceId }),
            DetailsJson = Serialize(entry), StatusCode = C.Accrued, RecordedAtUtc = Now
        });
        db.PlatformRevenueEntries.Add(new PlatformRevenueEntryRecord
        {
            Id = revenueId, RevenueSource = RetainedSurplusRevenueSource,
            SourceReferenceType = row.SourceKind, SourceReferenceId = row.SourceId,
            PayerUserId = payer, RelatedParticipantUserId = economics.PlatformPayeeUserId,
            GrossAmount = preview.SurplusAmountKrw.Value, PlatformRevenueAmount = retained,
            CurrencyCode = "KRW", OccurredAtUtc = Now, CreatedAtUtc = Now,
            Memo = "Simulation 잔여금 배분 계산 기록 · 실제 수입/송금/영업이익 인증 아님 · 주문 성과금과 같은 원자적 기록 · 기간 환원 제외"
        });
        return entry.EntryId;
    }

    // 약정·수령자·정책 판본이 바뀌어도 동일 원주문의 플랫폼 몫을 다시 발생시키지 않습니다.
    private static Guid RetainedRevenueId(string sourceKind, string sourceId)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes(RetainedSurplusRevenueSource + "\n" + sourceKind + "\n" + sourceId))[..16]);
}
