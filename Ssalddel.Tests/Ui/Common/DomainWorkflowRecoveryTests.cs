using System.Net;
using DriverApp.Services;
using DriverApp.ViewModels.Driver;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Tests.Clients;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DomainWorkflowRecoveryTests
{
    [Fact]
    public void 서버맥락은_파서에서_Api오류까지_보존하고_원문으로판정을덮지않는다()
    {
        var problem = SsalddelApiProblemParser.Parse("""
            { "errorCode":"CargoTransportHeld", "detail":"RAW_PRIVATE HTTP404 사진 없음",
              "availableRecoveryActions":["RefreshState"], "failureClassCode":"BusinessRuleBlocked",
              "workflowContext":{"operatingSystemId":"DomesticCargoTransportOS","stageId":"cargo.interruption-recovery","outcomeCode":"NotApplied"} }
            """);
        var exception = new SsalddelApiException(problem.Message!, 409, "상차", "RAW_BODY", null,
            failureClassCode: problem.FailureClassCode, availableRecoveryActions: problem.AvailableRecoveryActions,
            errorCode: problem.ErrorCode, workflowContext: problem.WorkflowContext);
        var error = Api작업오류.변환(exception);
        Assert.Equal(problem.WorkflowContext, error.WorkflowContext);
        var notice = 업무오류안내Presentation.생성(error);
        Assert.Equal("운송 보류", notice.제목);
        Assert.DoesNotContain("RAW", notice.제목 + notice.설명);
        Assert.True(notice.재조회허용);
    }

    [Theory]
    [InlineData("DomesticCargoTransportOS", "warehouse.receiving", "NotApplied")]
    [InlineData("not-an-os", "cargo.pickup", "NotApplied")]
    [InlineData("DomesticCargoTransportOS", "cargo.pickup", "AppliedMaybe")]
    public void 다른OS단계나_알수없는결과는_맥락으로받지않는다(string os, string stage, string outcome)
    {
        var body = System.Text.Json.JsonSerializer.Serialize(new
        { workflowContext = new { operatingSystemId = os, stageId = stage, outcomeCode = outcome } });
        Assert.Null(SsalddelApiProblemParser.Parse(body).WorkflowContext);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"workflowContext\":[]}")]
    [InlineData("{\"workflowContext\":{\"operatingSystemId\":7}}")]
    public void 구판이나잘못된응답은_업무맥락을추측하지않는다(string body)
        => Assert.Null(SsalddelApiProblemParser.Parse(body).WorkflowContext);

    [Theory]
    [InlineData("cargo.pickup", "상차")]
    [InlineData("cargo.dropoff", "하차")]
    [InlineData("cargo.transport", "운송")]
    public void 조회장애는_현재업무단계로안내하고_메시지문자열은읽지않는다(string stage, string label)
    {
        var context = new 업무실패맥락Dto(OperatingSystemIds.DomesticCargoTransport, stage, 업무처리결과Codes.조회);
        var notice = 업무오류안내Presentation.생성(new Api작업오류("transport-failure", "RAW NotFound Unauthorized"), context);
        Assert.Equal($"{label} 정보 확인 필요", notice.제목);
        Assert.Equal($"{label} 확인", notice.재조회문구);
        Assert.True(notice.재조회허용);
        Assert.DoesNotContain("RAW", notice.설명);
    }

    [Theory]
    [InlineData("CargoPickupPhotoRequired", "상차 사진 필요")]
    [InlineData("CargoDropoffPhotoRequired", "하차 사진 필요")]
    [InlineData("CargoReceiptEvidenceRequired", "인수 증빙 확인 필요")]
    public void 증빙누락은_GET재시도버튼보다_해당입력의확인을안내한다(string code, string title)
    {
        var context = Cargo(업무처리결과Codes.미적용);
        if (code == "CargoDropoffPhotoRequired") context = context with { StageId = OperatingSystemLifecycleStageIds.CargoDropoff };
        var error = new Api작업오류(code, "RAW", WorkflowContext: context,
            복구가능행동: [업무복구행동Ids.상태전체재조회]);
        var notice = 업무오류안내Presentation.생성(error);
        Assert.Equal(title, notice.제목); Assert.False(notice.재조회허용);
    }

    [Fact]
    public void 다른OS에들어온화물사유는_화물업무안내나새행동으로승격하지않는다()
    {
        var error = new Api작업오류("CargoPickupPhotoRequired", "RAW", 400,
            복구가능행동: [업무복구행동Ids.상태전체재조회],
            WorkflowContext: new("FoodDeliveryOS", "food.cooking", "NotApplied"));
        Assert.True(error.WorkflowContext!.유효한가());
        var notice = 업무오류안내Presentation.생성(error);
        Assert.NotEqual("상차 사진 필요", notice.제목);
        Assert.False(notice.재조회허용);
    }

    [Fact]
    public void 서버에없는복구행동이나_동일요청재전송은_GET행동으로승격하지않는다()
    {
        var error = new Api작업오류("CargoTransportStateChanged", "RAW", WorkflowContext: Cargo(업무처리결과Codes.미적용),
            복구가능행동: [업무복구행동Ids.동일요청멱등재시도]);
        Assert.False(업무오류안내Presentation.생성(error).재조회허용);
    }

    [Fact]
    public void 결과미확정은_404보다먼저_동일업무확인만허용한다()
    {
        var error = new Api작업오류("CargoTransportNotFound", "RAW", 404,
            WorkflowContext: Cargo(업무처리결과Codes.미확정));
        var notice = 업무오류안내Presentation.생성(error);
        Assert.Equal("상차 결과 확인 필요", notice.제목);
        Assert.False(notice.복귀허용); Assert.Equal("결과 확인", notice.재조회문구);
        Assert.DoesNotContain("취소", notice.설명);
    }

    [Fact]
    public void 등록후조회실패는_등록완료와_내역확인대기를구분한다()
    {
        var context = new 업무실패맥락Dto(OperatingSystemIds.WarehouseCommerceFulfillment,
            OperatingSystemLifecycleStageIds.WarehouseInboundPlan, 업무처리결과Codes.적용);
        var notice = 업무오류안내Presentation.생성(new Api작업오류("read-failure", "RAW"), context);
        Assert.Equal("입고 신청 완료 · 내역 확인 필요", notice.제목);
        Assert.Equal("내역 확인", notice.재조회문구);
        Assert.Contains("다시 제출하지 말고", notice.설명);
    }

    [Fact]
    public void 완료된입고의조회실패는_신청완료와구분한다()
    {
        var context = new 업무실패맥락Dto(OperatingSystemIds.WarehouseCommerceFulfillment,
            OperatingSystemLifecycleStageIds.WarehouseReceiving, 업무처리결과Codes.적용);
        var notice = 업무오류안내Presentation.생성(new Api작업오류("read-failure", "RAW"), context);
        Assert.Equal("입고 완료 · 내역 확인 필요", notice.제목);
        Assert.Contains("다시 완료하지 말고", notice.설명);
    }

    [Theory]
    [InlineData("NotApplicable", "상차")]
    [InlineData("NotApplied", "운송")]
    public void GET표시만같은OS의화면목적으로구체화하고_명령단계는덮지않는다(string outcome, string label)
    {
        var error = new Api작업오류("test-failure", "RAW", WorkflowContext: new(OperatingSystemIds.DomesticCargoTransport,
            OperatingSystemLifecycleStageIds.CargoTransport, outcome));
        var notice = 업무오류안내Presentation.생성(error, Cargo(업무처리결과Codes.조회));
        Assert.Equal($"{label} 정보 확인 필요", notice.제목);
    }

    [Fact]
    public async Task 기사조회실패는_구조화맥락을보존하고_정상재조회면해소한다()
    {
        var problem = new SsalddelApiProblem(null, null, "CargoTransportHeld", new Dictionary<string, string[]>(),
            업무실패분류Codes.업무규칙차단, null, true, null, [업무복구행동Ids.상태전체재조회], 9, null,
            Cargo(업무처리결과Codes.조회));
        using var page = new DriverPage(new DriverApiException(HttpStatusCode.Conflict, "운송", "CargoTransportHeld", null, problem));
        Assert.False(await page.초기화Async());
        Assert.Equal(problem.WorkflowContext, page.Api조회오류?.WorkflowContext);
        Assert.Equal("CargoTransportHeld", page.Api조회오류?.코드);
        Assert.Equal(9, page.Api조회오류?.현재Revision);
        page.Error = null;
        Assert.True(await page.새로고침Async()); Assert.Null(page.Api조회오류);
    }

    [Fact]
    public async Task 화주조회실패는_메시지로추측하지않고_서버사유를보존후_정상조회에서해소한다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var loading = fixture.Model.LoadAsync("A");
        (await fixture.Operations.NextAsync()).Completion.SetException(new SsalddelApiException("RAW_PRIVATE", 404,
            "의뢰 조회", "RAW_BODY", null, errorCode: "ShipperRequestNotFound",
            workflowContext: new("ShipperTransportManagementOS", "shipper.progress-change", "NotApplicable")));
        await loading;
        Assert.Equal("ShipperRequestNotFound", fixture.Model.State.QueryError?.코드);
        Assert.Equal("shipper.progress-change", fixture.Model.State.QueryError?.WorkflowContext?.StageId);
        var recovery = fixture.Model.LoadAsync("A", showMessage: false);
        (await fixture.Operations.NextAsync()).Completion.SetResult(new SsalddelApp.Models.Shipper.ShipperRequestItem { 의뢰Id = "A" });
        await recovery;
        Assert.Null(fixture.Model.State.QueryError);
        Assert.Empty(fixture.Model.State.StatusMessage);
    }

    private static 업무실패맥락Dto Cargo(string outcome)
        => new(OperatingSystemIds.DomesticCargoTransport, OperatingSystemLifecycleStageIds.CargoPickup, outcome);

    private sealed class DriverPage(Exception exception) : 기사PageViewModelBase
    {
        public Exception? Error { get; set; } = exception;
        protected override Task 불러오기Async(bool refresh, CancellationToken ct)
            => Error is null ? Task.CompletedTask : Task.FromException(Error);
    }
}
