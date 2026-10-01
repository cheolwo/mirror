using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App;
using Hongdal.Ui.Common.Areas.App.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record WarehouseProxySourceNode(
    string Title,
    string GroupLabel,
    string Description);

public sealed record WarehouseProxyCandidate(
    string Key,
    long? WarehouseId,
    string Name,
    string ScopeLabel,
    string ProxyTypeCode,
    string ProxyTypeLabel,
    string Address,
    string Description,
    bool IsWorkspaceWarehouse);

public sealed class PlatformCommunityWarehouseProxyDraft : ObservableObject
{
    private string _supplierCode = string.Empty;
    private string _supplierName = string.Empty;
    private string _orderReference = string.Empty;
    private DateTime? _expectedArrivalDate = DateTime.Today.AddDays(1);
    private string _contractNo = string.Empty;
    private string _contractType = 입고계약유형코드.보관대행;
    private string _contractCounterpartyName = string.Empty;
    private string _contractSettlementType = "보관료/작업비 협의";
    private decimal _contractCommissionRate;
    private decimal _contractDailyStorageFee;
    private string _notes = string.Empty;

    public string SupplierCode { get => _supplierCode; set => SetProperty(ref _supplierCode, value ?? string.Empty); }
    public string SupplierName { get => _supplierName; set => SetProperty(ref _supplierName, value ?? string.Empty); }
    public string OrderReference { get => _orderReference; set => SetProperty(ref _orderReference, value ?? string.Empty); }
    public DateTime? ExpectedArrivalDate { get => _expectedArrivalDate; set => SetProperty(ref _expectedArrivalDate, value); }
    public string ContractNo { get => _contractNo; set => SetProperty(ref _contractNo, value ?? string.Empty); }
    public string ContractType { get => _contractType; set => SetProperty(ref _contractType, value ?? 입고계약유형코드.보관대행); }
    public string ContractCounterpartyName { get => _contractCounterpartyName; set => SetProperty(ref _contractCounterpartyName, value ?? string.Empty); }
    public string ContractSettlementType { get => _contractSettlementType; set => SetProperty(ref _contractSettlementType, value ?? string.Empty); }
    public decimal ContractCommissionRate { get => _contractCommissionRate; set => SetProperty(ref _contractCommissionRate, value); }
    public decimal ContractDailyStorageFee { get => _contractDailyStorageFee; set => SetProperty(ref _contractDailyStorageFee, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value ?? string.Empty); }

    public void Fill(WarehouseProxySourceNode? sourceNode, WarehouseProxyCandidate candidate)
    {
        SupplierCode = "DIAGRAM-SUPPLIER";
        SupplierName = "다이어그램 물류 대행 신청";
        OrderReference = $"DIAGRAM-{DateTime.Today:yyyyMMdd}";
        ExpectedArrivalDate = DateTime.Today.AddDays(1);
        ContractNo = string.Empty;
        ContractType = ResolveContractType(candidate.ProxyTypeCode);
        ContractCounterpartyName = candidate.Name;
        ContractSettlementType = "보관료/작업비 협의";
        ContractCommissionRate = 0m;
        ContractDailyStorageFee = 0m;
        Notes = BuildNotes(sourceNode, candidate);
    }

    public void Reset()
    {
        SupplierCode = string.Empty;
        SupplierName = string.Empty;
        OrderReference = string.Empty;
        ExpectedArrivalDate = DateTime.Today.AddDays(1);
        ContractNo = string.Empty;
        ContractType = 입고계약유형코드.보관대행;
        ContractCounterpartyName = string.Empty;
        ContractSettlementType = "보관료/작업비 협의";
        ContractCommissionRate = 0m;
        ContractDailyStorageFee = 0m;
        Notes = string.Empty;
    }

    private static string ResolveContractType(string proxyType)
        => LogisticsProxySiteTypes.Normalize(proxyType) switch
        {
            LogisticsProxySiteTypes.MarketFulfillment => 입고계약유형코드.마켓풀필먼트,
            LogisticsProxySiteTypes.OverseasCustomsAgency => 입고계약유형코드.수입통관풀필먼트,
            _ => 입고계약유형코드.보관대행
        };

    private static string BuildNotes(
        WarehouseProxySourceNode? sourceNode,
        WarehouseProxyCandidate candidate)
        => string.Join(Environment.NewLine, new[]
        {
            "다이어그램 창고 블록에서 시작한 판매자 물류 대행 신청입니다.",
            $"- 창고 후보: {candidate.Name}",
            $"- 물류 대행 유형: {candidate.ProxyTypeLabel}",
            $"- 후보 범위: {candidate.ScopeLabel}",
            $"- 다이어그램 노드: {sourceNode?.Title ?? "창고 블록"}",
            $"- 블록 그룹: {sourceNode?.GroupLabel ?? string.Empty}",
            $"- 노드 설명: {sourceNode?.Description ?? string.Empty}",
            "- 보완 필요: 품목, 수량, 보관 기간, 피킹/포장/출고 대행 필요 여부"
        });
}

public sealed class PlatformCommunityWarehouseProxyViewModel : ObservableObject
{
    private static readonly IReadOnlyList<WarehouseProxyCandidate> DefaultCandidates =
    [
        new(
            "default:own",
            null,
            "내 기본 창고",
            "내 창고 후보",
            LogisticsProxySiteTypes.DeliveryAgency,
            LogisticsProxySiteTypes.GetDisplayName(LogisticsProxySiteTypes.DeliveryAgency),
            "주소 미정",
            "내가 관리하는 창고에 입고, 보관, 출고 대행을 맡기는 신청서로 이동합니다.",
            false),
        new(
            "default:shared-nearby",
            null,
            "가까운 공유 창고",
            "다른 사용자 창고 후보",
            LogisticsProxySiteTypes.DeliveryAgency,
            LogisticsProxySiteTypes.GetDisplayName(LogisticsProxySiteTypes.DeliveryAgency),
            "배송권 기준 조회 필요",
            "다른 사용자가 공개한 가까운 창고에 물류 대행 가능 여부를 신청서로 작성합니다.",
            false),
        new(
            "default:market-fulfillment",
            null,
            "마켓 물류 대행 창고",
            "판매채널 대행 후보",
            LogisticsProxySiteTypes.MarketFulfillment,
            LogisticsProxySiteTypes.GetDisplayName(LogisticsProxySiteTypes.MarketFulfillment),
            "스마트스토어/쿠팡 출고권역 기준 조회 필요",
            "스마트스토어, 쿠팡 같은 판매채널 주문의 입고, 피킹, 포장, 출고 대행을 신청서로 작성합니다.",
            false)
    ];

    private readonly IServiceProvider _services;
    private readonly List<WarehouseProxyCandidate> _candidates = [];
    private WarehouseProxySourceNode? _sourceNode;
    private string? _selectedCandidateKey;
    private bool _isLoading;
    private bool _isSubmitting;
    private string? _message;
    private Severity _messageSeverity = Severity.Info;

    public PlatformCommunityWarehouseProxyViewModel(IServiceProvider services)
    {
        _services = services;
        Draft.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanSubmit));
    }

    public static IReadOnlyList<string> ContractTypeOptions { get; } =
    [
        입고계약유형코드.보관대행,
        입고계약유형코드.위탁판매,
        입고계약유형코드.마켓풀필먼트,
        입고계약유형코드.수입통관풀필먼트
    ];

    public PlatformCommunityWarehouseProxyDraft Draft { get; } = new();
    public IReadOnlyList<WarehouseProxyCandidate> Candidates => _candidates;
    public WarehouseProxySourceNode? SourceNode { get => _sourceNode; private set => SetProperty(ref _sourceNode, value); }
    public string? SelectedCandidateKey { get => _selectedCandidateKey; private set => SetProperty(ref _selectedCandidateKey, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool IsSubmitting { get => _isSubmitting; private set { if (SetProperty(ref _isSubmitting, value)) OnPropertyChanged(nameof(CanSubmit)); } }
    public string? Message { get => _message; private set => SetProperty(ref _message, value); }
    public Severity MessageSeverity { get => _messageSeverity; private set => SetProperty(ref _messageSeverity, value); }

    public WarehouseProxyCandidate? SelectedCandidate
        => string.IsNullOrWhiteSpace(SelectedCandidateKey)
            ? null
            : _candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, SelectedCandidateKey, StringComparison.OrdinalIgnoreCase));

    public bool CanSubmit
        => !IsSubmitting
           && SelectedCandidate?.WarehouseId is not null
           && _services.GetService<IWarehouseWorkspaceService>() is not null
           && !string.IsNullOrWhiteSpace(Draft.SupplierName)
           && !string.IsNullOrWhiteSpace(Draft.OrderReference);

    public async Task OpenAsync(
        WarehouseProxySourceNode sourceNode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceNode);
        SourceNode = sourceNode;
        Message = null;
        MessageSeverity = Severity.Info;
        await LoadCandidatesAsync(cancellationToken);
    }

    public void Close()
    {
        SourceNode = null;
        SelectedCandidateKey = null;
        Message = null;
        _candidates.Clear();
        Draft.Reset();
        OnPropertyChanged(nameof(Candidates));
        OnPropertyChanged(nameof(SelectedCandidate));
        OnPropertyChanged(nameof(CanSubmit));
    }

    public void SelectCandidate(string candidateKey)
    {
        var candidate = _candidates.FirstOrDefault(item =>
            string.Equals(item.Key, candidateKey, StringComparison.OrdinalIgnoreCase));
        if (candidate is null)
        {
            SetMessage("선택한 창고 후보를 찾지 못했습니다.", Severity.Warning);
            return;
        }

        SelectedCandidateKey = candidate.Key;
        Draft.Fill(SourceNode, candidate);
        SetMessage(
            candidate.WarehouseId is null
                ? "이 후보는 아직 실제 창고가 아니므로 업무 화면에서 창고 등록 후 신청을 이어가세요."
                : "이 창고 후보로 다이어그램 안에서 입고/물류 대행 요청을 등록할 수 있습니다.",
            candidate.WarehouseId is null ? Severity.Info : Severity.Success);
        OnPropertyChanged(nameof(SelectedCandidate));
        OnPropertyChanged(nameof(CanSubmit));
    }

    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        var sourceNode = SourceNode;
        var candidate = SelectedCandidate;
        if (sourceNode is null || candidate is null)
        {
            SetMessage("물류 대행을 신청할 창고 블록과 후보를 먼저 선택하세요.", Severity.Warning);
            return;
        }

        if (candidate.WarehouseId is null)
        {
            SetMessage("실제 창고 ID가 없는 후보입니다. 업무 화면에서 창고를 등록한 뒤 신청을 이어가세요.", Severity.Warning);
            return;
        }

        var warehouseService = _services.GetService<IWarehouseWorkspaceService>();
        if (warehouseService is null)
        {
            SetMessage("현재 화면에는 창고 업무 서비스가 연결되어 있지 않습니다. 업무 화면에서 신청서를 작성하세요.", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Draft.SupplierName)
            || string.IsNullOrWhiteSpace(Draft.OrderReference))
        {
            SetMessage("공급처명과 원주문 참조번호를 입력해야 신청할 수 있습니다.", Severity.Warning);
            return;
        }

        IsSubmitting = true;
        SetMessage("다이어그램 신청 값을 창고 입고 API로 전송하고 있습니다.", Severity.Info);
        try
        {
            var response = await warehouseService.CreateInboundAsync(new 입고요청저장요청
            {
                창고Id = candidate.WarehouseId.Value,
                입고흐름유형 = 입고흐름유형코드.계약기반입고,
                입고생성경로 = $"다이어그램 창고 블록/{sourceNode.Title}",
                계약선행여부 = true,
                자동생성여부 = false,
                공급처코드 = Draft.SupplierCode.Trim(),
                공급처명 = Draft.SupplierName.Trim(),
                원주문참조번호 = Draft.OrderReference.Trim(),
                예정도착일 = Draft.ExpectedArrivalDate,
                비고 = Draft.Notes.Trim(),
                계약정보 = new 입고계약스냅샷
                {
                    계약번호 = Draft.ContractNo,
                    계약유형 = Draft.ContractType,
                    계약상대방명 = string.IsNullOrWhiteSpace(Draft.ContractCounterpartyName)
                        ? candidate.Name
                        : Draft.ContractCounterpartyName,
                    정산방식 = Draft.ContractSettlementType,
                    판매수수료율 = Draft.ContractCommissionRate,
                    보관료일단가 = Draft.ContractDailyStorageFee,
                    통관필요여부 = 입고계약유형코드.RequiresCustoms(Draft.ContractType),
                    계약시작일 = DateTime.Today,
                    계약메모 = $"다이어그램 노드 '{sourceNode.Title}'에서 {candidate.Name} 후보로 생성한 물류 대행 계약 초안입니다."
                }.Normalize()
            });

            SetMessage(
                response is null
                    ? "입고/물류 대행 요청을 등록했습니다. 창고 업무 화면에서 목록을 새로고침해 확인하세요."
                    : $"입고/물류 대행 요청 #{response.Id.ToString(CultureInfo.InvariantCulture)}을 등록했습니다. 상태: {response.상태}",
                Severity.Success);
        }
        catch (Exception ex)
        {
            SetMessage($"입고/물류 대행 요청 등록에 실패했습니다: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    public string? BuildWorkspaceUrl()
    {
        var sourceNode = SourceNode;
        var candidate = SelectedCandidate;
        if (sourceNode is null || candidate is null)
        {
            SetMessage("물류 대행을 신청할 창고 후보를 먼저 선택하세요.", Severity.Warning);
            return null;
        }

        var values = new Dictionary<string, string?>
        {
            ["source"] = "diagram-warehouse-proxy",
            ["warehouseId"] = candidate.WarehouseId?.ToString(CultureInfo.InvariantCulture),
            ["warehouseName"] = candidate.Name,
            ["proxyType"] = candidate.ProxyTypeCode,
            ["warehouseAddress"] = candidate.Address,
            ["nodeTitle"] = sourceNode.Title,
            ["nodeGroup"] = sourceNode.GroupLabel,
            ["nodeDescription"] = sourceNode.Description,
            ["scope"] = candidate.ScopeLabel
        };
        return PlatformCommunityNavigationQuery.Build("/shipper/inbound/requests", values);
    }

    private async Task LoadCandidatesAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        _candidates.Clear();
        SelectedCandidateKey = null;
        try
        {
            var warehouseService = _services.GetService<IWarehouseWorkspaceService>();
            if (warehouseService is not null)
            {
                var response = await warehouseService.GetWarehousesAsync();
                foreach (var warehouse in (response?.Items ?? []).Where(item => item.IsActive))
                {
                    _candidates.Add(MapCandidate(warehouse));
                }
            }
        }
        catch (Exception ex)
        {
            SetMessage(
                $"창고 목록을 불러오지 못했습니다. 기본 후보로 신청 초안을 만들 수 있습니다: {ex.Message}",
                Severity.Warning);
        }
        finally
        {
            if (_candidates.Count == 0)
            {
                _candidates.AddRange(DefaultCandidates);
            }

            OnPropertyChanged(nameof(Candidates));
            SelectCandidate(_candidates[0].Key);
            IsLoading = false;
        }
    }

    private static WarehouseProxyCandidate MapCandidate(창고요약응답 warehouse)
    {
        var proxyTypeCode = LogisticsProxySiteTypes.Normalize(warehouse.물류대행지분류);
        return new(
            $"warehouse:{warehouse.Id}",
            warehouse.Id,
            warehouse.창고명,
            warehouse.기본창고여부 ? "내 기본 창고 후보" : "공유/대행 창고 후보",
            proxyTypeCode,
            LogisticsProxySiteTypes.GetDisplayName(proxyTypeCode),
            warehouse.주소,
            $"{warehouse.창고명}에 입고, 보관, 피킹/포장, 출고 대행 가능 여부를 신청서로 작성합니다.",
            true);
    }

    private void SetMessage(string message, Severity severity)
    {
        MessageSeverity = severity;
        Message = message;
    }
}
