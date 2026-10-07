using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodRoleActionViewModelTests
{
    [Fact]
    public async Task CancelNeedsConfirmationAndRetriesImmutablePayloadWithSameRequestId()
    {
        var api = CancelApi(); var access = new Access();
        using var model = new FoodRoleActionViewModel(api, access);
        await model.InitializeAsync("orderer", "order-a", 음식배달가능행동Ids.주문취소);
        model.ReasonCode = 운영배차주문자취소사유Code.기타; model.Reason = "주문이 중복됐습니다."; model.Prepare();
        Assert.NotNull(model.RequestId);
        Assert.False(await model.SubmitAsync()); Assert.Empty(api.Writes);
        var attempts = 0;
        api.Post = (_, body) =>
        {
            if (++attempts == 1) throw new HttpRequestException("network");
            api.Reads[CancelPath] = CancelledDetail();
            return CancelledResponse(((주문자음식주문취소요청)body).클라이언트요청Id);
        };
        model.Confirmed = true;
        Assert.False(await model.SubmitAsync());
        Assert.False(model.IsAwaitingResult);
        Assert.False(await model.CheckResultAsync());
        var requestId = model.RequestId;
        model.Reason = "바뀐 입력"; model.Confirmed = false; model.Edit();
        Assert.Equal(requestId, model.RequestId);
        model.Confirmed = true;
        Assert.True(await model.SubmitAsync());
        Assert.Equal(2, api.Writes.Count);
        Assert.Same(api.Writes[0].Body, api.Writes[1].Body);
        var body = Assert.IsType<주문자음식주문취소요청>(api.Writes[1].Body);
        Assert.Equal(requestId, body.클라이언트요청Id);
        Assert.Equal("주문이 중복됐습니다.", body.사유);
        Assert.True(model.IsCompleted);
    }

    [Fact]
    public async Task AccountChangeClearsInputAndPendingRequest()
    {
        var api = CancelApi(); var access = new Access();
        using var model = new FoodRoleActionViewModel(api, access);
        await model.InitializeAsync("orderer", "order-a", 음식배달가능행동Ids.주문취소);
        model.ReasonCode = 운영배차주문자취소사유Code.기타; model.Reason = "개인 사유"; model.Prepare();
        access.Change(new("other-user", 2, true));
        Assert.Empty(model.Reason); Assert.Empty(model.ReasonCode); Assert.Null(model.RequestId);
        Assert.False(model.IsLoaded);
        model.Confirmed = true;
        Assert.False(await model.SubmitAsync()); Assert.Empty(api.Writes);
    }

    [Fact]
    public async Task LateCommandResponseCannotQueryOrApplyToAnotherAccount()
    {
        var api = CancelApi(); var access = new Access();
        var wait = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.AsyncPost = (_, _) => wait.Task;
        using var model = new FoodRoleActionViewModel(api, access);
        await model.InitializeAsync("orderer", "order-a", 음식배달가능행동Ids.주문취소);
        model.ReasonCode = 운영배차주문자취소사유Code.중복주문; model.Prepare(); model.Confirmed = true;
        var result = model.SubmitAsync();
        access.Change(new("another-user", 2, true)); wait.SetResult(new 음식주문응답());
        Assert.False(await result);
        Assert.Single(api.ReadRoles); Assert.False(model.IsCompleted); Assert.Null(model.RequestId);
    }

    [Fact]
    public async Task RestaurantChangedAddressDoesNotReuseOldCoordinate()
    {
        var api = new Api(); var access = new Access();
        api.Reads[RestaurantPath] = new 음식주문응답 { 주문번호 = "order-a", Revision = 12, 음식점명 = "음식점", 음식점주소 = "기존 주소",
            음식점위도 = 37.5m, 음식점경도 = 127m, AvailableActions = [new() { ActionId = 음식배달가능행동Ids.음식점주문수락 }] };
        api.Post = (_, body) =>
        {
            var request = (음식점주문수락요청)body;
            var response = AcceptedResponse(request);
            api.Reads[RestaurantPath] = response;
            return response;
        };
        using var model = new FoodRoleActionViewModel(api, access);
        await model.InitializeAsync("restaurant", "order-a", 음식배달가능행동Ids.음식점주문수락);
        model.RestaurantAddress = "확인한 새 주소"; model.PreparationMinutes = 15; model.Prepare(); model.Confirmed = true;
        Assert.True(await model.SubmitAsync());
        var body = Assert.IsType<음식점주문수락요청>(Assert.Single(api.Writes).Body);
        Assert.Null(body.음식점위도); Assert.Null(body.음식점경도); Assert.Equal(15, body.조리예상분);
    }

    [Fact]
    public async Task DriverInterruptionRefreshesWorkspaceWhenTheCompletedAttemptDisappears()
    {
        var api = new Api(); var access = new Access();
        var workspace = new FoodDeliveryDriverWorkspaceDto { DriverId = "user-a", ActiveDeliveries = [new() { OfferId = "offer-a", DeliveryAttemptId = "attempt-a", AttemptRevision = 9,
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사배달중단, ExpectedRevision = 9 }] }] };
        api.Reads["api/v1/driver/food-deliveries/workspace"] = workspace;
        api.Post = (_, _) => { workspace.ActiveDeliveries = []; return InterruptedResponse(); };
        using var model = new FoodRoleActionViewModel(api, access);
        await model.InitializeAsync("food-driver", "offer-a", 음식배달가능행동Ids.기사배달중단);
        model.ReasonCode = 음식배달중단사유Code.사고; model.Prepare(); model.Confirmed = true;
        Assert.True(await model.SubmitAsync());
        Assert.Equal(9, Assert.IsType<음식배달중단요청>(Assert.Single(api.Writes).Body).예상시도Revision);
        Assert.Equal(2, api.ReadRoles.Count);
    }

    [Fact]
    public async Task OperatorReviewRequiresSelectedInterruptedAttemptDecisionAndReason()
    {
        var api = new Api(); var access = new Access();
        api.Reads["api/v1/admin/food-orders/order-a/operations-trace"] = new 음식주문운영추적응답 { 주문번호 = "order-a", 배달시도목록 =
            [new() { 시도StableId = "attempt-a", Revision = 7, 상태Code = 음식배달시도상태Code.중단 }] };
        api.Post = (_, body) =>
        {
            var review = (음식배달중단검토요청)body;
            var response = new 음식배달시도운영응답 { 시도StableId = "attempt-a", Revision = 8, 상태Code = 음식배달시도상태Code.중단,
                책임Code = 운영배차책임Code.보호대상, 악용확정여부 = review.악용확정여부, 검토사유 = review.판정사유 };
            api.Reads[OperatorPath] = new 음식주문운영추적응답 { 주문번호 = "order-a", 배달시도목록 = [response] };
            return response;
        };
        using var model = new FoodRoleActionViewModel(api, access);
        await model.InitializeAsync("operator", "order-a", OperatorRoleWorkspaceAdapter.ReviewInterruption, "attempt-a");
        model.Prepare(); Assert.Null(model.RequestId); Assert.Empty(api.Writes);
        model.DecisionCode = 음식배달중단검토판정Code.보호; model.Reason = "사고 자료 확인"; model.Prepare(); model.Confirmed = true;
        Assert.True(await model.SubmitAsync());
        var write = Assert.Single(api.Writes);
        Assert.Equal("operator", write.Role);
        Assert.Equal("api/v1/admin/food-orders/delivery-attempts/attempt-a/interruption-review", write.Path);
        var body = Assert.IsType<음식배달중단검토요청>(write.Body);
        Assert.Equal(7, body.예상Revision); Assert.Equal(음식배달중단검토판정Code.보호, body.판정Code);
    }

    [Fact]
    public async Task UnchangedCancellationReadWaitsAndConfirmationOnlyReadsWithoutResending()
    {
        var api = CancelApi(); var access = new Access();
        using var model = new FoodRoleActionViewModel(api, access);
        await PrepareCancellationAsync(model);
        var requestId = model.RequestId;
        api.Post = (_, body) => CancelledResponse(((주문자음식주문취소요청)body).클라이언트요청Id);
        Assert.False(await model.SubmitAsync());
        Assert.True(model.IsAwaitingResult); Assert.False(model.IsCompleted); Assert.Equal(requestId, model.RequestId);
        model.Reason = "변경한 입력"; model.Prepare(); model.Edit();
        Assert.Equal(requestId, model.RequestId);
        api.Reads[CancelPath] = CancelledDetail();
        Assert.True(await model.SubmitAsync());
        Assert.Single(api.Writes); Assert.Equal(3, api.ReadRoles.Count); Assert.True(model.IsCompleted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task AcknowledgedCommandSurvivesFollowupReadFailureAndRecoversWithGetOnly(int status)
    {
        var api = CancelApi(); var access = new Access();
        using var model = new FoodRoleActionViewModel(api, access);
        await PrepareCancellationAsync(model);
        api.Post = (_, body) => CancelledResponse(((주문자음식주문취소요청)body).클라이언트요청Id);
        var reads = 0;
        api.AsyncGet = _ => ++reads == 1 ? Task.FromException<object>(status == 0
            ? new HttpRequestException("query unavailable") : new RoleWorkspaceAccessException(status, "query unavailable"))
            : Task.FromResult<object>(CancelledDetail());
        var requestId = model.RequestId;
        Assert.False(await model.SubmitAsync());
        Assert.True(model.IsAwaitingResult); Assert.False(model.IsCompleted); Assert.Equal(requestId, model.RequestId);
        Assert.True(await model.CheckResultAsync());
        Assert.Single(api.Writes); Assert.Equal(3, api.ReadRoles.Count); Assert.Null(model.RequestId);
    }

    [Theory]
    [InlineData("different-order")]
    [InlineData("different-request")]
    [InlineData("old-revision")]
    [InlineData("wrong-state")]
    [InlineData("empty-response")]
    public async Task CancellationRequiresItsOwnTypedCommandEvidenceEvenWhenGetShowsCancelled(string mismatch)
    {
        var api = CancelApi(); var access = new Access();
        using var model = new FoodRoleActionViewModel(api, access);
        await PrepareCancellationAsync(model);
        api.Post = (_, body) =>
        {
            var response = CancelledResponse(((주문자음식주문취소요청)body).클라이언트요청Id);
            if (mismatch == "different-order") response.주문번호 = "other-order";
            if (mismatch == "different-request") response.상태이력[0].클라이언트요청Id = Guid.NewGuid();
            if (mismatch == "old-revision") response.Revision = 3;
            if (mismatch == "wrong-state") response.상태 = 음식주문상태코드.주문대기;
            api.Reads[CancelPath] = CancelledDetail();
            return mismatch == "empty-response" ? null : response;
        };
        Assert.False(await model.SubmitAsync()); Assert.True(model.IsAwaitingResult);
        Assert.False(await model.CheckResultAsync()); Assert.False(model.IsCompleted); Assert.Single(api.Writes);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    public async Task RestaurantCompletionAcceptsRecordedLaterPickupOrPreparationChange(bool accepting, bool responseAlreadyAdvanced, bool pickupReady)
    {
        var api = RestaurantApi(accepting ? 음식배달가능행동Ids.음식점주문수락 : 음식배달가능행동Ids.음식점조리시간변경);
        using var model = new FoodRoleActionViewModel(api, new Access());
        await model.InitializeAsync("restaurant", "order-a", accepting ? 음식배달가능행동Ids.음식점주문수락 : 음식배달가능행동Ids.음식점조리시간변경);
        model.PreparationMinutes = 30; model.Prepare(); model.Confirmed = true;
        api.Post = (_, body) =>
        {
            var response = body is 음식점주문수락요청 acceptance ? AcceptedResponse(acceptance) : PreparationResponse((음식점주문진행변경요청)body);
            var advanced = AdvancePreparation(response, pickupReady);
            api.Reads[RestaurantPath] = advanced;
            return responseAlreadyAdvanced ? advanced : response;
        };
        Assert.True(await model.SubmitAsync()); Assert.True(model.IsCompleted); Assert.Single(api.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestaurantAcceptanceConfirmsLaterCookingStartWithDifferentMinutesAndMatchingCurrentTimes(bool responseAlreadyAdvanced)
    {
        var api = RestaurantApi(음식배달가능행동Ids.음식점주문수락);
        using var model = new FoodRoleActionViewModel(api, new Access());
        await model.InitializeAsync("restaurant", "order-a", 음식배달가능행동Ids.음식점주문수락);
        model.PreparationMinutes = 30; model.Prepare(); model.Confirmed = true;
        음식주문응답? advanced = null;
        api.Post = (_, body) =>
        {
            var response = AcceptedResponse((음식점주문수락요청)body);
            advanced = AdvanceCooking(response, false);
            var missingTime = AdvanceCooking(response, false); missingTime.CurrentCookingStartedAtUtc = null;
            api.Reads[RestaurantPath] = missingTime;
            return responseAlreadyAdvanced ? advanced : response;
        };
        Assert.False(await model.SubmitAsync()); Assert.True(model.IsAwaitingResult);
        api.Reads[RestaurantPath] = advanced!;
        Assert.True(await model.CheckResultAsync()); Assert.True(model.IsCompleted); Assert.Single(api.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestaurantAcceptanceConfirmsLaterRecookingWithItsCurrentRoundAndAppliedMinutes(bool responseAlreadyAdvanced)
    {
        var api = RestaurantApi(음식배달가능행동Ids.음식점주문수락);
        using var model = new FoodRoleActionViewModel(api, new Access());
        await model.InitializeAsync("restaurant", "order-a", 음식배달가능행동Ids.음식점주문수락);
        model.PreparationMinutes = 30; model.Prepare(); model.Confirmed = true;
        음식주문응답? advanced = null;
        api.Post = (_, body) =>
        {
            var response = AcceptedResponse((음식점주문수락요청)body);
            advanced = AdvanceCooking(response, true);
            var wrongRound = AdvanceCooking(response, true); wrongRound.CurrentPreparationRound = 1;
            api.Reads[RestaurantPath] = wrongRound;
            return responseAlreadyAdvanced ? advanced : response;
        };
        Assert.False(await model.SubmitAsync()); Assert.True(model.IsAwaitingResult);
        api.Reads[RestaurantPath] = advanced!;
        Assert.True(await model.CheckResultAsync()); Assert.True(model.IsCompleted); Assert.Single(api.Writes);
    }

    [Fact]
    public async Task PreparationChangeRequiresRequestedMinutesAndItsOwnActionHistory()
    {
        var api = RestaurantApi(음식배달가능행동Ids.음식점조리시간변경);
        using var model = new FoodRoleActionViewModel(api, new Access());
        await model.InitializeAsync("restaurant", "order-a", 음식배달가능행동Ids.음식점조리시간변경);
        model.PreparationMinutes = 240; model.Prepare(); model.Confirmed = true;
        음식주문응답? correct = null;
        api.Post = (_, body) =>
        {
            correct = PreparationResponse((음식점주문진행변경요청)body);
            api.Reads[RestaurantPath] = new 음식주문응답 { 주문번호 = "order-a", Revision = 13, 상태 = 음식주문상태코드.조리중,
                조리예상분 = 15, 상태이력 = correct.상태이력 };
            return correct;
        };
        Assert.False(await model.SubmitAsync()); Assert.True(model.IsAwaitingResult);
        Assert.Equal(180, correct!.조리예상분);
        api.Reads[RestaurantPath] = correct;
        Assert.True(await model.CheckResultAsync()); Assert.Single(api.Writes);

        var wrongApi = RestaurantApi(음식배달가능행동Ids.음식점조리시간변경);
        using var wrongModel = new FoodRoleActionViewModel(wrongApi, new Access());
        await wrongModel.InitializeAsync("restaurant", "order-a", 음식배달가능행동Ids.음식점조리시간변경);
        wrongModel.PreparationMinutes = 30; wrongModel.Prepare(); wrongModel.Confirmed = true;
        wrongApi.Post = (_, body) =>
        {
            var response = PreparationResponse((음식점주문진행변경요청)body);
            response.상태이력[0].사유 = "음식점 조리 시작";
            wrongApi.Reads[RestaurantPath] = response;
            return response;
        };
        Assert.False(await wrongModel.SubmitAsync()); Assert.False(wrongModel.IsCompleted);
    }

    [Fact]
    public async Task RestaurantRejectionRequiresRecordedRejectionBeforeCompleting()
    {
        var api = RestaurantApi(음식배달가능행동Ids.음식점주문거절);
        using var model = new FoodRoleActionViewModel(api, new Access());
        await model.InitializeAsync("restaurant", "order-a", 음식배달가능행동Ids.음식점주문거절);
        model.Reason = "재료 소진"; model.Prepare(); model.Confirmed = true;
        음식주문응답? response = null;
        api.Post = (_, body) =>
        {
            var request = (음식점주문진행변경요청)body;
            return response = new 음식주문응답 { 주문번호 = "order-a", Revision = 13, 상태 = 음식주문상태코드.거절,
                상태이력 = [new() { 클라이언트요청Id = request.클라이언트요청Id, 다음상태 = 음식주문상태코드.거절, 사유 = "음식점 주문 거절 · 재료 소진" }] };
        };
        Assert.False(await model.SubmitAsync()); Assert.True(model.IsAwaitingResult);
        api.Reads[RestaurantPath] = response!;
        Assert.True(await model.CheckResultAsync()); Assert.Single(api.Writes);
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("attempt")]
    [InlineData("responsibility")]
    [InlineData("abuse")]
    [InlineData("reason")]
    public async Task OperatorChecksExactAttemptRevisionAndAllReviewValues(string mismatch)
    {
        var api = new Api();
        api.Reads[OperatorPath] = new 음식주문운영추적응답 { 주문번호 = "order-a", 배달시도목록 =
            [new() { 시도StableId = "attempt-a", Revision = 7, 상태Code = 음식배달시도상태Code.중단 }] };
        using var model = new FoodRoleActionViewModel(api, new Access());
        await model.InitializeAsync("operator", "order-a", OperatorRoleWorkspaceAdapter.ReviewInterruption, "attempt-a");
        model.DecisionCode = 음식배달중단검토판정Code.기사책임; model.AbuseConfirmed = true; model.Reason = "확인한 자료";
        model.Prepare(); model.Confirmed = true;
        var response = new 음식배달시도운영응답 { 시도StableId = "attempt-a", Revision = 8, 상태Code = 음식배달시도상태Code.중단,
            책임Code = 운영배차책임Code.기사, 악용확정여부 = true, 검토사유 = "확인한 자료" };
        api.Post = (_, _) =>
        {
            var incorrect = new 음식배달시도운영응답 { 시도StableId = mismatch == "attempt" ? "other-attempt" : "attempt-a",
                Revision = mismatch == "revision" ? 7 : 8, 상태Code = 음식배달시도상태Code.중단,
                책임Code = mismatch == "responsibility" ? 운영배차책임Code.보호대상 : 운영배차책임Code.기사,
                악용확정여부 = mismatch != "abuse", 검토사유 = mismatch == "reason" ? "다른 자료" : "확인한 자료" };
            api.Reads[OperatorPath] = new 음식주문운영추적응답 { 주문번호 = "order-a", 배달시도목록 = [incorrect] };
            return response;
        };
        Assert.False(await model.SubmitAsync()); Assert.True(model.IsAwaitingResult);
        api.Reads[OperatorPath] = new 음식주문운영추적응답 { 주문번호 = "order-a", 배달시도목록 = [response] };
        Assert.True(await model.CheckResultAsync()); Assert.Single(api.Writes);
    }

    [Fact]
    public async Task DriverChecksOwnWorkspaceAndOriginalAttemptWhileAllowingNewAttemptForSameOffer()
    {
        var api = new Api();
        var workspace = new FoodDeliveryDriverWorkspaceDto { DriverId = "user-a", ActiveDeliveries = [new()
            { OfferId = "offer-a", DeliveryAttemptId = "attempt-a", AttemptRevision = 9,
                AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사배달중단, ExpectedRevision = 9 }] }] };
        api.Reads[DriverPath] = workspace; api.Post = (_, _) => InterruptedResponse();
        using var model = new FoodRoleActionViewModel(api, new Access());
        await model.InitializeAsync("food-driver", "offer-a", 음식배달가능행동Ids.기사배달중단);
        model.ReasonCode = 음식배달중단사유Code.사고; model.Prepare(); model.Confirmed = true;
        Assert.False(await model.SubmitAsync()); Assert.True(model.IsAwaitingResult);
        api.Reads[DriverPath] = new FoodDeliveryDriverWorkspaceDto { DriverId = "other-user" };
        Assert.False(await model.CheckResultAsync()); Assert.False(model.IsCompleted);
        api.Reads[DriverPath] = new FoodDeliveryDriverWorkspaceDto { DriverId = "user-a", ActiveDeliveries =
            [new() { OfferId = "offer-a", DeliveryAttemptId = "attempt-b", AttemptRevision = 1 }] };
        Assert.True(await model.CheckResultAsync()); Assert.Single(api.Writes);
    }

    [Fact]
    public async Task AccountChangeDiscardsLateConfirmationQueryAndPendingEvidence()
    {
        var api = CancelApi(); var access = new Access();
        using var model = new FoodRoleActionViewModel(api, access);
        await PrepareCancellationAsync(model);
        api.Post = (_, body) => CancelledResponse(((주문자음식주문취소요청)body).클라이언트요청Id);
        var wait = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.AsyncGet = _ => wait.Task;
        var result = model.SubmitAsync();
        Assert.True(model.IsAwaitingResult);
        access.Change(new("other-user", 2, true)); wait.SetResult(CancelledDetail());
        Assert.False(await result); Assert.False(model.IsCompleted); Assert.False(model.IsAwaitingResult);
        Assert.Null(model.RequestId); Assert.Empty(model.Reason); Assert.False(await model.CheckResultAsync());
        Assert.Single(api.Writes); Assert.Equal(2, api.ReadRoles.Count);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task PermissionFailureDuringConfirmationClearsPrivatePendingState(int status)
    {
        var api = CancelApi();
        using var model = new FoodRoleActionViewModel(api, new Access());
        await PrepareCancellationAsync(model);
        api.Post = (_, body) => CancelledResponse(((주문자음식주문취소요청)body).클라이언트요청Id);
        api.AsyncGet = _ => Task.FromException<object>(new RoleWorkspaceAccessException(status, "denied"));
        Assert.False(await model.SubmitAsync()); Assert.True(model.RequiresLogin);
        Assert.Null(model.RequestId); Assert.Empty(model.Reason); Assert.False(model.IsAwaitingResult);
        Assert.False(await model.CheckResultAsync()); Assert.Single(api.Writes);
    }

    private static async Task PrepareCancellationAsync(FoodRoleActionViewModel model)
    {
        await model.InitializeAsync("orderer", "order-a", 음식배달가능행동Ids.주문취소);
        model.ReasonCode = 운영배차주문자취소사유Code.중복주문; model.Prepare(); model.Confirmed = true;
    }

    private static Api RestaurantApi(string action)
    {
        var api = new Api();
        api.Reads[RestaurantPath] = new 음식주문응답 { 주문번호 = "order-a", Revision = 12, 음식점명 = "음식점", 음식점주소 = "주소",
            상태 = action == 음식배달가능행동Ids.음식점조리시간변경 ? 음식주문상태코드.조리중 : 음식주문상태코드.주문대기,
            조리예상분 = 15, AvailableActions = [new() { ActionId = action, ExpectedRevision = 12 }] };
        return api;
    }

    private static 음식주문응답 PreparationResponse(음식점주문진행변경요청 request)
    {
        var minutes = Math.Clamp(request.조리예상분!.Value, 1, 180);
        return new() { 주문번호 = "order-a", Revision = 13, 상태 = 음식주문상태코드.조리중, 조리예상분 = minutes,
            상태이력 = [new() { 클라이언트요청Id = request.클라이언트요청Id, 이전상태 = 음식주문상태코드.조리중,
                다음상태 = 음식주문상태코드.조리중, 사유 = $"조리 예상 시간 변경 · {minutes}분" }] };
    }

    private static 음식주문응답 AdvancePreparation(음식주문응답 response, bool pickupReady) => new()
    {
        주문번호 = response.주문번호, Revision = response.Revision + 1,
        상태 = pickupReady ? 음식주문상태코드.픽업대기 : response.상태, 조리예상분 = pickupReady ? 0 : 45,
        음식점명 = response.음식점명, 음식점주소 = response.음식점주소, 음식점상세주소 = response.음식점상세주소,
        음식점수락시각Utc = response.음식점수락시각Utc, 수락메모 = response.수락메모,
        상태이력 = response.상태이력.Concat([new 음식주문상태전이기록Dto { 클라이언트요청Id = Guid.NewGuid(),
            이전상태 = response.상태, 다음상태 = pickupReady ? 음식주문상태코드.픽업대기 : response.상태,
            사유 = pickupReady ? "음식점 픽업 준비 완료" : "조리 예상 시간 변경 · 45분" }]).ToArray()
    };

    private static 음식주문응답 AdvanceCooking(음식주문응답 response, bool recooking)
    {
        var history = response.상태이력.ToList();
        var time = response.음식점수락시각Utc!.Value.AddMinutes(1);
        history.Add(new() { 이전상태 = 음식주문상태코드.주문확인, 다음상태 = 음식주문상태코드.기사배정, 전이시각Utc = time });
        time = time.AddMinutes(1);
        history.Add(new() { 이전상태 = 음식주문상태코드.기사배정, 다음상태 = 음식주문상태코드.조리중,
            사유 = "음식점 조리 시작", 전이시각Utc = time });
        var firstStart = time;
        if (recooking)
        {
            time = time.AddMinutes(20);
            history.Add(new() { 이전상태 = 음식주문상태코드.조리중, 다음상태 = 음식주문상태코드.픽업대기,
                사유 = "음식점 픽업 준비 완료", 전이시각Utc = time });
            time = time.AddMinutes(1);
            history.Add(new() { 이전상태 = 음식주문상태코드.픽업대기, 다음상태 = 음식주문상태코드.픽업완료, 전이시각Utc = time });
            time = time.AddMinutes(1);
            history.Add(new() { 이전상태 = 음식주문상태코드.픽업완료, 다음상태 = 음식주문상태코드.조리중,
                사유 = "픽업 후 배달 중단 · 재조리·재배차", 전이시각Utc = time });
        }
        return new()
        {
            주문번호 = response.주문번호, Revision = response.Revision + history.Count - response.상태이력.Count,
            상태 = 음식주문상태코드.조리중, 조리예상분 = 20, 조리시작시각Utc = firstStart,
            CurrentPreparationRound = recooking ? 2 : 1, CurrentCookingStartedAtUtc = time,
            RecookingRequestedAtUtc = recooking ? time : null, 조리예상완료시각Utc = time.AddMinutes(20),
            음식점명 = response.음식점명,
            음식점주소 = response.음식점주소, 음식점상세주소 = response.음식점상세주소,
            음식점수락시각Utc = response.음식점수락시각Utc, 수락메모 = response.수락메모, 상태이력 = history
        };
    }

    private static Api CancelApi()
    {
        var api = new Api();
        api.Reads[CancelPath] = new 주문자음식주문상세응답 { 주문 = new() { 주문번호 = "order-a", 상태 = 음식주문상태코드.주문대기 },
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.주문취소, ExpectedRevision = 3 }] };
        return api;
    }

    private const string CancelPath = "api/v1/food-orders/order-a";
    private const string RestaurantPath = "api/v1/food-orders/restaurant/inbox/order-a";
    private const string DriverPath = "api/v1/driver/food-deliveries/workspace";
    private const string OperatorPath = "api/v1/admin/food-orders/order-a/operations-trace";
    private static 주문자음식주문상세응답 CancelledDetail() => new() { 주문 = new() { 주문번호 = "order-a", 상태 = 음식주문상태코드.취소 } };
    private static 음식주문응답 CancelledResponse(Guid requestId) => new() { 주문번호 = "order-a", Revision = 4, 상태 = 음식주문상태코드.취소,
        상태이력 = [new() { 클라이언트요청Id = requestId, 다음상태 = 음식주문상태코드.취소 }] };
    private static 음식주문응답 AcceptedResponse(음식점주문수락요청 request) => new()
    {
        주문번호 = "order-a", Revision = 13, 상태 = 음식주문상태코드.주문확인, 음식점명 = request.음식점명,
        음식점주소 = request.음식점주소, 음식점상세주소 = request.음식점상세주소,
        조리예상분 = request.즉시픽업가능여부 ? 0 : Math.Clamp(request.조리예상분 ?? 15, 1, 180),
        음식점수락시각Utc = DateTime.UtcNow, 수락메모 = request.수락메모,
        상태이력 = [new() { 클라이언트요청Id = request.클라이언트요청Id, 다음상태 = 음식주문상태코드.주문확인,
            사유 = request.즉시픽업가능여부 ? "음식점 주문 확인 · 기존 준비 완료" : "음식점 주문 확인 · 배차 후 조리" }]
    };
    private static FoodDeliveryDriverActionResponse InterruptedResponse() => new()
        { OfferId = "offer-a", DeliveryAttemptId = "attempt-a", AttemptRevision = 10, Status = "Interrupted" };

    private sealed class Api : IRoleWorkspaceApi
    {
        public Dictionary<string, object> Reads { get; } = new(StringComparer.Ordinal);
        public List<string> ReadRoles { get; } = [];
        public List<(string Role, string Path, object Body)> Writes { get; } = [];
        public Func<string, object, object?>? Post { get; set; }
        public Func<string, object, Task<object?>>? AsyncPost { get; set; }
        public Func<string, Task<object>>? AsyncGet { get; set; }
        public async Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); ReadRoles.Add(roleKey);
            return (T)(AsyncGet is null ? Reads[path] : await AsyncGet(path));
        }
        public async Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        {
            Writes.Add((roleKey, path, body));
            var response = AsyncPost is null ? Post?.Invoke(path, body) : await AsyncPost(path, body);
            return response is null ? default : (T)response;
        }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => PostAsync<T>(roleKey, path, body, cancellationToken);
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        private RoleWorkspaceIdentity identity = new("user-a", 1, true);
        public event Action? Changed;
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => identity;
        public void Change(RoleWorkspaceIdentity value) { identity = value; Changed?.Invoke(); }
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) { Change(new(null, identity.Revision + 1, false)); return Task.CompletedTask; }
    }
}
