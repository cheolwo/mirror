using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Admin.DispatchTraining;
using SsalddelAdmin.Components.DispatchTraining;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoPlanReviewRenderTests
{
    [Theory]
    [InlineData(true, true, "CargoPlan.Verified", "가능")]
    [InlineData(true, false, "FreightSegment.CapacityExceeded", "불가")]
    [InlineData(false, false, "CargoPlan.HandlingTimeMissing", "정보 부족")]
    public async Task 수행계획은_순서와작업시간을_가능불가정보부족으로구분한다(bool verified, bool feasible, string reason, string expected)
    {
        var html = WebUtility.HtmlDecode(await Render(Workspace(verified, feasible, reason)));
        Assert.Contains("상하차 순서와 약속 확인", html);
        Assert.Contains("P1 · 1 건 수행", html);
        Assert.Contains(expected, html);
        Assert.Contains("A 상차", html); Assert.Contains("A 하차", html);
        Assert.Contains(verified ? "작업 2분" : "작업 미확인", html);
        Assert.Contains("합의 보수 미확인", html);
        Assert.Contains("실제 도로 소요시간과 운영 중인 기사 상태는 아닙니다", html);
        Assert.Contains("선택한 계획들을 동시에 배차한다는 뜻은 아닙니다", html);
        Assert.DoesNotContain("운영 배차 완료", html);
        if (!feasible)
        {
            Assert.Contains("확인된 가능 계획이 없어 모델 비교를 시작하지 않습니다", html);
            Assert.Matches("<input[^>]*type=\"checkbox\"[^>]*disabled", html);
        }
    }

    [Fact]
    public async Task 모델원문과검증결과는_규칙과사람승인에서분리하고_HTML로실행하지않는다()
    {
        var w = Workspace(true, true, "CargoPlan.Verified");
        var item = w.Cases[0] with
        {
            Ai = new CargoTrainingDecision
            {
                Status = "invalid", RawResponse = "<script>bad()</script>", StructureValid = false,
                FactsValid = false, ErrorCode = "InvalidDecisionShape"
            },
            CodexOpinion = new CargoTrainingDecision { Status = "selected", CandidateId = "P1", Source = "codex_advisory_only" }
        };
        var raw = await Render(w with { Cases = [item] });
        var html = WebUtility.HtmlDecode(raw);
        Assert.Contains("기존 규칙", html); Assert.Contains("저장된 사람 의견", html);
        Assert.Contains("모델 원응답과 검증 결과", html);
        Assert.Contains("응답 구조 사용 불가", html); Assert.Contains("사실 근거 사용 불가", html);
        Assert.Contains("코덱스 의견", html); Assert.Contains("사람 승인과 별개", html);
        Assert.DoesNotContain("<script>bad()</script>", raw);
        Assert.Contains("아직 의견이 없습니다", html);
    }

    [Fact]
    public async Task 계획이없는과거사례는_기존후보기사검토를유지한다()
    {
        var w = Workspace(true, true, "CargoPlan.Verified");
        var c = w.Cases[0];
        var html = WebUtility.HtmlDecode(await Render(w with { Cases = [c with
        {
            PlanSetSha256 = null, Candidates = [c.Candidates[0] with { Plan = null }]
        }] }));
        Assert.Contains("후보 기사", html);
        Assert.DoesNotContain("상하차 순서와 약속 확인", html);
        Assert.Contains("검토 저장", html);
    }

    private static CargoTrainingWorkspace Workspace(bool verified, bool feasible, string reason)
    {
        var region = new CargoTrainingRegion("1126010100", "서울특별시 중랑구 면목동");
        return new()
        {
            DatasetId = "render-fixture-r76", DatasetSha256 = new string('a', 64),
            Cases = [new CargoTrainingCase
            {
                CaseId = "plan-render", Title = "상하차 계획 비교", Situation = "화물 한 건의 계획을 확인합니다.",
                PickupRegion = region, DropoffRegion = region, PlanSetSha256 = new string('b', 64),
                Candidates = [new CargoTrainingCandidate
                {
                    Id = "P1", Eligible = verified && feasible, Vehicle = "가상 실차", CurrentRegion = region, AvailableRegion = region,
                    Plan = new CargoTrainingPlan
                    {
                        PlanId = "render-plan", DriverId = "render-driver", Verified = verified, Feasible = feasible,
                        ReasonCode = reason, RequestIds = ["A"], PlanSha256 = new string('c', 64), InputSha256 = new string('d', 64),
                        TotalMinutes = verified ? 10m : null,
                        Stops = [new("A", "pickup", 0, verified ? 3m : null, verified ? 2m : null, verified ? 5m : null, 60),
                            new("A", "dropoff", 1, verified ? 8m : null, 2m, verified ? 10m : null, 90)],
                        Segments = [new(0, "A", "pickup", ["A"], 3m, .01m, 0, 0m, [new string('e', 64)])]
                    }
                }]
            }]
        };
    }

    private static async Task<string> Render(CargoTrainingWorkspace workspace)
    {
        var services = new ServiceCollection(); services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<CargoRegionalTrainingReview>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(CargoRegionalTrainingReview.Client)] = new Client(workspace) }));
            return root.ToHtmlString();
        });
    }

    private sealed class Client(CargoTrainingWorkspace workspace) : ICargoRegionalTrainingReviewClient
    {
        public Task<CargoTrainingWorkspace> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(workspace);
        public Task<CargoTrainingWorkspace> RecordAsync(CargoTrainingReviewCommand command, CancellationToken cancellationToken = default) => throw new InvalidOperationException("RenderDoesNotApprove");
        public Task<CargoTrainingWorkspace> CompareAsync(string caseId, string inputSha256, CancellationToken cancellationToken = default) => throw new InvalidOperationException("RenderDoesNotRunModel");
    }
}
