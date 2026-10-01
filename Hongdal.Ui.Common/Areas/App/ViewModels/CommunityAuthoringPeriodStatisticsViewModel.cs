using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Content;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class CommunityPeriodStatisticsMetricCodes
{
    public const string RecordCount = "record-count";
    public const string NumericAverage = "numeric-average";
}

public sealed record CommunityPeriodStatisticsMetricOption(
    string Code,
    string DisplayName,
    string Description);

public sealed record CommunityPeriodStatisticsBucket(
    DateOnly StartDate,
    DateOnly EndDate,
    string Label,
    int RecordCount,
    int NumericValueCount,
    decimal? Value);

public sealed record CommunityPeriodStatisticsSeriesOption(
    string SelectionKey,
    string DisplayName,
    string MetricLabel,
    string Unit,
    int ObservationCount);

public sealed class CommunityAuthoringPeriodStatisticsViewModel : ObservableObject
{
    private const int MaximumQueryCount = 100;
    private const int MaximumRangeDays = 366;
    private readonly ICommunityInformationReviewClient _client;
    private IReadOnlyList<CommunityInformationSourceDto> _sources = [];
    private IReadOnlyList<CommunityPeriodStatisticsBucket> _buckets = [];
    private IReadOnlyList<CommunityPeriodStatisticsSeriesOption> _availableSeries = [];
    private IReadOnlyList<CommunityInformationSourceFailureDto> _failures = [];
    private DateTime? _startDate;
    private DateTime? _endDate;
    private string _sourceKey = string.Empty;
    private string _countryCode = string.Empty;
    private string _searchText = string.Empty;
    private string _metricCode = CommunityPeriodStatisticsMetricCodes.RecordCount;
    private string _metricSeriesSelectionKey = string.Empty;
    private bool _isLoading;
    private bool _queryLimitReached;
    private int _recordCount;
    private int _numericValueCount;
    private DateTime? _generatedAtUtc;
    private CommunityEvidenceChartStatistics? _statistics;
    private CommunityEvidenceChartBlock? _preview;
    private string? _statusMessage;
    private CommunityComposerMessageKind _statusKind = CommunityComposerMessageKind.Info;

    public CommunityAuthoringPeriodStatisticsViewModel(ICommunityInformationReviewClient client)
    {
        _client = client;
        var today = DateTime.Today;
        _startDate = today.AddDays(-29);
        _endDate = today;
    }

    public static IReadOnlyList<CommunityPeriodStatisticsMetricOption> Metrics { get; } =
    [
        new(
            CommunityPeriodStatisticsMetricCodes.RecordCount,
            "자료 수",
            "선택 기간에 수집된 자료 건수를 구간별로 집계합니다."),
        new(
            CommunityPeriodStatisticsMetricCodes.NumericAverage,
            "수치 평균",
            "KAMIS·USDA 가격, ABS 식품물가지수, 수산업협동조합 월별 임직원 수처럼 숫자 관측값이 있는 자료를 같은 계열별로 집계합니다.")
    ];

    public IReadOnlyList<CommunityInformationSourceDto> Sources
    {
        get => _sources;
        private set => SetProperty(ref _sources, value);
    }

    public IReadOnlyList<CommunityPeriodStatisticsBucket> Buckets
    {
        get => _buckets;
        private set
        {
            if (SetProperty(ref _buckets, value))
            {
                OnPropertyChanged(nameof(HasResult));
            }
        }
    }

    public IReadOnlyList<CommunityPeriodStatisticsSeriesOption> AvailableSeries
    {
        get => _availableSeries;
        private set
        {
            if (SetProperty(ref _availableSeries, value))
            {
                OnPropertyChanged(nameof(HasSeriesChoices));
            }
        }
    }

    public IReadOnlyList<CommunityInformationSourceFailureDto> Failures
    {
        get => _failures;
        private set => SetProperty(ref _failures, value);
    }

    public DateTime? StartDate
    {
        get => _startDate;
        set => SetDateInput(ref _startDate, value);
    }

    public DateTime? EndDate
    {
        get => _endDate;
        set => SetDateInput(ref _endDate, value);
    }

    public string SourceKey
    {
        get => _sourceKey;
        set => SetFilter(ref _sourceKey, value ?? string.Empty);
    }

    public string CountryCode
    {
        get => _countryCode;
        set => SetFilter(ref _countryCode, value ?? string.Empty);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetFilter(ref _searchText, value ?? string.Empty);
    }

    public string MetricCode
    {
        get => _metricCode;
        set => SetFilter(ref _metricCode, value ?? CommunityPeriodStatisticsMetricCodes.RecordCount);
    }

    public string MetricSeriesSelectionKey
    {
        get => _metricSeriesSelectionKey;
        set
        {
            if (SetProperty(ref _metricSeriesSelectionKey, value ?? string.Empty))
            {
                ClearResult(clearSeriesOptions: false);
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool QueryLimitReached
    {
        get => _queryLimitReached;
        private set => SetProperty(ref _queryLimitReached, value);
    }

    public int RecordCount
    {
        get => _recordCount;
        private set => SetProperty(ref _recordCount, value);
    }

    public int NumericValueCount
    {
        get => _numericValueCount;
        private set => SetProperty(ref _numericValueCount, value);
    }

    public DateTime? GeneratedAtUtc
    {
        get => _generatedAtUtc;
        private set => SetProperty(ref _generatedAtUtc, value);
    }

    public CommunityEvidenceChartStatistics? Statistics
    {
        get => _statistics;
        private set => SetProperty(ref _statistics, value);
    }

    public CommunityEvidenceChartBlock? Preview
    {
        get => _preview;
        private set
        {
            if (SetProperty(ref _preview, value))
            {
                OnPropertyChanged(nameof(CanImportToEvidenceChart));
            }
        }
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

    public bool HasResult => Buckets.Count > 0;

    public bool HasSeriesChoices => AvailableSeries.Count > 1;

    public bool CanImportToEvidenceChart => Preview is not null;

    public void SetAvailableSources(IReadOnlyList<CommunityInformationSourceDto> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        Sources = sources
            .OrderBy(source => source.SourceType)
            .ThenBy(source => source.DisplayName)
            .ToArray();
    }

    public void PrepareFilters(string? sourceKey, string? countryCode, string? searchText)
    {
        _sourceKey = sourceKey?.Trim() ?? string.Empty;
        _countryCode = countryCode?.Trim() ?? string.Empty;
        _searchText = searchText?.Trim() ?? string.Empty;
        ClearResult();
        OnPropertyChanged(nameof(SourceKey));
        OnPropertyChanged(nameof(CountryCode));
        OnPropertyChanged(nameof(SearchText));
    }

    public void SelectSource(string? sourceKey)
    {
        SourceKey = sourceKey?.Trim() ?? string.Empty;
        switch (SourceKey)
        {
            case CommunityInformationSourceKeys.KamisPriceObservations:
            case CommunityInformationSourceKeys.FishCooperativeGeneralStatistics:
                CountryCode = "KR";
                MetricCode = CommunityPeriodStatisticsMetricCodes.NumericAverage;
                break;
            case CommunityInformationSourceKeys.UsdaNassPriceObservations:
                CountryCode = "US";
                MetricCode = CommunityPeriodStatisticsMetricCodes.NumericAverage;
                break;
            case CommunityInformationSourceKeys.AbsFoodPriceIndex:
                CountryCode = "AU";
                MetricCode = CommunityPeriodStatisticsMetricCodes.NumericAverage;
                break;
        }
    }

    public void SetDateRange(DateTime? startDate, DateTime? endDate)
    {
        _startDate = startDate?.Date;
        _endDate = endDate?.Date;
        ClearResult();
        OnPropertyChanged(nameof(StartDate));
        OnPropertyChanged(nameof(EndDate));
    }

    public async Task<bool> GenerateAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return false;
        }

        var rangeError = ValidateRange();
        if (rangeError is not null)
        {
            ClearResult();
            SetStatus(rangeError, CommunityComposerMessageKind.Warning);
            return false;
        }

        IsLoading = true;
        ClearResult(clearStatus: false, clearSeriesOptions: false);
        SetStatus("선택한 기간의 수집 자료를 조회하고 있습니다.", CommunityComposerMessageKind.Info);
        try
        {
            var startDate = DateOnly.FromDateTime(StartDate!.Value);
            var endDate = DateOnly.FromDateTime(EndDate!.Value);
            var query = new CommunityInformationCollectionQuery
            {
                SourceKey = NormalizeOptional(SourceKey),
                CountryCode = NormalizeOptional(CountryCode),
                SearchText = NormalizeOptional(SearchText),
                StartDate = startDate,
                EndDate = endDate,
                Take = MaximumQueryCount
            };
            var response = await _client.GetCandidatesAsync(query, cancellationToken);
            if (Sources.Count == 0 && response.Sources.Count > 0)
            {
                SetAvailableSources(response.Sources);
            }

            var matchedItems = response.Items
                .Where(item => IsInRange(item, startDate, endDate))
                .Where(item => string.IsNullOrWhiteSpace(SourceKey)
                               || string.Equals(item.SourceKey, SourceKey.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Failures = response.Failures;
            GeneratedAtUtc = response.GeneratedAtUtc;
            QueryLimitReached = response.Items.Count >= MaximumQueryCount;
            if (matchedItems.Length == 0)
            {
                UpdateAvailableSeries([]);
                SetStatus(
                    "선택한 기간과 조건에 맞는 수집 자료가 없습니다. 기간이나 자료 원천을 넓혀 주세요.",
                    CommunityComposerMessageKind.Info);
                return false;
            }

            var numericMode = string.Equals(
                MetricCode,
                CommunityPeriodStatisticsMetricCodes.NumericAverage,
                StringComparison.Ordinal);
            UpdateAvailableSeries(matchedItems);
            IReadOnlyList<CommunityInformationCandidateDto> items = matchedItems;
            if (numericMode)
            {
                RecordCount = matchedItems.Length;
                NumericValueCount = matchedItems.Count(item => item.NumericValue.HasValue);
                if (AvailableSeries.Count == 0)
                {
                    SetStatus(
                        "선택한 자료에는 집계할 숫자 관측값이 없습니다. 자료 수 통계를 사용하거나 숫자 통계 원천을 선택해 주세요.",
                        CommunityComposerMessageKind.Warning);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(MetricSeriesSelectionKey))
                {
                    SetStatus(
                        $"선택 기간에서 수치 계열 {AvailableSeries.Count:N0}개를 찾았습니다. 서로 다른 품목·지역·단위를 섞지 않도록 그래프로 만들 계열을 선택해 주세요.",
                        CommunityComposerMessageKind.Warning);
                    return false;
                }

                items = matchedItems
                    .Where(item => string.Equals(
                        BuildSeriesSelectionKey(item),
                        MetricSeriesSelectionKey,
                        StringComparison.Ordinal))
                    .ToArray();
            }

            RecordCount = items.Count;
            NumericValueCount = items.Count(item => item.NumericValue.HasValue);
            var boundaries = BuildBoundaries(startDate, endDate, items);
            var unit = numericMode ? ResolveNumericUnit(items) : "건";
            if (numericMode && unit is null)
            {
                SetStatus(
                    "선택한 계열의 통화·단위가 일관되지 않아 평균을 계산할 수 없습니다. 원천 자료의 단위를 확인해 주세요.",
                    CommunityComposerMessageKind.Warning);
                return false;
            }

            var buckets = boundaries
                .Select(boundary => BuildBucket(boundary, items, numericMode))
                .ToArray();
            Buckets = buckets;
            var points = buckets
                .Where(bucket => bucket.Value.HasValue)
                .Select(bucket => new CommunityEvidenceChartPoint(bucket.Label, bucket.Value!.Value))
                .ToArray();
            if (points.Length == 0)
            {
                SetStatus(
                    "선택한 기간에는 평균을 계산할 숫자 관측값이 없습니다.",
                    CommunityComposerMessageKind.Warning);
                return false;
            }

            var block = CreateEvidenceBlock(
                startDate,
                endDate,
                items,
                points,
                numericMode,
                unit ?? "값");
            Statistics = CommunityEvidenceChartPolicy.CalculateStatistics(block);
            var validation = CommunityEvidenceChartPolicy.Validate(block);
            Preview = validation.IsValid ? block : null;
            SetStatus(
                Preview is null
                    ? "기간 통계는 계산했습니다. 근거 그래프로 옮기려면 값이 있는 구간이 두 개 이상 필요합니다."
                    : $"{startDate:yyyy-MM-dd}부터 {endDate:yyyy-MM-dd}까지 {RecordCount:N0}건을 {Buckets.Count:N0}개 구간으로 나누고 값이 있는 {points.Length:N0}개 구간을 그래프로 만들었습니다.",
                Preview is null ? CommunityComposerMessageKind.Warning : CommunityComposerMessageKind.Success);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ClearResult(clearStatus: false);
            SetStatus($"기간 통계를 만들지 못했습니다: {exception.Message}", CommunityComposerMessageKind.Error);
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public CommunityEvidenceChartBlock? CreateEvidenceBlock()
        => Preview;

    public void Reset()
    {
        var today = DateTime.Today;
        _startDate = today.AddDays(-29);
        _endDate = today;
        _sourceKey = string.Empty;
        _countryCode = string.Empty;
        _searchText = string.Empty;
        _metricCode = CommunityPeriodStatisticsMetricCodes.RecordCount;
        ClearResult();
        OnPropertyChanged(nameof(StartDate));
        OnPropertyChanged(nameof(EndDate));
        OnPropertyChanged(nameof(SourceKey));
        OnPropertyChanged(nameof(CountryCode));
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(MetricCode));
    }

    private string? ValidateRange()
    {
        if (!StartDate.HasValue || !EndDate.HasValue)
        {
            return "통계를 계산할 시작일과 종료일을 모두 선택해 주세요.";
        }

        if (StartDate.Value.Date > EndDate.Value.Date)
        {
            return "통계 시작일은 종료일보다 늦을 수 없습니다.";
        }

        var rangeDays = (EndDate.Value.Date - StartDate.Value.Date).Days + 1;
        return rangeDays > MaximumRangeDays
            ? $"한 번에 계산할 수 있는 기간은 최대 {MaximumRangeDays:N0}일입니다."
            : null;
    }

    private CommunityEvidenceChartBlock CreateEvidenceBlock(
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlyList<CommunityInformationCandidateDto> items,
        IReadOnlyList<CommunityEvidenceChartPoint> points,
        bool numericMode,
        string unit)
    {
        var sourceLabel = BuildSourceLabel(items);
        var itemSourceKeys = items
            .Select(item => item.SourceKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sourceUrl = itemSourceKeys.Length == 1
            ? (Sources
                .FirstOrDefault(source => string.Equals(
                    source.SourceKey,
                    itemSourceKeys[0],
                    StringComparison.OrdinalIgnoreCase))
                ?.DocumentationUrl ?? string.Empty)
            : string.Empty;
        var limitationParts = new List<string>
        {
            "서버가 보관했거나 원천 선택 뒤 조회한 자료만 집계하며 원천의 전체 모집단을 뜻하지 않습니다."
        };
        if (QueryLimitReached)
        {
            limitationParts.Add($"조회 한도 {MaximumQueryCount:N0}건에 도달해 선택 기간 전체가 아닐 수 있습니다.");
        }

        if (Failures.Count > 0)
        {
            limitationParts.Add("일부 자료 원천 조회가 실패해 결과에서 제외됐습니다.");
        }

        var sourceLimitation = items
            .Select(item => item.Limitations?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(sourceLimitation))
        {
            limitationParts.Add(sourceLimitation);
        }

        var statistics = CommunityEvidenceChartPolicy.CalculateStatistics(new CommunityEvidenceChartBlock
        {
            Points = points
        });
        var metricLabel = items
            .Select(item => item.MetricLabel?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "관측값";
        var interpretation = numericMode
            ? $"선택 기간의 {metricLabel} 평균은 {statistics.Average:N2} {unit}, 최솟값은 {statistics.Minimum:N2}, 최댓값은 {statistics.Maximum:N2}입니다."
            : $"선택 기간에 자료 {RecordCount:N0}건이 확인됐고, 구간당 평균은 {statistics.Average:N2}건입니다.";
        return new CommunityEvidenceChartBlock
        {
            ChartTypeCode = CommunityEvidenceChartTypeCodes.Line,
            Title = Limit(numericMode ? $"기간별 {metricLabel} 평균" : "기간별 수집 자료 수", 120),
            Claim = numericMode
                ? "선택한 기간의 숫자 관측값이 구간마다 어떻게 달라졌는지 확인합니다."
                : "선택한 기간에 검토할 자료가 어느 구간에 모였는지 확인합니다.",
            SeriesLabel = Limit(numericMode ? $"{metricLabel} 평균" : "자료 수", 80),
            Unit = Limit(unit, 30),
            SourceLabel = Limit(sourceLabel, 160),
            SourceUrl = sourceUrl,
            ReferenceDate = $"{startDate:yyyy-MM-dd} ~ {endDate:yyyy-MM-dd}",
            Interpretation = Limit(interpretation, 500),
            Limitation = Limit(string.Join(" ", limitationParts), 500),
            Points = points
        };
    }

    private string BuildSourceLabel(IReadOnlyList<CommunityInformationCandidateDto> items)
    {
        var selectedSource = Sources.FirstOrDefault(source =>
            string.Equals(source.SourceKey, SourceKey, StringComparison.OrdinalIgnoreCase));
        if (selectedSource is not null)
        {
            return selectedSource.DisplayName;
        }

        var providers = items
            .Select(item => item.Provider.Trim())
            .Where(provider => provider.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        return providers.Length == 0 ? "커뮤니티 수집 보관 자료" : string.Join(", ", providers);
    }

    private static CommunityPeriodStatisticsBucket BuildBucket(
        PeriodBoundary boundary,
        IReadOnlyList<CommunityInformationCandidateDto> items,
        bool numericMode)
    {
        var bucketItems = items
            .Where(item => OverlapsRange(item, boundary.StartDate, boundary.EndDate))
            .ToArray();
        var numericValues = bucketItems
            .Where(item => item.NumericValue.HasValue)
            .Select(item => item.NumericValue!.Value)
            .ToArray();
        decimal? value = numericMode
            ? numericValues.Length == 0
                ? null
                : Math.Round(numericValues.Average(), 2, MidpointRounding.AwayFromZero)
            : bucketItems.Length;
        return new CommunityPeriodStatisticsBucket(
            boundary.StartDate,
            boundary.EndDate,
            FormatBoundaryLabel(boundary),
            bucketItems.Length,
            numericValues.Length,
            value);
    }

    private static IReadOnlyList<PeriodBoundary> BuildBoundaries(
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlyList<CommunityInformationCandidateDto> items)
    {
        if (UsesCalendarMonthPeriods(items))
        {
            return BuildCalendarMonthBoundaries(startDate, endDate);
        }

        return BuildEvenDayBoundaries(startDate, endDate);
    }

    private static IReadOnlyList<PeriodBoundary> BuildEvenDayBoundaries(
        DateOnly startDate,
        DateOnly endDate)
    {
        var rangeDays = endDate.DayNumber - startDate.DayNumber + 1;
        var bucketCount = Math.Min(CommunityEvidenceChartPolicy.MaximumPointCount, rangeDays);
        var baseSize = rangeDays / bucketCount;
        var remainder = rangeDays % bucketCount;
        var boundaries = new List<PeriodBoundary>(bucketCount);
        var cursor = startDate;
        for (var index = 0; index < bucketCount; index++)
        {
            var bucketDays = baseSize + (index < remainder ? 1 : 0);
            var bucketEnd = cursor.AddDays(bucketDays - 1);
            boundaries.Add(new PeriodBoundary(cursor, bucketEnd));
            cursor = bucketEnd.AddDays(1);
        }

        return boundaries;
    }

    private static IReadOnlyList<PeriodBoundary> BuildCalendarMonthBoundaries(
        DateOnly startDate,
        DateOnly endDate)
    {
        var months = new List<PeriodBoundary>();
        var cursor = new DateOnly(startDate.Year, startDate.Month, 1);
        while (cursor <= endDate)
        {
            var monthEnd = new DateOnly(
                cursor.Year,
                cursor.Month,
                DateTime.DaysInMonth(cursor.Year, cursor.Month));
            months.Add(new PeriodBoundary(
                cursor < startDate ? startDate : cursor,
                monthEnd > endDate ? endDate : monthEnd));
            cursor = cursor.AddMonths(1);
        }

        var maximumCount = CommunityEvidenceChartPolicy.MaximumPointCount;
        if (months.Count <= maximumCount)
        {
            return months;
        }

        var grouped = new List<PeriodBoundary>(maximumCount);
        var baseSize = months.Count / maximumCount;
        var remainder = months.Count % maximumCount;
        var monthIndex = 0;
        for (var index = 0; index < maximumCount; index++)
        {
            var groupSize = baseSize + (index < remainder ? 1 : 0);
            var first = months[monthIndex];
            var last = months[monthIndex + groupSize - 1];
            grouped.Add(new PeriodBoundary(first.StartDate, last.EndDate));
            monthIndex += groupSize;
        }

        return grouped;
    }

    private static bool UsesCalendarMonthPeriods(
        IReadOnlyList<CommunityInformationCandidateDto> items)
        => items.Count > 0
           && items.All(IsCalendarMonthPeriod);

    private static bool IsCalendarMonthPeriod(CommunityInformationCandidateDto item)
    {
        if (item.ReferenceDate is not { } startDate
            || item.ReferencePeriodEndDate is not { } endDate
            || startDate.Day != 1
            || startDate.Year != endDate.Year
            || startDate.Month != endDate.Month)
        {
            return false;
        }

        return endDate.Day == DateTime.DaysInMonth(endDate.Year, endDate.Month);
    }

    private void UpdateAvailableSeries(IReadOnlyList<CommunityInformationCandidateDto> items)
    {
        AvailableSeries = items
            .Where(item => item.NumericValue.HasValue)
            .GroupBy(BuildSeriesSelectionKey, StringComparer.Ordinal)
            .Select(group =>
            {
                var sample = group.First();
                var label = sample.MetricSeriesLabel?.Trim();
                if (string.IsNullOrWhiteSpace(label))
                {
                    label = sample.Title.Trim();
                }

                var metricLabel = string.IsNullOrWhiteSpace(sample.MetricLabel)
                    ? "관측값"
                    : sample.MetricLabel.Trim();
                var unit = FormatNumericUnit(sample.CurrencyCode, sample.Unit);
                return new CommunityPeriodStatisticsSeriesOption(
                    group.Key,
                    $"{label} · {unit} · {group.Count():N0}건",
                    metricLabel,
                    unit,
                    group.Count());
            })
            .OrderByDescending(option => option.ObservationCount)
            .ThenBy(option => option.DisplayName)
            .ToArray();

        var selectedStillExists = AvailableSeries.Any(option => string.Equals(
            option.SelectionKey,
            _metricSeriesSelectionKey,
            StringComparison.Ordinal));
        var nextSelection = selectedStillExists
            ? _metricSeriesSelectionKey
            : AvailableSeries.Count == 1
                ? AvailableSeries[0].SelectionKey
                : string.Empty;
        if (!string.Equals(_metricSeriesSelectionKey, nextSelection, StringComparison.Ordinal))
        {
            _metricSeriesSelectionKey = nextSelection;
            OnPropertyChanged(nameof(MetricSeriesSelectionKey));
        }
    }

    private static string BuildSeriesSelectionKey(CommunityInformationCandidateDto item)
        => string.Join(
            '\u001f',
            item.SourceKey.Trim(),
            string.IsNullOrWhiteSpace(item.MetricSeriesKey)
                ? item.Title.Trim()
                : item.MetricSeriesKey.Trim(),
            item.MetricLabel?.Trim() ?? string.Empty,
            item.CurrencyCode?.Trim().ToUpperInvariant() ?? string.Empty,
            item.Unit?.Trim() ?? string.Empty);

    private static string? ResolveNumericUnit(IReadOnlyList<CommunityInformationCandidateDto> items)
    {
        var numericItems = items.Where(item => item.NumericValue.HasValue).ToArray();
        if (numericItems.Length == 0)
        {
            return null;
        }

        var units = numericItems
            .Select(item => FormatNumericUnit(item.CurrencyCode, item.Unit))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var seriesKeys = numericItems
            .Select(item => item.MetricSeriesKey?.Trim() ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return units.Length == 1 && seriesKeys.Length == 1 ? units[0] : null;
    }

    private static string FormatNumericUnit(string? currencyCode, string? unit)
    {
        var currency = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var normalizedUnit = unit?.Trim() ?? string.Empty;
        if (currency.Length == 0)
        {
            return normalizedUnit.Length == 0 ? "값" : normalizedUnit;
        }

        return normalizedUnit.Length == 0 ? currency : $"{currency}/{normalizedUnit}";
    }

    private static bool IsInRange(
        CommunityInformationCandidateDto item,
        DateOnly startDate,
        DateOnly endDate)
        => OverlapsRange(item, startDate, endDate);

    private static bool OverlapsRange(
        CommunityInformationCandidateDto item,
        DateOnly startDate,
        DateOnly endDate)
    {
        var itemStartDate = ResolveDate(item);
        var itemEndDate = item.ReferencePeriodEndDate.HasValue
                          && item.ReferencePeriodEndDate.Value >= itemStartDate
            ? item.ReferencePeriodEndDate.Value
            : itemStartDate;
        return itemEndDate >= startDate && itemStartDate <= endDate;
    }

    private static DateOnly ResolveDate(CommunityInformationCandidateDto item)
        => item.ReferenceDate
           ?? (item.PublishedAtUtc.HasValue
               ? DateOnly.FromDateTime(item.PublishedAtUtc.Value)
               : DateOnly.FromDateTime(item.CollectedAtUtc));

    private static string FormatBoundaryLabel(PeriodBoundary boundary)
        => boundary.StartDate == boundary.EndDate
            ? boundary.StartDate.ToString("yyyy-MM-dd")
            : $"{boundary.StartDate:MM-dd}~{boundary.EndDate:MM-dd}";

    private void ClearResult(bool clearStatus = true, bool clearSeriesOptions = true)
    {
        Buckets = [];
        Failures = [];
        QueryLimitReached = false;
        RecordCount = 0;
        NumericValueCount = 0;
        GeneratedAtUtc = null;
        Statistics = null;
        Preview = null;
        if (clearSeriesOptions)
        {
            AvailableSeries = [];
            if (_metricSeriesSelectionKey.Length > 0)
            {
                _metricSeriesSelectionKey = string.Empty;
                OnPropertyChanged(nameof(MetricSeriesSelectionKey));
            }
        }

        if (clearStatus)
        {
            StatusMessage = null;
            StatusKind = CommunityComposerMessageKind.Info;
        }
    }

    private void SetDateInput(ref DateTime? storage, DateTime? value)
    {
        var normalized = value?.Date;
        if (SetProperty(ref storage, normalized))
        {
            ClearResult();
        }
    }

    private void SetFilter(ref string storage, string value)
    {
        if (SetProperty(ref storage, value))
        {
            ClearResult();
        }
    }

    private void SetStatus(string message, CommunityComposerMessageKind kind)
    {
        StatusKind = kind;
        StatusMessage = message;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Limit(string value, int maximumLength)
        => value.Length <= maximumLength ? value : value[..maximumLength];

    private sealed record PeriodBoundary(DateOnly StartDate, DateOnly EndDate);
}
