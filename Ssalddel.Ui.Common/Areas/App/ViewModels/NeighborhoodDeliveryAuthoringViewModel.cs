using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>비공개 배송 작성 수명과 견적 확인을 소유합니다. 계산·접수·배차 권위는 서버에 있습니다.</summary>
public sealed partial class NeighborhoodDeliveryAuthoringViewModel(
    INeighborhoodDeliveryClient client, ISsalddel현재사용자Context user,
    INeighborhoodCollaborationClient? collaborations = null, IWorkflowRecoveryStore? recoveryStore = null) : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private string? _owner;
    private string? _quotedInput;
    private string? _submittedInput;
    private NeighborhoodDeliveryRequest? _recoveryRequest;
    private bool _disposed;
    private Guid _consentRequestId = Guid.NewGuid();
    private long _generation;
    private string? _collaborationId;
    private NeighborhoodCollaborationResponse? _collaboration;
    private NeighborhoodCollaborationCommandRequest? _linkCommand;
    public bool IsLoadingCollaboration { get; private set; }
    public bool CanWriteCollaboration => _collaborationId is null || _collaboration is not null;
    public string? CollaborationTitle => _collaboration?.SourceTitle;
    public string? CollaborationId => _collaborationId;
    public bool RequiresLogin { get; private set; }
    public bool IsAuthenticated => user.현재사용자.인증됨 && !RequiresLogin;
    public bool IsSending { get; private set; }
    public string? Error { get; private set; }
    public bool CollectionConsent { get; set; }
    public bool AgeConfirmed { get; set; }
    public Guid? ConsentEvidenceId { get; private set; }
    public NeighborhoodDeliveryRequest Draft { get; private set; } = NewDraft();
    public string? PackageSizeCode
    {
        get => Draft.PackageSizeCode;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (Draft.PackageSizeCode == normalized || IsSending || NeedsSubmissionReview) return;
            Draft.PackageSizeCode = normalized;
            AutoConditions.InvalidateAcknowledgment();
            ResetFulfillmentChoice(); DirectPaymentAgreed = false; Changed();
        }
    }
    private DateTime _pickupStart = DateTime.Now.AddMinutes(30), _pickupEnd = DateTime.Now.AddHours(2);
    private DateTime _dropoffStart = DateTime.Now.AddMinutes(30), _dropoffEnd = DateTime.Now.AddHours(3);
    public DateTime PickupStart { get => _pickupStart; set { _pickupStart = value; _simpleInput?.TimeChanged("PickupStart"); } }
    public DateTime PickupEnd { get => _pickupEnd; set { _pickupEnd = value; _simpleInput?.TimeChanged("PickupEnd"); } }
    public DateTime DropoffStart { get => _dropoffStart; set { _dropoffStart = value; _simpleInput?.TimeChanged("DropoffStart"); } }
    public DateTime DropoffEnd { get => _dropoffEnd; set { _dropoffEnd = value; _simpleInput?.TimeChanged("DropoffEnd"); } }
    private 생활배송간편입력ViewModel? _simpleInput;
    public 생활배송간편입력ViewModel SimpleInput => _simpleInput ??= new(this, client);
    public bool CanEditInput => !_disposed && IsAuthenticated && !IsSending && !NeedsSubmissionReview && !RecoveryExpired
        && Submitted is null && ConsentEvidenceId.HasValue && CollectionConsent && AgeConfirmed && CanWriteCollaboration;
    public bool DispatchRequested { get; set; }
    public bool DirectPaymentAgreed { get; set; }
    public bool MemberDeliveryRequested
    {
        get => Draft.MemberDeliveryRequested;
        set
        {
            if (_disposed || IsSending || NeedsSubmissionReview || Draft.MemberDeliveryRequested == value) return;
            Draft.MemberDeliveryRequested = value; Draft.MemberDeliveryDisclosureAgreed = false;
            Draft.MemberDeliveryNoticeVersion = value ? NeighborhoodMemberDeliveryRoutes.NoticeVersion : null;
            InputChanged();
        }
    }
    public DateTime? DesiredCompletionLocal { get; set; }
    public string? SelectedFulfillmentQuoteId { get; private set; }
    public bool FulfillmentCostAgreed { get; set; }
    public bool ProviderInformationSharingAgreed { get; set; }
    private AutoFulfillmentConditionsViewModel? _autoConditions;
    public AutoFulfillmentConditionsViewModel AutoConditions
    {
        get
        {
            if (_autoConditions is null) { _autoConditions = new(); _autoConditions.PropertyChanged += (_, _) => Changed(); }
            return _autoConditions;
        }
    }
    public bool AutoRoutingEnabled { get; private set; }
    public bool CanConfirmCurrentQuote => MemberDeliveryRequested ? DirectPaymentAgreed && Draft.MemberDeliveryDisclosureAgreed
        : AutoRoutingEnabled ? AutoConditions.IsComplete : HasFulfillmentOptions ? CanConfirmFulfillment : DirectPaymentAgreed;
    public NeighborhoodFulfillmentOption? SelectedFulfillmentOption
        => Quote?.FulfillmentOptions.SingleOrDefault(option => option.QuoteId == SelectedFulfillmentQuoteId);
    public bool HasFulfillmentOptions => Quote?.FulfillmentOptions.Count > 0;
    public bool CanConfirmFulfillment => SelectedFulfillmentOption is { } option
        && NeighborhoodDeliveryPresentation.FulfillmentOptionSelectable(option) && FulfillmentCostAgreed
        && (option.ProviderId == "internal" ? DirectPaymentAgreed : ProviderInformationSharingAgreed);
    public NeighborhoodDeliveryQuoteResponse? Quote { get; private set; }
    public NeighborhoodDeliveryResponse? Submitted { get; private set; }
    public bool HasCurrentQuote => Quote is not null && _quotedInput == QuoteFingerprint(BuildRequest());
    public bool NeedsSubmissionReview => _submittedInput is not null;

    public Task ImportInputAsync(string fileName, byte[] content) => SimpleInput.ImportAsync(fileName, content);
    public Task LoadInputTemplateAsync() => SimpleInput.LoadTemplateAsync();
    internal void SimpleInputChanged() => Changed();
    public void InputChanged()
    {
        if (_disposed || IsSending || NeedsSubmissionReview || Submitted is not null) return;
        Quote = null; _quotedInput = null; DispatchRequested = false; DirectPaymentAgreed = false;
        Draft.AgreedFareKrw = null; Draft.FulfillmentSelection = null; Draft.AutoFulfillment = null; Draft.DirectPaymentAgreed = false;
        Draft.MemberDeliveryDisclosureAgreed = false;
        AutoConditions.InvalidateAcknowledgment(); ResetFulfillmentChoice(); Changed();
    }
    public NeighborhoodDeliveryRequest CurrentInputSnapshot() => BuildRequest();
    internal bool ApplyInputRow(NeighborhoodDeliveryInputRow row, Guid requestId)
    {
        SynchronizeOwner();
        if (!CanEditInput || requestId == Guid.Empty || row.Draft is not { Pickup: { 주소: not null, 연락처: not null }, Dropoff: { 주소: not null, 연락처: not null } } source) return false;
        // Only editable shipment facts cross this boundary; imported consent, price, routing and ownership do not.
        Draft = new()
        {
            ClientRequestId = requestId, CargoName = source.CargoName, Quantity = source.Quantity, WeightKg = source.WeightKg,
            PackageSizeCode = source.PackageSizeCode, VehicleType = source.VehicleType, Notes = source.Notes,
            Pickup = CopyLocation(source.Pickup), Dropoff = CopyLocation(source.Dropoff),
            SourcePostId = Draft.SourcePostId, CollaborationId = Draft.CollaborationId, ExpectedTermsRevision = Draft.ExpectedTermsRevision,
            MemberDeliveryRequested = Draft.MemberDeliveryRequested, MemberDeliveryNoticeVersion = Draft.MemberDeliveryNoticeVersion,
            DispatchMode = NeighborhoodDispatchModes.IsKnown(source.DispatchMode) ? source.DispatchMode : NeighborhoodDispatchModes.Automatic
        };
        if (source.Pickup.시간창 is { } pickup) { PickupStart = pickup.시작일시.ToLocalTime(); PickupEnd = pickup.종료일시.ToLocalTime(); }
        if (source.Dropoff.시간창 is { } dropoff) { DropoffStart = dropoff.시작일시.ToLocalTime(); DropoffEnd = dropoff.종료일시.ToLocalTime(); }
        DesiredCompletionLocal = null; AutoRoutingEnabled = false; AutoConditions.Clear(); InputChanged(); return true;
    }
    private static LocationContactDTO CopyLocation(LocationContactDTO value) => new()
    {
        주소 = new() { 도로명주소 = value.주소.도로명주소, 상세주소 = value.주소.상세주소 },
        연락처 = new() { 이름 = value.연락처.이름, 전화번호 = value.연락처.전화번호 },
        시간창 = value.시간창 is { } window ? new() { 시작일시 = window.시작일시, 종료일시 = window.종료일시 } : null
    };

    public void Initialize(long? sourcePostId)
    {
        SynchronizeOwner();
        if (NeedsSubmissionReview || RecoveryExpired || Submitted is not null) return;
        Draft.SourcePostId = sourcePostId is > 0 ? sourcePostId : null;
    }

    public async Task LoadCollaborationAsync(string? collaborationId)
    {
        SynchronizeOwner();
        var id = string.IsNullOrWhiteSpace(collaborationId) ? null : collaborationId.Trim();
        if (_collaborationId == id && (id is null || _collaboration is not null || IsLoadingCollaboration)) return;
        if (Submitted is not null || NeedsSubmissionReview) return;
        _collaborationId = id; _collaboration = null; _linkCommand = null;
        Quote = null; _quotedInput = null;
        if (id is null || !CheckOwner()) { Changed(); return; }
        if (collaborations is null) { Error = "협업 연결을 확인할 수 없습니다."; Changed(); return; }
        var owner = _owner; var generation = ++_generation; IsLoadingCollaboration = true; Changed();
        try
        {
            var value = await collaborations.ReadAsync(id, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return;
            if (value is null || !(value.CanRequestDelivery || value.DeliveryRegistrationPending && value.MyPendingDeliveryRequest is not null || value.AllowedActions.Contains(NeighborhoodCollaborationActions.LinkDelivery)))
            { Error = "현재 협업에 배송을 연결할 수 없습니다. 양측 합의를 먼저 확인해 주세요."; return; }
            _collaboration = value; Draft.SourcePostId = value.SourcePostId;
            if (value.Terms?.TransferMethod is not null)
            {
                Draft.CollaborationId = value.StableId; Draft.ExpectedTermsRevision = value.TermsRevision;
                Draft.CargoName = value.Terms.Summary; Draft.Quantity = value.Terms.Quantity;
                PickupStart = value.Terms.FromUtc.ToLocalTime(); PickupEnd = value.Terms.UntilUtc.ToLocalTime();
                DropoffStart = PickupStart; DropoffEnd = PickupEnd;
            }
            if (value.MyPendingDeliveryRequest is { } pending)
            {
                Draft = JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(pending))!;
                DesiredCompletionLocal = Draft.DesiredCompletionUtc?.ToLocalTime();
                ConsentEvidenceId = Draft.PrivacyConsentEvidenceId; CollectionConsent = true; AgeConfirmed = true;
                PickupStart = Draft.Pickup.시간창!.시작일시.ToLocalTime(); PickupEnd = Draft.Pickup.시간창.종료일시.ToLocalTime();
                if (Draft.Dropoff.시간창 is { } window) { DropoffStart = window.시작일시.ToLocalTime(); DropoffEnd = window.종료일시.ToLocalTime(); }
                else { DropoffStart = PickupStart; DropoffEnd = PickupEnd; }
                DispatchRequested = true; DirectPaymentAgreed = true;
                // Original payload remains immutable; recovery submits it without inventing a replacement request.
                _recoveryRequest = JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(pending))!;
                _submittedInput = Fingerprint(_recoveryRequest);
            }
        }
        catch (Exception ex) { if (OwnerMatches(generation, owner)) ApplyError(ex); }
        finally { if (OwnerMatches(generation, owner)) { IsLoadingCollaboration = false; Changed(); } }
    }

    public void SynchronizeOwner()
    {
        if (_disposed) return;
        var owner = CurrentOwner;
        if (string.Equals(_owner, owner, StringComparison.Ordinal)) return;
        ++_generation; ClearPrivateInput(); _owner = owner; RequiresLogin = false; IsSending = false; Error = null; Changed();
    }

    public async Task AcceptConsentAsync()
    {
        if (IsSending || !CheckOwner()) return;
        Error = null;
        if (!CollectionConsent || !AgeConfirmed)
        { Error = "개인정보 이용 안내를 확인하고 두 항목을 모두 선택해 주세요."; Changed(); return; }
        var owner = _owner; var generation = ++_generation; IsSending = true; Changed();
        try
        {
            var response = await client.ConsentAsync(new()
            {
                증적Id = _consentRequestId, 업무Code = 신청개인정보업무Codes.운송대행,
                출처Code = 신청개인정보출처Codes.생활교류, 동의문버전 = 신청개인정보동의정책.현재버전,
                수집이용동의 = true, 연령요건확인 = true
            }, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return;
            if (response is null || response.증적Id == Guid.Empty || response.상태Code != 신청개인정보동의상태Codes.유효)
            { Error = "동의 저장을 확인하지 못했습니다. 입력 정보를 보내지 않았습니다."; return; }
            ConsentEvidenceId = response.증적Id;
        }
        catch (Exception ex) { if (OwnerMatches(generation, owner)) ApplyError(ex); }
        finally { if (OwnerMatches(generation, owner)) { IsSending = false; Changed(); } }
    }

    public async Task QuoteAsync()
    {
        if (IsSending || NeedsSubmissionReview || !CheckOwner() || !Validate()) return;
        var request = BuildRequest(); var input = QuoteFingerprint(request); var owner = _owner; var generation = ++_generation;
        Quote = null; _quotedInput = null; Error = null; IsSending = true; Changed();
        ResetFulfillmentChoice();
        try
        {
            var quote = await client.QuoteAsync(request, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return;
            if (input != QuoteFingerprint(BuildRequest()))
            { Error = "입력 내용이 바뀌었습니다. 다시 견적을 확인해 주세요."; return; }
            if (quote is null) { Error = "견적을 확인하지 못했습니다. 다시 견적을 요청해 주세요."; return; }
            Quote = quote; AutoRoutingEnabled = !MemberDeliveryRequested && quote.FulfillmentRoutingEnabled;
            if (AutoRoutingEnabled) AutoConditions.Configure(quote.AutomaticProviders.Select(value =>
                new AutoFulfillmentProviderChoice(value.ProviderId, value.ProviderName, value.IsSimulation)).ToArray(), AutoFulfillmentNotice.Version);
            else AutoConditions.Clear();
            _quotedInput = QuoteFingerprint(BuildRequest()); DirectPaymentAgreed = false;
        }
        catch (Exception ex) { if (OwnerMatches(generation, owner)) ApplyError(ex); }
        finally { if (OwnerMatches(generation, owner)) { IsSending = false; Changed(); } }
    }

    public void SelectFulfillment(string? quoteId)
    {
        if (_disposed || IsSending || NeedsSubmissionReview || !HasCurrentQuote) return;
        var option = Quote?.FulfillmentOptions.SingleOrDefault(value => value.QuoteId == quoteId);
        if (option is null || !NeighborhoodDeliveryPresentation.FulfillmentOptionSelectable(option)) return;
        SelectedFulfillmentQuoteId = quoteId; FulfillmentCostAgreed = false;
        ProviderInformationSharingAgreed = false; DirectPaymentAgreed = false; Changed();
    }

    public async Task<string?> SubmitAsync()
    {
        if (!await RestoreAsync() || RecoveryExpired) return null;
        if (Submitted is not null) return await RetryLinkAsync();
        if (IsSending || !CheckOwner() || !Validate()) return null;
        if (!CanWriteCollaboration) { Error = "협업의 현재 합의를 먼저 확인해 주세요."; Changed(); return null; }
        Error = null;
        if (_submittedInput is null && (!HasCurrentQuote || !DispatchRequested || !CanConfirmCurrentQuote))
        { Error = "현재 견적의 처리처와 비용·정보 제공 조건을 확인하고 배송 요청을 선택해 주세요."; Changed(); return null; }
        var request = BuildRequest(); request.DispatchRequested = true;
        if (_submittedInput is null && MemberDeliveryRequested)
        {
            request.AutoFulfillment = null; request.FulfillmentSelection = null; request.DirectPaymentAgreed = DirectPaymentAgreed;
            request.MemberDeliveryNoticeVersion = NeighborhoodMemberDeliveryRoutes.NoticeVersion;
            Draft.AutoFulfillment = null; Draft.FulfillmentSelection = null; Draft.DirectPaymentAgreed = request.DirectPaymentAgreed;
        }
        else if (_submittedInput is null && AutoRoutingEnabled)
        {
            request.AutoFulfillment = AutoConditions.BuildConsent(); request.FulfillmentSelection = null;
            request.DirectPaymentAgreed = AutoConditions.DirectPaymentAgreed;
            Draft.AutoFulfillment = request.AutoFulfillment; Draft.FulfillmentSelection = null; Draft.DirectPaymentAgreed = request.DirectPaymentAgreed;
        }
        else if (_submittedInput is null && HasFulfillmentOptions && SelectedFulfillmentOption is { } option)
        {
            request.FulfillmentSelection = new() { ProviderId = option.ProviderId, QuoteId = option.QuoteId,
                ConfirmedCostKrw = option.TotalCostKrw!.Value, ProviderInformationSharingAgreed = ProviderInformationSharingAgreed,
                DirectPaymentAgreed = option.ProviderId == "internal" && DirectPaymentAgreed };
            request.DirectPaymentAgreed = option.ProviderId == "internal" && DirectPaymentAgreed;
            Draft.FulfillmentSelection = request.FulfillmentSelection; Draft.DirectPaymentAgreed = request.DirectPaymentAgreed;
        }
        else if (_submittedInput is null && request.FulfillmentSelection is null) request.DirectPaymentAgreed = true;
        request.AgreedFareKrw = _submittedInput is not null ? Draft.AgreedFareKrw : Quote!.Fare.최종운임;
        Draft.AgreedFareKrw = request.AgreedFareKrw;
        var input = Fingerprint(request);
        if (_submittedInput is not null && input != _submittedInput)
        { Error = "접수 확인 중인 요청의 입력을 바꿀 수 없습니다. 내 배송 의뢰를 먼저 확인해 주세요."; Changed(); return null; }
        var checkingOriginal = _submittedInput is not null;
        _submittedInput = input;
        _recoveryRequest ??= JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(request))!;
        var owner = _owner; var generation = ++_generation; IsSending = true; Changed();
        try
        {
            await PersistRecoveryAsync(owner!, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return null;
            var result = checkingOriginal ? await client.ReadSubmissionAsync(request.ClientRequestId, _lifetime.Token) : null;
            if (!OwnerMatches(generation, owner)) return null;
            RegistrationRecoveryPending = result?.ProposalStateCode == NeighborhoodDeliveryProposalStates.RegistrationPending;
            if (RegistrationRecoveryPending) result = null;
            result ??= await client.CreateAsync(request, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return null;
            if (result is null || string.IsNullOrWhiteSpace(result.RequestId))
            { Error = "접수 결과를 확인하지 못했습니다. 내 배송 의뢰를 먼저 확인해 주세요. 다시 제출할 때 같은 요청 번호를 사용합니다."; return null; }
            RegistrationRecoveryPending = result.ProposalStateCode == NeighborhoodDeliveryProposalStates.RegistrationPending;
            if (RegistrationRecoveryPending) { Error = RegistrationPendingNotice; return null; }
            Submitted = result;
            await PersistRecoveryAsync(owner!, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return null;
            if (_collaboration is not null && !await LinkSubmittedAsync(generation, owner)) return null;
            await RemoveRecoveryAsync(owner!, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return null;
            Draft = NewDraft(); Quote = null; _quotedInput = null; _submittedInput = null; _recoveryRequest = null; DispatchRequested = false; DirectPaymentAgreed = false; AutoRoutingEnabled = false; AutoConditions.Clear(); ResetFulfillmentChoice(); _simpleInput?.Completed();
            return result.RequestId;
        }
        catch (Exception ex)
        {
            if (OwnerMatches(generation, owner))
            {
                if (ex is SsalddelApiException { StatusCode: 400 or 422 }
                    or SsalddelApiException { ErrorCode: "QuoteChanged" or "FulfillmentQuoteExpired" or "FulfillmentQuoteChanged" or "FulfillmentQuoteMismatch" or "FulfillmentCostChanged" })
                {
                    try
                    {
                        await RemoveRecoveryAsync(owner!, _lifetime.Token);
                        if (!OwnerMatches(generation, owner)) return null;
                        _submittedInput = null; _recoveryRequest = null;
                    }
                    catch (Exception)
                    {
                        if (OwnerMatches(generation, owner))
                            Error = "거절된 원 요청의 보관 상태를 확인하지 못했습니다. 같은 요청 결과를 다시 확인해 주세요.";
                        return null;
                    }
                }
                ApplyError(ex);
            }
            return null;
        }
        finally { if (OwnerMatches(generation, owner)) { IsSending = false; Changed(); } }
    }

    public async Task<string?> RetryLinkAsync()
    {
        if (IsSending || !CheckOwner() || Submitted is null) return null;
        var owner = _owner; var generation = ++_generation; IsSending = true; Error = null; Changed();
        try
        {
            if (!await LinkSubmittedAsync(generation, owner)) return null;
            await RemoveRecoveryAsync(owner!, _lifetime.Token);
            return OwnerMatches(generation, owner) ? Submitted?.RequestId : null;
        }
        catch (Exception ex) { if (OwnerMatches(generation, owner)) ApplyError(ex); return null; }
        finally { if (OwnerMatches(generation, owner)) { IsSending = false; Changed(); } }
    }

    private async Task<bool> LinkSubmittedAsync(long generation, string? owner)
    {
        if (_collaborationId is null || Submitted?.CollaborationId == _collaborationId) return true;
        if (collaborations is null || _collaboration is null || Submitted is null) return false;
        try
        {
            // 배송이 이미 접수된 뒤에는 협업 연결만 재시도하며 새 배송 의뢰를 만들지 않습니다.
            _linkCommand ??= new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = _collaboration.Revision,
                Action = NeighborhoodCollaborationActions.LinkDelivery, LinkedDeliveryRequestId = Submitted.RequestId };
            await PersistRecoveryAsync(owner!, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return false;
            var linked = await collaborations.CommandAsync(_collaborationId, _linkCommand, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return false;
            if (linked?.LinkedDeliveryRequestId == Submitted.RequestId) { _collaboration = linked; return true; }
            Error = "배송은 접수됐지만 협업 연결을 확인하지 못했습니다. 아래에서 같은 배송 연결만 다시 확인해 주세요.";
        }
        catch (Exception ex)
        {
            if (!OwnerMatches(generation, owner)) return false;
            if (ex is SsalddelApiException { StatusCode: 409 } && ex is not SsalddelApiException { ErrorCode: "IdempotencyConflict" })
            {
                NeighborhoodCollaborationResponse? latest;
                try { latest = await collaborations.ReadAsync(_collaborationId, _lifetime.Token); }
                catch { Error = "배송은 접수됐습니다. 협업 상태를 다시 확인해 주세요."; Changed(); return false; }
                if (!OwnerMatches(generation, owner)) return false;
                if (latest?.LinkedDeliveryRequestId == Submitted.RequestId) { _collaboration = latest; return true; }
                if (latest?.AllowedActions.Contains(NeighborhoodCollaborationActions.LinkDelivery) == true)
                { _collaboration = latest; _linkCommand = null; }
            }
            Error = "배송은 접수됐습니다. 협업 연결은 확인이 필요하며 배송을 다시 만들지 않습니다.";
        }
        Changed(); return false;
    }

    private bool Validate()
    {
        Error = null;
        if (ConsentEvidenceId is null || !CollectionConsent || !AgeConfirmed) Error = "배송 정보의 수집·이용 동의를 먼저 확인해 주세요.";
        else if (string.IsNullOrWhiteSpace(Draft.CargoName) || Draft.CargoName.Length > 120
            || Draft.Quantity is < 1 or > 1000 || Draft.WeightKg <= 0 || Draft.WeightKg > 10000 || string.IsNullOrWhiteSpace(Draft.VehicleType))
            Error = "물건 이름, 수량, 전체 중량과 배송 차량을 확인해 주세요.";
        else if (!ValidLocation(Draft.Pickup) || !ValidLocation(Draft.Dropoff))
            Error = "픽업지와 전달지의 도로명 주소, 담당자 이름과 전화번호를 확인해 주세요.";
        else if (Draft.PackageSizeCode is not null && !NeighborhoodPackageSizes.IsKnown(Draft.PackageSizeCode))
            Error = "포장 크기는 작음·보통·큼 중에서 선택하거나 미입력으로 두세요.";
        else if (_simpleInput?.HasMissingTimes == true || _simpleInput is { IsActive: true, TimeReviewed: false } && !NeedsSubmissionReview)
            Error = "픽업·전달 시간창을 보완하고 실제 표시된 시각을 확인해 주세요.";
        else if (PickupEnd <= PickupStart || _simpleInput?.DropoffTimeUnspecified != true && (DropoffEnd <= DropoffStart || DropoffEnd < PickupStart))
            Error = "픽업과 전달 가능 시간을 확인해 주세요. 종료 시각은 시작 시각보다 뒤여야 합니다.";
        else if (Draft.Notes?.Length > 1000) Error = "배송 요청 사항은 1,000자 이내로 입력해 주세요.";
        if (Error is not null) Changed();
        return Error is null;
    }
    private static bool ValidLocation(LocationContactDTO location)
        => !string.IsNullOrWhiteSpace(location.주소.도로명주소) && location.주소.도로명주소.Length <= 300
           && location.주소.상세주소?.Length is not > 300 && !string.IsNullOrWhiteSpace(location.연락처.이름)
           && location.연락처.이름.Length <= 100 && !string.IsNullOrWhiteSpace(location.연락처.전화번호)
           && location.연락처.전화번호.Length <= 40;
    private NeighborhoodDeliveryRequest BuildRequest()
    {
        if (_recoveryRequest is not null) return JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(_recoveryRequest))!;
        var request = JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(Draft))!;
        request.PrivacyConsentEvidenceId = ConsentEvidenceId;
        request.DesiredCompletionUtc = DesiredCompletionLocal is { } desired ? DateTime.SpecifyKind(desired, DateTimeKind.Local).ToUniversalTime() : null;
        if (AutoRoutingEnabled) request.AutoFulfillment = AutoConditions.BuildConsent();
        request.Pickup.시간창 = _simpleInput?.IsTimeMissing("PickupStart") == true || _simpleInput?.IsTimeMissing("PickupEnd") == true ? null : Window(PickupStart, PickupEnd);
        request.Dropoff.시간창 = _simpleInput?.DropoffTimeUnspecified == true || _simpleInput?.IsTimeMissing("DropoffStart") == true || _simpleInput?.IsTimeMissing("DropoffEnd") == true ? null : Window(DropoffStart, DropoffEnd);
        return request;
    }
    private static TimeWindowDTO Window(DateTime start, DateTime end)
        => new() { 시작일시 = DateTime.SpecifyKind(start, DateTimeKind.Local).ToUniversalTime(), 종료일시 = DateTime.SpecifyKind(end, DateTimeKind.Local).ToUniversalTime() };
    private static string Fingerprint(NeighborhoodDeliveryRequest request) => JsonSerializer.Serialize(request);
    private static string QuoteFingerprint(NeighborhoodDeliveryRequest request)
    {
        request.FulfillmentSelection = null; request.AgreedFareKrw = null; request.DirectPaymentAgreed = false;
        request.MemberDeliveryDisclosureAgreed = false;
        if (request.AutoFulfillment is { } automatic)
        { automatic.ClientRequestId = Guid.Empty; automatic.ExpectedRevision = 0; automatic.AutomaticSubmissionAgreed = false; automatic.ProviderInformationSharingAgreed = false; automatic.DirectPaymentAgreed = false; }
        return Fingerprint(request);
    }
    private void ResetFulfillmentChoice()
    { SelectedFulfillmentQuoteId = null; FulfillmentCostAgreed = false; ProviderInformationSharingAgreed = false; }
    private bool CheckOwner()
    {
        if (_disposed) return false;
        SynchronizeOwner();
        if (IsAuthenticated) return true;
        Error = "배송 의뢰를 등록하려면 로그인해 주세요."; Changed(); return false;
    }
    private string? CurrentOwner => user.현재사용자.인증됨 ? user.현재사용자.UserId : null;
    private bool OwnerMatches(long generation, string? owner)
    {
        if (_disposed) return false;
        if (!string.Equals(owner, CurrentOwner, StringComparison.Ordinal)) { SynchronizeOwner(); return false; }
        return generation == _generation;
    }
    private void ApplyError(Exception ex)
    {
        Error = NeighborhoodDeliveryPresentation.Error(ex, writing: true);
        if (ex is SsalddelApiException { StatusCode: 401 })
        { ClearPrivateInput(); RequiresLogin = true; }
        if (ex is SsalddelApiException { ErrorCode: "QuoteChanged" or "FulfillmentQuoteExpired" or "FulfillmentQuoteChanged" or "FulfillmentQuoteMismatch" or "FulfillmentCostChanged" })
        { Quote = null; _quotedInput = null; _submittedInput = null; _recoveryRequest = null; DirectPaymentAgreed = false; Draft.FulfillmentSelection = null; ResetFulfillmentChoice(); }
    }
    private static NeighborhoodDeliveryRequest NewDraft() => new() { ClientRequestId = Guid.NewGuid(), DispatchMode = NeighborhoodDispatchModes.Automatic };
    private void ClearPrivateInput()
    {
        _recoveryLoaded = false; _expiredRecoveryId = null; _recoveryCreatedAtUtc = null; RecoveryCheckedMissing = false; RegistrationRecoveryPending = false;
        _simpleInput?.Clear();
        _pickupStart = DateTime.Now.AddMinutes(30); _pickupEnd = DateTime.Now.AddHours(2);
        _dropoffStart = DateTime.Now.AddMinutes(30); _dropoffEnd = DateTime.Now.AddHours(3);
        _recoveryRequest = null; Draft = NewDraft(); ConsentEvidenceId = null; _consentRequestId = Guid.NewGuid();
        CollectionConsent = false; AgeConfirmed = false; Quote = null; _quotedInput = null; _submittedInput = null;
        DispatchRequested = false; DirectPaymentAgreed = false; Submitted = null;
        DesiredCompletionLocal = null; AutoRoutingEnabled = false; AutoConditions.Clear(); ResetFulfillmentChoice();
        _collaborationId = null; _collaboration = null; _linkCommand = null; IsLoadingCollaboration = false;
    }
    private void Changed() => OnPropertyChanged(string.Empty);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; ++_generation; _lifetime.Cancel(); _lifetime.Dispose(); ClearPrivateInput(); _simpleInput?.Dispose();
    }
}
