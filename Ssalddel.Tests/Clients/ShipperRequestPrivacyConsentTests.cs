using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Tests.UiCommon;
using Ssalddel.Ui.Common.Areas.App.Services;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;
using SsalddelApp.ViewModels.Shipper;

namespace Ssalddel.Tests.Clients;

/// <summary>명시적 사용자 입력과 모의 HTTP/서비스 응답의 계약 시험이며 실제 동의·운영 증거가 아닙니다.</summary>
public sealed class ShipperRequestPrivacyConsentTests
{
    [Fact]
    public async Task 체크전에는_동의나운송접수를보내지않는다()
    {
        var (operations, stub) = Operations();
        using var model = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations, new Navigation());
        await model.InitializeAsync();
        Assert.False(model.CollectionConsent); Assert.False(model.AgeConfirmed);
        await model.RecordPrivacyConsentAsync(); await model.SubmitAsync();
        model.CollectionConsent = true;
        await model.RecordPrivacyConsentAsync(); await model.SubmitAsync();
        Assert.Empty(stub.Consents); Assert.Empty(stub.Posts);
        Assert.False(model.HasPendingRequest); Assert.False(model.HasValidPrivacyConsent);
    }

    [Fact]
    public async Task 본인명시동의의유효한서버증적만_생성원문에연결한다()
    {
        var (operations, stub) = Operations();
        using var model = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations, new Navigation());
        await model.InitializeAsync();
        await ShipperConsentFixture.ConfirmAsync(model);
        var evidence = Assert.Single(stub.Consents);
        Assert.NotEqual(Guid.Empty, evidence.증적Id);
        Assert.Equal(신청개인정보업무Codes.운송대행, evidence.업무Code);
        Assert.Equal(신청개인정보출처Codes.커뮤니티지도, evidence.출처Code);
        Assert.Equal(신청개인정보동의정책.현재버전, evidence.동의문버전);
        Assert.True(evidence.수집이용동의); Assert.True(evidence.연령요건확인);
        await model.SubmitAsync();
        Assert.Equal(evidence.증적Id, Assert.Single(stub.Posts).신청개인정보동의증적Id);
        Assert.Equal(evidence.출처Code, stub.Posts[0].신청출처Code);
        Assert.False(model.CollectionConsent); Assert.False(model.AgeConfirmed);
        Assert.False(model.HasValidPrivacyConsent);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("id")]
    [InlineData("work")]
    [InlineData("source")]
    [InlineData("version")]
    [InlineData("withdrawn")]
    public async Task 다른업무나판본또는미확인증적은_접수에사용하지않는다(string fault)
    {
        var (operations, stub) = Operations();
        stub.Consent = request =>
        {
            if (fault == "null") return Task.FromResult<신청개인정보동의증적Response?>(null);
            var response = ShipperConsentFixture.Response(request);
            switch (fault)
            {
                case "id": response.증적Id = Guid.NewGuid(); break;
                case "work": response.업무Code = 신청개인정보업무Codes.물류대행; break;
                case "source": response.출처Code = 신청개인정보출처Codes.생활교류; break;
                case "version": response.동의문버전 = "old-notice"; break;
                case "withdrawn": response.상태Code = 신청개인정보동의상태Codes.철회; break;
            }
            return Task.FromResult<신청개인정보동의증적Response?>(response);
        };
        using var model = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations, new Navigation());
        await model.InitializeAsync();
        model.CollectionConsent = model.AgeConfirmed = true;
        await model.RecordPrivacyConsentAsync(); await model.SubmitAsync();
        Assert.False(model.HasValidPrivacyConsent); Assert.Empty(stub.Posts); Assert.False(model.HasPendingRequest);
    }

    [Fact]
    public async Task 동의기록응답유실은_같은증적번호로명시재확인하며_접수를먼저보내지않는다()
    {
        var (operations, stub) = Operations();
        stub.Consent = _ => throw new IOException("consent response lost");
        using var model = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations, new Navigation());
        await model.InitializeAsync(); model.CollectionConsent = model.AgeConfirmed = true;
        await model.RecordPrivacyConsentAsync(); await model.SubmitAsync();
        Assert.False(model.HasValidPrivacyConsent); Assert.Empty(stub.Posts);
        var id = Assert.Single(stub.Consents).증적Id;
        stub.Consent = request => Task.FromResult<신청개인정보동의증적Response?>(ShipperConsentFixture.Response(request));
        await model.RecordPrivacyConsentAsync(); await model.SubmitAsync();
        Assert.Equal(id, stub.Consents[1].증적Id);
        Assert.Equal(id, Assert.Single(stub.Posts).신청개인정보동의증적Id);
    }

    [Fact]
    public async Task 체크해제후늦은동의응답은_등록가능상태를복구하지않는다()
    {
        var (operations, stub) = Operations();
        var pending = new TaskCompletionSource<신청개인정보동의증적Response?>();
        stub.Consent = _ => pending.Task;
        using var model = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations, new Navigation());
        await model.InitializeAsync(); model.CollectionConsent = model.AgeConfirmed = true;
        var recording = model.RecordPrivacyConsentAsync();
        model.CollectionConsent = false;
        pending.SetResult(ShipperConsentFixture.Response(stub.Consents[0])); await recording;
        Assert.False(model.HasValidPrivacyConsent); Assert.False(model.IsBusy);
        await model.SubmitAsync(); Assert.Empty(stub.Posts);
    }

    [Fact]
    public async Task 계정변경후늦은동의응답은_새계정에적용하지않는다()
    {
        var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var (operations, stub) = Operations();
        var pending = new TaskCompletionSource<신청개인정보동의증적Response?>();
        stub.Consent = _ => pending.Task;
        using var model = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations, new Navigation(), auth, new VolatileWorkflowRecoveryStore());
        await model.InitializeAsync(); model.CollectionConsent = model.AgeConfirmed = true;
        var recording = model.RecordPrivacyConsentAsync();
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot("shipper-b"));
        pending.SetResult(ShipperConsentFixture.Response(stub.Consents[0])); await recording;
        Assert.False(model.HasValidPrivacyConsent); Assert.False(model.CollectionConsent); Assert.False(model.AgeConfirmed);
        Assert.Empty(stub.Posts);
    }

    [Fact]
    public async Task 구판접수의동의누락은_새증적을꾸며붙이지않고_결과조회만허용한다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var id = Guid.NewGuid();
        var state = ShipperRequestContactWindowTests.CompleteState();
        var body = JsonSerializer.Serialize(new
        {
            Request = new ShipperRequestItem { 의뢰Id = $"shipper-app-{id:N}", 화물종류 = state.화물종류 },
            Draft = state.ToDraft()
        });
        await store.SaveAsync(new("local-session", ShipperRequestAuthoringPageViewModel.RecoveryScope, id, body, DateTime.UtcNow), default);
        var (operations, stub) = Operations();
        using var model = new ShipperRequestAuthoringPageViewModel(new(), operations, new Navigation(), recovery: store);
        await model.InitializeAsync(); await model.RetryPendingAsync(); await model.RecordPrivacyConsentAsync();
        Assert.True(model.HasPendingRequest); Assert.False(model.CanRetryPendingRequest);
        Assert.Empty(stub.Posts); Assert.Empty(stub.Consents); Assert.NotEmpty(stub.Gets);
        Assert.Contains("동의 증적", model.StatusMessage);
    }

    private static (IShipperOperationsService, OperationsStub) Operations()
    {
        var operations = DispatchProxy.Create<IShipperOperationsService, OperationsStub>();
        return (operations, (OperationsStub)(object)operations);
    }
    public class OperationsStub : DispatchProxy
    {
        public List<신청개인정보동의기록Request> Consents { get; } = [];
        public List<ShipperRequestItem> Posts { get; } = [];
        public List<string> Gets { get; } = [];
        public Func<신청개인정보동의기록Request, Task<신청개인정보동의증적Response?>> Consent = request
            => Task.FromResult<신청개인정보동의증적Response?>(ShipperConsentFixture.Response(request));
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(IShipperOperationsService.GetVehicleTypesAsync))
                return Task.FromResult<IReadOnlyList<string>>(["1톤 카고"]);
            if (method?.Name == nameof(IShipperOperationsService.RecordPrivacyConsentAsync))
            { var request = (신청개인정보동의기록Request)args![0]!; Consents.Add(request); return Consent(request); }
            if (method?.Name == nameof(IShipperOperationsService.AddRequestAsync))
            { Posts.Add((ShipperRequestItem)args![0]!); return Task.FromResult(new ShipperRequestItem { 의뢰Id = "ledger-one" }); }
            if (method?.Name == nameof(IShipperOperationsService.GetRequestByClientRequestIdAsync))
            { Gets.Add((string)args![0]!); return Task.FromResult<ShipperRequestItem?>(null); }
            throw new NotSupportedException();
        }
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("https://test.invalid/", "https://test.invalid/shipper/request/review");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
        protected override void NavigateToCore(string uri, NavigationOptions options) { }
    }
}

internal static class ShipperConsentFixture
{
    public static async Task ConfirmAsync(ShipperRequestAuthoringPageViewModel model)
    {
        model.CollectionConsent = true; model.AgeConfirmed = true;
        await model.RecordPrivacyConsentAsync();
        Assert.True(model.HasValidPrivacyConsent, model.StatusMessage);
    }
    public static 신청개인정보동의증적Response Response(신청개인정보동의기록Request request) => new()
    {
        증적Id = request.증적Id, 업무Code = request.업무Code, 출처Code = request.출처Code,
        동의문버전 = request.동의문버전, 상태Code = 신청개인정보동의상태Codes.유효
    };
}
