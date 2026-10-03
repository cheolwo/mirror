using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed partial class 운송의뢰초안원장ViewModel(I출고예정검토페이지Service service) : 업무작업ViewModelBase
{
    private long _queryRevision;
    [ObservableProperty] public partial long? 조회대상Id { get; private set; }
    [ObservableProperty] public partial 출고예정검토상세응답? 원장 { get; private set; }
    [ObservableProperty] public partial bool 대상없음 { get; private set; }
    [ObservableProperty] public partial bool 기사신원확인 { get; set; }
    [ObservableProperty] public partial bool 등록차량확인 { get; set; }
    [ObservableProperty] public partial bool 상품인계확인 { get; set; }
    [ObservableProperty] public partial string 인계메모 { get; set; } = string.Empty;
    [ObservableProperty] public partial 출고운송인계완료응답? 인계완료결과 { get; private set; }

    public void 조회대상설정(long? id)
    {
        Interlocked.Increment(ref _queryRevision);
        조회대상Id = id is > 0 ? id : null;
        원장 = null;
        대상없음 = false;
        인계확인초기화();
        작업상태초기화();
    }

    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
    {
        if (조회대상Id is not > 0)
            return Task.FromResult(유효성실패("출고 운송의뢰를 작성할 출고예정 원장을 선택해 주세요."));

        원장 = null;
        대상없음 = false;
        var targetId = 조회대상Id.Value;
        var revision = Interlocked.Read(ref _queryRevision);
        return 작업실행Async(
            async token =>
            {
                var result = await service.상세조회Async(targetId, token);
                if (revision != Interlocked.Read(ref _queryRevision) || 조회대상Id != targetId)
                    return;
                원장 = result?.OutboundPlanId == targetId ? result : null;
                대상없음 = 원장 is null;
            },
            "선택한 출고예정 원장을 같은 ID로 조회했습니다.",
            cancellationToken,
            ex => $"출고 운송의뢰의 출고예정 원장을 조회하지 못했습니다. {ex.Message}");
    }

    public Task<bool> 인계완료Async(CancellationToken cancellationToken = default)
    {
        if (원장 is not { CanCompleteHandoff: true } plan)
            return Task.FromResult(유효성실패("기사 수락과 등록 차량이 서버 원장에서 확인된 뒤에만 출고할 수 있습니다."));
        if (!기사신원확인 || !등록차량확인 || !상품인계확인)
            return Task.FromResult(유효성실패("기사 신원, 등록 차량, 상품 인계를 모두 현장에서 확인해 주세요."));

        return 작업실행Async(
            async token =>
            {
                인계완료결과 = await service.인계완료Async(
                    plan.OutboundPlanId,
                    new 출고운송인계완료요청
                    {
                        DriverIdentityConfirmed = 기사신원확인,
                        VehicleConfirmed = 등록차량확인,
                        CargoReleasedConfirmed = 상품인계확인,
                        Memo = 인계메모
                    },
                    token);
                원장 = await service.상세조회Async(plan.OutboundPlanId, token)
                    ?? throw new InvalidOperationException("인계 완료 후 같은 출고예정 원장을 다시 조회할 수 없습니다.");
                대상없음 = false;
                기사신원확인 = false;
                등록차량확인 = false;
                상품인계확인 = false;
                인계메모 = string.Empty;
            },
            "기사와 차량 확인 뒤 출고 인계를 완료하고 같은 원장을 다시 조회했습니다.",
            cancellationToken,
            ex => $"출고 운송 인계를 완료하지 못했습니다. {ex.Message}");
    }

    private void 인계확인초기화()
    {
        기사신원확인 = false;
        등록차량확인 = false;
        상품인계확인 = false;
        인계메모 = string.Empty;
        인계완료결과 = null;
    }
}

public sealed record 운송의뢰저장전검토결과(
    long OutboundPlanId,
    string ReviewReference,
    string DestinationAddress,
    string DestinationAddressDetail,
    DateTime PickupAt,
    DateTime ArrivalAt,
    string VehicleType,
    string HandlingNote,
    string DestinationContactName = "",
    string DestinationContactPhone = "");

public sealed partial class 운송의뢰초안작성ViewModel : ObservableObject
{
    public static IReadOnlyList<string> 차량유형목록 { get; } = ["1톤 카고", "1톤 냉장탑차", "2.5톤 카고", "2.5톤 냉장탑차", "냉동탑차"];

    [ObservableProperty] public partial 출고예정검토상세응답? 원장 { get; private set; }
    [ObservableProperty] public partial string 하차지주소 { get; set; } = string.Empty;
    [ObservableProperty] public partial string 하차지상세주소 { get; set; } = string.Empty;
    [ObservableProperty] public partial string 하차담당자명 { get; set; } = string.Empty;
    [ObservableProperty] public partial string 하차연락처 { get; set; } = string.Empty;
    [ObservableProperty] public partial DateTime? 희망상차일 { get; set; }
    [ObservableProperty] public partial TimeSpan? 희망상차시각 { get; set; }
    [ObservableProperty] public partial DateTime? 희망도착일 { get; set; }
    [ObservableProperty] public partial TimeSpan? 희망도착시각 { get; set; }
    [ObservableProperty] public partial string 차량유형 { get; set; } = string.Empty;
    [ObservableProperty] public partial string 취급메모 { get; set; } = string.Empty;
    [ObservableProperty] public partial bool 상품수량확인 { get; set; }
    [ObservableProperty] public partial string? 검증오류 { get; private set; }
    [ObservableProperty] public partial 운송의뢰저장전검토결과? 검토결과 { get; private set; }

    public bool 작성가능 => 원장?.CanStartTransportRequestDraft == true;
    public bool 검토완료 => 검토결과 is not null;

    public void 원장설정(출고예정검토상세응답? plan)
    {
        원장 = plan;
        하차지주소 = string.Empty;
        하차지상세주소 = string.Empty;
        하차담당자명 = string.Empty;
        하차연락처 = string.Empty;
        희망상차일 = DateTime.Today.AddDays(1);
        희망상차시각 = new TimeSpan(9, 0, 0);
        희망도착일 = DateTime.Today.AddDays(1);
        희망도착시각 = new TimeSpan(11, 0, 0);
        차량유형 = string.Empty;
        취급메모 = string.Empty;
        상품수량확인 = false;
        검토초기화();
        OnPropertyChanged(nameof(작성가능));
    }

    public void 저장후원장갱신(출고예정검토상세응답? plan)
    {
        원장 = plan;
        OnPropertyChanged(nameof(작성가능));
    }

    public bool 입력값검토()
    {
        검토초기화();
        if (!작성가능 || 원장 is null)
            return 실패("출고예정 검토 조건을 먼저 충족해 주세요.");

        var destination = 하차지주소.Trim();
        if (destination.Length is < 5 or > 200
            || destination.StartsWith("주문자:", StringComparison.OrdinalIgnoreCase))
        {
            return 실패("실제 하차지 도로명 주소를 5자 이상 200자 이하로 입력해 주세요.");
        }
        if (하차지상세주소.Trim().Length > 200)
            return 실패("하차지 상세 주소는 200자 이하로 입력해 주세요.");
        if (하차담당자명.Trim().Length > 100 || 하차연락처.Trim().Length > 50)
            return 실패("하차 담당자는 100자, 연락처는 50자 이하로 입력해 주세요.");
        if (희망상차일 is null || 희망상차시각 is null || 희망도착일 is null || 희망도착시각 is null)
            return 실패("희망 상차·도착 일시를 모두 입력해 주세요.");

        var pickupAt = 희망상차일.Value.Date + 희망상차시각.Value;
        var arrivalAt = 희망도착일.Value.Date + 희망도착시각.Value;
        if (arrivalAt <= pickupAt)
            return 실패("희망 도착 일시는 희망 상차 일시보다 뒤여야 합니다.");
        if (!차량유형목록.Contains(차량유형, StringComparer.Ordinal))
            return 실패("지원하는 차량 유형을 선택해 주세요.");
        if (취급메모.Trim().Length > 300)
            return 실패("취급 메모는 300자 이하로 입력해 주세요.");
        if (!상품수량확인)
            return 실패("표시된 상품과 출고 수량을 확인해 주세요.");

        검토결과 = new 운송의뢰저장전검토결과(
            원장.OutboundPlanId,
            $"OUT-{원장.OutboundPlanId:D6}-REVIEW",
            destination,
            하차지상세주소.Trim(),
            pickupAt,
            arrivalAt,
            차량유형,
            취급메모.Trim(),
            하차담당자명.Trim(),
            하차연락처.Trim());
        OnPropertyChanged(nameof(검토완료));
        return true;
    }

    public void 다시수정() => 검토초기화();

    partial void On하차지주소Changed(string value) => 검토초기화();
    partial void On하차지상세주소Changed(string value) => 검토초기화();
    partial void On하차담당자명Changed(string value) => 검토초기화();
    partial void On하차연락처Changed(string value) => 검토초기화();
    partial void On희망상차일Changed(DateTime? value) => 검토초기화();
    partial void On희망상차시각Changed(TimeSpan? value) => 검토초기화();
    partial void On희망도착일Changed(DateTime? value) => 검토초기화();
    partial void On희망도착시각Changed(TimeSpan? value) => 검토초기화();
    partial void On차량유형Changed(string value) => 검토초기화();
    partial void On취급메모Changed(string value) => 검토초기화();
    partial void On상품수량확인Changed(bool value) => 검토초기화();

    private bool 실패(string message)
    {
        검증오류 = message;
        return false;
    }

    private void 검토초기화()
    {
        검증오류 = null;
        검토결과 = null;
        OnPropertyChanged(nameof(검토완료));
    }
}

public sealed partial class 운송의뢰초안저장ViewModel(I입출고작업Service service) : 업무작업ViewModelBase
{
    [ObservableProperty] public partial 화주운송의뢰응답? 저장결과 { get; private set; }

    public void 초기화()
    {
        저장결과 = null;
        작업상태초기화();
    }

    public Task<bool> 저장Async(
        출고예정검토상세응답 plan,
        운송의뢰저장전검토결과 draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(draft);

        if (plan.InboundItemId is not > 0)
            return Task.FromResult(유효성실패("출고예정 원장에 연결된 입고상품을 확인할 수 없습니다."));
        if (plan.OutboundPlanId != draft.OutboundPlanId)
            return Task.FromResult(유효성실패("검토한 초안과 현재 출고예정 원장이 일치하지 않습니다."));

        저장결과 = null;
        return 작업실행Async(
            async token =>
            {
                저장결과 = await service.운송인계Async(
                    new 재고운송의뢰생성요청
                    {
                        출고예정Id = plan.OutboundPlanId,
                        입고상품Id = plan.InboundItemId.Value,
                        요청수량 = plan.Quantity,
                        하차지주소 = draft.DestinationAddress,
                        하차지상세주소 = draft.DestinationAddressDetail,
                        하차담당자명 = draft.DestinationContactName,
                        하차연락처 = draft.DestinationContactPhone,
                        화물종류 = plan.ProductName,
                        차량종류 = draft.VehicleType,
                        희망상차일시 = ToUtc(draft.PickupAt),
                        희망도착일시 = ToUtc(draft.ArrivalAt),
                        취급메모 = draft.HandlingNote
                    },
                    token)
                    ?? throw new InvalidOperationException("출고 운송 인계 응답이 비어 있습니다.");
            },
            "출고예정 원장에 운송의뢰를 연결했습니다.",
            cancellationToken,
            ex => $"운송의뢰를 저장하지 못했습니다. {ex.Message}");
    }

    private static DateTime ToUtc(DateTime koreanTime)
        => new DateTimeOffset(DateTime.SpecifyKind(koreanTime, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).UtcDateTime;
}

public sealed partial class 운송의뢰초안PageViewModel : 조립ViewModelBase
{
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private long _initializationRevision;
    public 운송의뢰초안PageViewModel(
        운송의뢰초안원장ViewModel ledger,
        운송의뢰초안작성ViewModel draft,
        운송의뢰초안저장ViewModel save)
    {
        원장 = 하위ViewModel등록(ledger);
        초안 = 하위ViewModel등록(draft);
        저장 = 하위ViewModel등록(save);
    }

    public 운송의뢰초안원장ViewModel 원장 { get; }
    public 운송의뢰초안작성ViewModel 초안 { get; }
    public 운송의뢰초안저장ViewModel 저장 { get; }
    [ObservableProperty] public partial bool 초기화됨 { get; private set; }

    public async Task<bool> 초기화Async(long? outboundPlanId, CancellationToken cancellationToken = default)
    {
        var revision = Interlocked.Increment(ref _initializationRevision);
        초기화됨 = false;
        저장.초기화();
        원장.조회대상설정(outboundPlanId);
        초안.원장설정(null);
        var entered = false;
        try
        {
            await _initializationGate.WaitAsync(cancellationToken);
            entered = true;
            if (revision != Interlocked.Read(ref _initializationRevision)) return false;
            if (outboundPlanId is not > 0) return true;

            var loaded = await 원장.조회Async(cancellationToken);
            if (revision != Interlocked.Read(ref _initializationRevision)) return false;
            초안.원장설정(원장.원장);
            return loaded && 원장.원장?.OutboundPlanId == outboundPlanId;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            if (revision == Interlocked.Read(ref _initializationRevision)) 초기화됨 = true;
            if (entered) _initializationGate.Release();
        }
    }

    public async Task<bool> 서버저장Async(CancellationToken cancellationToken = default)
    {
        if (원장.원장 is not { } plan || 초안.검토결과 is not { } draft)
            return false;

        var saved = await 저장.저장Async(plan, draft, cancellationToken);
        if (!saved) return false;

        var reloaded = await 원장.조회Async(cancellationToken);
        if (reloaded)
        {
            초안.저장후원장갱신(원장.원장);
        }
        return true;
    }

    public async Task<bool> 서버인계완료Async(CancellationToken cancellationToken = default)
    {
        var completed = await 원장.인계완료Async(cancellationToken);
        if (completed)
        {
            초안.저장후원장갱신(원장.원장);
        }
        return completed;
    }
}
