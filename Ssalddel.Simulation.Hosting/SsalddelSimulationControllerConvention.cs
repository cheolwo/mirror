using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace Ssalddel.Simulation.Hosting;

/// <summary>
/// Hosting 모듈에 포함된 Controller에만 살뜰 로그인·세션 소유권·오류 계약을 적용한다.
/// 운영 Controller의 filter 구성에는 영향을 주지 않는다.
/// </summary>
public sealed class SsalddelSimulationControllerConvention
    : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        if (controller.ControllerType.Assembly
            != typeof(SsalddelSimulationControllerConvention).Assembly)
        {
            return;
        }

        controller.Filters.Add(new AuthorizeFilter(
            SsalddelSimulationAuthorizationPolicies.Participant));
        controller.Filters.Add(new ServiceFilterAttribute(
            typeof(SimulationSessionAccessActionFilter)));
        controller.Filters.Add(new ServiceFilterAttribute(
            typeof(SimulationApiExceptionFilter)));
    }
}
