using System.Text;
using Ssalddel.CodeMap;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Tests.Architecture;

public sealed class FoodDriverCodeMapSourceBindingTests
{
    [Fact]
    public void 실제음식기사소스결속은_반영된단계와존재하는파일및심볼을가리킨다()
    {
        var manifest = FoodDriverCodeMetadataTests.ReadManifest();
        var knownSteps = FoodDriverCodeMetadataTests.ReadMetadata()
            .Select(item => new CodeMapStepIdentity(item.FeatureKey, item.StepKey)).ToArray();
        Assert.Empty(CodeMapSourceBindingValidator.ValidateManifest(
            FoodDriverCodeMetadataTests.FindRepositoryRoot(), manifest));
        var result = CodeMapSourceBindingValidator.Validate(
            FoodDriverCodeMetadataTests.FindRepositoryRoot(), manifest, knownSteps);

        Assert.Empty(result.Diagnostics);
        Assert.NotEmpty(result.Bindings);
        Assert.Equal(manifest.Features.Sum(feature => feature.SourceBindings.Length), result.Bindings.Length);
        var feature = Assert.Single(manifest.Features,
            item => item.Key == SsalddelCodeFeatureKeys.FoodDriverWork);
        Assert.All(new[]
        {
            "native.current-work",
            "native.automatic-start",
            "native.pending-recovery",
            "native.location-transmission",
            "native.work-screen",
            "native.profile-settlement",
            "native.foreground-notification",
        }, key => Assert.Single(feature.SourceBindings, binding => binding.Key == key));
        Assert.All(result.Bindings, binding =>
        {
            Assert.All(binding.RelatedStepKeys, key => Assert.Contains(knownSteps,
                step => step.FeatureKey == binding.FeatureKey && step.StepKey == key));
            Assert.NotEmpty(binding.Sources);
            Assert.All(binding.Sources, source => Assert.True(source.Line > 0));
        });
        Assert.All(new[]
        {
            "FDriverApp/PageModels/FDriverWorkspaceRefreshSession.cs",
            "FDriverApp/PageModels/FDriverPendingOperationCoordinator.cs",
            "FDriverApp/PageModels/FDriverLocationTransmissionSession.cs",
            "Ssalddel/Services/Community/음식주문원장동기화PayloadHandler.cs",
            "Ssalddel/Services/Community/창고출고원장동기화PayloadHandler.cs",
            "Ssalddel/Services/Community/음식합의취소투영PayloadHandler.cs",
            "Ssalddel/Services/Community/음식기사후속PayloadHandler.cs"
        }, path => Assert.Contains(result.Bindings.SelectMany(binding => binding.Sources), source => source.Path == path));
    }

    [Fact]
    public void CS와Razor및짝이맞는XAML은_정확한소스행으로결속한다()
    {
        using var fixture = new SourceFixture();
        fixture.Write("src/Worker.cs", "namespace Fixture;\npublic class Worker { }\n");
        fixture.Write("src/Workspace.razor", "@page \"/driver\"\n<p>현재 업무</p>\n");
        fixture.Write("src/DriverPage.xaml", Xaml("Fixture.DriverPage"));
        fixture.Write("src/DriverPage.xaml.cs", "namespace Fixture;\npublic partial class DriverPage { }\n");
        fixture.Binding.Sources =
        [
            Source("src/Worker.cs", "public class Worker"),
            Source("src/Workspace.razor", "@page \"/driver\""),
            Source("src/DriverPage.xaml", "x:Class=\"Fixture.DriverPage\""),
            Source("src/DriverPage.xaml.cs", "public partial class DriverPage"),
        ];

        var result = fixture.Validate();

        Assert.Empty(result.Diagnostics);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal("fixture.source", binding.Key);
        Assert.Equal(["application.current-work"], binding.RelatedStepKeys);
        Assert.Equal([2, 1, 1, 2], binding.Sources.Select(source => source.Line));
    }

    [Theory]
    [InlineData("")]
    [InlineData("application.current-work")]
    [InlineData("fixture.source")]
    public void 비어있거나단계및다른결속과중복된키는_거부한다(string bindingKey)
    {
        using var fixture = new SourceFixture();
        if (bindingKey == "fixture.source")
        {
            fixture.Feature.SourceBindings = [fixture.Binding, new CodeMapSourceBinding
            {
                Key = bindingKey,
                RelatedStepKeys = fixture.Binding.RelatedStepKeys,
                Responsibility = fixture.Binding.Responsibility,
                Boundary = fixture.Binding.Boundary,
                Sources = fixture.Binding.Sources,
            }];
        }
        else fixture.Binding.Key = bindingKey;

        AssertError(fixture.Validate(), "CODEMAP050");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 소스결속에는_책임과경계가모두필요하다(bool missingResponsibility)
    {
        using var fixture = new SourceFixture();
        if (missingResponsibility) fixture.Binding.Responsibility = "";
        else fixture.Binding.Boundary = "";

        AssertError(fixture.Validate(), "CODEMAP051");
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("other-feature")]
    public void 관련단계는_동일기능의실재단계를중복없이참조해야한다(string invalidKind)
    {
        using var fixture = new SourceFixture();
        fixture.Binding.RelatedStepKeys = invalidKind switch
        {
            "empty" => [],
            "missing" => ["application.missing"],
            "duplicate" => ["application.current-work", "application.current-work"],
            _ => ["application.other-feature-only"],
        };
        var knownSteps = new[]
        {
            new CodeMapStepIdentity(SourceFixture.FeatureKey, "application.current-work"),
            new CodeMapStepIdentity("other-feature", "application.other-feature-only"),
        };

        AssertError(fixture.Validate(knownSteps), "CODEMAP052");
    }

    [Theory]
    [InlineData("../outside.cs")]
    [InlineData("/outside.cs")]
    [InlineData("C:/outside.cs")]
    [InlineData("src/../outside.cs")]
    [InlineData("src\\Worker.cs")]
    [InlineData("src//Worker.cs")]
    [InlineData("./src/Worker.cs")]
    [InlineData("src-extra/Worker.cs")]
    [InlineData("other/Worker.cs")]
    [InlineData("src/bin/Worker.cs")]
    [InlineData("src/obj/Worker.cs")]
    [InlineData("src/private/Worker.cs")]
    [InlineData("src/Worker.json")]
    public void 루트이탈과허용범위밖및제외소스경로는_거부한다(string invalidPath)
    {
        using var fixture = new SourceFixture();
        fixture.Write("src-extra/Worker.cs", "public class Worker { }");
        fixture.Write("other/Worker.cs", "public class Worker { }");
        fixture.Write("src/bin/Worker.cs", "public class Worker { }");
        fixture.Write("src/obj/Worker.cs", "public class Worker { }");
        fixture.Write("src/private/Worker.cs", "public class Worker { }");
        fixture.Write("src/Worker.json", "public class Worker { }");
        fixture.Binding.Sources = [Source(invalidPath, "public class Worker")];

        var result = fixture.Validate();

        AssertError(result, "CODEMAP054");
        Assert.Empty(result.Bindings);
    }

    [Fact]
    public void 허용경로라도_파일이없으면_결속하지않는다()
    {
        using var fixture = new SourceFixture();
        fixture.Binding.Sources = [Source("src/Missing.cs", "public class Missing")];

        var result = fixture.Validate();

        AssertError(result, "CODEMAP054");
        Assert.Empty(result.Bindings);
    }

    [Fact]
    public void 존재하는파일이라도_심볼이없으면_결속하지않는다()
    {
        using var fixture = new SourceFixture();
        fixture.Binding.Sources = [Source("src/Worker.cs", "public class Missing")];

        var result = fixture.Validate();

        AssertError(result, "CODEMAP055");
        Assert.Empty(result.Bindings);
    }

    [Theory]
    [InlineData("empty-sources")]
    [InlineData("empty-symbol")]
    [InlineData("multiline-symbol")]
    [InlineData("duplicate-symbol")]
    public void 소스와한줄심볼이빠지거나중복되면_거부한다(string invalidKind)
    {
        using var fixture = new SourceFixture();
        fixture.Binding.Sources = invalidKind switch
        {
            "empty-sources" => [],
            "empty-symbol" => [Source("src/Worker.cs", "")],
            "multiline-symbol" => [Source("src/Worker.cs", "namespace Fixture;\npublic class Worker")],
            _ => [fixture.Binding.Sources[0], fixture.Binding.Sources[0]],
        };

        AssertError(fixture.Validate(), "CODEMAP053");
    }

    [Fact]
    public void UTF8이아닌소스는_정상결속으로보고하지않는다()
    {
        using var fixture = new SourceFixture();
        File.WriteAllBytes(Path.Combine(fixture.Root, "src", "Worker.cs"), [0xc3, 0x28]);

        var result = fixture.Validate();

        AssertError(result, "CODEMAP054");
        Assert.Empty(result.Bindings);
    }

    [Theory]
    [InlineData("missing-code")]
    [InlineData("wrong-namespace")]
    [InlineData("wrong-class")]
    [InlineData("not-partial")]
    [InlineData("invalid-xml")]
    public void XAML은_같은결속의코드부와네임스페이스및partial클래스가맞아야한다(string invalidKind)
    {
        using var fixture = new SourceFixture();
        fixture.Write("src/DriverPage.xaml", invalidKind == "invalid-xml"
            ? "<ContentPage x:Class=\"Fixture.DriverPage\"" : Xaml("Fixture.DriverPage"));
        var code = invalidKind switch
        {
            "wrong-namespace" => "namespace Other;\npublic partial class DriverPage { }",
            "wrong-class" => "namespace Fixture;\npublic partial class OtherPage { }",
            "not-partial" => "namespace Fixture;\npublic class DriverPage { }",
            _ => "namespace Fixture;\npublic partial class DriverPage { }",
        };
        fixture.Write("src/DriverPage.xaml.cs", code);
        fixture.Binding.Sources = invalidKind == "missing-code"
            ? [Source("src/DriverPage.xaml", "x:Class=\"Fixture.DriverPage\"")]
            : [Source("src/DriverPage.xaml", "x:Class=\"Fixture.DriverPage\""),
               Source("src/DriverPage.xaml.cs", "namespace ")];

        var result = fixture.Validate();

        AssertError(result, "CODEMAP056");
        Assert.Empty(result.Bindings);
    }

    [Fact]
    public void Partial타입은_명시한파일과메서드심볼의행을각각선택한다()
    {
        using var fixture = new SourceFixture();
        fixture.Write("src/DriverWork.cs", "namespace Fixture;\npublic partial class DriverWork\n{\n    public void Start() { }\n}\n");
        fixture.Write("src/DriverWork.Current.cs", "namespace Fixture;\n\npublic partial class DriverWork\n{\n    public void LoadCurrent() { }\n}\n");
        fixture.Binding.Sources =
        [
            Source("src/DriverWork.cs", "public void Start()"),
            Source("src/DriverWork.Current.cs", "public void LoadCurrent()"),
        ];

        var result = fixture.Validate();

        Assert.Empty(result.Diagnostics);
        var sources = Assert.Single(result.Bindings).Sources;
        Assert.Equal(["src/DriverWork.cs", "src/DriverWork.Current.cs"], sources.Select(source => source.Path));
        Assert.Equal([4, 5], sources.Select(source => source.Line));
    }

    [Theory]
    [InlineData("src")]
    [InlineData("src/Worker.cs")]
    public void 음식기사Manifest는_실존디렉터리나개별소스파일범위와필수단계를허용한다(string sourceRoot)
    {
        using var fixture = new SourceFixture();
        fixture.Manifest.SourceRoots = [sourceRoot];

        Assert.Empty(CodeMapSourceBindingValidator.ValidateManifest(fixture.Root, fixture.Manifest));
        Assert.Empty(fixture.Validate().Diagnostics);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("blank")]
    [InlineData("duplicate")]
    [InlineData("absolute")]
    [InlineData("parent")]
    [InlineData("backslash")]
    [InlineData("missing-directory")]
    [InlineData("missing-file")]
    [InlineData("excluded")]
    public void 음식기사Manifest의_빈소스루트와중복및허용범위이탈은_거부한다(string invalidKind)
    {
        using var fixture = new SourceFixture();
        fixture.Write("src/private/Worker.cs", "public class Worker { }");
        fixture.Manifest.SourceRoots = invalidKind switch
        {
            "empty" => [],
            "blank" => [""],
            "duplicate" => ["src", "src"],
            "absolute" => [fixture.Root],
            "parent" => ["../src"],
            "backslash" => ["src\\private"],
            "missing-directory" => ["not-present"],
            "missing-file" => ["src/Missing.cs"],
            _ => ["src/private"],
        };

        Assert.Contains(CodeMapSourceBindingValidator.ValidateManifest(fixture.Root, fixture.Manifest),
            item => item.Code == "CODEMAP023" && item.Severity == SsalddelCodeMetadataDiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("empty-features")]
    [InlineData("empty-key")]
    [InlineData("duplicate-key")]
    [InlineData("empty-required-steps")]
    [InlineData("blank-required-step")]
    [InlineData("duplicate-required-step")]
    public void 음식기사Manifest의_기능과필수단계삭제및중복은_거부한다(string invalidKind)
    {
        using var fixture = new SourceFixture();
        switch (invalidKind)
        {
            case "empty-features": fixture.Manifest.Features = []; break;
            case "empty-key": fixture.Feature.Key = ""; break;
            case "duplicate-key": fixture.Manifest.Features = [fixture.Feature, fixture.Feature]; break;
            case "empty-required-steps": fixture.Feature.RequiredStepKeys = []; break;
            case "blank-required-step": fixture.Feature.RequiredStepKeys = [""]; break;
            default: fixture.Feature.RequiredStepKeys = ["application.current-work", "application.current-work"]; break;
        }

        var expectedCode = invalidKind is "empty-key" or "duplicate-key" ? "CODEMAP022" : "CODEMAP024";
        Assert.Contains(CodeMapSourceBindingValidator.ValidateManifest(fixture.Root, fixture.Manifest),
            item => item.Code == expectedCode && item.Severity == SsalddelCodeMetadataDiagnosticSeverity.Error);
    }

    [Fact]
    public void 기존SimulationManifest는_음식기사전용필수항목으로실패시키지않는다()
    {
        using var fixture = new SourceFixture();
        fixture.Manifest.Key = "simulation-unity";
        fixture.Manifest.SourceRoots = [];
        fixture.Manifest.Features = [];

        Assert.Empty(CodeMapSourceBindingValidator.ValidateManifest(fixture.Root, fixture.Manifest));
    }

    [Theory]
    [InlineData("Ssalddel/Application/Current.cs", "namespace Ssalddel.Application; public partial class CurrentWork { }", true)]
    [InlineData("FDriverApp/Application/Current.cs", "namespace Ssalddel.Application; public partial class CurrentWork { }", false)]
    [InlineData("Ssalddel/Application/Current.cs", "namespace Other.Application; public partial class CurrentWork { }", false)]
    [InlineData("Ssalddel/Application/Current.cs", "namespace Ssalddel.Application; public partial class OtherWork { }", false)]
    public void Reflection선언소스는_동명네이티브타입대신_프로젝트와네임스페이스를확인한다(
        string path, string content, bool expected)
    {
        Assert.Equal(expected, CodeMapSourceBindingValidator.MatchesReflectedSource(
            path, content, "Ssalddel", "Ssalddel.Application", "CurrentWork"));
    }

    [Theory]
    [InlineData("@namespace Ssalddel.Ui.Common.Areas.App\n<p>현재 업무</p>", true)]
    [InlineData("@namespace Other.Areas.App\n<p>현재 업무</p>", false)]
    [InlineData("<p>현재 업무</p>", true)]
    public void RazorReflection선언은_명시또는경로기반네임스페이스를확인한다(string content, bool expected)
    {
        Assert.Equal(expected, CodeMapSourceBindingValidator.MatchesReflectedSource(
            "Ssalddel.Ui.Common/Areas/App/CurrentWork.razor", content,
            "Ssalddel.Ui.Common", "Ssalddel.Ui.Common.Areas.App", "CurrentWork"));
    }

    private static CodeMapSourceReference Source(string path, string symbol)
        => new() { Path = path, Symbol = symbol };

    private static string Xaml(string className)
        => $"<ContentPage xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\" x:Class=\"{className}\" />";

    private static void AssertError(CodeMapSourceBindingValidationResult result, string code)
        => Assert.Contains(result.Diagnostics, item => item.Code == code
            && item.Severity == SsalddelCodeMetadataDiagnosticSeverity.Error);

    private sealed class SourceFixture : IDisposable
    {
        public const string FeatureKey = "food-driver-work-fixture";
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "Ssalddel.CodeMap.Tests", Guid.NewGuid().ToString("N"));
        public CodeMapSourceBinding Binding { get; } = new()
        {
            Key = "fixture.source",
            RelatedStepKeys = ["application.current-work"],
            Responsibility = "현재 업무 화면에서 관련 구현으로 이동한다.",
            Boundary = "소스 탐색이며 런타임 호출이나 저장 완료 증거가 아니다.",
            Sources = [Source("src/Worker.cs", "public class Worker")],
        };
        public WorkAreaFeature Feature { get; }
        public WorkAreaManifest Manifest { get; }

        public SourceFixture()
        {
            Feature = new WorkAreaFeature
            {
                Key = FeatureKey,
                RequiredStepKeys = ["application.current-work"],
                SourceBindings = [Binding],
            };
            Manifest = new WorkAreaManifest
            {
                Key = "food-driver",
                SourceRoots = ["src"],
                ExcludedRoots = ["bin", "obj", "src/private"],
                Features = [Feature],
            };
            Write("src/Worker.cs", "namespace Fixture;\npublic class Worker { }\n");
        }

        public void Write(string relativePath, string content)
        {
            var path = Path.GetFullPath(Path.Combine(Root, relativePath));
            Assert.StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        public CodeMapSourceBindingValidationResult Validate(IReadOnlyCollection<CodeMapStepIdentity>? knownSteps = null)
            => CodeMapSourceBindingValidator.Validate(Root, Manifest,
                knownSteps ?? [new CodeMapStepIdentity(FeatureKey, "application.current-work")]);

        public void Dispose()
        {
            var fullRoot = Path.GetFullPath(Root);
            var fixtureBase = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Ssalddel.CodeMap.Tests"))
                + Path.DirectorySeparatorChar;
            if (!fullRoot.StartsWith(fixtureBase, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("임시 시험 디렉터리 범위를 벗어났습니다.");
            Directory.Delete(fullRoot, recursive: true);
        }
    }
}
