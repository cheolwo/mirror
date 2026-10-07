using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Clients;

public sealed class MobileWorkflowEnvironmentTests
{
    [Theory]
    [InlineData("OrdererApp")]
    [InlineData("RestaurantDeskApp")]
    [InlineData("FDriverApp")]
    [InlineData("SsalddelAdminApp")]
    [InlineData("DriverApp")]
    [InlineData("SsalddelApp")]
    [InlineData("WarehouseManagerApp")]
    public void RoleAppPackagingConnectsBuildAddressToTheSharedMobileResolver(string app)
    {
        var project = XDocument.Load(RepoFile($"{app}/{app}.csproj"));
        var metadata = Assert.Single(project.Descendants("AssemblyAttribute"),
            attribute => (string?)attribute.Attribute("Include") == typeof(AssemblyMetadataAttribute).FullName
                && (string?)attribute.Element("_Parameter1") == SsalddelServerEndpoint.AssemblyMetadataKey);
        Assert.Equal("$(SsalddelServerBaseAddress)", (string?)metadata.Element("_Parameter2"));
        Assert.Equal("'$(SsalddelServerBaseAddress)' != ''", (string?)metadata.Parent!.Attribute("Condition"));

        // Packaging cannot affect a client unless its startup reads the app assembly metadata.
        var startup = File.ReadAllText(RepoFile($"{app}/MauiProgram.cs"));
        Assert.Contains("SsalddelServerEndpoint.ResolveMobileBaseAddress(", startup, StringComparison.Ordinal);
        Assert.Contains("typeof(MauiProgram).Assembly", startup, StringComparison.Ordinal);
        Assert.Contains("#if DEBUG || SSALDDEL_USB_FIELD_TEST", startup, StringComparison.Ordinal);
        Assert.Contains("const bool allowInsecureDebugEndpoint = false;", startup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5321/", "http://127.0.0.1:5321/")]
    [InlineData("http://127.0.0.1:5322/api", "http://127.0.0.1:5322/api/")]
    [InlineData("http://10.0.2.2:5322/", "http://10.0.2.2:5322/")]
    public void ExplicitUsbOrEmulatorBuildAddressOverridesUnrelatedRuntimeDefaults(string embedded, string expected)
    {
        var result = SsalddelServerEndpoint.ResolveMobileBaseAddress(AssemblyWithAddress(embedded),
            "https://runtime.mirror.test/", "https://legacy.mirror.test/", allowInsecureDebugEndpoint: true);

        Assert.Equal(new Uri(expected), result);
    }

    [Fact]
    public void IndependentReleaseUsesTheEmbeddedHttpsBasePath()
    {
        var result = SsalddelServerEndpoint.ResolveMobileBaseAddress(AssemblyWithAddress("https://field.mirror.test/base"),
            "http://127.0.0.1:5322/", null, allowInsecureDebugEndpoint: false);

        Assert.Equal(new Uri("https://field.mirror.test/base/"), result);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5322/")]
    [InlineData("https://127.0.0.1:5322/")]
    [InlineData("https://localhost:7117/")]
    [InlineData("https://10.0.2.2:5322/")]
    [InlineData("http://field.mirror.test/")]
    public void IndependentReleaseRejectsLocalOrHttpBuildAddressInsteadOfFallingBack(string embedded)
    {
        Assert.Throws<InvalidOperationException>(() =>
            SsalddelServerEndpoint.ResolveMobileBaseAddress(AssemblyWithAddress(embedded),
                "https://field.mirror.test/", null, allowInsecureDebugEndpoint: false));
    }

    [Fact]
    public void MissingReleaseAddressStopsStartupInsteadOfUsingASampleServer()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SsalddelServerEndpoint.ResolveMobileBaseAddress(typeof(MobileWorkflowEnvironmentTests).Assembly,
                null, null, allowInsecureDebugEndpoint: false));
    }

    [Fact]
    public void RuntimeConfigurationRemainsAvailableWhenNoBuildAddressWasEmbedded()
    {
        var result = SsalddelServerEndpoint.ResolveMobileBaseAddress(typeof(MobileWorkflowEnvironmentTests).Assembly,
            "https://configured.mirror.test/base", "https://legacy.mirror.test/", allowInsecureDebugEndpoint: false);

        Assert.Equal(new Uri("https://configured.mirror.test/base/"), result);
    }

    [Theory]
    [InlineData("SsalddelApp")]
    [InlineData("WarehouseManagerApp")]
    public void LocalHttpExceptionIsLimitedToLoopbackAndTheAndroidEmulator(string app)
    {
        XNamespace android = "http://schemas.android.com/apk/res/android";
        var manifest = XDocument.Load(RepoFile($"{app}/Platforms/Android/AndroidManifest.xml"));
        var application = Assert.Single(manifest.Descendants("application"));
        Assert.Equal("false", (string?)application.Attribute(android + "usesCleartextTraffic"));
        Assert.Equal("@xml/network_security_config", (string?)application.Attribute(android + "networkSecurityConfig"));
        Assert.Contains(manifest.Descendants("uses-permission"),
            permission => (string?)permission.Attribute(android + "name") == "android.permission.INTERNET");

        var security = XDocument.Load(RepoFile($"{app}/Platforms/Android/Resources/xml/network_security_config.xml"));
        var baseline = Assert.Single(security.Descendants("base-config"));
        Assert.Equal("false", (string?)baseline.Attribute("cleartextTrafficPermitted"));
        var localException = Assert.Single(security.Descendants("domain-config"));
        Assert.Equal("true", (string?)localException.Attribute("cleartextTrafficPermitted"));
        var domains = localException.Elements("domain").ToArray();
        Assert.Equal(new[] { "10.0.2.2", "127.0.0.1", "localhost" }, domains.Select(domain => domain.Value).OrderBy(domain => domain, StringComparer.Ordinal));
        Assert.All(domains, domain => Assert.Equal("false", (string?)domain.Attribute("includeSubdomains")));
    }

    private static Assembly AssemblyWithAddress(string address)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"MobileEnvironment-{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        var constructor = typeof(AssemblyMetadataAttribute).GetConstructor(new[] { typeof(string), typeof(string) })!;
        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor,
            new object[] { SsalddelServerEndpoint.AssemblyMetadataKey, address }));
        return assembly;
    }

    private static string RepoFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.v3.5.slnx")))
                return Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        throw new InvalidOperationException("Repository root was not found for mobile packaging contract checks.");
    }
}
