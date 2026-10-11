using System.Net;
using System.Text.RegularExpressions;

namespace 살뜰.Services.Dispatch.AI;

public sealed class FoodDispatchOllamaOptions
{
    public const string SectionName = "FoodDispatchOllama";
    public const string AllowedModel = "qwen3:1.7b";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:12681/";
    public string Model { get; set; } = AllowedModel;
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxCandidates { get; set; } = 12;

    public bool Validate()
    {
        if (TimeoutSeconds is < 1 or > 90 || MaxCandidates is < 2 or > 30
            || !string.Equals(Model, AllowedModel, StringComparison.Ordinal)
            || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttp || uri.AbsolutePath != "/"
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.Port is < 1 or > 65535
            || !IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address)
            || !IPAddress.IsLoopback(address))
            return false;

        // Uri.Authority drops an explicitly supplied default port. Check the original authority too.
        var authority = BaseUrl[(BaseUrl.IndexOf("://", StringComparison.Ordinal) + 3)..].TrimEnd('/');
        return Regex.IsMatch(authority, @"^(?:\[[0-9a-fA-F:]+\]|[0-9.]+):[0-9]{1,5}$",
            RegexOptions.CultureInvariant);
    }
}
