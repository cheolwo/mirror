using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Hr;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Controllers.Common;
using Ssalddel.Security;
using Ssalddel.Services.LogisticsProcessing.Warehouse;

namespace Ssalddel.Tests.Controllers;

public sealed class PhotoInboundRequestControllerTests
{
    [Theory]
    [InlineData(nameof(창고작업Controller.가입고사진신청생성))]
    [InlineData(nameof(창고작업Controller.가입고사진신청목록))]
    [InlineData(nameof(창고작업Controller.가입고사진신청상세))]
    [InlineData(nameof(창고작업Controller.가입고사진내용))]
    [InlineData(nameof(창고작업Controller.가입고사진이름변경))]
    public void 사진신청은_기존운영인증과입고역할범위를유지한다(string actionName)
    {
        Assert.Equal("운영사용자전용", typeof(창고작업Controller).GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        var action = typeof(창고작업Controller).GetMethod(actionName)!;
        Assert.Equal(new[] { HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseInboundOperator },
            action.GetCustomAttribute<RequireHrRoleAttribute>()!.RoleCodes);
        Assert.Contains("inbound-photo-requests", action.GetCustomAttributes().OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Single().Template!);
    }

    [Fact]
    public async Task 사진접수와조회는_객체경로나원본파일명을응답하지않고_캐시를막는다()
    {
        var spy = new Spy(); var id = Guid.NewGuid();
        spy.Create = (requestId, name, file) => Task.FromResult(new 가입고사진신청응답 { Id = requestId, 이름 = name ?? "", Revision = 1 });
        var controller = Controller(spy);
        var form = new FormFile(new MemoryStream([1]), 0, 1, "file", "private-filename.jpg");
        var result = Assert.IsType<OkObjectResult>(await controller.가입고사진신청생성(id, null, form, default));
        var response = Assert.IsType<가입고사진신청응답>(result.Value);
        Assert.Equal(id, response.Id); Assert.Equal(id, spy.CreateId); Assert.Same(form, spy.File);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl);
        var json = System.Text.Json.JsonSerializer.Serialize(response);
        Assert.DoesNotContain("private-filename", json); Assert.DoesNotContain("ObjectName", json); Assert.DoesNotContain("Container", json);
        Assert.Equal(1, spy.CreateCalls);
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("photo")]
    [InlineData("list")]
    [InlineData("name")]
    public async Task 조회와이름수정은_같은신청번호와개정을전달하고_비공개캐시를유지한다(string action)
    {
        var spy = new Spy(); var id = Guid.NewGuid(); var controller = Controller(spy);
        var rename = new 가입고사진이름변경요청 { Revision = 7, 이름 = "새 이름" };
        IActionResult result = action switch
        {
            "photo" => await controller.가입고사진내용(id, default),
            "list" => await controller.가입고사진신청목록(2, 10, default),
            "name" => await controller.가입고사진이름변경(id, rename, default),
            _ => await controller.가입고사진신청상세(id, default)
        };
        Assert.IsType<OkObjectResult>(result); Assert.Equal("private, no-store", controller.Response.Headers.CacheControl);
        if (action == "list") { Assert.Equal(2, spy.Page); Assert.Equal(10, spy.PageSize); }
        else Assert.Equal(id, spy.ReadId);
        if (action == "name") Assert.Same(rename, spy.RenameRequest);
        Assert.Equal(0, spy.CreateCalls);
    }

    [Fact]
    public async Task 접수오류는_내부원문없이_같은신청결과확인으로안내한다()
    {
        var spy = new Spy { Create = (_, _, _) => throw new IOException("private bucket and credentials") };
        var controller = Controller(spy);
        var problem = Problem(await controller.가입고사진신청생성(Guid.NewGuid(), null, null, default),
            503, "PhotoInboundOutcomeUnknown", 업무처리결과Codes.미확정);
        Assert.DoesNotContain("private", problem.Title!);
        Assert.Equal(업무재시도정책Codes.자동재시도안함, problem.Extensions["retryPolicyCode"]);
        Assert.Equal(new[] { 업무복구행동Ids.상태전체재조회 }, problem.Extensions["availableRecoveryActions"]);
        Assert.Equal(1, spy.CreateCalls);
    }

    [Fact]
    public async Task 결과없음은_조회미확인이며_다시접수하라는판정이아니다()
    {
        var spy = new Spy { Missing = true }; var controller = Controller(spy);
        Problem(await controller.가입고사진신청상세(Guid.NewGuid(), default), 404, "PhotoInboundNotFound", 업무처리결과Codes.조회);
        Assert.Equal(0, spy.CreateCalls);
    }

    [Fact]
    public async Task 업로드전미설정은503이어도_확정미적용으로알려입력잠금을풀수있다()
    {
        var spy = new Spy { Create = (_, _, _) => throw new 가입고사진신청Exception(
            "PhotoInboundStorageUnavailable", "사진 보관을 준비하지 못했습니다.", 503, definitivelyNotApplied: true) };
        Problem(await Controller(spy).가입고사진신청생성(Guid.NewGuid(), null, null, default),
            503, "PhotoInboundStorageUnavailable", 업무처리결과Codes.미적용);
    }

    [Theory]
    [InlineData(400, "NotApplied")]
    [InlineData(409, "Unknown")]
    public async Task 명확한입력거절과기존신청충돌을구분한다(int status, string outcome)
    {
        var spy = new Spy { Create = (_, _, _) => throw new 가입고사진신청Exception("PhotoInboundKnownFailure", "신청을 확인해 주세요.", status) };
        Problem(await Controller(spy).가입고사진신청생성(Guid.NewGuid(), null, null, default), status, "PhotoInboundKnownFailure", outcome);
    }

    private static ProblemDetails Problem(IActionResult result, int status, string code, string outcome)
    {
        var response = Assert.IsType<ObjectResult>(result); Assert.Equal(status, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value); Assert.Equal(code, problem.Extensions["errorCode"]);
        var context = Assert.IsType<업무실패맥락Dto>(problem.Extensions["workflowContext"]);
        Assert.Equal(OperatingSystemIds.WarehouseCommerceFulfillment, context.OperatingSystemId);
        Assert.Equal(OperatingSystemLifecycleStageIds.WarehouseInboundPlan, context.StageId); Assert.Equal(outcome, context.OutcomeCode);
        return problem;
    }
    private static 창고작업Controller Controller(Spy spy) => new(null!, null!, null!, null!, null!, null!, null!, null!, null!, new 가입고사진신청UseCase(spy))
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
    private sealed class Spy : I가입고사진신청Service
    {
        public Func<Guid, string?, IFormFile?, Task<가입고사진신청응답>> Create { get; set; }
            = (id, _, _) => Task.FromResult(new 가입고사진신청응답 { Id = id, Revision = 1 });
        public int CreateCalls; public Guid CreateId; public IFormFile? File; public Guid ReadId;
        public int Page; public int PageSize; public 가입고사진이름변경요청? RenameRequest; public bool Missing;
        public Task<가입고사진신청응답> CreateAsync(Guid requestId, string? name, IFormFile? photo, CancellationToken ct)
        { CreateCalls++; CreateId = requestId; File = photo; return Create(requestId, name, photo); }
        public Task<가입고사진신청목록응답> GetListAsync(int page, int pageSize, CancellationToken ct)
        { Page = page; PageSize = pageSize; return Task.FromResult(new 가입고사진신청목록응답()); }
        public Task<가입고사진신청응답?> GetAsync(Guid id, CancellationToken ct)
        { ReadId = id; return Task.FromResult<가입고사진신청응답?>(Missing ? null : new() { Id = id }); }
        public Task<가입고사진내용응답?> GetPhotoAsync(Guid id, CancellationToken ct)
        { ReadId = id; return Task.FromResult<가입고사진내용응답?>(Missing ? null : new() { ContentType = "image/png", Bytes = [1] }); }
        public Task<가입고사진신청응답> RenameAsync(Guid id, 가입고사진이름변경요청 request, CancellationToken ct)
        { ReadId = id; RenameRequest = request; return Task.FromResult(new 가입고사진신청응답 { Id = id, 이름 = request.이름, Revision = request.Revision + 1 }); }
    }
}
