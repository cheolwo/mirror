using System.Text.Json;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Warehouse;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "로컬 생활 시작 자료의 봉인·결정성·저장 복원을 검증한다.",
    Boundary = "자동 시험이며 실제 운영 원장이나 Unity 실행 증거가 아니다.")]
public sealed class 로컬생활시작자료Tests
{
    public static 로컬생활시작묶음 Bundle(int couriers=3,string prefix="")
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root!=null && !Directory.Exists(Path.Combine(root.FullName,"eng/world-seedbeds"))) root=root.Parent;
        Assert.NotNull(root);
        var path=Path.Combine(root!.FullName,"eng/world-seedbeds/placement-map-profiles/synthetic-neighborhood.v1.json");
        return 로컬생활시작자료.Seal(new 로컬생활시작Payload {InitialRequest=로컬생활구성표본.Create(Guid.Parse("b39956c7-6e4a-4677-b33a-90d047a331db"),couriers,prefix),
            PlacementMap=JsonDocument.Parse(File.ReadAllBytes(path)).RootElement.Clone(),
            Sources=new[]{"map","graph","plan"}.Select(x=>new 로컬생활원천 {Path=x,Sha256=new string('a',64)}).ToArray()});
    }
    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(4)]
    public void 이름과기사수가바뀌어도_오프라인업무와저장복원이이어진다(int count)
    {
        var bundle=Bundle(count,"custom:");var core=new 경영SimulationSessionAggregate(bundle.Payload.InitialRequest);
        for(int i=0;i<300;i++) Tick(core,i);
        var state=core.Snapshot();Assert.Equal(6+count,state.WaitingFleet!.NeighborhoodLife!.Actors.Length);
        Assert.All(state.WaitingFleet.NeighborhoodLife.Actors,x=>Assert.Contains(":custom:",x.ActorId));
        Assert.All(state.FoodDeliveries,x=>Assert.Contains(":custom:",x.OrdererStableId));
        Assert.True(state.FoodDeliveries.Count(x=>x.ReceivedTick.HasValue)>=2);
        var saved=Save(core);var restored=SimulationSessionReplay.Restore(saved);
        Assert.Equal(JsonSerializer.Serialize(state),JsonSerializer.Serialize(restored.Snapshot()));
        for(int i=300;i<340;i++)Assert.Equal(JsonSerializer.Serialize(Tick(core,i)),JsonSerializer.Serialize(Tick(restored,i)));
        var report=로컬생활보고.Create(saved,bundle);로컬생활보고.Validate(report);
        Assert.NotEmpty(report.ActionLedger!.TailRecords);
        var tampered=로컬생활시작자료.Copy(report);tampered.Tick++;Assert.Throws<InvalidDataException>(()=>로컬생활보고.Validate(tampered));
    }
    [Fact]
    public void 사기사와창고_삼십분재고보존및명시종료()
    {
        var core=new 경영SimulationSessionAggregate(Bundle(4,"renamed:").Payload.InitialRequest);
        for(int i=0;i<1800;i++) {
            var s=Tick(core,i);var f=s.WaitingFleet!;var d=f.NeighborhoodLife!.Depot;
            for(int k=0;k<2;k++) Assert.Equal(42,d.Incoming[k]+d.Stock[k]+d.Shipments.Sum(x=>x.Cargo[k])+f.Mart!.Stock[k]+f.Mart.Orders.Count(x=>x.PickedUnits>k));
        }
        var s2=core.Snapshot();Assert.True(s2.IsCompleted);
        Assert.Contains(s2.WaitingFleet!.NeighborhoodLife!.Depot.Shipments,x=>x.ReceivedTick.HasValue);
        Assert.True(s2.WaitingFleet.Mart!.Orders.Count(x=>x.ReceivedTick.HasValue)>2);
    }
    [Fact]
    public void 배열재정렬은업무결정을바꾸지않고_입력사본은보존한다()
    {
        var b=Bundle();var original=JsonSerializer.Serialize(b);var changed=로컬생활시작자료.Copy(b.Payload.InitialRequest);
        changed.LocalLife!.Actors=changed.LocalLife.Actors.Reverse().ToArray();changed.LocalLife.Facilities=changed.LocalLife.Facilities.Reverse().ToArray();
        changed.NpcWorkforce!.Actors=changed.NpcWorkforce.Actors.Reverse().ToArray();
        var a=new 경영SimulationSessionAggregate(b.Payload.InitialRequest);var c=new 경영SimulationSessionAggregate(changed);
        for(int i=0;i<100;i++) Assert.Equal(JsonSerializer.Serialize(Tick(a,i).WaitingFleet),JsonSerializer.Serialize(Tick(c,i).WaitingFleet));
        Assert.Equal(original,JsonSerializer.Serialize(b));
    }
    [Theory]
    [InlineData("role")] [InlineData("capability")] [InlineData("duplicate")] [InlineData("visual")] [InlineData("hash")] [InlineData("profile")]
    public void 누락과불일치는시작전에거부(string kind)
    {
        var r=Bundle().Payload.InitialRequest;
        if(kind=="role")r.LocalLife!.Actors[0].Role="Unknown";
        if(kind=="capability")r.NpcWorkforce!.CapabilityGrants=Array.Empty<SimulationNpcCapabilityGrantInitialRequest>();
        if(kind=="duplicate")r.LocalLife!.Actors[0].ActorStableId=r.LocalLife.Actors[1].ActorStableId;
        if(kind=="visual")r.LocalLife!.Actors[0].VisualKey="unknown";
        if(kind=="hash")r.LocalLife!.SourceBundleSha256="";
        if(kind=="profile")r.NeighborhoodDayEnabled=true;
        Assert.Throws<SimulationContractException>(()=>new 경영SimulationSessionAggregate(r));
    }
    [Fact]
    public void 유니코드와수치표기차이는동일해시_값차이는다른해시()
    {
        byte[] Bytes(string x)=>System.Text.Encoding.UTF8.GetBytes(x);
        Assert.Equal(로컬생활시작자료.CanonicalHash(Bytes("{\"x\":\"가\",\"z\":3.95}")),
            로컬생활시작자료.CanonicalHash(Bytes("{\"z\":3.950,\"x\":\"\\uac00\"}")));
        Assert.NotEqual(로컬생활시작자료.CanonicalHash(Bytes("{\"z\":3.95}")),로컬생활시작자료.CanonicalHash(Bytes("{\"z\":3.9500000000000002}")));
    }
    [Fact]
    public void 같은요청ID의구성교체와저장해시변조를거부한다()
    {
        var b=Bundle();var core=new 경영SimulationSessionAggregate(b.Payload.InitialRequest);
        var changed=로컬생활시작자료.Copy(b.Payload.InitialRequest);changed.LocalLife!.Actors[0].DisplayName="another";
        Assert.Throws<SimulationConflictException>(()=>core.EnsureSameCreationRequest(changed));
        Tick(core,0);var package=Save(core);package.SessionCreateRequest.LocalLife!.SourceBundleSha256=new string('f',64);
        var error=Assert.Throws<SimulationConflictException>(()=>SimulationSessionReplay.Restore(package));
        Assert.Equal("SimulationReplayHashMismatch",error.Message);
    }
    [Fact]
    public void 파일동결과재저장은멱등_다른내용과잘못된경로는거부()
    {
        var dir=Path.Combine(Path.GetTempPath(),"offline-life-test-"+Guid.NewGuid().ToString("N"));
        try {
            var b=Bundle();var path=Path.Combine(dir,"bundle.json");var bytes=로컬생활시작자료.JsonBytes(b);
            로컬생활시작자료.WriteOnce(path,bytes,로컬생활시작자료.MaxBundleBytes);
            로컬생활시작자료.WriteOnce(path,bytes,로컬생활시작자료.MaxBundleBytes);
            Assert.Equal(b.ContentSha256,로컬생활시작자료.Read(path,b.ContentSha256).ContentSha256);
            Assert.Throws<InvalidDataException>(()=>로컬생활시작자료.Read(path,new string('f',64)));
            Assert.Throws<InvalidDataException>(()=>로컬생활시작자료.WriteOnce(path,new byte[]{1},10));
            Assert.Throws<FileNotFoundException>(()=>로컬생활시작자료.Read(Path.Combine(dir,"absent"),b.ContentSha256));
            var bad=System.Text.Encoding.UTF8.GetBytes("{\"x\":1,\"x\":2}");Assert.Throws<InvalidDataException>(()=>로컬생활시작자료.CanonicalHash(bad));
            var changed=로컬생활시작자료.Copy(b);changed.Payload.InitialRequest.LocalLife!.Actors[0].DisplayName="changed";
            Assert.Throws<InvalidDataException>(()=>로컬생활시작자료.Validate(changed));
        } finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    private static 경영SimulationSessionSnapshot Tick(경영SimulationSessionAggregate c,int i)=>c.Advance(new 경영SimulationTick진행Request {CommandId="offline:tick:"+i,ExpectedRevision=c.Snapshot().Revision,TickCount=1});
    private static SimulationSessionSavePackage Save(경영SimulationSessionAggregate c)=>c.CreateSavePackage(new SimulationSessionSaveRequest {SaveStableId="save:offline-test",ExpectedRevision=c.Snapshot().Revision});
}
