namespace Ssalddel.Simulation.Hosting;

public sealed class SimulationSharedPublicDataOptions
{
    public const string SectionName = "SimulationSharedPublicData";

    public bool Enabled { get; set; }

    public string ConnectionStringName { get; set; } = "SharedPublicData";

    public string? FallbackConnectionStringName { get; set; }

    public int MaxItems { get; set; } = 50;
}
