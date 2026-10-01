using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Ui.Common.Areas.App.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class CommunityGroupImportHsSelectionWorkspace
{
    private const int CatalogPageSize = 20;

    [Inject]
    public PlatformCommunityService CommunityService { get; set; } = null!;

    private readonly List<ImportCandidateDraft> selectedCandidates = [];
    private readonly HashSet<string> selectedOptionIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ImportSelectionDraft draft = new();
    private GroupImportHsCodeSearchResponse catalogResult = new();
    private List<CommunityVoteResponse> importCampaigns = [];
    private CommunityVoteResponse? selectedCampaign;
    private GroupImportView activeView = GroupImportView.Catalog;
    private string searchQuery = string.Empty;
    private string selectedBusinessCategory = "all";
    private string marketCountryCode = "CN";
    private string marketReferenceMonth = DateTime.UtcNow.AddMonths(-2).ToString("yyyyMM", CultureInfo.InvariantCulture);
    private int marketLookbackMonths = 3;
    private decimal marketFxRateKrwPerUsd = 1350m;
    private string participantDisplayName = "공동수입 참여자";
    private string statusMessage = string.Empty;
    private string campaignLoadMessage = string.Empty;
    private Severity statusSeverity = Severity.Info;
    private int requestedQuantity = 1;
    private bool isCatalogLoading;
    private bool isCampaignLoading;
    private bool isCampaignBusy;

    private int CatalogPageCount
        => Math.Max(1, (int)Math.Ceiling(catalogResult.TotalCount / (double)Math.Max(1, catalogResult.PageSize)));

    protected override async Task OnInitializedAsync()
    {
        await SearchHsCodesAsync();
        await LoadImportCampaignsAsync();
    }

    private void SelectView(GroupImportView view)
    {
        activeView = view;
        statusMessage = string.Empty;
    }

    private string BuildTabClass(GroupImportView view)
        => activeView == view ? "group-import-hs-tab is-active" : "group-import-hs-tab";

    private Task SearchHsCodesAsync() => LoadCatalogPageAsync(1);

    private Task LoadPreviousCatalogPageAsync()
        => LoadCatalogPageAsync(Math.Max(1, catalogResult.Page - 1));

    private Task LoadNextCatalogPageAsync()
        => LoadCatalogPageAsync(Math.Min(CatalogPageCount, catalogResult.Page + 1));

    private async Task LoadCatalogPageAsync(int page)
    {
        if (isCatalogLoading)
        {
            return;
        }

        isCatalogLoading = true;
        statusMessage = string.Empty;
        try
        {
            catalogResult = await CommunityService.SearchGroupImportHsCodesAsync(
                searchQuery,
                ParseBusinessCategory(),
                page,
                CatalogPageSize);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            catalogResult = new GroupImportHsCodeSearchResponse();
            ShowStatus(Severity.Warning, "HS 코드 카탈로그를 불러오지 못했습니다. 서버의 공동수입 기능 설정을 확인해 주세요.");
        }
        finally
        {
            isCatalogLoading = false;
        }
    }

    private int? ParseBusinessCategory()
        => int.TryParse(selectedBusinessCategory, out var value) ? value : null;

    private void AddCandidate(GroupImportHsCodeItemResponse item)
    {
        if (IsCandidateSelected(item.Id))
        {
            return;
        }

        selectedCandidates.Add(new ImportCandidateDraft
        {
            HsCodeEntryId = item.Id,
            HsCode = item.Code,
            NormalizedHsCode = item.NormalizedCode,
            ProductName = DisplayHsCodeName(item),
            CategoryLabel = item.BusinessCategoryLabel
        });
    }

    private void RemoveCandidate(long hsCodeEntryId)
        => selectedCandidates.RemoveAll(candidate => candidate.HsCodeEntryId == hsCodeEntryId);

    private bool IsCandidateSelected(long hsCodeEntryId)
        => selectedCandidates.Any(candidate => candidate.HsCodeEntryId == hsCodeEntryId);

    private string BuildHsCodeItemClass(GroupImportHsCodeItemResponse item)
        => IsCandidateSelected(item.Id)
            ? "group-import-hs-result is-selected"
            : "group-import-hs-result";

    private static string DisplayHsCodeName(GroupImportHsCodeItemResponse item)
        => !string.IsNullOrWhiteSpace(item.KoreanName)
            ? item.KoreanName
            : !string.IsNullOrWhiteSpace(item.EnglishName)
                ? item.EnglishName
                : item.Code;

    private async Task CreateImportSelectionAsync()
    {
        if (selectedCandidates.Count < 2)
        {
            ShowStatus(Severity.Warning, "공동 선택을 열려면 비교할 상품 후보를 두 개 이상 담아주세요.");
            return;
        }

        if (string.IsNullOrWhiteSpace(draft.Title) || string.IsNullOrWhiteSpace(draft.CreatorDisplayName))
        {
            ShowStatus(Severity.Warning, "공동 선택 제목과 개설자 표시명을 입력해 주세요.");
            return;
        }

        if (selectedCandidates.Any(candidate => string.IsNullOrWhiteSpace(candidate.ProductName)))
        {
            ShowStatus(Severity.Warning, "각 HS 코드에 구매 후보 상품명을 입력해 주세요.");
            return;
        }

        isCampaignBusy = true;
        try
        {
            var campaign = await CommunityService.CreateGroupPurchaseVoteAsync(new CommunityVoteCreateRequest
            {
                CommunityScope = string.IsNullOrWhiteSpace(draft.CommunityScope)
                    ? "platform"
                    : draft.CommunityScope.Trim(),
                Title = draft.Title.Trim(),
                Description = BuildCampaignDescription(),
                AllowMultipleSelection = false,
                ResolutionDocumentEnabled = true,
                SignatureRequired = true,
                ClosesAtUtc = DateTime.UtcNow.AddDays(Math.Clamp(draft.OpenDays, 1, 30)),
                CreatedByDisplayName = draft.CreatorDisplayName.Trim(),
                StructuredOptions = selectedCandidates
                    .Select(candidate => new CommunityVoteOptionCreateRequest
                    {
                        Text = candidate.ProductName.Trim(),
                        ProductKey = $"hs:{candidate.NormalizedHsCode}",
                        HsCode = candidate.HsCode,
                        TemperatureCode = "상온",
                        LogisticsMode = draft.LogisticsMode,
                        QuantityUnit = draft.QuantityUnit.Trim()
                    })
                    .ToArray(),
                GroupPurchase = new CommunityGroupPurchaseVoteSettingsRequest
                {
                    SellerCountryCode = marketCountryCode,
                    ShipFromCountryCode = marketCountryCode,
                    DeliveryCountryCode = CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode,
                    CustomsClearanceStatusCode = CommunityGroupPurchaseCustomsClearanceStatusCodes.NotCleared,
                    ParticipationPolicyCode = CommunityVoteParticipationPolicyCodes.Hybrid,
                    HsCode = selectedCandidates.Count == 1 ? selectedCandidates[0].HsCode : string.Empty,
                    TemperatureCode = "상온",
                    LogisticsMode = draft.LogisticsMode,
                    QuantityUnit = draft.QuantityUnit.Trim(),
                    ServiceAreaKey = draft.CommunityScope.Trim(),
                    ServiceAreaLabel = draft.CommunityScope.Trim(),
                    MinimumParticipantCount = Math.Max(1, draft.MinimumParticipantCount),
                    MinimumTotalQuantity = Math.Max(1, draft.MinimumTotalQuantity)
                }
            });

            if (campaign is null)
            {
                throw new InvalidOperationException("공동 선택 생성 응답이 비어 있습니다.");
            }

            importCampaigns.RemoveAll(item => item.Id == campaign.Id);
            importCampaigns.Insert(0, campaign);
            selectedCampaign = campaign;
            selectedOptionIds.Clear();
            selectedCandidates.Clear();
            activeView = GroupImportView.Campaigns;
            ShowStatus(Severity.Success, "HS 코드 상품 후보를 공동수입 선택안으로 열었습니다.");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            ShowStatus(Severity.Error, "공동 선택안을 열지 못했습니다. 로그인 상태와 공동수입 기능 설정을 확인해 주세요.");
        }
        finally
        {
            isCampaignBusy = false;
        }
    }

    private string BuildCampaignDescription()
    {
        var productSummary = string.Join(
            ", ",
            selectedCandidates.Select(candidate => $"{candidate.ProductName}({candidate.HsCode})"));
        var memo = string.IsNullOrWhiteSpace(draft.Description)
            ? string.Empty
            : $" {draft.Description.Trim()}";
        return $"HS 코드 기준 공동수입 상품 선택: {productSummary}.{memo}".Trim();
    }

    private async Task LoadImportCampaignsAsync()
    {
        if (isCampaignLoading)
        {
            return;
        }

        isCampaignLoading = true;
        campaignLoadMessage = string.Empty;
        try
        {
            var response = await CommunityService.GetGroupPurchaseVotesAsync();
            importCampaigns = response.Items
                .Where(CommunityVoteWorkflowClassifier.IsGroupImport)
                .OrderByDescending(campaign => campaign.CreatedAtUtc)
                .ToList();

            if (selectedCampaign is not null &&
                importCampaigns.Any(campaign => campaign.Id == selectedCampaign.Id))
            {
                await SelectCampaignAsync(selectedCampaign.Id);
            }
            else if (importCampaigns.Count > 0)
            {
                await SelectCampaignAsync(importCampaigns[0].Id);
            }
            else
            {
                selectedCampaign = null;
            }
        }
        catch (HttpRequestException)
        {
            importCampaigns = [];
            selectedCampaign = null;
            campaignLoadMessage = "공동 선택 참여 내역은 로그인 후 확인할 수 있습니다.";
        }
        finally
        {
            isCampaignLoading = false;
        }
    }

    private async Task SelectCampaignAsync(Guid campaignId)
    {
        if (isCampaignBusy)
        {
            return;
        }

        isCampaignBusy = true;
        try
        {
            var campaign = await CommunityService.GetGroupPurchaseVoteAsync(campaignId);
            if (campaign is null || !CommunityVoteWorkflowClassifier.IsGroupImport(campaign))
            {
                return;
            }

            selectedCampaign = campaign;
            selectedOptionIds.Clear();
            var index = importCampaigns.FindIndex(item => item.Id == campaign.Id);
            if (index >= 0)
            {
                importCampaigns[index] = campaign;
            }
        }
        catch (HttpRequestException)
        {
            ShowStatus(Severity.Warning, "공동 선택 상세를 불러오지 못했습니다.");
        }
        finally
        {
            isCampaignBusy = false;
        }
    }

    private void ToggleCampaignOption(string optionId)
    {
        if (selectedCampaign is null)
        {
            return;
        }

        if (selectedOptionIds.Remove(optionId))
        {
            return;
        }

        if (!selectedCampaign.AllowMultipleSelection)
        {
            selectedOptionIds.Clear();
        }

        selectedOptionIds.Add(optionId);
    }

    private bool IsCampaignOptionSelected(string optionId)
        => selectedOptionIds.Contains(optionId);

    private string BuildOptionClass(string optionId)
        => IsCampaignOptionSelected(optionId)
            ? "group-import-hs-option is-selected"
            : "group-import-hs-option";

    private async Task CastImportChoiceAsync()
    {
        if (selectedCampaign is null || selectedOptionIds.Count == 0)
        {
            return;
        }

        isCampaignBusy = true;
        try
        {
            var updated = await CommunityService.CastGroupPurchaseVoteAsync(
                selectedCampaign.Id,
                new CommunityVoteCastRequest
                {
                    VoterDisplayName = participantDisplayName.Trim(),
                    OptionIds = selectedOptionIds.ToArray(),
                    RequestedQuantity = Math.Max(1, requestedQuantity),
                    ParticipationMethodCode = CommunityVoteParticipationMethodCodes.CommunityMember
                });

            if (updated is not null)
            {
                selectedCampaign = updated;
                var index = importCampaigns.FindIndex(campaign => campaign.Id == updated.Id);
                if (index >= 0)
                {
                    importCampaigns[index] = updated;
                }
            }

            selectedOptionIds.Clear();
            ShowStatus(Severity.Success, "공동수입 상품 선택을 반영했습니다.");
        }
        catch (HttpRequestException)
        {
            ShowStatus(Severity.Error, "선택을 반영하지 못했습니다. 로그인 상태와 참여 조건을 확인해 주세요.");
        }
        finally
        {
            isCampaignBusy = false;
        }
    }

    private string BuildCampaignClass(CommunityVoteResponse campaign)
        => selectedCampaign?.Id == campaign.Id
            ? "group-import-hs-campaign is-selected"
            : "group-import-hs-campaign";

    private static string BuildCampaignHsSummary(CommunityVoteResponse campaign)
    {
        var codes = campaign.Options
            .Select(option => option.HsCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        return codes.Length == 0 ? "HS 코드 검토 중" : string.Join(" · ", codes);
    }

    private static string GetCampaignStateLabel(CommunityVoteResponse campaign)
        => campaign.Status == CommunityVoteStatusCodes.Open ? "선택 진행" : "선택 마감";

    private static Color GetCampaignColor(CommunityVoteResponse campaign)
        => campaign.Status == CommunityVoteStatusCodes.Open ? Color.Success : Color.Default;

    private void ShowStatus(Severity severity, string message)
    {
        statusSeverity = severity;
        statusMessage = message;
    }

    private enum GroupImportView
    {
        Catalog,
        Campaigns
    }

    private sealed class ImportCandidateDraft
    {
        public long HsCodeEntryId { get; init; }

        public string HsCode { get; init; } = string.Empty;

        public string NormalizedHsCode { get; init; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public string CategoryLabel { get; init; } = string.Empty;
    }

    private sealed class ImportSelectionDraft
    {
        public string Title { get; set; } = "함께 들여올 상품을 정해요";

        public string CreatorDisplayName { get; set; } = "공동수입 제안자";

        public string CommunityScope { get; set; } = "platform";

        public string LogisticsMode { get; set; } = "LCL";

        public string QuantityUnit { get; set; } = "개";

        public int MinimumParticipantCount { get; set; } = 3;

        public int MinimumTotalQuantity { get; set; } = 10;

        public int OpenDays { get; set; } = 7;

        public string Description { get; set; } = string.Empty;
    }
}
