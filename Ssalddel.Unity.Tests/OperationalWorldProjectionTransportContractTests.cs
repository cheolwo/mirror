using System;
using System.IO;
using System.Linq;
using Ssalddel.Unity.WorldProjection;
using Xunit;

namespace Ssalddel.Unity.Tests
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "운영 서버 읽기 전용 전송과 운영·Simulation 자료원 분리를 자동 검증한다.",
        Boundary = "정적 계약·샘플 중앙화 검사와 실제 서버·Unity 실행 증거를 구분한다.")]
    public sealed class OperationalWorldProjectionTransportContractTests
    {
        [Fact]
        public void Endpoint는_BaseUrl과상대Route를안전하게정규화한다()
        {
            var endpoint = new OperationalWorldProjectionEndpoint(
                "https://api.ssalddel.test/root",
                20);

            Assert.Equal(new Uri("https://api.ssalddel.test/root/"), endpoint.BaseUri);
            Assert.Equal(
                new Uri("https://api.ssalddel.test/root/api/v1/world"),
                endpoint.BuildRequestUri("/api/v1/world"));
            Assert.Equal(20, endpoint.TimeoutSeconds);
        }

        [Theory]
        [InlineData("file:///tmp/server")]
        [InlineData("https://api.ssalddel.test/?token=secret")]
        [InlineData("relative/server")]
        public void Endpoint는_HTTP가아니거나비밀이섞일수있는주소를거부한다(
            string baseUrl)
        {
            Assert.Throws<ArgumentException>(() =>
                new OperationalWorldProjectionEndpoint(baseUrl));
        }

        [Fact]
        public void Transport계약은_GET읽기만제공한다()
        {
            var methods = typeof(IOperationalWorldProjectionTransport)
                .GetMethods()
                .Select(method => method.Name)
                .ToArray();

            Assert.Equal(new[] { "GetAsync" }, methods);
            Assert.DoesNotContain(methods, method =>
                method.Contains("Post", StringComparison.OrdinalIgnoreCase)
                || method.Contains("Command", StringComparison.OrdinalIgnoreCase)
                || method.Contains("Write", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void 운영상태사본과SimulationSession은_명시적으로다른자료원이다()
        {
            Assert.NotEqual(
                WorldProjectionSourceModeCodes.SimulationSession,
                WorldProjectionSourceModeCodes.OperationalSnapshot);
        }

        [Fact]
        public void 자료원선택은_운영과Simulation사이자동Fallback을거부한다()
        {
            var operational = new WorldProjectionSourceSelection(
                "world-module:warehouse-observation",
                WorldProjectionSourceModeCodes.OperationalSnapshot);

            Assert.False(operational.AllowAutomaticFallback);
            Assert.Throws<ArgumentException>(() =>
                new WorldProjectionSourceSelection(
                    "world-module:warehouse-observation",
                    WorldProjectionSourceModeCodes.OperationalSnapshot,
                    true));
        }

        [Fact]
        public void Unity샘플은_공통운영조회전송만사용한다()
        {
            var repositoryRoot = FindRepositoryRoot();
            var samplesRoot = Path.Combine(repositoryRoot, "Ssalddel.Unity", "Samples~");
            var directTransportConsumers = Directory
                .EnumerateFiles(samplesRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains(
                            "using UnityEngine.Networking;",
                            StringComparison.Ordinal)
                        || source.Contains(
                            "UnityWebRequest.Get(",
                            StringComparison.Ordinal)
                        || source.Contains(
                            "new UnityWebRequest(",
                            StringComparison.Ordinal);
                })
                .Select(path => Path.GetRelativePath(repositoryRoot, path))
                .ToArray();

            Assert.Empty(directTransportConsumers);

            var transportSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Ssalddel.Unity",
                "Runtime",
                "OperationalTransport",
                "UnityWebRequestOperationalWorldProjectionTransport.cs"));
            Assert.Contains("UnityWebRequest.Get(", transportSource, StringComparison.Ordinal);
        }

        [Fact]
        public void 운영조회샘플Assembly는_공통전송Assembly를참조한다()
        {
            var repositoryRoot = FindRepositoryRoot();
            var samplesRoot = Path.Combine(repositoryRoot, "Ssalddel.Unity", "Samples~");
            var operationalSampleAssemblies = Directory
                .EnumerateFiles(samplesRoot, "*.asmdef", SearchOption.AllDirectories)
                .Where(path => Directory
                    .EnumerateFiles(Path.GetDirectoryName(path)!, "*.cs", SearchOption.TopDirectoryOnly)
                    .Any(sourcePath => File.ReadAllText(sourcePath).Contains(
                        "IOperationalWorldProjectionTransport",
                        StringComparison.Ordinal)))
                .ToArray();

            Assert.NotEmpty(operationalSampleAssemblies);
            foreach (var assemblyPath in operationalSampleAssemblies)
            {
                var assemblyDefinition = File.ReadAllText(assemblyPath);
                Assert.Contains(
                    "Ssalddel.Unity.OperationalTransport",
                    assemblyDefinition,
                    StringComparison.Ordinal);
            }
        }

        private static string FindRepositoryRoot()
        {
            var candidates = new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
            };

            foreach (var candidate in candidates)
            {
                var directory = new DirectoryInfo(candidate);
                while (directory != null)
                {
                    if (Directory.Exists(Path.Combine(directory.FullName, "Ssalddel.Unity"))
                        && File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new DirectoryNotFoundException("HongdalRepositoryRootNotFound");
        }
    }
}
