using Ssalddel.Contracts.Common.Warehouse;
namespace Ssalddel.Application.Warehouse;

public interface IDistributedBuildingCandidateProvider
{
    Task<IReadOnlyList<DistributedBuildingCandidateDto>> FindAsync(long warehouseId, string confirmedWarehouseAddress, CancellationToken ct);
}
public sealed class EmptyDistributedBuildingCandidateProvider : IDistributedBuildingCandidateProvider
{
    public Task<IReadOnlyList<DistributedBuildingCandidateDto>> FindAsync(long warehouseId, string confirmedWarehouseAddress, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<DistributedBuildingCandidateDto>>([]);
}
