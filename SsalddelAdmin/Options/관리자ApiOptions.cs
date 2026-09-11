namespace SsalddelAdmin.Options;

public sealed class 관리자ApiOptions
{
    public const string SectionName = "AdminApi";

    public string BaseUrl { get; set; } =
        Ssalddel.Ui.Common.Areas.App.Services
            .SsalddelServerEndpoint.LocalDevelopmentBaseAddress;
}
