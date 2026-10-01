using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Hongdal.Domain.Community;
using Hongdal.Domain.Content;
using Hongdal.Domain.Education;
using Hongdal.Domain.HumanResources;
using Hongdal.Domain.HsCodes;
using Hongdal.Domain.Speech;
using 홍달.도메인.기사;
using 홍달.도메인.업체;
using 홍달.도메인.배차;
using 홍달.도메인.결제;
using 홍달.도메인.차량;
using 홍달.도메인.화물;
using 홍달.도메인.탐색캠페인;
using 홍달.도메인.설정;
using 홍달.도메인.공통;
using 홍달.도메인.사용자;
using 홍달.도메인.운송;
using 홍달.도메인.창고;
using 홍달.도메인.판매;
using 홍달.도메인.화주;
using 홍달.도메인.공통콘텐츠;
using 홍달.도메인.통관;
using 홍달.도메인.정산;
using 홍달.도메인.음식;
using 홍달.도메인.마트;
using 홍달.Infrastructure.Persistence;
using 홍달.Infrastructure.Security;

namespace 홍달.Data
{
    public class HongdalContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IPersonalDataEncryptionService _personalDataProtector;

        public HongdalContext(DbContextOptions<HongdalContext> options, IPersonalDataEncryptionService personalDataProtector) : base(options)
        {
            _personalDataProtector = personalDataProtector;
        }

        public DbSet<업체> 업체 { get; set; } = null!;
        public DbSet<배달기사> 배달기사 { get; set; } = null!;
        public DbSet<용달기사> 용달기사 { get; set; } = null!;
        public DbSet<기사근무> 기사근무 { get; set; } = null!;
        public DbSet<기사위치기록> 기사위치기록 { get; set; } = null!;
        public DbSet<기사월정산> 기사월정산 { get; set; } = null!;
        public DbSet<차량제원> 차량제원 { get; set; } = null!;
        public DbSet<탐색캠페인> 탐색캠페인 { get; set; } = null!;
        public DbSet<탐색캠페인대상자> 탐색캠페인대상자 { get; set; } = null!;
        public DbSet<탐색캠페인응답> 탐색캠페인응답 { get; set; } = null!;
        public DbSet<기사화주관계집계> 기사화주관계집계 { get; set; } = null!;

        public DbSet<배차계획신청> 배차계획신청 { get; set; } = null!;
        public DbSet<기사배차> 기사배차 { get; set; } = null!;

        public DbSet<화주운송의뢰> 화주운송의뢰 { get; set; } = null!;
        public DbSet<화물요구조건> 화물요구조건 { get; set; } = null!;
        public DbSet<운송원장> 운송원장 { get; set; } = null!;
        public DbSet<운송이벤트> 운송이벤트 { get; set; } = null!;
        public DbSet<운송의뢰상품연결> 운송의뢰상품연결 { get; set; } = null!;

        public DbSet<운임구성> 운임구성 { get; set; } = null!;
        public DbSet<차량단가> 차량단가 { get; set; } = null!;
        public DbSet<결제> 결제 { get; set; } = null!;

        public DbSet<사용자Command기능설정> 사용자Command기능설정 { get; set; } = null!;
        public DbSet<Command알림Outbox> Command알림Outbox { get; set; } = null!;
        public DbSet<배차추천알림Outbox> 배차추천알림Outbox { get; set; } = null!;
        public DbSet<결제승인완료Outbox> 결제승인완료Outbox { get; set; } = null!;
        public DbSet<플랫폼View정책> 플랫폼View정책 { get; set; } = null!;
        public DbSet<사용자View설정> 사용자View설정 { get; set; } = null!;
        public DbSet<사용자행위로그> 사용자행위로그 { get; set; } = null!;
        public DbSet<생성이미지작업> 생성이미지작업 { get; set; } = null!;

        public DbSet<주문자프로필> 주문자프로필 { get; set; } = null!;
        public DbSet<홍달참여자> 홍달참여자 { get; set; } = null!;
        public DbSet<홍달참여자역할> 홍달참여자역할 { get; set; } = null!;
        public DbSet<HrRoleAssignmentRecord> HrRoleAssignments { get; set; } = null!;
        public DbSet<HrEmploymentContractRecord> HrEmploymentContracts { get; set; } = null!;
        public DbSet<HrPayrollScheduleRecord> HrPayrollSchedules { get; set; } = null!;
        public DbSet<WorkRelationshipSnapshotRecord> WorkRelationshipSnapshots { get; set; } = null!;
        public DbSet<인연연결요청> 인연연결요청 { get; set; } = null!;
        public DbSet<연락처공개동의> 연락처공개동의 { get; set; } = null!;
        public DbSet<관세사프로필> 관세사프로필 { get; set; } = null!;

        public DbSet<창고> 창고 { get; set; } = null!;
        public DbSet<창고사용자> 창고사용자 { get; set; } = null!;
        public DbSet<입고요청> 입고요청 { get; set; } = null!;
        public DbSet<입고상품> 입고상품 { get; set; } = null!;
        public DbSet<재고이력> 재고이력 { get; set; } = null!;
        public DbSet<출고묶음> 출고묶음 { get; set; } = null!;
        public DbSet<출고예정> 출고예정 { get; set; } = null!;
        public DbSet<피킹포장작업> 피킹포장작업 { get; set; } = null!;
        public DbSet<재고이동> 재고이동 { get; set; } = null!;
        public DbSet<통관절차> 통관절차 { get; set; } = null!;
        public DbSet<통관수임> 통관수임 { get; set; } = null!;
        public DbSet<통관조회연동> 통관조회연동 { get; set; } = null!;
        public DbSet<HsCodeCatalogVersion> HsCodeCatalogVersions { get; set; } = null!;
        public DbSet<HsCodeEntry> HsCodeEntries { get; set; } = null!;
        public DbSet<HsCodeEntryRiskTag> HsCodeEntryRiskTags { get; set; } = null!;
        public DbSet<HsCodeClassificationCase> HsCodeClassificationCases { get; set; } = null!;
        public DbSet<HsCodePlatformAgencyExperience> HsCodePlatformAgencyExperiences { get; set; } = null!;
        public DbSet<Typecast음성> Typecast음성 { get; set; } = null!;
        public DbSet<Typecast음성모델> Typecast음성모델 { get; set; } = null!;
        public DbSet<Typecast음성용도> Typecast음성용도 { get; set; } = null!;
        public DbSet<YouTube감시채널> YouTube감시채널 { get; set; } = null!;
        public DbSet<YouTube채널영상> YouTube채널영상 { get; set; } = null!;
        public DbSet<YouTube영상상품후보> YouTube영상상품후보 { get; set; } = null!;
        public DbSet<HongikHakdangCardCollection> HongikHakdangCardCollections { get; set; } = null!;
        public DbSet<HongikHakdangCard> HongikHakdangCards { get; set; } = null!;
        public DbSet<HongikHakdangCardCollectionItem> HongikHakdangCardCollectionItems { get; set; } = null!;
        public DbSet<HongikHakdangCardImageVariant> HongikHakdangCardImageVariants { get; set; } = null!;
        public DbSet<HongikHakdangCardDeliveryPreference> HongikHakdangCardDeliveryPreferences { get; set; } = null!;
        public DbSet<HongikHakdangDailyCardSelection> HongikHakdangDailyCardSelections { get; set; } = null!;
        public DbSet<HongikHakdangCardDeliveryOutbox> HongikHakdangCardDeliveryOutbox { get; set; } = null!;
        public DbSet<Hongdal.Domain.Notifications.HongdalMobilePushInstallation> HongdalMobilePushInstallations { get; set; } = null!;
        public DbSet<교육과정> 교육과정 { get; set; } = null!;
        public DbSet<교육과정과목> 교육과정과목 { get; set; } = null!;
        public DbSet<교육과정양식> 교육과정양식 { get; set; } = null!;
        public DbSet<교육과정신청> 교육과정신청 { get; set; } = null!;
        public DbSet<교육과정등록> 교육과정등록 { get; set; } = null!;
        public DbSet<교육과정참석기록> 교육과정참석기록 { get; set; } = null!;
        public DbSet<교육과정과제제출> 교육과정과제제출 { get; set; } = null!;

        public DbSet<판매채널계정> 판매채널계정 { get; set; } = null!;
        public DbSet<판매상품> 판매상품 { get; set; } = null!;
        public DbSet<채널출품> 채널출품 { get; set; } = null!;
        public DbSet<상품식별코드맵> 상품식별코드맵 { get; set; } = null!;
        public DbSet<상품물류자산> 상품물류자산 { get; set; } = null!;
        public DbSet<상품상세이미지생성작업> 상품상세이미지생성작업 { get; set; } = null!;
        public DbSet<상품판매이미지초안> 상품판매이미지초안 { get; set; } = null!;
        public DbSet<감사메시지> 감사메시지 { get; set; } = null!;
        public DbSet<음식주문> 음식주문 { get; set; } = null!;
        public DbSet<음식주문상품> 음식주문상품 { get; set; } = null!;
        public DbSet<음식주문상태이력> 음식주문상태이력 { get; set; } = null!;
        public DbSet<마트주문> 마트주문 { get; set; } = null!;
        public DbSet<마트주문상품> 마트주문상품 { get; set; } = null!;

        public DbSet<홍달공통콘텐츠> 홍달공통콘텐츠 { get; set; } = null!;
        public DbSet<홍달콘텐츠보상정책> 홍달콘텐츠보상정책 { get; set; } = null!;
        public DbSet<홍달콘텐츠시청세션> 홍달콘텐츠시청세션 { get; set; } = null!;
        public DbSet<홍달콘텐츠보상지급> 홍달콘텐츠보상지급 { get; set; } = null!;
        public DbSet<PlatformRevenueEntryRecord> PlatformRevenueEntries { get; set; } = null!;
        public DbSet<PlatformProfitReturnPolicyRecord> PlatformProfitReturnPolicies { get; set; } = null!;
        public DbSet<PlatformProfitReturnScheduleRecord> PlatformProfitReturnSchedules { get; set; } = null!;
        public DbSet<PlatformCommunityPost> PlatformCommunityPosts { get; set; } = null!;
        public DbSet<PlatformCommunityPostTranslation> PlatformCommunityPostTranslations { get; set; } = null!;
        public DbSet<PlatformCommunityBoardRequest> PlatformCommunityBoardRequests { get; set; } = null!;
        public DbSet<PlatformCommunityPostAttachment> PlatformCommunityPostAttachments { get; set; } = null!;
        public DbSet<PlatformCommunityPostAttachmentComment> PlatformCommunityPostAttachmentComments { get; set; } = null!;
        public DbSet<PlatformCommunityPostComment> PlatformCommunityPostComments { get; set; } = null!;
        public DbSet<PlatformCommunityPostRecommendation> PlatformCommunityPostRecommendations { get; set; } = null!;
        public DbSet<CommunityKeywordSubscription> CommunityKeywordSubscriptions { get; set; } = null!;
        public DbSet<PlatformCommunityPostKeywordScan> PlatformCommunityPostKeywordScans { get; set; } = null!;
        public DbSet<CommunityKeywordNotification> CommunityKeywordNotifications { get; set; } = null!;
        public DbSet<CommunityKeywordNotificationDelivery> CommunityKeywordNotificationDeliveries { get; set; } = null!;
        public DbSet<PlatformCommunityPostAudio> PlatformCommunityPostAudio { get; set; } = null!;
        public DbSet<PlatformCommunityPostAudioSegment> PlatformCommunityPostAudioSegments { get; set; } = null!;
        public DbSet<PlatformCommunityPostAudioAccessLog> PlatformCommunityPostAudioAccessLogs { get; set; } = null!;
        public DbSet<커뮤니티원장상태이벤트> 커뮤니티원장상태이벤트 { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyPersonalDataProtection(_personalDataProtector);
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(HongdalContext).Assembly,
                configurationType =>
                    !string.Equals(
                        configurationType.Namespace,
                        "Hongdal.Infrastructure.Persistence.AgriculturalFisheries",
                        StringComparison.Ordinal)
                    && !string.Equals(
                        configurationType.Namespace,
                        "Hongdal.Infrastructure.Persistence.TraditionalMarkets",
                        StringComparison.Ordinal));

        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            배차엔진판단감사불변성검사();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            배차엔진판단감사불변성검사();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void 배차엔진판단감사불변성검사()
        {
            var changedAuditEntry = ChangeTracker
                .Entries<운송이벤트>()
                .FirstOrDefault(entry =>
                    entry.State is EntityState.Modified or EntityState.Deleted
                    && (string.Equals(
                            entry.Entity.이벤트타입,
                            운송이벤트유형.배차엔진판단감사,
                            StringComparison.Ordinal)
                        || string.Equals(
                            entry.Property(x => x.이벤트타입).OriginalValue,
                            운송이벤트유형.배차엔진판단감사,
                            StringComparison.Ordinal)));

            if (changedAuditEntry is not null)
            {
                throw new InvalidOperationException(
                    $"배차 엔진 판단 감사 이벤트는 추가 후 수정하거나 삭제할 수 없습니다. EventId={changedAuditEntry.Entity.Id}");
            }
        }
    }
}
