using Ssalddel.Ui.Common.Areas.App.Services;

namespace RoleWorkspacePreview;

// This loopback-only fixture supplies feature-readiness reads, never business writes.
public sealed class UsabilityPreviewFixture : I마트페이지접근Service
{
    public string Scenario { get; private set; } = "home";
    public int ReadCount { get; private set; }

    public void Reset(string scenario)
    {
        Scenario = scenario;
        ReadCount = 0;
    }

    public Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;
        if (Scenario == "mart-error" && ReadCount == 1)
            throw new InvalidOperationException("PreviewInternalFeatureReadFailure");
        return Task.FromResult(Scenario != "mart-off");
    }
}
