using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.BackOffice.Components.PrivacySupport;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 보호지원관리UiTests
{
    [Fact]
    public async Task 일반계정은관리사건_API를호출하지않는다()
    {
        var client = new Client(); using var model = new 보호지원관리ViewModel(client, () => null);
        await model.LoadAsync(); await model.LoadAsync("case-one"); await model.VerifyRecipientsAsync(); await model.SubmitAsync();
        Assert.Equal(0, client.Reads); Assert.Empty(client.Commands); Assert.Null(model.Detail);
    }
    [Fact]
    public async Task 권한이없는행동과미지원권리완료는전송하지않는다()
    {
        var client = new Client { DetailFactory = id => Task.FromResult(Row(id, "assign-self")) };
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-one");
        model.Action = "complete-rights"; model.DeliveryEvidenceRef = "executor-proof"; await model.SubmitAsync();
        await model.VerifyRecipientsAsync(); Assert.Empty(client.Commands); Assert.Equal(0, client.EvidenceReads);
    }
    [Fact]
    public async Task 같은사건의통신미확정재시도는최초문안과요청번호를보존한다()
    {
        var client = new Client { FailCommand = true };
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-one");
        model.Action = "prepare-result"; model.Summary = "처음 작성한 안내"; await model.SubmitAsync();
        Assert.True(model.HasPending); model.Summary = "새 문안"; client.FailCommand = false; await model.RetryAsync();
        Assert.Equal(2, client.Commands.Count); Assert.Equal(client.Commands[0].ClientRequestId, client.Commands[1].ClientRequestId);
        Assert.All(client.Commands, command => Assert.Equal("처음 작성한 안내", command.Summary)); Assert.False(model.HasPending);
        Assert.Contains("실제 전달은 별도로", model.Notice);
    }
    [Fact]
    public async Task 사건전환은미확정명령과원문_당사자선택_초안을비운다()
    {
        var client = new Client { FailCommand = true };
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("old-case"); await model.VerifyRecipientsAsync();
        model.Action = "prepare-result"; model.Summary = "이전 원문"; model.PrivateEvidence = "이전 증빙"; model.RecipientIndex = 1;
        await model.SubmitAsync(); Assert.True(model.HasPending);
        await model.LoadAsync("new-case");
        Assert.Equal("new-case", model.Detail!.CaseId); Assert.False(model.HasPending); Assert.Empty(model.PrivateRecords);
        Assert.Empty(model.PrivateRecipientIds); Assert.Equal(-1, model.RecipientIndex); Assert.Empty(model.Summary); Assert.Empty(model.PrivateEvidence);
        await model.RetryAsync(); Assert.Single(client.Commands);
    }
    [Fact]
    public async Task 이전사건의지연조회는새사건과현재원문을덮지않는다()
    {
        var delay = new TaskCompletionSource<보호지원CaseResponse>();
        var client = new Client { DetailFactory = id => id == "old" ? delay.Task : Task.FromResult(Row(id, "add-evidence")) };
        using var model = new 보호지원관리ViewModel(client, () => "admin"); var oldRead = model.LoadAsync("old"); await model.LoadAsync("new");
        model.PrivateEvidence = "현재 입력"; delay.SetResult(Row("old", "close")); await oldRead;
        Assert.Equal("new", model.Detail!.CaseId); Assert.Equal("현재 입력", model.PrivateEvidence); Assert.DoesNotContain("close", model.Actions);
    }
    [Fact]
    public async Task 담당자감사열람후최신판본으로안내증빙을기록한다()
    {
        var client = new Client(); using var model = new 보호지원관리ViewModel(client, () => "admin");
        await model.LoadAsync("case-one"); await model.VerifyRecipientsAsync();
        Assert.Single(model.PrivateRecords); Assert.Equal(2, model.RecipientCount); Assert.Equal(2, model.Detail!.Revision);
        model.Action = "record-result-notified"; model.RecipientIndex = 1; model.LocalOccurredAt = "2026-10-06T09:30";
        model.DeliveryEvidenceRef = "verified-send-reference"; model.ActualDeliveryConfirmed = true; await model.SubmitAsync();
        var command = Assert.Single(client.Commands); Assert.Equal(2, command.ExpectedRevision); Assert.Equal("party-two", command.RecipientUserId);
        Assert.Equal(DateTimeKind.Utc, command.OccurredAtUtc!.Value.Kind); Assert.Equal(0, command.OccurredAtUtc.Value.Hour);
        Assert.Empty(model.PrivateRecords); Assert.Empty(model.PrivateRecipientIds);
    }
    [Fact]
    public async Task 조회후전달한안내시각은새조회없이서버판정으로전송한다()
    {
        var client = new Client(); using var model = new 보호지원관리ViewModel(client, () => "admin");
        await model.LoadAsync("case-one"); await model.VerifyRecipientsAsync();
        model.Action = "record-result-notified"; model.RecipientIndex = 0; model.LocalOccurredAt = "2026-10-06T10:30";
        model.DeliveryEvidenceRef = "later-send-reference"; model.ActualDeliveryConfirmed = true;
        Assert.True(model.CanSubmit);
        var reads = client.Reads; var snapshotNow = model.Detail!.ServerNowUtc; await model.SubmitAsync();
        var command = Assert.Single(client.Commands);
        Assert.True(command.OccurredAtUtc > snapshotNow); Assert.Equal(reads, client.Reads);
        Assert.Equal(new DateTime(2026, 10, 6, 1, 30, 0, DateTimeKind.Utc), command.OccurredAtUtc);
    }
    [Fact]
    public async Task 미래시각을서버가거부하면전달완료로표시하지않는다()
    {
        var client = new Client { CommandError = new SsalddelApiException("private rejection", 400, "support", "", null, errorCode: "InvalidNotificationTime") };
        using var model = new 보호지원관리ViewModel(client, () => "admin");
        await model.LoadAsync("case-one"); await model.VerifyRecipientsAsync();
        model.Action = "record-result-notified"; model.RecipientIndex = 0; model.LocalOccurredAt = "2030-10-06T10:30";
        model.DeliveryEvidenceRef = "unconfirmed-future-reference"; model.ActualDeliveryConfirmed = true;
        await model.SubmitAsync();
        Assert.Single(client.Commands); Assert.Null(model.Notice); Assert.True(model.RequiresRefresh);
        Assert.False(model.HasPending); Assert.Empty(model.PrivateRecipientIds); Assert.DoesNotContain("private rejection", model.Error);
    }
    [Theory]
    [InlineData(404)]
    [InlineData(403)]
    [InlineData(0)]
    public async Task 최초상세조회실패화면에서도목록으로복귀할수있다(int status)
    {
        var client = new Client { DetailFactory = _ => Task.FromException<보호지원CaseResponse>(status == 0
            ? new InvalidOperationException("private failure")
            : new SsalddelApiException("private failure", status, "support", "", null)) };
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<I보호지원관리Client>(client);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var page = await renderer.RenderComponentAsync<보호지원관리Page>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(보호지원관리Page.CaseId)] = "missing-case",
                [nameof(보호지원관리Page.AdminOwnerProvider)] = (Func<string?>)(() => "admin")
            }));
            return WebUtility.HtmlDecode(page.ToHtmlString());
        });
        Assert.Contains("요청 목록으로", html); Assert.Contains("href=\"/privacy-support\"", html);
        Assert.DoesNotContain("private failure", html); Assert.DoesNotContain("<form", html);
        Assert.Equal(1, client.Reads); Assert.Equal(0, client.EvidenceReads); Assert.Empty(client.Commands);
    }
    [Fact]
    public async Task 안내문과대상선택만으로실제전달을기록하지않는다()
    {
        var client = new Client(); using var model = new 보호지원관리ViewModel(client, () => "admin");
        await model.LoadAsync("case-one"); await model.VerifyRecipientsAsync(); model.Action = "record-result-notified";
        model.RecipientIndex = 0; model.LocalOccurredAt = "2026-10-06T09:30"; model.Summary = "작성한 문안";
        await model.SubmitAsync(); Assert.Empty(client.Commands);
        model.DeliveryEvidenceRef = "send-reference"; await model.SubmitAsync(); Assert.Empty(client.Commands);
        model.ActualDeliveryConfirmed = true; await model.SubmitAsync(); Assert.Single(client.Commands);
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task 인증권한실패는기존원문과입력_미확정명령을삭제한다(int status)
    {
        var client = new Client(); using var model = new 보호지원관리ViewModel(client, () => "admin");
        await model.LoadAsync("case-one"); await model.VerifyRecipientsAsync();
        model.Action = "add-evidence"; model.PrivateEvidence = "비공개 초안";
        client.CommandError = new SsalddelApiException("PII error text", status, "support", "sensitive-response", null);
        await model.SubmitAsync(); Assert.Null(model.Detail); Assert.Empty(model.Items); Assert.Empty(model.PrivateRecords);
        Assert.Empty(model.PrivateRecipientIds); Assert.Empty(model.PrivateEvidence); Assert.False(model.HasPending); Assert.DoesNotContain("PII", model.Error);
    }
    [Fact]
    public async Task 계정전환중늦게오는담당자원문은표시하지않는다()
    {
        string? owner = "first"; var delay = new TaskCompletionSource<보호지원증거Response>();
        var client = new Client { EvidenceFactory = _ => delay.Task }; using var model = new 보호지원관리ViewModel(client, () => owner);
        await model.LoadAsync("case-one"); var read = model.VerifyRecipientsAsync(); owner = "second"; model.SynchronizeOwner();
        delay.SetResult(new() { CaseId = "case-one", Items = [new() { Text = "first-private" }], AuthorizedRecipientIds = ["first"] }); await read;
        Assert.Null(model.Detail); Assert.Empty(model.PrivateRecords); Assert.Empty(model.PrivateRecipientIds); Assert.False(model.IsBusy);
    }
    [Fact]
    public async Task 보존정지결과미확정은409이어도동일명령재확인번호를유지한다()
    {
        var client = new Client { CommandError = new SsalddelApiException("pending", 409, "support", "", null, errorCode: "RetentionPending") };
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-one");
        model.Action = "hold-record"; model.LegalBasis = "분쟁 조사"; model.LocalReviewAt = "2026-10-10T10:00";
        await model.SubmitAsync(); Assert.True(model.HasPending); client.CommandError = null; await model.RetryAsync();
        Assert.Equal(client.Commands[0].ClientRequestId, client.Commands[1].ClientRequestId);
    }
    [Fact]
    public async Task 서버보존확인중이면판본과행동을변경하지않는다()
    {
        var client = new Client { DetailFactory = id => Task.FromResult(new 보호지원CaseResponse { CaseId = id, RetentionPending = true, AllowedActions = ["close"] }) };
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-one"); model.Action = "close";
        await model.SubmitAsync(); Assert.Empty(model.Actions); Assert.Empty(client.Commands);
    }
    private static 보호지원CaseResponse Row(string id, params string[] actions) => new()
    {
        CaseId = id, Kind = 보호지원종류Codes.분쟁, Revision = 1, CreatedAtUtc = new(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc),
        ServerNowUtc = new(2026, 10, 6, 1, 0, 0, DateTimeKind.Utc), AllowedActions = actions
    };
    private sealed class Client : I보호지원관리Client
    {
        public int Reads; public int EvidenceReads; public bool FailCommand; public Exception? CommandError;
        public List<보호지원CommandRequest> Commands { get; } = [];
        public Func<string, Task<보호지원CaseResponse>>? DetailFactory;
        public Func<string, Task<보호지원증거Response>>? EvidenceFactory;
        public Task<보호지원ListResponse> 목록Async(string kind, int page, CancellationToken ct)
        { Reads++; return Task.FromResult(new 보호지원ListResponse()); }
        public async Task<보호지원CaseResponse> 상세Async(string id, CancellationToken ct)
        {
            Reads++; if (DetailFactory is not null) return await DetailFactory(id);
            var row = Row(id, "prepare-result", "record-result-notified", "hold-record", "add-evidence"); row.Revision = EvidenceReads > 0 ? 2 : 1; return row;
        }
        public Task<보호지원증거Response> 비공개확인Async(string id, CancellationToken ct)
        { EvidenceReads++; return EvidenceFactory?.Invoke(id) ?? Task.FromResult(new 보호지원증거Response { CaseId = id, Items = [new() { Text = "가짜 시험 사건 내용" }], AuthorizedRecipientIds = ["party-one", "party-two"] }); }
        public Task<보호지원CaseResponse> 변경Async(string id, 보호지원CommandRequest request, CancellationToken ct)
        {
            Commands.Add(request); if (CommandError is not null) throw CommandError; if (FailCommand) throw new HttpRequestException();
            return Task.FromResult(Row(id, "add-evidence"));
        }
    }
}
