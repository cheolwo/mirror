using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext)), Migration("20261009100000_AddNeighborhoodMemberDelivery")]
public sealed class AddNeighborhoodMemberDelivery : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<bool>("neighborhood_member_only", "운송실행투영", type: "tinyint(1)", nullable: false, defaultValue: false);
        m.AddColumn<string>("assigned_member_user_id", "운송실행투영", type: "varchar(191)", maxLength:191, nullable:true);
        m.AddColumn<string>("performer_kind", "운송실행투영", type: "varchar(40)", maxLength:40, nullable:true);
        var p = Table("neighborhood_member_participation", "UserId");
        Strings(p,191,"UserId"); Long(p,"Revision","WorkLockRevision"); Bool(p,"ReceiveOffers");
        Strings(p,null,"PrivatePayload"); String(p,"ActiveRequestId",120,true); Date(p,"UpdatedAtUtc"); m.Operations.Add(p);
        m.CreateIndex("IX_neighborhood_member_participation_ReceiveOffers",p.Name,"ReceiveOffers");
        var d = Table("neighborhood_member_delivery", "RequestId");
        Strings(d,120,"RequestId"); Strings(d,191,"OwnerUserId"); String(d,"AssignedMemberUserId",191,true); Long(d,"Revision");
        Strings(d,30,"StateCode"); Strings(d,80,"NoticeVersion"); Column(d,"ConsentEvidenceId",typeof(Guid),"char(36)");
        Column(d,"FareKrw",typeof(decimal),"decimal(18,2)"); Column(d,"DistanceKm",typeof(decimal),"decimal(18,3)",true);
        Date(d,"WindowEndUtc","UpdatedAtUtc"); NullableDate(d,"AssignedAtUtc","CompletedAtUtc");
        String(d,"PickupEvidenceId",64,true); String(d,"DeliveryEvidenceId",64,true); String(d,"IssueCode",40,true);
        String(d,"SupportCaseId",120,true); String(d,"PrivateIssuePayload",null,true); m.Operations.Add(d);
        m.CreateIndex("IX_neighborhood_member_delivery_AssignedMemberUserId_StateCode",d.Name,new[]{"AssignedMemberUserId","StateCode"});
        var o = Table("neighborhood_member_offer", "OfferId");
        Strings(o,64,"OfferId"); Strings(o,120,"RequestId"); Strings(o,191,"MemberUserId");
        Long(o,"Revision","ParticipationRevision","DeliveryRevision","PushClaimRevision"); Strings(o,30,"StateCode","PushStateCode");
        Date(o,"CreatedAtUtc","ExpiresAtUtc"); NullableDate(o,"PushClaimUntilUtc","PushLastAttemptUtc"); Column(o,"PushAttempts",typeof(int),"int"); m.Operations.Add(o);
        m.CreateIndex("IX_neighborhood_member_offer_RequestId_MemberUserId",o.Name,new[]{"RequestId","MemberUserId"},unique:true);
        m.CreateIndex("IX_neighborhood_member_offer_MemberUserId_CreatedAtUtc",o.Name,new[]{"MemberUserId","CreatedAtUtc"});
        m.CreateIndex("IX_neighborhood_member_offer_PushStateCode_ExpiresAtUtc",o.Name,new[]{"PushStateCode","ExpiresAtUtc"});
        var r = Table("neighborhood_member_receipt", "ActorUserId", "ClientRequestId");
        Strings(r,191,"ActorUserId"); Column(r,"ClientRequestId",typeof(Guid),"char(36)"); Strings(r,40,"Kind");
        Strings(r,120,"AggregateId"); Strings(r,64,"InputDigest"); Date(r,"RecordedAtUtc"); m.Operations.Add(r);
        var e = Table("neighborhood_member_photo", "EvidenceId");
        Strings(e,64,"EvidenceId","InputDigest","StoredSha256"); Strings(e,120,"RequestId"); Strings(e,191,"ActorUserId");
        Column(e,"UploadRequestId",typeof(Guid),"char(36)"); Long(e,"WorkRevision"); Strings(e,30,"StageCode","StateCode");
        Strings(e,null,"PrivatePayload"); Date(e,"CreatedAtUtc"); m.Operations.Add(e);
        m.CreateIndex("IX_neighborhood_member_photo_ActorUserId_UploadRequestId",e.Name,new[]{"ActorUserId","UploadRequestId"},unique:true);
    }
    private static CreateTableOperation Table(string name, params string[] primary)
        => new() { Name=name, PrimaryKey=new() { Name="PK_"+name, Table=name, Columns=primary } };
    private static void Column(CreateTableOperation t,string name,Type type,string sql,bool nullable=false,int? length=null)
        => t.Columns.Add(new() { Name=name,Table=t.Name,ClrType=type,ColumnType=sql,IsNullable=nullable,MaxLength=length });
    private static void String(CreateTableOperation t,string name,int? length,bool nullable=false)
        => Column(t,name,typeof(string),length is null?"longtext":$"varchar({length})",nullable,length);
    private static void Strings(CreateTableOperation t,int? length,params string[] names) { foreach(var n in names) String(t,n,length); }
    private static void Long(CreateTableOperation t,params string[] names) { foreach(var n in names) Column(t,n,typeof(long),"bigint"); }
    private static void Date(CreateTableOperation t,params string[] names) { foreach(var n in names) Column(t,n,typeof(DateTime),"datetime(6)"); }
    private static void NullableDate(CreateTableOperation t,params string[] names) { foreach(var n in names) Column(t,n,typeof(DateTime),"datetime(6)",true); }
    private static void Bool(CreateTableOperation t,string name) => Column(t,name,typeof(bool),"tinyint(1)");
    protected override void Down(MigrationBuilder m) => throw new NotSupportedException("배송 수락·사진·지원 사건 원장은 별도의 보존 정리 없이 삭제할 수 없습니다.");
}
