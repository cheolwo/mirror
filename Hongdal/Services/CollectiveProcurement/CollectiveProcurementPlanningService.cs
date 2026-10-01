using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hongdal.Contracts.Common.CollectiveProcurement;

namespace Hongdal.Services.CollectiveProcurement;

public interface ICollectiveProcurementPlanningClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemCollectiveProcurementPlanningClock : ICollectiveProcurementPlanningClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class CollectiveProcurementPlanState
{
    public Guid PlanId { get; set; }
    public long PlanRevision { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SourceTypeCode { get; set; } = string.Empty;
    public string SourceReferenceId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public List<CollectiveProcurementPlanParticipantState> Participants { get; set; } = [];
    public List<CollectiveProcurementCalculationRevisionState> CalculationRevisions { get; set; } = [];
}

public sealed class CollectiveProcurementPlanParticipantState
{
    public string ReferenceCode { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public int? ExactDisclosureConsentCalculationRevision { get; set; }
    public DateTimeOffset? ExactDisclosureConsentedAtUtc { get; set; }
    public int? AcceptedCalculationRevision { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
}

public sealed class CollectiveProcurementCalculationRevisionState
{
    public int CalculationRevision { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public CollectiveProcurementAssessmentRequest Request { get; set; } = new();
    public CollectiveProcurementAssessmentResponse Assessment { get; set; } = new();
}

public interface ICollectiveProcurementPlanningStore
{
    Task<CollectiveProcurementPlanState?> GetAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollectiveProcurementPlanState>> ListBySourceAsync(
        string sourceTypeCode,
        string sourceReferenceId,
        CancellationToken cancellationToken = default);

    Task CreateAsync(
        CollectiveProcurementPlanState state,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        CollectiveProcurementPlanState state,
        long expectedPlanRevision,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryCollectiveProcurementPlanningStore : ICollectiveProcurementPlanningStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<Guid, Entry> plans = new();

    public Task<CollectiveProcurementPlanState?> GetAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!plans.TryGetValue(planId, out var entry))
        {
            return Task.FromResult<CollectiveProcurementPlanState?>(null);
        }

        lock (entry.SyncRoot)
        {
            return Task.FromResult<CollectiveProcurementPlanState?>(Clone(entry.State));
        }
    }

    public Task<IReadOnlyList<CollectiveProcurementPlanState>> ListBySourceAsync(
        string sourceTypeCode,
        string sourceReferenceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceType = sourceTypeCode?.Trim() ?? string.Empty;
        var sourceReference = sourceReferenceId?.Trim() ?? string.Empty;
        if (sourceType.Length == 0 || sourceReference.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<CollectiveProcurementPlanState>>([]);
        }

        var matches = plans.Values
            .Select(entry =>
            {
                lock (entry.SyncRoot)
                {
                    return Clone(entry.State);
                }
            })
            .Where(state => string.Equals(state.SourceTypeCode, sourceType, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(state.SourceReferenceId, sourceReference, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(state => state.UpdatedAtUtc)
            .ToArray();
        return Task.FromResult<IReadOnlyList<CollectiveProcurementPlanState>>(matches);
    }

    public Task CreateAsync(
        CollectiveProcurementPlanState state,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(state);
        if (!plans.TryAdd(state.PlanId, new Entry(Clone(state))))
        {
            throw new CollectiveProcurementPlanConcurrencyException("공동조달계획이 다른 요청에서 먼저 생성되었습니다.");
        }

        return Task.CompletedTask;
    }

    public Task SaveAsync(
        CollectiveProcurementPlanState state,
        long expectedPlanRevision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(state);
        if (!plans.TryGetValue(state.PlanId, out var entry))
        {
            throw new KeyNotFoundException("공동조달계획을 찾을 수 없습니다.");
        }

        lock (entry.SyncRoot)
        {
            if (entry.State.PlanRevision != expectedPlanRevision)
            {
                throw new CollectiveProcurementPlanConcurrencyException(
                    "공동조달계획이 다른 참여자에 의해 먼저 변경되었습니다.");
            }

            entry.State = Clone(state);
        }

        return Task.CompletedTask;
    }

    private static CollectiveProcurementPlanState Clone(CollectiveProcurementPlanState state)
        => JsonSerializer.Deserialize<CollectiveProcurementPlanState>(
               JsonSerializer.Serialize(state, JsonOptions),
               JsonOptions)
           ?? throw new InvalidOperationException("공동조달계획 상태를 복제할 수 없습니다.");

    private sealed class Entry
    {
        public Entry(CollectiveProcurementPlanState state)
        {
            State = state;
        }

        public object SyncRoot { get; } = new();
        public CollectiveProcurementPlanState State { get; set; }
    }
}

public interface ICollectiveProcurementPlanningService
{
    Task<CollectiveProcurementPlanResponse> CreateAsync(
        CreateCollectiveProcurementPlanRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<CollectiveProcurementPlanResponse?> GetAsync(
        Guid planId,
        string viewerUserId,
        CancellationToken cancellationToken = default);

    Task<CollectiveProcurementPlanResponse> RecalculateAsync(
        Guid planId,
        RecalculateCollectiveProcurementPlanRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<CollectiveProcurementPlanResponse> UpdateDisclosureConsentAsync(
        Guid planId,
        UpdateCollectiveProcurementDisclosureConsentRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<CollectiveProcurementPlanResponse> AcceptRevisionAsync(
        Guid planId,
        AcceptCollectiveProcurementRevisionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed partial class CollectiveProcurementPlanningService : ICollectiveProcurementPlanningService
{
    private const int MaximumParticipants = 100;
    private const int MaximumTitleLength = 200;
    private const int MaximumDisplayNameLength = 100;
    private const int MaximumRoleCodeLength = 80;

    private readonly ICollectiveProcurementPlanningStore store;
    private readonly ICollectiveProcurementEconomicsEngine economicsEngine;
    private readonly ICollectiveProcurementPlanningClock clock;

    public CollectiveProcurementPlanningService(
        ICollectiveProcurementPlanningStore store,
        ICollectiveProcurementEconomicsEngine economicsEngine,
        ICollectiveProcurementPlanningClock clock)
    {
        this.store = store;
        this.economicsEngine = economicsEngine;
        this.clock = clock;
    }

    public async Task<CollectiveProcurementPlanResponse> CreateAsync(
        CreateCollectiveProcurementPlanRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ownerUserId = RequireUserId(actorUserId);
        var participants = ValidateAndCreateParticipants(request.Participants, ownerUserId);
        ValidateBenefitPositionCoverage(request.Assessment, participants);
        var now = clock.UtcNow;
        var assessment = economicsEngine.Evaluate(request.Assessment, now);
        var state = new CollectiveProcurementPlanState
        {
            PlanId = Guid.NewGuid(),
            PlanRevision = 1,
            OwnerUserId = ownerUserId,
            Title = RequireText(request.Title, "계획 제목", MaximumTitleLength),
            SourceTypeCode = Clean(request.SourceTypeCode, 80),
            SourceReferenceId = Clean(request.SourceReferenceId, 160),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Participants = participants,
            CalculationRevisions =
            [
                new CollectiveProcurementCalculationRevisionState
                {
                    CalculationRevision = 1,
                    CreatedByUserId = ownerUserId,
                    CreatedAtUtc = now,
                    Request = request.Assessment,
                    Assessment = assessment
                }
            ]
        };

        await store.CreateAsync(state, cancellationToken);
        return BuildResponse(state, ownerUserId);
    }

    public async Task<CollectiveProcurementPlanResponse?> GetAsync(
        Guid planId,
        string viewerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(planId, Guid.Empty);
        var state = await store.GetAsync(planId, cancellationToken);
        if (state is null)
        {
            return null;
        }

        var viewer = RequireUserId(viewerUserId);
        EnsureParticipant(state, viewer);
        return BuildResponse(state, viewer);
    }

    public async Task<CollectiveProcurementPlanResponse> RecalculateAsync(
        Guid planId,
        RecalculateCollectiveProcurementPlanRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireUserId(actorUserId);
        var state = await GetRequiredStateAsync(planId, cancellationToken);
        EnsureExpectedRevision(state, request.ExpectedPlanRevision);
        if (!string.Equals(state.OwnerUserId, actor, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("공동조달계획을 만든 참여자만 계산 조건과 참여자 구성을 변경할 수 있습니다.");
        }

        var participants = request.Participants is null
            ? state.Participants.Select(CloneParticipantWithoutDecisions).ToList()
            : ValidateAndCreateParticipants(request.Participants, state.OwnerUserId);
        ValidateBenefitPositionCoverage(request.Assessment, participants);
        var now = clock.UtcNow;
        var nextCalculationRevision = state.CalculationRevisions.Max(item => item.CalculationRevision) + 1;
        state.Participants = participants;
        state.CalculationRevisions.Add(new CollectiveProcurementCalculationRevisionState
        {
            CalculationRevision = nextCalculationRevision,
            CreatedByUserId = actor,
            CreatedAtUtc = now,
            Request = request.Assessment,
            Assessment = economicsEngine.Evaluate(request.Assessment, now)
        });
        state.PlanRevision++;
        state.UpdatedAtUtc = now;

        await store.SaveAsync(state, request.ExpectedPlanRevision, cancellationToken);
        return BuildResponse(state, actor);
    }

    public async Task<CollectiveProcurementPlanResponse> UpdateDisclosureConsentAsync(
        Guid planId,
        UpdateCollectiveProcurementDisclosureConsentRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireUserId(actorUserId);
        var state = await GetRequiredStateAsync(planId, cancellationToken);
        EnsureExpectedRevision(state, request.ExpectedPlanRevision);
        var currentRevision = CurrentCalculation(state).CalculationRevision;
        EnsureCurrentCalculationRevision(currentRevision, request.CalculationRevision);
        var participant = EnsureParticipant(state, actor);

        if (request.AllowExactBenefitDisclosure
            && (!request.ConfirmCommercialInformationHandling
                || !request.ConfirmIndependentPricingDecision))
        {
            throw new InvalidOperationException(
                "정확한 편익을 공유하려면 상업정보 취급과 독립적인 가격 결정을 모두 확인해야 합니다.");
        }

        participant.ExactDisclosureConsentCalculationRevision = request.AllowExactBenefitDisclosure
            ? currentRevision
            : null;
        participant.ExactDisclosureConsentedAtUtc = request.AllowExactBenefitDisclosure
            ? clock.UtcNow
            : null;
        state.PlanRevision++;
        state.UpdatedAtUtc = clock.UtcNow;

        await store.SaveAsync(state, request.ExpectedPlanRevision, cancellationToken);
        return BuildResponse(state, actor);
    }

    public async Task<CollectiveProcurementPlanResponse> AcceptRevisionAsync(
        Guid planId,
        AcceptCollectiveProcurementRevisionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireUserId(actorUserId);
        var state = await GetRequiredStateAsync(planId, cancellationToken);
        EnsureExpectedRevision(state, request.ExpectedPlanRevision);
        var current = CurrentCalculation(state);
        EnsureCurrentCalculationRevision(current.CalculationRevision, request.CalculationRevision);
        var participant = EnsureParticipant(state, actor);

        if (!current.Assessment.BenefitAgreementReady)
        {
            throw new InvalidOperationException(
                "전체 편익 범위, 참여자별 비공개 최소조건과 합의한 편익 집중 상한을 먼저 충족해야 합니다.");
        }

        if (!request.ConfirmValuesAreEstimates || !request.ConfirmIndependentDecision)
        {
            throw new InvalidOperationException(
                "계산값이 추정치라는 점과 참여자가 독립적으로 결정한다는 점을 모두 확인해야 합니다.");
        }

        participant.AcceptedCalculationRevision = current.CalculationRevision;
        participant.AcceptedAtUtc = clock.UtcNow;
        state.PlanRevision++;
        state.UpdatedAtUtc = clock.UtcNow;

        await store.SaveAsync(state, request.ExpectedPlanRevision, cancellationToken);
        return BuildResponse(state, actor);
    }

    private static CollectiveProcurementPlanResponse BuildResponse(
        CollectiveProcurementPlanState state,
        string viewerUserId)
    {
        var current = CurrentCalculation(state);
        var positions = current.Request.BenefitPositions.ToDictionary(
            position => position.ParticipantReferenceCode,
            StringComparer.OrdinalIgnoreCase);
        var exactDisclosureActive = state.Participants.Count > 0
                                    && state.Participants.All(participant =>
                                        participant.ExactDisclosureConsentCalculationRevision
                                        == current.CalculationRevision);
        var allAccepted = state.Participants.Count > 0
                          && state.Participants.All(participant =>
                              participant.AcceptedCalculationRevision == current.CalculationRevision);
        var executionReady = allAccepted
                             && current.Assessment.BenefitAgreementReady
                             && current.Assessment.CurrentQuantityEconomicallyViable;
        var totalProposed = current.Request.BenefitPositions.Sum(position => position.ProposedBenefitAmount);
        var participants = state.Participants.Select(participant =>
        {
            var position = positions[participant.ReferenceCode];
            var isViewer = string.Equals(participant.UserId, viewerUserId, StringComparison.Ordinal);
            var canSeeExact = isViewer || exactDisclosureActive;
            return new CollectiveProcurementParticipantResponse
            {
                ReferenceCode = participant.ReferenceCode,
                DisplayName = participant.DisplayName,
                RoleCode = participant.RoleCode,
                BenefitKindCode = position.BenefitKindCode,
                IsCurrentViewer = isViewer,
                HasExactDisclosureConsentForCurrentRevision =
                    participant.ExactDisclosureConsentCalculationRevision == current.CalculationRevision,
                HasAcceptedCurrentRevision =
                    participant.AcceptedCalculationRevision == current.CalculationRevision,
                ProposedBenefitAmount = canSeeExact ? position.ProposedBenefitAmount : null,
                BenefitSharePercent = canSeeExact && totalProposed > 0m
                    ? Math.Round(position.ProposedBenefitAmount / totalProposed * 100m, 2, MidpointRounding.AwayFromZero)
                    : null,
                MinimumAcceptableBenefitAmount = isViewer ? position.MinimumAcceptableBenefitAmount : null,
                MeetsPrivateMinimum = isViewer
                    ? position.ProposedBenefitAmount >= position.MinimumAcceptableBenefitAmount
                    : null
            };
        }).ToArray();

        return new CollectiveProcurementPlanResponse
        {
            PlanId = state.PlanId,
            PlanRevision = state.PlanRevision,
            CurrentCalculationRevision = current.CalculationRevision,
            Title = state.Title,
            SourceTypeCode = state.SourceTypeCode,
            SourceReferenceId = state.SourceReferenceId,
            StatusCode = ResolveStatus(current.Assessment, allAccepted, executionReady),
            ExactBenefitDisclosureActive = exactDisclosureActive,
            AllParticipantsAcceptedCurrentRevision = allAccepted,
            ExecutionReady = executionReady,
            CreatedAtUtc = state.CreatedAtUtc,
            UpdatedAtUtc = state.UpdatedAtUtc,
            DisclosurePolicy = new CollectiveProcurementDisclosurePolicyResponse(),
            CurrentAssessment = current.Assessment,
            Participants = participants
        };
    }

    private async Task<CollectiveProcurementPlanState> GetRequiredStateAsync(
        Guid planId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(planId, Guid.Empty);
        return await store.GetAsync(planId, cancellationToken)
               ?? throw new KeyNotFoundException("공동조달계획을 찾을 수 없습니다.");
    }

    private static List<CollectiveProcurementPlanParticipantState> ValidateAndCreateParticipants(
        IReadOnlyCollection<CollectiveProcurementParticipantRequest> participants,
        string ownerUserId)
    {
        if (participants.Count is < 2 or > MaximumParticipants)
        {
            throw new ArgumentException($"공동조달계획에는 2명 이상 {MaximumParticipants}명 이하의 참여자가 필요합니다.");
        }

        var result = participants.Select(participant =>
        {
            ArgumentNullException.ThrowIfNull(participant);
            var referenceCode = participant.ReferenceCode?.Trim() ?? string.Empty;
            if (!ReferenceCodePattern().IsMatch(referenceCode))
            {
                throw new ArgumentException("참여자 참조 코드는 영문자 또는 숫자로 시작하는 64자 이하 코드여야 합니다.");
            }

            return new CollectiveProcurementPlanParticipantState
            {
                ReferenceCode = referenceCode,
                UserId = RequireUserId(participant.UserId),
                DisplayName = RequireText(participant.DisplayName, "참여자 표시명", MaximumDisplayNameLength),
                RoleCode = RequireText(participant.RoleCode, "참여자 역할", MaximumRoleCodeLength)
            };
        }).ToList();

        if (result.GroupBy(item => item.ReferenceCode, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)
            || result.GroupBy(item => item.UserId, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("참여자 참조 코드와 사용자 식별자는 중복될 수 없습니다.");
        }

        if (!result.Any(item => string.Equals(item.UserId, ownerUserId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("계획을 만드는 사용자가 참여자 목록에 포함되어야 합니다.");
        }

        return result;
    }

    private static void ValidateBenefitPositionCoverage(
        CollectiveProcurementAssessmentRequest assessment,
        IReadOnlyCollection<CollectiveProcurementPlanParticipantState> participants)
    {
        var participantReferences = participants
            .Select(participant => participant.ReferenceCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var positionReferences = assessment.BenefitPositions
            .Select(position => position.ParticipantReferenceCode?.Trim() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!participantReferences.SetEquals(positionReferences))
        {
            throw new ArgumentException("모든 참여자에 대해 하나씩 편익 제안과 비공개 최소조건을 입력해야 합니다.");
        }
    }

    private static CollectiveProcurementPlanParticipantState CloneParticipantWithoutDecisions(
        CollectiveProcurementPlanParticipantState participant)
        => new()
        {
            ReferenceCode = participant.ReferenceCode,
            UserId = participant.UserId,
            DisplayName = participant.DisplayName,
            RoleCode = participant.RoleCode
        };

    private static CollectiveProcurementCalculationRevisionState CurrentCalculation(
        CollectiveProcurementPlanState state)
        => state.CalculationRevisions.MaxBy(item => item.CalculationRevision)
           ?? throw new InvalidOperationException("공동조달계획에 계산 리비전이 없습니다.");

    private static CollectiveProcurementPlanParticipantState EnsureParticipant(
        CollectiveProcurementPlanState state,
        string userId)
        => state.Participants.FirstOrDefault(participant =>
               string.Equals(participant.UserId, userId, StringComparison.Ordinal))
           ?? throw new UnauthorizedAccessException("이 공동조달계획의 참여자만 조회하거나 동의할 수 있습니다.");

    private static void EnsureExpectedRevision(CollectiveProcurementPlanState state, long expectedRevision)
    {
        if (state.PlanRevision != expectedRevision)
        {
            throw new CollectiveProcurementPlanConcurrencyException(
                "공동조달계획이 이미 변경되었습니다. 최신 리비전을 다시 조회해 주세요.");
        }
    }

    private static void EnsureCurrentCalculationRevision(int currentRevision, int requestedRevision)
    {
        if (currentRevision != requestedRevision)
        {
            throw new InvalidOperationException("최신 계산 리비전에 대해서만 공개 동의하거나 수락할 수 있습니다.");
        }
    }

    private static string ResolveStatus(
        CollectiveProcurementAssessmentResponse assessment,
        bool allAccepted,
        bool executionReady)
    {
        if (executionReady)
        {
            return CollectiveProcurementPlanStatusCodes.ReadyForExecution;
        }

        if (allAccepted)
        {
            return CollectiveProcurementPlanStatusCodes.TargetAgreed;
        }

        if (!assessment.CurrentQuantityEconomicallyViable)
        {
            return CollectiveProcurementPlanStatusCodes.CollectingDemand;
        }

        return assessment.BenefitAgreementReady
            ? CollectiveProcurementPlanStatusCodes.AwaitingAcceptance
            : CollectiveProcurementPlanStatusCodes.ResolvingBenefitTerms;
    }

    private static string RequireUserId(string? value)
        => RequireText(value, "사용자 식별자", 160);

    private static string RequireText(string? value, string fieldName, int maximumLength)
    {
        var cleaned = value?.Trim() ?? string.Empty;
        if (cleaned.Length == 0 || cleaned.Length > maximumLength)
        {
            throw new ArgumentException($"{fieldName}은 1자 이상 {maximumLength}자 이하여야 합니다.");
        }

        return cleaned;
    }

    private static string Clean(string? value, int maximumLength)
    {
        var cleaned = value?.Trim() ?? string.Empty;
        if (cleaned.Length > maximumLength)
        {
            throw new ArgumentException($"참조 정보는 {maximumLength}자를 초과할 수 없습니다.");
        }

        return cleaned;
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceCodePattern();
}

public sealed class CollectiveProcurementPlanConcurrencyException : Exception
{
    public CollectiveProcurementPlanConcurrencyException(string message)
        : base(message)
    {
    }

    public CollectiveProcurementPlanConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
