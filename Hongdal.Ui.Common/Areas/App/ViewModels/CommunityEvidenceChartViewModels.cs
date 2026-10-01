using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record CommunityEvidenceChartTypeOption(
    string Code,
    string DisplayName,
    string Description);

public sealed record CommunityEvidenceChartDraftApplyResult(
    bool Succeeded,
    bool ReplacedExistingBlock,
    string Message);

public sealed class CommunityEvidenceDataPointViewModel : ObservableObject
{
    private string _label;
    private decimal? _value;

    public CommunityEvidenceDataPointViewModel(
        string pointKey,
        string label,
        decimal? value)
    {
        PointKey = pointKey;
        _label = label;
        _value = value;
    }

    public string PointKey { get; }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value ?? string.Empty);
    }

    public decimal? Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

public sealed class CommunityAuthoringEvidenceChartViewModel : ObservableObject
{
    private string _chartTypeCode = CommunityEvidenceChartTypeCodes.Bar;
    private string _title = string.Empty;
    private string _claim = string.Empty;
    private string _seriesLabel = "값";
    private string _unit = string.Empty;
    private string _sourceLabel = string.Empty;
    private string _sourceUrl = string.Empty;
    private string _referenceDate = string.Empty;
    private string _interpretation = string.Empty;
    private string _limitation = string.Empty;
    private CommunityEvidenceChartBlock? _preview;
    private CommunityEvidenceChartStatistics? _statistics;
    private IReadOnlyList<string> _validationErrors = [];
    private string? _statusMessage;
    private CommunityComposerMessageKind _statusKind = CommunityComposerMessageKind.Info;

    public CommunityAuthoringEvidenceChartViewModel()
    {
        Reset();
    }

    public static IReadOnlyList<CommunityEvidenceChartTypeOption> ChartTypes { get; } =
    [
        new(CommunityEvidenceChartTypeCodes.Bar, "막대", "항목별 크기를 비교합니다."),
        new(CommunityEvidenceChartTypeCodes.Line, "선", "시간이나 순서에 따른 변화를 봅니다."),
        new(CommunityEvidenceChartTypeCodes.Donut, "도넛", "전체 안에서 각 항목의 비중을 봅니다.")
    ];

    public ObservableCollection<CommunityEvidenceDataPointViewModel> Points { get; } = [];

    public string ChartTypeCode
    {
        get => _chartTypeCode;
        set => SetInput(ref _chartTypeCode, value ?? CommunityEvidenceChartTypeCodes.Bar);
    }

    public string Title
    {
        get => _title;
        set => SetInput(ref _title, value ?? string.Empty);
    }

    public string Claim
    {
        get => _claim;
        set => SetInput(ref _claim, value ?? string.Empty);
    }

    public string SeriesLabel
    {
        get => _seriesLabel;
        set => SetInput(ref _seriesLabel, value ?? string.Empty);
    }

    public string Unit
    {
        get => _unit;
        set => SetInput(ref _unit, value ?? string.Empty);
    }

    public string SourceLabel
    {
        get => _sourceLabel;
        set => SetInput(ref _sourceLabel, value ?? string.Empty);
    }

    public string SourceUrl
    {
        get => _sourceUrl;
        set => SetInput(ref _sourceUrl, value ?? string.Empty);
    }

    public string ReferenceDate
    {
        get => _referenceDate;
        set => SetInput(ref _referenceDate, value ?? string.Empty);
    }

    public string Interpretation
    {
        get => _interpretation;
        set => SetInput(ref _interpretation, value ?? string.Empty);
    }

    public string Limitation
    {
        get => _limitation;
        set => SetInput(ref _limitation, value ?? string.Empty);
    }

    public CommunityEvidenceChartBlock? Preview
    {
        get => _preview;
        private set
        {
            if (SetProperty(ref _preview, value))
            {
                OnPropertyChanged(nameof(HasPreview));
            }
        }
    }

    public CommunityEvidenceChartStatistics? Statistics
    {
        get => _statistics;
        private set => SetProperty(ref _statistics, value);
    }

    public IReadOnlyList<string> ValidationErrors
    {
        get => _validationErrors;
        private set => SetProperty(ref _validationErrors, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public CommunityComposerMessageKind StatusKind
    {
        get => _statusKind;
        private set => SetProperty(ref _statusKind, value);
    }

    public bool HasPreview => Preview is not null;

    public bool CanAddPoint => Points.Count < CommunityEvidenceChartPolicy.MaximumPointCount;

    public void PrepareFromDraft(string? draftTitle, string? draftBody = null)
    {
        var existingBlock = CommunityEvidenceChartTextCodec.DecodeAll(draftBody).LastOrDefault();
        if (existingBlock is not null)
        {
            Load(existingBlock);
            SetStatus(
                "글에 들어 있는 마지막 근거 그래프를 불러왔습니다. 수정한 뒤 다시 넣으면 같은 블록이 갱신됩니다.",
                CommunityComposerMessageKind.Info);
            return;
        }

        var normalizedTitle = draftTitle?.Trim() ?? string.Empty;
        if (normalizedTitle.Length == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            Title = "핵심 수치 비교";
        }

        if (string.IsNullOrWhiteSpace(Claim))
        {
            Claim = $"'{normalizedTitle}'에서 제안하는 방향을 수치로 함께 확인합니다.";
        }
    }

    public CommunityEvidenceChartDraftApplyResult ApplyToDraft(
        CommunityPostComposerDraftViewModel draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        string evidenceBlock;
        try
        {
            evidenceBlock = BuildBodyBlock();
        }
        catch (InvalidOperationException)
        {
            return new CommunityEvidenceChartDraftApplyResult(
                false,
                false,
                StatusMessage ?? "그래프의 수치와 근거를 먼저 완성해 주세요.");
        }

        var wasBlankDraft = !draft.HasContent;
        var replacementBody = string.Empty;
        var replacedExistingBlock = Preview is not null
                                    && CommunityEvidenceChartTextCodec.TryReplaceLastBlock(
                                        draft.Body,
                                        Preview,
                                        out replacementBody);
        string nextBody;
        if (replacedExistingBlock)
        {
            nextBody = replacementBody;
        }
        else if (wasBlankDraft)
        {
            nextBody = evidenceBlock;
        }
        else
        {
            var prefix = string.IsNullOrWhiteSpace(draft.Body)
                ? string.Empty
                : $"{Environment.NewLine}{Environment.NewLine}---{Environment.NewLine}{Environment.NewLine}";
            nextBody = string.Concat(
                draft.Body,
                prefix,
                "수치로 함께 확인할 근거",
                Environment.NewLine,
                Environment.NewLine,
                evidenceBlock);
        }

        if (nextBody.Length > 4000)
        {
            SetStatus(
                "그래프 근거가 본문 제한을 넘었습니다. 설명이나 데이터 수를 줄여 주세요.",
                CommunityComposerMessageKind.Warning);
            return new CommunityEvidenceChartDraftApplyResult(false, false, StatusMessage!);
        }

        if (wasBlankDraft)
        {
            draft.Category = CommunityBoardCatalog.Vow.DisplayName;
            draft.WorkflowTag = "통계 근거 검토";
            draft.Title = Limit($"[서원] {Title.Trim()}", 160);
        }

        draft.Body = nextBody;
        var message = replacedExistingBlock
            ? "글에 들어 있던 근거 그래프를 새 수치와 설명으로 갱신했습니다."
            : "출처와 한계를 포함한 근거 그래프를 글에 추가했습니다.";
        SetStatus(message, CommunityComposerMessageKind.Success);
        return new CommunityEvidenceChartDraftApplyResult(true, replacedExistingBlock, message);
    }

    public CommunityEvidenceDataPointViewModel? AddPoint()
    {
        if (!CanAddPoint)
        {
            SetStatus(
                $"그래프 데이터는 최대 {CommunityEvidenceChartPolicy.MaximumPointCount:N0}개까지 입력할 수 있습니다.",
                CommunityComposerMessageKind.Warning);
            return null;
        }

        var point = AddPointCore(string.Empty, null);
        InvalidatePreview();
        SetStatus("데이터 행을 추가했습니다.", CommunityComposerMessageKind.Info);
        OnPropertyChanged(nameof(CanAddPoint));
        return point;
    }

    public bool RemovePoint(CommunityEvidenceDataPointViewModel point)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (!Points.Remove(point))
        {
            return false;
        }

        point.PropertyChanged -= HandlePointChanged;
        InvalidatePreview();
        SetStatus("데이터 행을 제거했습니다.", CommunityComposerMessageKind.Info);
        OnPropertyChanged(nameof(CanAddPoint));
        return true;
    }

    public bool ImportMutualBenefit(CommunityAuthoringMutualBenefitViewModel mutualBenefit)
    {
        ArgumentNullException.ThrowIfNull(mutualBenefit);
        var quantifiedRoles = mutualBenefit.Roles
            .Where(role => role.ExpectedBenefitAmount.HasValue && role.ExpectedBurdenAmount.HasValue)
            .Select(role => new CommunityEvidenceChartPoint(
                role.RoleLabel.Trim(),
                role.ExpectedBenefitAmount!.Value - role.ExpectedBurdenAmount!.Value))
            .Where(point => point.Label.Length > 0)
            .ToArray();
        if (quantifiedRoles.Length < CommunityEvidenceChartPolicy.MinimumPointCount)
        {
            SetStatus(
                "Win-Win 검토에서 기대 편익과 예상 부담을 모두 입력한 역할이 두 개 이상 필요합니다.",
                CommunityComposerMessageKind.Warning);
            return false;
        }

        ReplacePoints(quantifiedRoles);
        ChartTypeCode = CommunityEvidenceChartTypeCodes.Bar;
        Title = "역할별 순편익 추정";
        Claim = quantifiedRoles.All(point => point.Value > 0m)
            ? "현재 가정에서는 영향을 받는 각 역할의 기대 편익이 부담보다 큽니다."
            : "현재 가정에서 편익보다 부담이 크거나 같은 역할이 있는지 비교합니다.";
        SeriesLabel = "순편익 추정";
        Unit = string.IsNullOrWhiteSpace(mutualBenefit.CurrencyCode)
            ? "금액"
            : mutualBenefit.CurrencyCode.Trim().ToUpperInvariant();
        SourceLabel = "작성 중인 Win-Win 사전 검토";
        SourceUrl = string.Empty;
        ReferenceDate = DateTime.Today.ToString("yyyy-MM-dd");
        Interpretation = BuildMutualBenefitInterpretation(quantifiedRoles);
        Limitation = "작성자가 입력한 추정값이며, 실제 물량·단가·비용과 참여자별 최소 편익은 공동조달 경제성 계획의 새 계산 리비전에서 다시 확인해야 합니다.";
        var evaluated = Evaluate();
        if (evaluated)
        {
            SetStatus(
                $"Win-Win 검토의 역할별 순편익 {quantifiedRoles.Length:N0}개를 그래프로 가져왔습니다.",
                CommunityComposerMessageKind.Success);
        }

        return evaluated;
    }

    public bool ImportPeriodStatistics(CommunityEvidenceChartBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var validation = CommunityEvidenceChartPolicy.Validate(block);
        if (!validation.IsValid)
        {
            ValidationErrors = validation.Errors;
            SetStatus(
                "기간 통계를 근거 그래프로 가져오지 못했습니다. 기간과 자료 단위를 다시 확인해 주세요.",
                CommunityComposerMessageKind.Warning);
            return false;
        }

        Load(block);
        SetStatus(
            "기간 통계를 근거 그래프로 가져왔습니다. 해석과 자료의 한계를 확인한 뒤 글에 넣어 주세요.",
            CommunityComposerMessageKind.Success);
        return true;
    }

    public bool Evaluate()
    {
        var rowErrors = new List<string>();
        if (Points.Any(point => string.IsNullOrWhiteSpace(point.Label)))
        {
            rowErrors.Add("모든 데이터 행에 이름을 입력해 주세요.");
        }

        if (Points.Any(point => !point.Value.HasValue))
        {
            rowErrors.Add("모든 데이터 행에 수치를 입력해 주세요.");
        }

        if (rowErrors.Count > 0)
        {
            Preview = null;
            Statistics = null;
            ValidationErrors = rowErrors;
            SetStatus("그래프에 필요한 데이터 이름과 수치를 보완해 주세요.", CommunityComposerMessageKind.Warning);
            return false;
        }

        var block = CreateBlock();
        var validation = CommunityEvidenceChartPolicy.Validate(block);
        ValidationErrors = validation.Errors;
        if (!validation.IsValid)
        {
            Preview = null;
            Statistics = null;
            SetStatus("그래프 근거와 표시 조건을 보완해 주세요.", CommunityComposerMessageKind.Warning);
            return false;
        }

        Preview = block;
        Statistics = CommunityEvidenceChartPolicy.CalculateStatistics(block);
        SetStatus(
            "그래프와 요약 통계를 갱신했습니다. 주장과 자료의 한계가 수치에 맞는지 다시 확인해 주세요.",
            CommunityComposerMessageKind.Success);
        return true;
    }

    public string BuildBodyBlock()
    {
        if (!Evaluate() || Preview is null)
        {
            throw new InvalidOperationException("유효한 통계 근거를 먼저 만들어야 합니다.");
        }

        return CommunityEvidenceChartTextCodec.Encode(Preview);
    }

    public void Reset()
    {
        foreach (var point in Points)
        {
            point.PropertyChanged -= HandlePointChanged;
        }

        Points.Clear();
        _chartTypeCode = CommunityEvidenceChartTypeCodes.Bar;
        _title = string.Empty;
        _claim = string.Empty;
        _seriesLabel = "값";
        _unit = string.Empty;
        _sourceLabel = string.Empty;
        _sourceUrl = string.Empty;
        _referenceDate = DateTime.Today.ToString("yyyy-MM-dd");
        _interpretation = string.Empty;
        _limitation = "자료의 범위와 계산 가정을 확인하고 실제 결정 전 최신 원문을 다시 확인해야 합니다.";
        AddPointCore("현재", null);
        AddPointCore("목표", null);
        Preview = null;
        Statistics = null;
        ValidationErrors = [];
        StatusMessage = null;
        StatusKind = CommunityComposerMessageKind.Info;
        OnPropertyChanged(nameof(ChartTypeCode));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Claim));
        OnPropertyChanged(nameof(SeriesLabel));
        OnPropertyChanged(nameof(Unit));
        OnPropertyChanged(nameof(SourceLabel));
        OnPropertyChanged(nameof(SourceUrl));
        OnPropertyChanged(nameof(ReferenceDate));
        OnPropertyChanged(nameof(Interpretation));
        OnPropertyChanged(nameof(Limitation));
        OnPropertyChanged(nameof(Points));
        OnPropertyChanged(nameof(CanAddPoint));
    }

    private CommunityEvidenceChartBlock CreateBlock()
        => new()
        {
            ChartTypeCode = ChartTypeCode.Trim(),
            Title = Title.Trim(),
            Claim = Claim.Trim(),
            SeriesLabel = SeriesLabel.Trim(),
            Unit = Unit.Trim(),
            SourceLabel = SourceLabel.Trim(),
            SourceUrl = SourceUrl.Trim(),
            ReferenceDate = ReferenceDate.Trim(),
            Interpretation = Interpretation.Trim(),
            Limitation = Limitation.Trim(),
            Points = Points.Select(point => new CommunityEvidenceChartPoint(
                point.Label.Trim(),
                point.Value ?? 0m)).ToArray()
        };

    private CommunityEvidenceDataPointViewModel AddPointCore(string label, decimal? value)
    {
        var point = new CommunityEvidenceDataPointViewModel(
            $"evidence-point-{Guid.NewGuid():N}",
            label,
            value);
        point.PropertyChanged += HandlePointChanged;
        Points.Add(point);
        return point;
    }

    private void ReplacePoints(IEnumerable<CommunityEvidenceChartPoint> points)
    {
        foreach (var point in Points)
        {
            point.PropertyChanged -= HandlePointChanged;
        }

        Points.Clear();
        foreach (var point in points.Take(CommunityEvidenceChartPolicy.MaximumPointCount))
        {
            AddPointCore(point.Label, point.Value);
        }

        InvalidatePreview();
        OnPropertyChanged(nameof(Points));
        OnPropertyChanged(nameof(CanAddPoint));
    }

    private void Load(CommunityEvidenceChartBlock block)
    {
        _chartTypeCode = block.ChartTypeCode;
        _title = block.Title;
        _claim = block.Claim;
        _seriesLabel = block.SeriesLabel;
        _unit = block.Unit;
        _sourceLabel = block.SourceLabel;
        _sourceUrl = block.SourceUrl;
        _referenceDate = block.ReferenceDate;
        _interpretation = block.Interpretation;
        _limitation = block.Limitation;
        ReplacePoints(block.Points);
        Preview = block;
        Statistics = CommunityEvidenceChartPolicy.CalculateStatistics(block);
        ValidationErrors = [];
        OnPropertyChanged(nameof(ChartTypeCode));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Claim));
        OnPropertyChanged(nameof(SeriesLabel));
        OnPropertyChanged(nameof(Unit));
        OnPropertyChanged(nameof(SourceLabel));
        OnPropertyChanged(nameof(SourceUrl));
        OnPropertyChanged(nameof(ReferenceDate));
        OnPropertyChanged(nameof(Interpretation));
        OnPropertyChanged(nameof(Limitation));
    }

    private void HandlePointChanged(object? sender, PropertyChangedEventArgs e)
        => InvalidatePreview();

    private bool SetInput(ref string storage, string value)
    {
        if (!SetProperty(ref storage, value))
        {
            return false;
        }

        InvalidatePreview();
        return true;
    }

    private void InvalidatePreview()
    {
        if (Preview is null && Statistics is null && ValidationErrors.Count == 0)
        {
            return;
        }

        Preview = null;
        Statistics = null;
        ValidationErrors = [];
        SetStatus("입력값이 바뀌었습니다. 그래프를 다시 갱신해 주세요.", CommunityComposerMessageKind.Info);
    }

    private void SetStatus(string message, CommunityComposerMessageKind kind)
    {
        StatusKind = kind;
        StatusMessage = message;
    }

    private static string BuildMutualBenefitInterpretation(
        IReadOnlyList<CommunityEvidenceChartPoint> points)
    {
        var nonPositiveRoles = points
            .Where(point => point.Value <= 0m)
            .Select(point => point.Label)
            .ToArray();
        return nonPositiveRoles.Length == 0
            ? "입력된 역할 모두에서 기대 편익이 예상 부담보다 크게 나타납니다."
            : $"{string.Join(", ", nonPositiveRoles)} 역할은 기대 편익이 예상 부담보다 크지 않아 조건 조정이 필요합니다.";
    }

    private static string Limit(string value, int maximumLength)
        => value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
