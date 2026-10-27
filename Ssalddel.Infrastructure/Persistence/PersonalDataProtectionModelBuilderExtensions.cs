using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.기사;
using 살뜰.도메인.업체;
using 살뜰.도메인.화주;
using 살뜰.Infrastructure.Security;
using Ssalddel.Domain.Notifications;

namespace 살뜰.Infrastructure.Persistence
{
    public static class PersonalDataProtectionModelBuilderExtensions
    {
        public static void ApplyPersonalDataProtection(this ModelBuilder modelBuilder, IPersonalDataEncryptionService protector)
        {
            modelBuilder.Entity<살뜰.도메인.판매.판매채널계정>().Property(x => x.동기화진단Json)
                .HasConversion(v => protector.Protect(v), v => protector.Unprotect(v));
            modelBuilder.Entity<살뜰.도메인.마트.마트배송시도>().Property(x => x.고객수령근거Json)
                .HasConversion(v => protector.Protect(v), v => protector.Unprotect(v));
            modelBuilder.Entity<살뜰.도메인.창고.피킹포장작업>().Property(x => x.완료근거Json)
                .HasConversion(v => protector.Protect(v), v => protector.Unprotect(v));
            modelBuilder.Entity<살뜰.도메인.창고.분산거점Record>().Property(x => x.PrivateDetailsJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.창고.분산거점Record>().Property(x => x.AcceptancePolicyJson)
                .HasConversion(v => protector.Protect(v), v => protector.Unprotect(v));
            modelBuilder.Entity<살뜰.도메인.창고.분산재고LotRecord>().Property(x => x.InboundCollaborationJson)
                .HasConversion(v => protector.Protect(v), v => protector.Unprotect(v));
            modelBuilder.Entity<살뜰.도메인.창고.분산보관합의Record>().Property(x => x.PendingTermsJson)
                .HasConversion(v => protector.Protect(v), v => protector.Unprotect(v));
            modelBuilder.Entity<살뜰.도메인.창고.분산배송WaveRecord>().Property(x => x.RouteJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.창고.분산보관합의Record>().Property(x => x.TermsJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.창고.분산보관합의Record>().Property(x => x.HistoryJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.창고.분산재고LotRecord>().Property(x => x.StorageAgreementSnapshotJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.창고.분산물류CommandReceipt>().Property(x => x.CompletionLinesJson)
                .HasConversion(v => protector.Protect(v), v => protector.Unprotect(v));
            modelBuilder.Entity<살뜰.도메인.창고.분산사진증빙Record>().Property(x => x.PrivateDetailsJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.창고.분산사진보존정지Record>().Property(x => x.LegalBasisJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.정산.NeighborhoodSettlementAgreementRecord>().Property(x => x.TermsJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.정산.NeighborhoodSettlementAgreementRecord>().Property(x => x.SourceSnapshotJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.정산.NeighborhoodSettlementAgreementRecord>().Property(x => x.ConsentsJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.정산.NeighborhoodSettlementAgreementRecord>().Property(x => x.HistoryJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.정산.NeighborhoodSettlementAgreementVersionRecord>().Property(x => x.TermsJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.정산.NeighborhoodSettlementAgreementVersionRecord>().Property(x => x.SourceSnapshotJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.정산.NeighborhoodSettlementAgreementVersionRecord>().Property(x => x.ConsentsJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.정산.NeighborhoodSettlementEntryRecord>().Property(x => x.DetailsJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.정산.NeighborhoodSettlementPaymentRecord>().Property(x => x.DetailsJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.정산.NeighborhoodSettlementCommandReceiptRecord>().Property(x => x.ResponseJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<살뜰.도메인.정산.NeighborhoodInventoryCostAttributionRecord>().Property(x => x.DetailsJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            ArgumentNullException.ThrowIfNull(modelBuilder);
            ArgumentNullException.ThrowIfNull(protector);

            modelBuilder.Entity<운송자격Record>().Property(x => x.PrivateDetailsJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<운송자격Record>().Property(x => x.HistoryJson)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
            modelBuilder.Entity<용달기사>().Property(x => x.현재실차참조)
                .HasMaxLength(1000).HasConversion(v => protector.Protect(v), v => protector.Unprotect(v));
            modelBuilder.Entity<용달기사>().Property(x => x.실차적재제원Json)
                .HasColumnType("longtext").HasConversion(v => protector.Protect(v), v => protector.Unprotect(v));
            modelBuilder.Entity<용달기사>().Property(x => x.실차적재판본).IsConcurrencyToken();
            modelBuilder.Entity<배달기사>().Property(x => x.현재실차참조)
                .HasMaxLength(1000).HasConversion(v => protector.Protect(v), v => protector.Unprotect(v));

            ConfigurePersonalData(modelBuilder, protector);
        }

        private static void ConfigurePersonalData(ModelBuilder modelBuilder, IPersonalDataEncryptionService protector)
        {
            modelBuilder.Entity<ApplicationUser>()
                .Property(x => x.BusinessRegistrationNumber)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<업체>()
                .Property(x => x.대표_연락처)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<업체>()
                .Property(x => x.담당자)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<업체>()
                .Property(x => x.이메일)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<업체>()
                .Property(x => x.주소)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<배달기사>()
                .Property(x => x.연락처)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<용달기사>()
                .Property(x => x.연락처)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<기사정산계좌>()
                .Property(x => x.예금주명)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<기사정산계좌>()
                .Property(x => x.계좌번호)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<화주운송의뢰>()
                .Property(x => x.픽업_도로명주소)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<화주운송의뢰>()
                .Property(x => x.픽업_상세주소)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<화주운송의뢰>()
                .Property(x => x.픽업_연락처_이름)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<화주운송의뢰>()
                .Property(x => x.픽업_연락처_전화번호)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<화주운송의뢰>()
                .Property(x => x.하차_도로명주소)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<화주운송의뢰>()
                .Property(x => x.하차_상세주소)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<화주운송의뢰>()
                .Property(x => x.하차_연락처_이름)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<화주운송의뢰>()
                .Property(x => x.하차_연락처_전화번호)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);

            modelBuilder.Entity<SsalddelMobilePushInstallation>()
                .Property(x => x.PushToken)
                .HasConversion(v => protector.Protect(v)!, v => protector.Unprotect(v)!);
        }
    }
}
