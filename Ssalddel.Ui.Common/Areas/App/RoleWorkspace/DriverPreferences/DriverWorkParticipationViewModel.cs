using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverPreferences;

/// <summary>업무 희망 초안과 저장 확인만 소유합니다. 배차 자격과 수락 가능 여부는 서버가 판정합니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.ViewModel,
    "기사의 업무·적재 희망 초안을 저장하고 같은 계정·판본의 정본으로 확인",
    StepKey = "ui.driver-participation-settings", FlowOrder = 205,
    ExecutionStage = SsalddelCodeExecutionStage.Presentation,
    ReadsFrom = SsalddelCodeDataScope.ClientPresentation | SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.ClientPresentation | SsalddelCodeDataScope.OperationalState,
    Effects = SsalddelCodeEffect.UiStateMutation | SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    Boundary = "배차 자격·권한·수락은 서버가 결정하며 선택 설정으로 역할이나 운영 활성화를 만들지 않는다.")]
public sealed class DriverWorkParticipationViewModel : IDisposable
{
    private readonly IDriverWorkParticipationClient client;
    private CancellationTokenSource lifetime = new();
    private DriverWorkParticipationIdentity? owner;
    private DriverWorkParticipationResponse? confirmed;
    private DriverWorkParticipationUpdate? pending;
    private long generation;
    private bool disposed;

    public DriverWorkParticipationViewModel(IDriverWorkParticipationClient client)
    {
        this.client = client;
        RequiresLogin = !client.Identity.IsAuthenticated;
        client.SessionChanged += AuthenticationChanged;
    }

    public event Action? Changed;
    public bool IsBusy { get; private set; }
    public bool IsLoaded { get; private set; }
    public bool RequiresLogin { get; private set; }
    public bool NeedsResultCheck { get; private set; }
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public bool FoodDelivery { get; set; }
    public bool QuickDelivery { get; set; }
    public bool CargoTransport { get; set; }
    public bool DirectPicking { get; set; }
    public string VehicleKind { get; set; } = string.Empty;
    public int MaxConcurrentJobs { get; set; } = 1;
    public decimal? CapacityKg { get; set; }
    public decimal? CapacityLitres { get; set; }
    public bool FoodCargoSeparationConfirmed { get; set; }
    public bool ReceiveAdditionalOffers { get; set; } = true;
    public int OperationalMaxConcurrentJobs => confirmed?.OperationalMaxConcurrentJobs ?? 1;
    public bool SharedAdmissionEnabled => confirmed?.SharedAdmissionEnabled == true;
    public bool FoodEligible => confirmed?.FoodEligible == true;
    public bool CargoEligible => confirmed?.CargoEligible == true;
    public bool CanLoad => !disposed && !IsBusy && client.Identity.IsAuthenticated && !string.IsNullOrWhiteSpace(client.Identity.DriverId);
    public bool CanEdit => !disposed && IsLoaded && !IsBusy && !NeedsResultCheck && IsCurrent(owner, lifetime.Token);
    public long? ConfirmedRevision => confirmed?.Settings.Revision;

    public async Task LoadAsync()
    {
        if (!CanLoad) return;
        var identity = client.Identity;
        var token = lifetime.Token;
        var operation = ++generation;
        owner = identity;
        IsBusy = true; IsLoaded = false; Error = null; Message = "설정을 확인하고 있습니다.";
        RequiresLogin = false; Notify();
        try
        {
            var response = await client.GetAsync(token);
            if (!CurrentOperation(identity, token, operation)) return;
            Validate(response, identity);
            if (response.Settings.Revision < (pending?.ExpectedRevision ?? confirmed?.Settings.Revision ?? 0))
                throw new InvalidDataException("이전 판본의 설정입니다.");
            var resultMessage = pending is { } sent
                ? response.Settings.Revision == sent.ExpectedRevision + 1 && SameTerms(response.Settings, sent.Settings)
                    ? "저장한 업무·적재 설정을 확인했습니다."
                    : response.Settings.Revision == sent.ExpectedRevision
                        ? "변경 전 설정이 확인되었습니다. 필요한 내용을 다시 선택한 뒤 저장해 주세요."
                        : "다른 화면에서 변경되었거나 저장 내용이 다른 최신 설정을 확인했습니다. 내용을 확인해 주세요."
                : "받을 업무와 적재 조건을 선택한 뒤 저장해 주세요.";
            Apply(response); pending = null;
            NeedsResultCheck = false;
            Message = resultMessage;
        }
        catch (OperationCanceledException) when (!CurrentOperation(identity, token, operation)) { }
        catch
        {
            if (CurrentOperation(identity, token, operation))
            { Error = "설정을 확인하지 못했습니다. 다시 확인해 주세요."; Message = null; }
        }
        finally
        {
            FinishOperation(identity, token, operation);
        }
    }

    public async Task SaveAsync()
    {
        if (!CanEdit || confirmed is null || owner is null) return;
        if (!DriverWorkParticipationSettingsRules.ValidMeasure(CapacityKg, DriverWorkParticipationSettingsRules.MaxCapacityKg)
            || !DriverWorkParticipationSettingsRules.ValidMeasure(CapacityLitres, DriverWorkParticipationSettingsRules.MaxCapacityLitres))
        {
            Error = "중량은 0 초과 50,000kg 이하, 부피는 0 초과 1,000,000L 이하로 소수 3자리까지 입력해 주세요. 모르면 빈칸으로 두세요.";
            Message = null; Notify(); return;
        }
        if (!DriverVehicleKinds.IsKnown(VehicleKind) || MaxConcurrentJobs < 1 || MaxConcurrentJobs > OperationalMaxConcurrentJobs
            || DirectPicking && !QuickDelivery && !CargoTransport)
        {
            Error = "차량·동시 업무 수·적재 조건을 확인해 주세요. 직접 피킹은 소형 물품이나 화물을 선택해야 합니다.";
            Message = null; Notify(); return;
        }
        var identity = owner;
        var token = lifetime.Token;
        var operation = ++generation;
        var expected = confirmed.Settings.Revision;
        var request = new DriverWorkParticipationUpdate
        {
            ExpectedRevision = expected,
            Settings = new()
            {
                Revision = expected, FoodDelivery = FoodDelivery, QuickDelivery = QuickDelivery, CargoTransport = CargoTransport,
                DirectPicking = DirectPicking, VehicleKind = VehicleKind, MaxConcurrentJobs = MaxConcurrentJobs,
                CapacityKg = CapacityKg, CapacityLitres = CapacityLitres,
                FoodCargoSeparationConfirmed = FoodCargoSeparationConfirmed, ReceiveAdditionalOffers = ReceiveAdditionalOffers
            }
        };
        pending = request;
        IsBusy = true; NeedsResultCheck = true; Error = null; Message = "설정을 저장하고 있습니다."; Notify();
        try
        {
            var written = await client.UpdateAsync(request, token);
            if (!CurrentOperation(identity, token, operation)) return;
            Validate(written, identity);
            if (written.Settings.Revision != expected + 1 || !SameTerms(written.Settings, request.Settings))
                throw new InvalidDataException("저장한 설정의 내용이 일치하지 않습니다.");
            var read = await client.GetAsync(token);
            if (!CurrentOperation(identity, token, operation)) return;
            Validate(read, identity);
            if (read.Settings.Revision < written.Settings.Revision
                || read.Settings.Revision == written.Settings.Revision && !SameTerms(read.Settings, written.Settings))
                throw new InvalidDataException("저장 결과를 확인하지 못했습니다.");
            Apply(read); pending = null; NeedsResultCheck = false;
            Message = read.Settings.Revision == written.Settings.Revision
                ? "저장한 업무·적재 설정을 확인했습니다. 현재 업무는 그대로 이어집니다."
                : "다른 화면에서 변경된 최신 설정을 확인했습니다.";
        }
        catch (OperationCanceledException) when (!CurrentOperation(identity, token, operation)) { }
        catch
        {
            if (CurrentOperation(identity, token, operation))
            {
                IsLoaded = false; confirmed = null; Message = null;
                Error = "저장 결과를 확인하지 못했습니다. 다시 전송하지 말고 설정을 다시 확인해 주세요.";
            }
        }
        finally
        {
            FinishOperation(identity, token, operation);
        }
    }

    private static bool SameTerms(DriverWorkParticipationSettings left, DriverWorkParticipationSettings right)
        => (left with { Revision = 0 }) == (right with { Revision = 0 });

    private static void Validate(DriverWorkParticipationResponse response, DriverWorkParticipationIdentity identity)
    {
        if (response is null || !string.Equals(response.DriverId, identity.DriverId, StringComparison.Ordinal)
            || response.Settings is not { } value || value.Revision < 0
            || !DriverVehicleKinds.IsKnown(value.VehicleKind) && !(value.Revision == 0 && string.IsNullOrEmpty(value.VehicleKind))
            || response.OperationalMaxConcurrentJobs is < 1 or > 10 || value.MaxConcurrentJobs is < 1 or > 10
            || !DriverWorkParticipationSettingsRules.ValidMeasure(value.CapacityKg, DriverWorkParticipationSettingsRules.MaxCapacityKg)
            || !DriverWorkParticipationSettingsRules.ValidMeasure(value.CapacityLitres, DriverWorkParticipationSettingsRules.MaxCapacityLitres)
            || value.DirectPicking && !value.QuickDelivery && !value.CargoTransport)
            throw new InvalidDataException("설정의 대상 또는 내용을 확인하지 못했습니다.");
    }

    private void Apply(DriverWorkParticipationResponse value)
    {
        confirmed = value; var settings = value.Settings;
        FoodDelivery = settings.FoodDelivery; QuickDelivery = settings.QuickDelivery; CargoTransport = settings.CargoTransport;
        DirectPicking = settings.DirectPicking; VehicleKind = settings.VehicleKind; MaxConcurrentJobs = settings.MaxConcurrentJobs;
        CapacityKg = settings.CapacityKg; CapacityLitres = settings.CapacityLitres;
        FoodCargoSeparationConfirmed = settings.FoodCargoSeparationConfirmed; ReceiveAdditionalOffers = settings.ReceiveAdditionalOffers;
        IsLoaded = true;
    }

    private bool IsCurrent(DriverWorkParticipationIdentity? identity, CancellationToken token)
        => !disposed && !token.IsCancellationRequested && identity is { IsAuthenticated: true }
            && identity == client.Identity && !string.IsNullOrWhiteSpace(identity.DriverId);
    private bool CurrentOperation(DriverWorkParticipationIdentity identity, CancellationToken token, long operation)
        => generation == operation && IsCurrent(identity, token);
    private void FinishOperation(DriverWorkParticipationIdentity identity, CancellationToken token, long operation)
    {
        if (disposed || generation != operation) return;
        if (!IsCurrent(identity, token)) { AuthenticationChanged(); return; }
        IsBusy = false; Notify();
    }
    private void AuthenticationChanged()
    {
        if (disposed || owner == client.Identity && client.Identity.IsAuthenticated) return;
        ++generation; lifetime.Cancel(); lifetime.Dispose(); lifetime = new(); owner = null; confirmed = null; pending = null;
        IsBusy = IsLoaded = NeedsResultCheck = false; RequiresLogin = !client.Identity.IsAuthenticated;
        FoodDelivery = QuickDelivery = CargoTransport = DirectPicking = FoodCargoSeparationConfirmed = false;
        VehicleKind = string.Empty; MaxConcurrentJobs = 1; CapacityKg = CapacityLitres = null; ReceiveAdditionalOffers = true;
        Error = null; Message = "현재 계정으로 설정을 확인해 주세요."; Notify();
    }
    private void Notify() => Changed?.Invoke();
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; ++generation; client.SessionChanged -= AuthenticationChanged;
        lifetime.Cancel(); lifetime.Dispose();
    }
}
