using System.Net;
using System.Text.RegularExpressions;

namespace 살뜰.Services.Dispatch.AI;

public sealed class CargoDispatchOllamaOptions
{
    public const string SectionName = "CargoDispatchOllama";
    public const string AllowedModel = "qwen3:1.7b";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:12681/";
    public string Model { get; set; } = AllowedModel;
    public int TimeoutSeconds { get; set; } = 2;
    public int MaxCandidates { get; set; } = 12;
    public int MaxCallsPerWindow { get; set; } = 4;
    public int WindowSeconds { get; set; } = 30;
    public int FailureCooldownSeconds { get; set; } = 30;

    public bool Validate()
    {
        if (TimeoutSeconds is < 1 or > 5 || MaxCandidates is < 2 or > 30
            || MaxCallsPerWindow is < 1 or > 10 || WindowSeconds is < 10 or > 120
            || FailureCooldownSeconds is < 1 or > 120 || Model != AllowedModel
            || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttp || uri.AbsolutePath != "/"
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.Port is < 1 or > 65535 || !IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address)
            || !IPAddress.IsLoopback(address)) return false;
        var authority = BaseUrl[(BaseUrl.IndexOf("://", StringComparison.Ordinal) + 3)..].TrimEnd('/');
        return Regex.IsMatch(authority, @"^(?:\[[0-9a-fA-F:]+\]|[0-9.]+):[0-9]{1,5}$", RegexOptions.CultureInvariant);
    }
}
