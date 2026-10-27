using Microsoft.EntityFrameworkCore;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    private Task RefreshInboundCompletion(분산입고계획Record plan, CancellationToken ct)
        => inboundCompletion.RefreshInboundCompletion(plan, ct);
}
