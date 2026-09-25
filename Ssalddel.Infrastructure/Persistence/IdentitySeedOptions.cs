namespace 살뜰.Data;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";

    public BootstrapAdminSeedOptions BootstrapAdmin { get; set; } = new();

    public DevelopmentAccountSeedOptions DevelopmentAccounts { get; set; } = new();
}

public sealed class BootstrapAdminSeedOptions
{
    public bool Enabled { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class DevelopmentAccountSeedOptions
{
    public bool Enabled { get; set; }

    public bool AllowInSimulation { get; set; }

    public string AdminPassword { get; set; } = string.Empty;

    public string OrdererPassword { get; set; } = string.Empty;

    public string DriverPassword { get; set; } = string.Empty;

    public string ShipperPassword { get; set; } = string.Empty;
}
