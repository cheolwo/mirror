using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.화주;

namespace 살뜰.Infrastructure.Persistence.Configurations.Shipper;

public sealed class 화주운송의뢰Configuration : IEntityTypeConfiguration<화주운송의뢰>
{
    public void Configure(EntityTypeBuilder<화주운송의뢰> builder)
    {
        builder.ToTable("shipper_requests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.의뢰Id).HasColumnName("request_id").IsRequired();
        builder.Property(x => x.화주Id).HasColumnName("shipper_id").IsRequired();
        builder.Property(x => x.주문자UserId).HasColumnName("orderer_user_id").IsRequired();
        builder.Property(x => x.화물종류).HasColumnName("cargo_type").IsRequired();
        builder.Property(x => x.화물설명).HasColumnName("cargo_description").IsRequired();
        builder.Property(x => x.화물수량).HasColumnName("cargo_quantity");
        builder.Property(x => x.화물길이Mm).HasColumnName("cargo_length_mm");
        builder.Property(x => x.화물폭Mm).HasColumnName("cargo_width_mm");
        builder.Property(x => x.화물높이Mm).HasColumnName("cargo_height_mm");
        builder.Property(x => x.화물팔레트개수).HasColumnName("cargo_pallet_count");
        builder.Property(x => x.화물중량Kg).HasColumnName("cargo_weight_kg");
        builder.Property(x => x.화물부피Cbm).HasColumnName("cargo_volume_cbm");
        builder.Property(x => x.화물파손주의여부).HasColumnName("cargo_fragile");
        builder.Property(x => x.화물온도조건).HasColumnName("cargo_temperature").IsRequired();
        builder.Property(x => x.운송방식).HasColumnName("transport_type").IsRequired();
        builder.Property(x => x.차량종류).HasColumnName("vehicle_type").IsRequired();
        builder.Property(x => x.결제수단).HasColumnName("payment_method").IsRequired();
        builder.Property(x => x.정산시점).HasColumnName("settlement_time").IsRequired();
        builder.Property(x => x.증빙방식).HasColumnName("evidence_method").IsRequired();
        builder.Property(x => x.수납주체).HasColumnName("collector").IsRequired();
        builder.Property(x => x.정산상태).HasColumnName("settlement_status").IsRequired();
        builder.Property(x => x.정산메모).HasColumnName("settlement_memo").IsRequired();
        builder.Property(x => x.인수증번호).HasColumnName("receipt_number").IsRequired();
        builder.Property(x => x.인수증등록일시).HasColumnName("receipt_issued_at");
        builder.Property(x => x.현장수금확인일시).HasColumnName("cash_settled_at");
        builder.Property(x => x.현장지급메모).HasColumnName("cash_settlement_memo").IsRequired();
        builder.Property(x => x.세금계산서필요).HasColumnName("tax_invoice_required");
        builder.Property(x => x.현금영수증필요).HasColumnName("cash_receipt_required");
        builder.Property(x => x.결제예정금액).HasColumnName("estimated_payment_amount");
        builder.Property(x => x.운임구성Id).HasColumnName("pricing_config_id");
        builder.Property(x => x.픽업_도로명주소).HasColumnName("pickup_address").IsRequired();
        builder.Property(x => x.픽업_상세주소).HasColumnName("pickup_address_detail").IsRequired();
        builder.Property(x => x.픽업_위도).HasColumnName("pickup_latitude");
        builder.Property(x => x.픽업_경도).HasColumnName("pickup_longitude");
        builder.Property(x => x.픽업_연락처_이름).HasColumnName("pickup_contact_name").IsRequired();
        builder.Property(x => x.픽업_연락처_전화번호).HasColumnName("pickup_contact_phone").IsRequired();
        builder.Property(x => x.픽업_시간창_시작일시).HasColumnName("pickup_window_start");
        builder.Property(x => x.픽업_시간창_종료일시).HasColumnName("pickup_window_end");
        builder.Property(x => x.하차_도로명주소).HasColumnName("dropoff_address").IsRequired();
        builder.Property(x => x.하차_상세주소).HasColumnName("dropoff_address_detail").IsRequired();
        builder.Property(x => x.하차_위도).HasColumnName("dropoff_latitude");
        builder.Property(x => x.하차_경도).HasColumnName("dropoff_longitude");
        builder.Property(x => x.하차_연락처_이름).HasColumnName("dropoff_contact_name").IsRequired();
        builder.Property(x => x.하차_연락처_전화번호).HasColumnName("dropoff_contact_phone").IsRequired();
        builder.Property(x => x.하차_시간창_시작일시).HasColumnName("dropoff_window_start");
        builder.Property(x => x.하차_시간창_종료일시).HasColumnName("dropoff_window_end");
        builder.Property(x => x.서비스레벨).HasColumnName("service_level").IsRequired();
        builder.Property(x => x.요청사항).HasColumnName("request_text").IsRequired();
        builder.Property(x => x.대기료).HasColumnName("waiting_fee");
        builder.Property(x => x.수작업비).HasColumnName("manual_fee");
        builder.Property(x => x.할증).HasColumnName("surcharge");
        builder.Property(x => x.최종운임).HasColumnName("final_fare");
        builder.Property(x => x.생활배송접수키).HasColumnName("neighborhood_registration_key").HasMaxLength(64);
        builder.HasIndex(x => x.생활배송접수키).IsUnique().HasDatabaseName("ux_neighborhood_registration_key");
        builder.Property(x => x.클라이언트요청Id).HasColumnName("client_request_id").IsRequired();
        builder.Property(x => x.상태).HasColumnName("status").IsRequired();
        builder.Property(x => x.결제상태).HasColumnName("payment_status").IsRequired();
        builder.Property(x => x.배차상태).HasColumnName("dispatch_status").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
