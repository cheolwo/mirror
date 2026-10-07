using System.Text.Json;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Observation;
using Ssalddel.Unity.Warehouse;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "사가정 관찰 프로필의 명령 제한·역사 설명·원장 불변성과 슬롯 분리를 시험한다.",
    Boundary = "단위 시험이며 Unity 실제 입력·화면·통행 증거가 아니다.")]
public sealed class 사가정음식생활관찰Tests
{
    [Fact]
    public void 새로운프로필은_기존Core를쓰고_저장슬롯을분리한다()
    {
        var request = 사가정음식생활관찰Profile.생성(Guid.NewGuid());
        Assert.Equal(가상동네생활기준.ScenarioId, request.ScenarioStableId);
        Assert.Equal(1800, request.DurationTicks);
        Assert.True(request.NeighborhoodDayEnabled);
        Assert.NotEqual(가상배달관찰표본.NeighborhoodLifeSlotId, 사가정음식생활관찰Profile.SlotId);
    }
    [Theory]
    [InlineData("policy", false)] [InlineData("discard", false)] [InlineData("dispatch", false)]
    [InlineData("play", true)] [InlineData("step", true)] [InlineData("save", true)]
    [InlineData("overview", true)] [InlineData("restart", true)]
    public void 관찰명령은_정책과운영변경을허용하지않는다(string command, bool allowed)
        => Assert.Equal(allowed, 사가정음식생활관찰Profile.관찰명령허용(command));

    [Fact]
    public void 과거메뉴설명과_여섯명표시는_현재원장을변경하지않는다()
    {
        var actors = new[] { 가상동네생활기준.RestaurantActorId, "participant:synthetic:a", "participant:synthetic:b",
            "actor:synthetic-courier:1", "actor:synthetic-courier:2", "actor:synthetic-courier:3", 가상동네생활기준.MartWorkerId };
        var state = new 경영SimulationSessionSnapshot { DurationTicks = 1800, Revision = 17,
            WaitingFleet = new() { NeighborhoodLife = new() { DayEnabled = true,
                Actors = actors.Select(id => new 가상생활NpcSnapshot { ActorId = id,
                    Stage = id == 가상동네생활기준.MartWorkerId ? "Resting" : id == "participant:synthetic:a" ? "Sleeping" : "Working" }).ToArray() }, Mart = new(), OrderFlow = new() } };
        var before = JsonSerializer.Serialize(state);
        var model = 사가정음식생활관찰Profile.조회(state);
        Assert.True(model.ObservationOnly); Assert.Equal(17, model.Revision); Assert.Equal(6, model.Actors.Length);
        Assert.Equal("관찰 대상 6명 · 휴식/수면 1명\n음식 수령 0건", model.Summary);
        Assert.Contains(model.VisitHistory, row => row.Body.Contains("훈제오리 포케"));
        Assert.Contains(model.VisitHistory, row => row.Body.Contains("현재 판매 여부는 별개"));
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }
    [Fact]
    public void 잘못된표본은_가상위치Fallback없이거부한다()
        => Assert.Throws<ArgumentException>(() => 사가정음식생활관찰Profile.조회(new()));
}
