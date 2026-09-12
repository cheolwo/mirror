using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "가상 동네의 합성 기준점과 경로 계약을 검증한다.",
    Boundary = "합성 좌표 시험이며 실제 위치나 Unity Scene 배치 증거가 아니다.")]
public sealed class 가상동네배치기준Tests
{
    [Fact]
    public void 기존대기점과작업경로좌표를유지한다()
    {
        Assert.Equal(new[]{(-6d,-4d),(0d,-4d),(6d,-4d),(10d,16d),(30d,16d)},가상동네배치기준.대기점());
        Assert.Equal(new[]{(0d,-14d),(-10d,-14d),(-10d,-8d),(-10d,-2.5d),(-10d,0d)},가상동네배치기준.경로("mart-exit"));
        Assert.Equal((10d,7d,6d,6d),가상동네배치기준.시설("facility:sim.restaurant-1"));
        Assert.Equal((30d,27d,6d,6d),가상동네배치기준.시설("facility:synthetic:a"));
    }
    [Fact]
    public void 반환경로수정은정본을바꾸지않고_없는기준점은거절한다()
    {
        var route=가상동네배치기준.경로("mart-exit"); route[0]=(900,900);
        Assert.Equal((0d,-14d),가상동네배치기준.경로("mart-exit")[0]);
        Assert.Throws<ArgumentException>(()=>가상동네배치기준.기준점("missing"));
        Assert.Throws<ArgumentException>(()=>가상동네배치기준.경로("missing"));
    }
}
