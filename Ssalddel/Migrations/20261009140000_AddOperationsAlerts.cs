using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext)), Migration("20261009140000_AddOperationsAlerts")]
public sealed class AddOperationsAlerts : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateIndex("IX_neighborhood_member_delivery_UpdatedAtUtc_RequestId","neighborhood_member_delivery",new[]{"UpdatedAtUtc","RequestId"});
        m.CreateIndex("IX_비정상운송사건_갱신_사건","비정상운송사건",new[]{"UpdatedAt","사건StableId"});
        var a = Table("operations_alerts");
        Str(a, "SourceKind", 32); Str(a, "SourceId", 128); Str(a, "ProblemCode", 64); Str(a, "Severity", 16); Str(a, "Status", 16);
        Col(a,"Episode",typeof(int),"int"); Col(a,"Revision",typeof(long),"bigint"); Col(a,"ReminderQueued",typeof(bool),"tinyint(1)");
        Date(a,"CreatedAtUtc"); Date(a,"LastVerifiedAtUtc"); Date(a,"AcknowledgedAtUtc",true); Str(a,"AcknowledgedBy",450,true); m.Operations.Add(a);
        m.CreateIndex("IX_operations_alerts_Status_CreatedAtUtc_Id",a.Name,new[]{"Status","CreatedAtUtc","Id"});
        m.CreateIndex("IX_operations_alerts_SourceKind_SourceId",a.Name,new[]{"SourceKind","SourceId"});
        var o = Table("operations_alert_outbox");
        Str(o,"AlertId",64,true); Col(o,"Episode",typeof(int),"int"); Str(o,"Kind",16); Str(o,"Channel",16); Str(o,"RecipientUserId",450);
        Str(o,"Status",24); Col(o,"Attempts",typeof(int),"int"); Col(o,"Revision",typeof(long),"bigint");
        Date(o,"CreatedAtUtc"); Date(o,"NextAttemptUtc"); Date(o,"LeaseUntilUtc",true); Str(o,"ErrorCode",64,true); m.Operations.Add(o);
        m.CreateIndex("IX_operations_alert_outbox_Status_NextAttemptUtc_LeaseUntilUtc",o.Name,new[]{"Status","NextAttemptUtc","LeaseUntilUtc"});
        var c=Table("operations_alert_checkpoints"); Date(c,"CursorUtc"); Str(c,"CursorId",128); Col(c,"Revision",typeof(long),"bigint"); Date(c,"StartedAtUtc",true); m.Operations.Add(c);
        var r=Table("operations_alert_receipts"); Str(r,"AlertId",64); Str(r,"ActorUserId",450); Col(r,"RequestId",typeof(Guid),"char(36)");
        Col(r,"ExpectedRevision",typeof(long),"bigint"); Col(r,"Episode",typeof(int),"int"); m.Operations.Add(r);
        m.CreateIndex("IX_operations_alert_receipts_ActorUserId_RequestId",r.Name,new[]{"ActorUserId","RequestId"},unique:true);
        var u=Table("operations_budget_usage"); Str(u,"Category",32); Date(u,"DayUtc"); Col(u,"Count",typeof(int),"int"); Date(u,"UpdatedAtUtc"); m.Operations.Add(u);
        m.CreateIndex("IX_operations_budget_usage_DayUtc_Category",u.Name,new[]{"DayUtc","Category"});
    }
    private static CreateTableOperation Table(string name)
    {
        var t = new CreateTableOperation { Name=name, PrimaryKey=new(){Name="PK_"+name,Table=name,Columns=["Id"]} };
        Str(t,"Id",64); return t;
    }
    private static void Col(CreateTableOperation t,string name,Type type,string sql,bool nullable=false,int? length=null)
        => t.Columns.Add(new(){Name=name,Table=t.Name,ClrType=type,ColumnType=sql,IsNullable=nullable,MaxLength=length});
    private static void Str(CreateTableOperation t,string n,int length,bool nullable=false)=>Col(t,n,typeof(string),"varchar("+length+")",nullable,length);
    private static void Date(CreateTableOperation t,string n,bool nullable=false)=>Col(t,n,typeof(DateTime),"datetime(6)",nullable);
    protected override void Down(MigrationBuilder m)
    { m.DropIndex("IX_비정상운송사건_갱신_사건","비정상운송사건");m.DropIndex("IX_neighborhood_member_delivery_UpdatedAtUtc_RequestId","neighborhood_member_delivery");m.DropTable("operations_budget_usage");m.DropTable("operations_alert_receipts");m.DropTable("operations_alert_checkpoints");m.DropTable("operations_alert_outbox");m.DropTable("operations_alerts"); }
}
