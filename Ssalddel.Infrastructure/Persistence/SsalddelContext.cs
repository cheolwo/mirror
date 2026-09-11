using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.Community;
using Ssalddel.Domain.Content;
using Ssalddel.Domain.Education;
using Ssalddel.Domain.HumanResources;
using Ssalddel.Domain.HsCodes;
using Ssalddel.Domain.Geography;
using Ssalddel.Domain.Speech;
using Ssalddel.Infrastructure.Persistence;
using 살뜰.도메인.기사;
using 살뜰.도메인.업체;
using 살뜰.도메인.배차;
using 살뜰.도메인.배달권;
using 살뜰.도메인.결제;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.탐색캠페인;
using 살뜰.도메인.설정;
using 살뜰.도메인.공통;
using 살뜰.도메인.사용자;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.판매;
using 살뜰.도메인.화주;
using 살뜰.도메인.공통콘텐츠;
using 살뜰.도메인.통관;
using 살뜰.도메인.정산;
using 살뜰.도메인.음식;
using 살뜰.도메인.마트;
using 살뜰.도메인.공급중개;
using 살뜰.도메인.농업;
using 살뜰.Infrastructure.Persistence;
using 살뜰.Infrastructure.Security;

namespace 살뜰.Data
{
    public class SsalddelContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IPersonalDataEncryptionService _personalDataProtector;

        public SsalddelContext(DbContextOptions<SsalddelContext> options, IPersonalDataEncryptionService personalDataProtector) : base(options)
        {
            _personalDataProtector = personalDataProtector;
        }

        public DbSet<업체> 업체 { get; set; } = null!;
        public DbSet<배달기사> 배달기사 { get; set; } = null!;
        public DbSet<용달기사> 용달기사 { get; set; } = null!;
        public DbSet<기사근무> 기사근무 { get; set; } = null!;
        public DbSet<기사위치기록> 기사위치기록 { get; set; } = null!;
        public DbSet<기사월정산> 기사월정산 { get; set; } = null!;
        public DbSet<기사운송대금지급요청> 기사운송대금지급요청 { get; set; } = null!;
        public DbSet<차량제원> 차량제원 { get; set; } = null!;
        public DbSet<탐색캠페인> 탐색캠페인 { get; set; } = null!;
        public DbSet<탐색캠페인대상자> 탐색캠페인대상자 { get; set; } = null!;
        public DbSet<탐색캠페인응답> 탐색캠페인응답 { get; set; } = null!;
        public DbSet<기사화주관계집계> 기사화주관계집계 { get; set; } = null!;

        public DbSet<배차계획신청> 배차계획신청 { get; set; } = null!;
        public DbSet<기사배차> 기사배차 { get; set; } = null!;
        public DbSet<운영배차활동사건> 운영배차활동사건 { get; set; } = null!;
        public DbSet<플랫폼배달권> 플랫폼배달권 { get; set; } = null!;
        public DbSet<원장배달권투영> 원장배달권투영 { get; set; } = null!;

        public DbSet<화주운송의뢰> 화주운송의뢰 { get; set; } = null!;
        public DbSet<화물요구조건> 화물요구조건 { get; set; } = null!;
        public DbSet<운송원장> 운송원장 { get; set; } = null!;
        public DbSet<운송이벤트> 운송이벤트 { get; set; } = null!;
        public DbSet<운송의뢰상품연결> 운송의뢰상품연결 { get; set; } = null!;
        public DbSet<화물연속배차상태> 화물연속배차상태 { get; set; } = null!;
        public DbSet<화물운송시간약속> 화물운송시간약속 { get; set; } = null!;
        public DbSet<화물다음콜예약> 화물다음콜예약 { get; set; } = null!;
        public DbSet<살뜰.도메인.운영.운영체제업무인계> 운영체제업무인계 { get; set; } = null!;
        public DbSet<살뜰.도메인.운영.운영체제업무인계Outbox> 운영체제업무인계Outbox { get; set; } = null!;

        public DbSet<운임구성> 운임구성 { get; set; } = null!;
        public DbSet<차량단가> 차량단가 { get; set; } = null!;
        public DbSet<결제> 결제 { get; set; } = null!;

        public DbSet<사용자Command기능설정> 사용자Command기능설정 { get; set; } = null!;
        public DbSet<Command알림Outbox> Command알림Outbox { get; set; } = null!;
        public DbSet<배차추천알림Outbox> 배차추천알림Outbox { get; set; } = null!;
        public DbSet<결제승인완료Outbox> 결제승인완료Outbox { get; set; } = null!;
        public DbSet<기사지급Outbox> 기사지급Outbox { get; set; } = null!;
        public DbSet<음식마트원장동기화Outbox> 음식마트원장동기화Outbox { get; set; } = null!;
        public DbSet<플랫폼View정책> 플랫폼View정책 { get; set; } = null!;
        public DbSet<사용자View설정> 사용자View설정 { get; set; } = null!;
        public DbSet<사용자행위로그> 사용자행위로그 { get; set; } = null!;
        public DbSet<생성이미지작업> 생성이미지작업 { get; set; } = null!;
        public DbSet<앱문맥이미지자산> 앱문맥이미지자산들 { get; set; } = null!;

        public DbSet<주문자프로필> 주문자프로필 { get; set; } = null!;
        public DbSet<살뜰참여자> 살뜰참여자 { get; set; } = null!;
        public DbSet<살뜰참여자역할> 살뜰참여자역할 { get; set; } = null!;
        public DbSet<HrRoleAssignmentRecord> HrRoleAssignments { get; set; } = null!;
        public DbSet<HrRoleApplicationRecord> HrRoleApplications { get; set; } = null!;
        public DbSet<HrEmploymentContractRecord> HrEmploymentContracts { get; set; } = null!;
        public DbSet<HrPayrollScheduleRecord> HrPayrollSchedules { get; set; } = null!;
        public DbSet<WorkRelationshipSnapshotRecord> WorkRelationshipSnapshots { get; set; } = null!;
        public DbSet<친구요청> 친구요청 { get; set; } = null!;
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
        public DbSet<지역문화이미지Prompt> 지역문화이미지Prompts { get; set; } = null!;
        public DbSet<지역문화공공기관Source> 지역문화공공기관Sources { get; set; } = null!;
        public DbSet<지역농수산Map행정구역> 지역농수산Map행정구역들 { get; set; } = null!;
        public DbSet<지역농수산Map행정구역CodeAssignment> 지역농수산Map행정구역CodeAssignments { get; set; } = null!;
        public DbSet<지역농수산Map행정구역Boundary> 지역농수산Map행정구역Boundaries { get; set; } = null!;
        public DbSet<지역농수산Map지역Crosswalk> 지역농수산Map지역Crosswalks { get; set; } = null!;
        public DbSet<Ssalddel.Domain.Notifications.SsalddelMobilePushInstallation> SsalddelMobilePushInstallations { get; set; } = null!;
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
        public DbSet<음식점공개프로필> 음식점공개프로필 { get; set; } = null!;
        public DbSet<음식점메뉴> 음식점메뉴 { get; set; } = null!;
        public DbSet<음식주문> 음식주문 { get; set; } = null!;
        public DbSet<음식주문상품> 음식주문상품 { get; set; } = null!;
        public DbSet<음식주문상태이력> 음식주문상태이력 { get; set; } = null!;
        public DbSet<음식배달완료WorldSnapshot> 음식배달완료WorldSnapshot { get; set; } = null!;
        public DbSet<음식점조리시간설정> 음식점조리시간설정 { get; set; } = null!;
        public DbSet<음식배달시도> 음식배달시도 { get; set; } = null!;
        public DbSet<음식점리뷰> 음식점리뷰 { get; set; } = null!;
        public DbSet<음식운영정책> 음식운영정책 { get; set; } = null!;
        public DbSet<마트공개상품> 마트공개상품 { get; set; } = null!;
        public DbSet<마트주문요청> 마트주문요청 { get; set; } = null!;
        public DbSet<마트주문> 마트주문 { get; set; } = null!;
        public DbSet<마트주문상품> 마트주문상품 { get; set; } = null!;
        public DbSet<플랫폼공급조건계약> 플랫폼공급조건계약 { get; set; } = null!;
        public DbSet<플랫폼공급조건계약품목> 플랫폼공급조건계약품목 { get; set; } = null!;
        public DbSet<공급계약이용등록> 공급계약이용등록 { get; set; } = null!;
        public DbSet<조직개별공급발주> 조직개별공급발주 { get; set; } = null!;

        public DbSet<살뜰공통콘텐츠> 살뜰공통콘텐츠 { get; set; } = null!;
        public DbSet<살뜰콘텐츠보상정책> 살뜰콘텐츠보상정책 { get; set; } = null!;
        public DbSet<살뜰콘텐츠시청세션> 살뜰콘텐츠시청세션 { get; set; } = null!;
        public DbSet<살뜰콘텐츠보상지급> 살뜰콘텐츠보상지급 { get; set; } = null!;
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
        public DbSet<CommunityPostEmailNotificationOutbox> CommunityPostEmailNotificationOutbox { get; set; } = null!;
        public DbSet<CommunityKeywordSubscription> CommunityKeywordSubscriptions { get; set; } = null!;
        public DbSet<PlatformCommunityPostKeywordScan> PlatformCommunityPostKeywordScans { get; set; } = null!;
        public DbSet<CommunityKeywordNotification> CommunityKeywordNotifications { get; set; } = null!;
        public DbSet<CommunityKeywordNotificationDelivery> CommunityKeywordNotificationDeliveries { get; set; } = null!;
        public DbSet<PlatformCommunityPostAudio> PlatformCommunityPostAudio { get; set; } = null!;
        public DbSet<PlatformCommunityPostAudioSegment> PlatformCommunityPostAudioSegments { get; set; } = null!;
        public DbSet<PlatformCommunityPostAudioAccessLog> PlatformCommunityPostAudioAccessLogs { get; set; } = null!;
        public DbSet<커뮤니티원장상태이벤트> 커뮤니티원장상태이벤트 { get; set; } = null!;
        public DbSet<커뮤니티활동공개Projection> 커뮤니티활동공개Projections { get; set; } = null!;
        public DbSet<커뮤니티활동처리기록> 커뮤니티활동처리기록 { get; set; } = null!;
        public DbSet<커뮤니티활동유료상세> 커뮤니티활동유료상세목록 { get; set; } = null!;
        public DbSet<커뮤니티활동상세열람권> 커뮤니티활동상세열람권목록 { get; set; } = null!;
        public DbSet<커뮤니티활동상세구매> 커뮤니티활동상세구매목록 { get; set; } = null!;
        public DbSet<커뮤니티활동상세구매상태이력> 커뮤니티활동상세구매상태이력목록 { get; set; } = null!;
        public DbSet<농장> 농장 { get; set; } = null!;
        public DbSet<농장구획> 농장구획 { get; set; } = null!;
        public DbSet<재배작기> 재배작기 { get; set; } = null!;
        public DbSet<농업센서> 농업센서 { get; set; } = null!;
        public DbSet<농업센서관측> 농업센서관측 { get; set; } = null!;
        public DbSet<농장작업> 농장작업 { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyPersonalDataProtection(_personalDataProtector);
            // 같은 assembly에 있는 전용 Context 구성은 명시적 소유권 표식으로 격리합니다.
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(SsalddelContext).Assembly,
                configurationType =>
                    !typeof(IDedicatedDbContextConfiguration)
                        .IsAssignableFrom(configurationType));

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
