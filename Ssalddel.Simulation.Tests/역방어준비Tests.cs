using Ssalddel.Simulation.Domain;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
    "방어 준비 명령의 단일 점유·멱등성·세션 격리를 검증한다.",
    Boundary = "단위 시험이며 실제 Unity 입력·Hosted·영속 저장 증거는 아니다.")]
public sealed class 역방어준비Tests
{
    private static 역방어준비Session New()=>new("preparation:test","map:r1",new string('A',64),new[]{"roof:a","roof:b"});
    [Fact] public void 미리보기는_상태와기록을변경하지않는다()
    {var s=New();Assert.True(s.Preview("preparation:test","roof:a",0).Accepted);Assert.Equal(0,s.Read().Revision);Assert.Empty(s.ReadActions());}
    [Fact] public void 배치는_인원하나와기록하나만남긴다()
    {var s=New();Assert.True(s.Assign("preparation:test","cmd:1","roof:a",0).Accepted);Assert.Equal("roof:a",s.Read().AssignedSlotId);Assert.Single(s.ReadActions());}
    [Fact] public void 중복명령은_다시적용하지않고_다른내용은거절한다()
    {var s=New();s.Assign("preparation:test","cmd:1","roof:a",0);Assert.Equal("AlreadyApplied",s.Assign("preparation:test","cmd:1","roof:a",0).Code);Assert.Equal("DefenseCommandConflict",s.Assign("preparation:test","cmd:1","roof:b",0).Code);Assert.Equal(1,s.Read().Revision);}
    [Theory]
    [InlineData("other","roof:a",0,"DefenseSessionMismatch")]
    [InlineData("preparation:test","roof:missing",0,"DefenseSlotUnknown")]
    [InlineData("preparation:test","roof:a",9,"DefenseRevisionConflict")]
    public void 잘못된입력은_원상태유지(string id,string slot,long rev,string code)
    {var s=New();Assert.Equal(code,s.Assign(id,"cmd:1",slot,rev).Code);Assert.Equal(0,s.Read().Revision);}
    [Fact] public void 재배치는_이전자리를해제한다()
    {var s=New();s.Assign("preparation:test","cmd:1","roof:a",0);s.Assign("preparation:test","cmd:2","roof:b",1);Assert.Equal("roof:b",s.Read().AssignedSlotId);Assert.Equal("roof:a",s.ReadActions()[1].PreviousSlotId);}
    [Fact] public void 오래된미리보기는_재조회후회복한다()
    {var s=New();s.Preview("preparation:test","roof:b",0);s.Assign("preparation:test","cmd:1","roof:a",0);Assert.False(s.Assign("preparation:test","cmd:2","roof:b",0).Accepted);Assert.True(s.Assign("preparation:test","cmd:2","roof:b",s.Read().Revision).Accepted);}
    [Fact] public void 같은초기값과명령은_같은기록으로재생된다()
    {var a=New();var b=New();foreach(var s in new[]{a,b}){s.Assign("preparation:test","cmd:1","roof:a",0);s.Assign("preparation:test","cmd:2","roof:b",1);}Assert.Equal(a.Read().AssignedSlotId,b.Read().AssignedSlotId);Assert.Equal(a.Read().Revision,b.Read().Revision);Assert.Equal(a.ReadActions().Select(x=>(x.CommandId,x.SlotId,x.Revision)),b.ReadActions().Select(x=>(x.CommandId,x.SlotId,x.Revision)));}
    [Fact] public void 세션끼리는_배치가전파되지않는다()
    {var a=New();var b=New();a.Assign("preparation:test","cmd:1","roof:a",0);Assert.Empty(b.Read().AssignedSlotId);}
    [Fact] public void 불변사본과기록배열은_내부상태를노출하지않는다()
    {var s=New();var before=s.Read();s.Assign("preparation:test","cmd:1","roof:a",0);var history=s.ReadActions();history[0]=null!;Assert.Empty(before.AssignedSlotId);Assert.NotNull(s.ReadActions()[0]);}
    [Fact] public void 중복슬롯과잘못된지도해시는_초기화를거절한다()
    {Assert.Throws<ArgumentException>(()=>new 역방어준비Session("a","r","bad",new[]{"a"}));Assert.Throws<ArgumentException>(()=>new 역방어준비Session("a","r",new string('B',64),new[]{"a","a"}));}
}
