using System.Reflection;
using Hongdal.ApiMetadata;
using Hongdal.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Controllers.Admin.Content07;
using Hongdal.Controllers.Common;
using Hongdal.Services.Community;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Architecture;

public sealed class HongdalCommunityV0ModuleMetadataTests
{
    [Fact]
    public void Reader_FindsEveryCommunityV0ModuleGroup()
    {
        var modules = ReadModules();
        var moduleKeys = modules
            .Select(module => module.ModuleKey)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(HongdalCommunityV0ModuleKeys.All.Count, moduleKeys.Count);
        Assert.All(HongdalCommunityV0ModuleKeys.All, moduleKey => Assert.Contains(moduleKey, moduleKeys));
        Assert.Contains(modules, module => module.ComponentName.EndsWith("CommunityPlatformUiModule", StringComparison.Ordinal));
        Assert.Contains(modules, module => module.ComponentName.EndsWith("CommunityWritingUiModule", StringComparison.Ordinal));
        Assert.Contains(modules, module => module.Component == typeof(PlatformCommunityHomePageViewModel));
        Assert.Contains(modules, module => module.Component == typeof(CommunityPostComposerViewModel));
        Assert.Contains(modules, module => module.Component == typeof(커뮤니티게시글UseCase));
        Assert.Contains(modules, module => module.Component == typeof(커뮤니티투표UseCase));
        Assert.Contains(modules, module => module.Component == typeof(Mongo커뮤니티원장저장소));
        Assert.Contains(modules, module => module.Component == typeof(Mongo커뮤니티원장투영작업저장소));
        Assert.Contains(modules, module => module.Component == typeof(CommunityBoardWritePolicy));
        Assert.Contains(modules, module => module.Component == typeof(CommunityInformationCollectionController));
        Assert.Contains(modules, module => module.Component == typeof(CommunityContentApplicationModule));
        Assert.Contains(modules, module => module.Component == typeof(CommunityParticipationApplicationModule));
        Assert.Contains(modules, module => module.Component == typeof(CommunityLedgerApplicationModule));
    }

    [Fact]
    public void CommunityApplicationModule_DependsOnlyOnSharedContracts()
    {
        var moduleAssembly = typeof(CommunityContentApplicationModule).Assembly;
        var references = moduleAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("Hongdal.Contracts", references);
        Assert.DoesNotContain("Hongdal", references);
        Assert.DoesNotContain("Hongdal.Ui.Common", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("MongoDB.Driver", references);
        Assert.Equal(moduleAssembly, typeof(CommunityDriverAvailabilityService).Assembly);
        Assert.Equal(moduleAssembly, typeof(커뮤니티원장Dto).Assembly);
        Assert.Equal(moduleAssembly, typeof(주문원장구성정책).Assembly);
    }

    [Fact]
    public void CommunityV0Modules_HaveStableVersionFlagStageAndBoundary()
    {
        var modules = ReadModules();

        Assert.NotEmpty(modules);
        Assert.All(modules, module =>
        {
            Assert.Equal(HongdalProductVersionCodes.V0_0, module.ProductVersion);
            Assert.Equal(HongdalCommunityV0Metadata.FeatureFlag, module.FeatureFlag);
            Assert.Equal(HongdalCommunityV0Metadata.WorkflowKey, module.WorkflowKey);
            Assert.True(module.DefaultEnabled);
            Assert.Contains(module.ModuleKey, HongdalCommunityV0ModuleKeys.All);
            Assert.Contains(module.ReleaseStage, HongdalCommunityV0ReleaseStages.All);
            Assert.False(string.IsNullOrWhiteSpace(module.Responsibility));
            Assert.False(string.IsNullOrWhiteSpace(module.Boundary));
        });
    }

    [Fact]
    public void CommunityV0ApiModules_AlsoUseV0ApiVersionMetadata()
    {
        var apiModules = ReadModules()
            .Where(module => module.Kind == HongdalModuleKind.Api)
            .ToArray();

        Assert.NotEmpty(apiModules);
        Assert.All(apiModules, module =>
        {
            var controllerType = Assert.IsAssignableFrom<Type>(module.Component);
            var apiVersion = controllerType.GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true);
            Assert.NotNull(apiVersion);
            Assert.Equal(HongdalProductVersion.V0_0, apiVersion.Version);
        });
    }

    private static IReadOnlyList<HongdalModuleDescriptor> ReadModules()
        => HongdalModuleMetadataReader.ReadVersion(
            HongdalProductVersionCodes.V0_0,
            typeof(PlatformCommunityHomePageViewModel).Assembly,
            typeof(커뮤니티게시글UseCase).Assembly,
            typeof(CommunityContentApplicationModule).Assembly);
}
