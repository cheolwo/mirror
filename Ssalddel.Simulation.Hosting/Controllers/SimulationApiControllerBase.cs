using System;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

public abstract class SimulationApiControllerBase : ControllerBase
{
    protected ActionResult<T> Execute<T>(Func<ActionResult<T>> action)
    {
        try
        {
            return action();
        }
        catch (SimulationContractException error)
        {
            return BadRequest(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationNotFoundException error)
        {
            return NotFound(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
        catch (SimulationConflictException error)
        {
            return Conflict(new SimulationErrorResponse { ErrorCode = error.ErrorCode });
        }
    }
}
