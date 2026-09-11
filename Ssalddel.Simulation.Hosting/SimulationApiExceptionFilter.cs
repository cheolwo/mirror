using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting;

public sealed class SimulationApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var statusCode = context.Exception switch
        {
            SimulationContractException => StatusCodes.Status400BadRequest,
            SimulationNotFoundException => StatusCodes.Status404NotFound,
            SimulationConflictException => StatusCodes.Status409Conflict,
            _ => 0,
        };
        if (statusCode == 0)
            return;

        var errorCode = context.Exception switch
        {
            SimulationContractException error => error.ErrorCode,
            SimulationNotFoundException error => error.ErrorCode,
            SimulationConflictException error => error.ErrorCode,
            _ => string.Empty,
        };
        context.Result = new ObjectResult(new SimulationErrorResponse
        {
            ErrorCode = errorCode,
        })
        {
            StatusCode = statusCode,
        };
        context.ExceptionHandled = true;
    }
}
