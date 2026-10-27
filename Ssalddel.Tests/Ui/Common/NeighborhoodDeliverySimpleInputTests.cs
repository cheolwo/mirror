using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodDeliverySimpleInputTests
{
    [Fact]
    public async Task Wizard_asks_missing_weight_places_and_explicit_time_confirmation()
    {
        var client = new Client(); using var vm = await Ready(client, fill: false); var input = vm.SimpleInput; input.IsActive = true;
        Assert.False(input.Next()); Assert.Contains(input.MissingForStep(0), x => x.Contains("중량"));
        vm.Draft.CargoName = "책"; vm.Draft.WeightKg = 2; Assert.True(input.Next()); Assert.Equal(1, input.Step);
        Assert.False(input.Next()); Assert.Contains(input.MissingForStep(1), x => x.Contains("전화번호"));
        FillPlaces(vm.Draft); Assert.True(input.Next()); Assert.Equal(2, input.Step);
        Assert.False(input.Next()); input.TimeReviewed = true; Assert.True(input.Next()); Assert.Equal(3, input.Step);
        input.Previous(); Assert.Equal(2, input.Step); input.ToggleDetailed(); Assert.True(input.ShowDetailed);
        input.ToggleDetailed(); Assert.False(input.ShowDetailed); Assert.Equal("책", vm.Draft.CargoName);
        Assert.Equal(0, client.QuoteCalls); Assert.Empty(client.Created);
    }

    [Fact]
    public async Task Upload_selects_exactly_one_row_without_quote_or_create()
    {
        var first = Row(4, "책"); var second = Row(5, "옷"); var client = new Client { Preview = _ => Task.FromResult<NeighborhoodDeliveryInputPreviewResponse?>(Response(first, second)) };
        using var vm = await Ready(client); await vm.ImportInputAsync("양식.xlsx", [1, 2, 3]);
        Assert.Equal(2, vm.SimpleInput.Preview!.Rows.Count); Assert.NotEqual("옷", vm.Draft.CargoName);
        Assert.True(vm.SimpleInput.ApplyRow(second)); Assert.Equal("옷", vm.Draft.CargoName); Assert.Same(second, vm.SimpleInput.SelectedRow);
        Assert.Equal(vm.ConsentEvidenceId, client.Uploaded.Single().PrivacyConsentEvidenceId);
        Assert.Equal("AQID", client.Uploaded.Single().ContentBase64); Assert.Equal(0, client.QuoteCalls); Assert.Empty(client.Created);
    }

    [Fact]
    public async Task Import_whitelists_facts_and_discards_price_consent_coordinates_and_foreign_context()
    {
        var row = Row(); row.SourceRowId = Guid.NewGuid(); row.Draft.ClientRequestId = Guid.NewGuid(); row.Draft.SourcePostId = 900;
        row.Draft.PrivacyConsentEvidenceId = Guid.NewGuid(); row.Draft.AgreedFareKrw = 99; row.Draft.DirectPaymentAgreed = true; row.Draft.DispatchRequested = true;
        row.Draft.CommerceProtection = new() { NoticeAccepted = true }; row.Draft.AutoFulfillment = new() { AutomaticSubmissionAgreed = true };
        row.Draft.FulfillmentSelection = new() { ProviderId = "foreign", ConfirmedCostKrw = 99 }; row.Draft.CollaborationId = "foreign";
        row.Draft.Pickup.주소.위도 = 37; row.Draft.Pickup.주소.경도 = 127;
        row.Draft.PackageLengthMm = 293; row.Draft.PackageWidthMm = 350; row.Draft.PackageHeightMm = 200;
        row.Draft.VolumeCbm = .02051m; row.Draft.PalletCount = 0; row.Draft.FloorRotationAllowed = false; row.Draft.StackingAllowed = false;
        row.Draft.Pickup.작업시간분 = 0m; row.Draft.Dropoff.작업시간분 = 2.5m;
        var client = ClientWith(row); using var vm = await Ready(client); vm.Initialize(41); var consent = vm.ConsentEvidenceId;
        await vm.QuoteAsync(); vm.DirectPaymentAgreed = true; vm.DispatchRequested = true;
        await vm.ImportInputAsync("양식.xlsx", [1]); Assert.True(vm.SimpleInput.ApplyRow(row));
        Assert.Equal(row.SourceRowId.Value, vm.Draft.ClientRequestId); Assert.Equal(41, vm.Draft.SourcePostId); Assert.Null(vm.Draft.CollaborationId);
        Assert.Equal(consent, vm.CurrentInputSnapshot().PrivacyConsentEvidenceId); Assert.Null(vm.Draft.PrivacyConsentEvidenceId);
        Assert.Null(vm.Draft.CommerceProtection); Assert.Null(vm.Draft.AutoFulfillment); Assert.Null(vm.Draft.FulfillmentSelection); Assert.Null(vm.Draft.AgreedFareKrw);
        Assert.Null(vm.Draft.Pickup.주소.위도); Assert.Null(vm.Draft.Pickup.주소.경도); Assert.False(vm.DirectPaymentAgreed); Assert.False(vm.DispatchRequested); Assert.Null(vm.Quote);
        Assert.Equal(293, vm.Draft.PackageLengthMm); Assert.Equal(.02051m, vm.Draft.VolumeCbm); Assert.Equal(0, vm.Draft.PalletCount);
        Assert.False(vm.Draft.FloorRotationAllowed); Assert.False(vm.Draft.StackingAllowed);
        Assert.Equal(0m, vm.Draft.Pickup.작업시간분); Assert.Equal(2.5m, vm.Draft.Dropoff.작업시간분);
    }

    [Fact]
    public async Task Same_file_and_row_reupload_preserves_session_request_id_but_changed_file_does_not()
    {
        var client = new Client { Preview = _ => Task.FromResult<NeighborhoodDeliveryInputPreviewResponse?>(Response(Row())) };
        using var vm = await Ready(client);
        await vm.ImportInputAsync("a.xlsx", [1]); Assert.True(vm.SimpleInput.ApplyRow(vm.SimpleInput.Preview!.Rows[0])); var id = vm.Draft.ClientRequestId;
        Assert.True(vm.SimpleInput.ApplyRow(vm.SimpleInput.Preview.Rows[0])); Assert.Equal(id, vm.Draft.ClientRequestId);
        await vm.ImportInputAsync("a.xlsx", [1]); Assert.True(vm.SimpleInput.ApplyRow(vm.SimpleInput.Preview!.Rows[0])); Assert.Equal(id, vm.Draft.ClientRequestId);
        await vm.ImportInputAsync("a.xlsx", [2]); Assert.True(vm.SimpleInput.ApplyRow(vm.SimpleInput.Preview!.Rows[0])); Assert.NotEqual(id, vm.Draft.ClientRequestId);
    }

    [Fact]
    public async Task Missing_imported_time_is_blank_not_defaulted_and_cannot_be_quoted()
    {
        var row = Row(); row.Draft.Pickup.시간창 = null; row.Draft.Dropoff.시간창 = null;
        var client = ClientWith(row); using var vm = await Ready(client); vm.SimpleInput.IsActive = true;
        await vm.ImportInputAsync("양식.xlsx", [1]); Assert.True(vm.SimpleInput.ApplyRow(row));
        Assert.Null(vm.SimpleInput.PickupStart); Assert.Null(vm.SimpleInput.DropoffEnd); Assert.Null(vm.CurrentInputSnapshot().Pickup.시간창);
        vm.SimpleInput.TimeReviewed = true; await vm.QuoteAsync(); Assert.Equal(0, client.QuoteCalls);
        var start = DateTime.Now.AddHours(1); vm.SimpleInput.PickupStart = start; vm.SimpleInput.PickupEnd = start.AddHours(1);
        vm.SimpleInput.DropoffStart = start; vm.SimpleInput.DropoffEnd = start.AddHours(2);
        Assert.False(vm.SimpleInput.HasMissingTimes); Assert.False(vm.SimpleInput.TimeReviewed); vm.SimpleInput.TimeReviewed = true;
        await vm.QuoteAsync(); Assert.Equal(1, client.QuoteCalls);
    }

    [Fact]
    public async Task Late_upload_after_owner_change_is_ignored_and_private_preview_cleared()
    {
        var delayed = new TaskCompletionSource<NeighborhoodDeliveryInputPreviewResponse?>(); var user = new User();
        var client = new Client { Preview = _ => delayed.Task }; using var vm = await Ready(client, user);
        var pending = vm.ImportInputAsync("양식.xlsx", [1]); Assert.True(vm.SimpleInput.IsBusy);
        user.Value = new("other", "other", []); vm.SynchronizeOwner(); delayed.SetResult(Response(Row())); await pending;
        Assert.Null(vm.SimpleInput.Preview); Assert.Null(vm.ConsentEvidenceId); Assert.Empty(vm.Draft.CargoName); Assert.False(vm.SimpleInput.IsBusy);
    }

    [Fact]
    public async Task Logout_and_dispose_discard_preview_selected_row_template_and_late_responses()
    {
        var row = Row(); var client = ClientWith(row); var user = new User(); var vm = await Ready(client, user);
        await vm.ImportInputAsync("a.xlsx", [1]); Assert.True(vm.SimpleInput.ApplyRow(row)); await vm.LoadInputTemplateAsync(); Assert.NotNull(vm.SimpleInput.Template);
        user.Value = 현재사용자Snapshot.익명; vm.SynchronizeOwner();
        Assert.Null(vm.SimpleInput.Preview); Assert.Null(vm.SimpleInput.SelectedRow); Assert.Null(vm.SimpleInput.Template); Assert.Empty(vm.Draft.CargoName);
        vm.Dispose(); Assert.False(vm.SimpleInput.ApplyRow(row));
        var delayed = new TaskCompletionSource<NeighborhoodDeliveryInputPreviewResponse?>(); client = new Client { Preview = _ => delayed.Task };
        vm = await Ready(client); var pending = vm.ImportInputAsync("a.xlsx", [1]); vm.Dispose(); delayed.SetResult(Response(Row())); await pending; Assert.Null(vm.SimpleInput.Preview);
    }

    [Fact]
    public async Task Unknown_submission_locks_import_selection_and_preserves_original_payload_for_retry()
    {
        var row = Row(); var client = ClientWith(row); client.Create = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(null);
        using var vm = await Ready(client); await vm.ImportInputAsync("a.xlsx", [1]); vm.SimpleInput.ApplyRow(row);
        FillLoading(vm.Draft); // 이 시험의 명시 보완값이며 엑셀 v1에서 수집됐다는 뜻이 아닙니다.
        vm.SimpleInput.TimeReviewed = true; await vm.QuoteAsync(); vm.DirectPaymentAgreed = true; vm.DispatchRequested = true;
        await vm.SubmitAsync(); Assert.True(vm.NeedsSubmissionReview); var original = JsonSerializer.Serialize(client.Created[0]);
        Assert.False(vm.SimpleInput.ApplyRow(row)); await vm.ImportInputAsync("b.xlsx", [2]); Assert.Single(client.Uploaded);
        vm.Draft.CargoName = "수정 시도"; await vm.SubmitAsync(); Assert.Equal(original, JsonSerializer.Serialize(client.Created[1]));
    }

    [Fact]
    public async Task Edited_input_invalidates_quote_and_all_authorizations()
    {
        var client = new Client(); using var vm = await Ready(client); await vm.QuoteAsync(); vm.DirectPaymentAgreed = true; vm.DispatchRequested = true;
        vm.AutoConditions.ConfigureDefault(); vm.AutoConditions.AutomaticSubmissionAgreed = true; vm.AutoConditions.ProviderInformationSharingAgreed = true;
        vm.Draft.CargoName = "바뀐 물건"; vm.InputChanged();
        Assert.Null(vm.Quote); Assert.False(vm.HasCurrentQuote); Assert.False(vm.DirectPaymentAgreed); Assert.False(vm.DispatchRequested);
        Assert.False(vm.AutoConditions.AutomaticSubmissionAgreed); Assert.False(vm.AutoConditions.ProviderInformationSharingAgreed);
        await vm.SubmitAsync(); Assert.Empty(client.Created);
    }

    [Fact]
    public async Task Final_review_matches_actual_submitted_places_contacts_and_utc_windows()
    {
        var client = new Client(); using var vm = await Ready(client); vm.SimpleInput.IsActive = true;
        vm.Draft.PackageSizeCode = "Medium"; vm.Draft.Notes = "문 앞에서 연락"; vm.Draft.Pickup.주소.상세주소 = "1층";
        vm.SimpleInput.TimeReviewed = true; var review = vm.CurrentInputSnapshot(); await vm.QuoteAsync(); vm.DirectPaymentAgreed = true; vm.DispatchRequested = true;
        await vm.SubmitAsync(); var actual = Assert.Single(client.Created);
        Assert.Equal(review.CargoName, actual.CargoName); Assert.Equal(review.Quantity, actual.Quantity); Assert.Equal(review.WeightKg, actual.WeightKg);
        Assert.Equal(JsonSerializer.Serialize(review.Pickup), JsonSerializer.Serialize(actual.Pickup)); Assert.Equal(JsonSerializer.Serialize(review.Dropoff), JsonSerializer.Serialize(actual.Dropoff));
        Assert.Equal(review.Notes, actual.Notes); Assert.Equal(review.PackageSizeCode, actual.PackageSizeCode); Assert.Equal(4000, actual.AgreedFareKrw);
    }

    [Theory]
    [InlineData("file.csv", 2)]
    [InlineData("file.xlsx", 0)]
    [InlineData("file.xlsx", 2097153)]
    public async Task Unsupported_or_oversized_input_does_not_call_server(string name, int size)
    {
        var client = new Client(); using var vm = await Ready(client); await vm.ImportInputAsync(name, new byte[size]);
        Assert.Empty(client.Uploaded); Assert.NotNull(vm.SimpleInput.Error);
    }

    [Fact]
    public async Task Missing_consent_and_unsafe_preview_do_not_apply_or_call_submission()
    {
        var client = ClientWith(Row()); using var withoutConsent = new NeighborhoodDeliveryAuthoringViewModel(client, new User()); withoutConsent.Initialize(null);
        await withoutConsent.ImportInputAsync("a.xlsx", [1]); Assert.Empty(client.Uploaded);
        using var vm = await Ready(client); await vm.ImportInputAsync("a.xlsx", [1]); Assert.False(vm.SimpleInput.ApplyRow(Row()));
        var current = vm.Draft.ClientRequestId; vm.SimpleInput.Preview!.Rows[0].Draft = null!; Assert.False(vm.SimpleInput.ApplyRow(vm.SimpleInput.Preview.Rows[0])); Assert.Equal(current, vm.Draft.ClientRequestId);
        client.Preview = _ => Task.FromResult<NeighborhoodDeliveryInputPreviewResponse?>(new() { SchemaVersion = "unknown", Rows = [Row()] });
        await vm.ImportInputAsync("a.xlsx", [2]); Assert.Null(vm.SimpleInput.Preview); Assert.NotNull(vm.SimpleInput.Error); Assert.Empty(client.Created);
    }

    [Theory]
    [InlineData("InvalidRowId")]
    [InlineData("DuplicateRowId")]
    public async Task Invalid_or_duplicate_explicit_row_id_cannot_be_applied(string code)
    {
        var row = Row(); row.SourceRowId = code == "DuplicateRowId" ? Guid.NewGuid() : null;
        row.Issues = [new("SourceRowId", code, "양식의 행 ID를 수정해 주세요.")];
        var client = ClientWith(row); using var vm = await Ready(client); var original = vm.Draft.ClientRequestId;
        await vm.ImportInputAsync("a.xlsx", [1]); Assert.False(vm.SimpleInput.CanApplyRow(row)); Assert.False(vm.SimpleInput.ApplyRow(row));
        Assert.Equal(original, vm.Draft.ClientRequestId); Assert.Empty(client.Created);
    }

    [Fact]
    public async Task Blank_optional_dropoff_stays_unknown_and_internal_courier_submission_requires_completion()
    {
        var row = Row(); row.Draft.Dropoff.시간창 = null; var client = ClientWith(row); using var vm = await Ready(client);
        vm.SimpleInput.IsActive = true; await vm.ImportInputAsync("a.xlsx", [1]); Assert.True(vm.SimpleInput.ApplyRow(row));
        Assert.True(vm.SimpleInput.DropoffTimeUnspecified); Assert.False(vm.SimpleInput.HasMissingTimes);
        Assert.Null(vm.CurrentInputSnapshot().Dropoff.시간창); vm.SimpleInput.TimeReviewed = true;
        await vm.QuoteAsync(); Assert.Equal(1, client.QuoteCalls); vm.DirectPaymentAgreed = true; vm.DispatchRequested = true;
        await vm.SubmitAsync(); Assert.Empty(client.Created); Assert.Contains("마감", vm.Error);
        Assert.Null(vm.CurrentInputSnapshot().Dropoff.시간창); Assert.Null(vm.Draft.PackageLengthMm);
    }

    [Fact]
    public async Task Partially_supplied_dropoff_window_needs_repair_or_explicit_omission()
    {
        var row = Row(); row.Draft.Dropoff.시간창 = null; row.RawValues = new Dictionary<string, string> { ["DropoffStart"] = "2030-01-02 10:00" };
        row.Issues = [new("DropoffEnd", "Required", "전달 종료 시각을 입력해 주세요.")];
        var client = ClientWith(row); using var vm = await Ready(client); vm.SimpleInput.IsActive = true;
        await vm.ImportInputAsync("a.xlsx", [1]); vm.SimpleInput.ApplyRow(row);
        Assert.False(vm.SimpleInput.DropoffTimeUnspecified); Assert.True(vm.SimpleInput.HasMissingTimes);
        vm.SimpleInput.TimeReviewed = true; await vm.QuoteAsync(); Assert.Equal(0, client.QuoteCalls);
        vm.SimpleInput.DropoffTimeUnspecified = true; Assert.False(vm.SimpleInput.TimeReviewed); vm.SimpleInput.TimeReviewed = true;
        await vm.QuoteAsync(); Assert.Equal(1, client.QuoteCalls); Assert.Null(vm.CurrentInputSnapshot().Dropoff.시간창);
    }

    [Fact]
    public async Task Preserved_invalid_optional_fields_remain_visible_and_block_step_progress()
    {
        var row = Row(); row.Draft.PackageSizeCode = "unknown"; row.Draft.Notes = new string('x', 1001); row.Draft.Pickup.주소.상세주소 = new string('x', 301);
        var client = ClientWith(row); using var vm = await Ready(client); await vm.ImportInputAsync("a.xlsx", [1]); vm.SimpleInput.ApplyRow(row);
        Assert.Equal("unknown", vm.Draft.PackageSizeCode); Assert.Equal(1001, vm.Draft.Notes!.Length); Assert.Equal(301, vm.Draft.Pickup.주소.상세주소!.Length);
        Assert.Contains(vm.SimpleInput.MissingForStep(0), x => x.Contains("포장 크기")); Assert.Contains(vm.SimpleInput.MissingForStep(0), x => x.Contains("요청 사항"));
        Assert.Contains(vm.SimpleInput.MissingForStep(1), x => x.Contains("상세주소")); Assert.False(vm.SimpleInput.Next());
        await vm.QuoteAsync(); Assert.Equal(0, client.QuoteCalls);
    }

    private static Client ClientWith(NeighborhoodDeliveryInputRow row) => new() { Preview = _ => Task.FromResult<NeighborhoodDeliveryInputPreviewResponse?>(Response(row)) };
    private static NeighborhoodDeliveryInputPreviewResponse Response(params NeighborhoodDeliveryInputRow[] rows) => new() { Rows = rows };
    private static NeighborhoodDeliveryInputRow Row(int number = 4, string cargo = "책")
    {
        var start = DateTime.UtcNow.AddHours(1); var row = new NeighborhoodDeliveryInputRow { RowNumber = number, Draft = new() { CargoName = cargo, WeightKg = 2, VehicleType = "오토바이" } };
        FillPlaces(row.Draft); row.Draft.Pickup.시간창 = new() { 시작일시 = start, 종료일시 = start.AddHours(1) };
        row.Draft.Dropoff.시간창 = new() { 시작일시 = start, 종료일시 = start.AddHours(2) }; return row;
    }
    [Fact]
    public async Task Native_package_and_handling_inputs_survive_review_submission_and_decimal_precision()
    {
        var client = new Client(); using var vm = await Ready(client); vm.SimpleInput.IsActive = true;
        vm.Draft.Quantity = 3; vm.Draft.WeightKg = 12.5m;
        vm.Draft.PackageLengthMm = 293; vm.Draft.PackageWidthMm = 350; vm.Draft.PackageHeightMm = 200;
        vm.Draft.VolumeCbm = .05m; vm.Draft.PalletCount = 0; vm.Draft.FloorRotationAllowed = false; vm.Draft.StackingAllowed = false;
        vm.Draft.Pickup.작업시간분 = 0m; vm.Draft.Dropoff.작업시간분 = 2.5m;
        vm.SimpleInput.TimeReviewed = true; var review = vm.CurrentInputSnapshot();
        Assert.Equal(.06153m, vm.LoadingInput.유효부피Cbm); Assert.Equal(.05m, vm.LoadingInput.입력.입력부피Cbm);
        await vm.QuoteAsync(); vm.DirectPaymentAgreed = true; vm.DispatchRequested = true; await vm.SubmitAsync();
        var sent = Assert.Single(client.Created);
        Assert.Equal(JsonSerializer.Serialize(review.Pickup), JsonSerializer.Serialize(sent.Pickup));
        Assert.Equal(JsonSerializer.Serialize(review.Dropoff), JsonSerializer.Serialize(sent.Dropoff));
        Assert.Equal(NeighborhoodDeliveryLoadingInput.Calculate(review).입력지문, NeighborhoodDeliveryLoadingInput.Calculate(sent).입력지문);
        Assert.Equal(12.5m, sent.WeightKg); Assert.False(sent.FloorRotationAllowed); Assert.False(sent.StackingAllowed);
    }

    [Fact]
    public async Task Imported_missing_package_values_stay_unknown_until_explicitly_completed()
    {
        var row = Row(); var client = ClientWith(row); using var vm = await Ready(client);
        await vm.ImportInputAsync("v1.xlsx", [1]); Assert.True(vm.SimpleInput.ApplyRow(row));
        Assert.Null(vm.Draft.PackageLengthMm); Assert.Null(vm.Draft.VolumeCbm); Assert.Null(vm.Draft.FloorRotationAllowed);
        Assert.Null(vm.Draft.Pickup.작업시간분); Assert.Contains(vm.InternalCourierMissing, x => x.Contains("포장"));
        Assert.Null(NeighborhoodDeliveryLoadingInput.Calculate(vm.CurrentInputSnapshot()).유효부피Cbm);
    }

    private static void FillLoading(NeighborhoodDeliveryRequest request)
    {
        request.PackageLengthMm = 300; request.PackageWidthMm = 200; request.PackageHeightMm = 200;
        request.PalletCount = 0; request.FloorRotationAllowed = true; request.StackingAllowed = true;
        request.Pickup.작업시간분 = 2m; request.Dropoff.작업시간분 = 1m;
    }
    private static void FillPlaces(NeighborhoodDeliveryRequest request)
    {
        foreach (var place in new[] { request.Pickup, request.Dropoff })
        { place.주소.도로명주소 = "시험로 10"; place.연락처.이름 = "시험 이용자"; place.연락처.전화번호 = "01000000000"; }
    }
    private static async Task<NeighborhoodDeliveryAuthoringViewModel> Ready(Client client, User? user = null, bool fill = true)
    {
        var vm = new NeighborhoodDeliveryAuthoringViewModel(client, user ?? new User()); vm.Initialize(null);
        vm.CollectionConsent = true; vm.AgeConfirmed = true; await vm.AcceptConsentAsync();
        if (fill) { vm.Draft.CargoName = "책"; vm.Draft.WeightKg = 2; FillPlaces(vm.Draft); FillLoading(vm.Draft); } return vm;
    }
    private sealed class User : ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot Value { get; set; } = new("owner", "owner", []);
        public 현재사용자Snapshot 현재사용자 => Value;
    }
    private sealed class Client : INeighborhoodDeliveryClient
    {
        public Func<NeighborhoodDeliveryInputPreviewRequest, Task<NeighborhoodDeliveryInputPreviewResponse?>> Preview { get; set; } = _ => Task.FromResult<NeighborhoodDeliveryInputPreviewResponse?>(Response(Row()));
        public Func<NeighborhoodDeliveryRequest, Task<NeighborhoodDeliveryResponse?>> Create { get; set; } = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(new() { RequestId = "request" });
        public List<NeighborhoodDeliveryInputPreviewRequest> Uploaded { get; } = [];
        public List<NeighborhoodDeliveryRequest> Created { get; } = [];
        public int QuoteCalls;
        public Task<NeighborhoodDeliveryInputPreviewResponse?> PreviewInputAsync(NeighborhoodDeliveryInputPreviewRequest request, CancellationToken ct) { Uploaded.Add(request); return Preview(request); }
        public Task<NeighborhoodDeliveryInputTemplateResponse?> GetInputTemplateAsync(CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryInputTemplateResponse?>(new() { ContentBase64 = "AQ==" });
        public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct) => Task.FromResult<신청개인정보동의증적Response?>(new() { 증적Id = Guid.NewGuid(), 상태Code = 신청개인정보동의상태Codes.유효 });
        public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) { QuoteCalls++; return Task.FromResult<NeighborhoodDeliveryQuoteResponse?>(new() { Fare = new() { 최종운임 = 4000, 예상거리Km = 2 } }); }
        public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) { Created.Add(JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(request))!); return Create(request); }
        public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodDeliveryResponse>>([]);
        public Task<NeighborhoodDeliveryResponse?> ReadAsync(string requestId, CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryResponse?>(null);
    }
}
