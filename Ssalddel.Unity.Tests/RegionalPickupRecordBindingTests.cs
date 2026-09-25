using Ssalddel.Unity.Data.WorldProjection;
namespace Ssalddel.Unity.Tests;
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "영상 없는 방문 기록의 행정동 및 역세권 선택과 미배정 격리를 검증한다.",
    Boundary = "지역 소속 확정, DB/API 공급, 실제 Unity 지역 화면 표시의 증거가 아니다.")]
public sealed class RegionalPickupRecordBindingTests
{
    static RegionalPickupVisit Visit(string id,string area,string status="Verified") => new() {
        VisitId=id,StoreId="store:"+id,AdministrativeAreaStableId=area,AreaBindingStatus=status,
        AreaBindingEvidence=status=="Verified"?"boundary:reviewed-sample":null
    };
    static RegionalPickupRecord Record(params RegionalPickupVisit[] visits)=>new() {
        SchemaVersion="delivery-visit-record.v2",RecordId="test",LocalReviewOnly=true,Visits=visits
    };
    [Fact] public void 미확인과다른동을해당동에복제하지않는다()
    {
        var r=RegionalPickupRecordBinding.Bind("a",false,Record(Visit("1","a"),Visit("2","b"),Visit("3","a","Unresolved")));
        Assert.Single(r.Visits);Assert.Equal(1,r.UnresolvedCount);Assert.Equal(1,r.OtherModuleCount);
    }
    [Fact] public void 행정동을역세권으로추정하지않는다()
    {
        var r=RegionalPickupRecordBinding.Bind("a",true,Record(Visit("1","a")));
        Assert.Empty(r.Visits);Assert.Equal(1,r.UnresolvedCount);
    }
    [Fact] public void 중복방문과운영사본을거부한다()
    {
        Assert.Throws<ArgumentException>(()=>RegionalPickupRecordBinding.Bind("a",false,Record(Visit("1","a"),Visit("1","a"))));
        var r=Record();r.IsOperationalState=true;
        Assert.Throws<ArgumentException>(()=>RegionalPickupRecordBinding.Bind("a",false,r));
    }
    [Fact] public void 인계완료를방문기록으로확정하지않는다()
    {
        var v=Visit("1","a");v.PickupCompletionConfirmed=true;
        Assert.Throws<ArgumentException>(()=>RegionalPickupRecordBinding.Bind("a",false,Record(v)));
    }
}
