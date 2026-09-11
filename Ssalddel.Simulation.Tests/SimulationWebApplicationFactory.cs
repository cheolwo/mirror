using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ssalddel.Simulation.Tests;

/// <summary>
/// 통합 Ssalddel 호스트를 운영 DB·백그라운드 작업 없이 실행하는
/// Simulation HTTP 시험 전용 진입점입니다.
/// </summary>
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "통합 Ssalddel 호스트의 Simulation HTTP 계약과 회귀를 시험한다.",
    Boundary = "Testing 환경 조립은 실제 운영 서버·DB 연결이나 Hosted 실행 증거가 아니다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3계약회귀)]
public sealed class SimulationWebApplicationFactory
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
