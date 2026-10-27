using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Community;

/// <summary>공간 모임의 합의만 소유합니다. 실제 상품·입고·배송·수령·정산은 전문 원장 bridge가 조회합니다.</summary>
public sealed class 공간공동운영UseCase(I공간공동운영Store store, ICurrentUserAccessor currentUser,
    ISsalddelExecutionModePolicy execution, I생활교류공개지역Source regions, TimeProvider clock,
    I공간공동운영전문업무Bridge bridge) : I공간공동운영UseCase, I공간공동운영연결Query
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] ReferenceKinds = ["DistributedSite", "StorageAgreement", "InboundPlan", "DistributedWave", "DistributedOrder", "RootOrderLedger", "MartOrder", "Settlement"];
    private string? Actor => string.IsNullOrWhiteSpace(currentUser.UserId) ? null : currentUser.UserId;

    public async Task<Result<공간공동운영상세Response>> 생성Async(공간공동운영생성Request request, CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor) return Fail("AuthenticationRequired", "로그인 후 제안할 수 있습니다.", 401);
        if (request is null || request.ClientRequestId == Guid.Empty || !request.ConfirmNonBinding || !공간공동운영역할Codes.IsKnown(request.RoleCode))
            return Fail("InvalidProposal", "요청 번호·본인 역할과 비구속 참여 안내를 확인해 주세요.", 400);
        var id = StableId(actor, request.ClientRequestId);
        var fingerprint = Fingerprint(request);
        if (await store.조회Async(id, cancellationToken) is { } prior)
            return prior.OwnerUserId == actor && prior.CreateFingerprint == fingerprint
                ? Result.Ok(await ToResponseAsync(prior, actor, true, cancellationToken))
                : Fail("IdempotencyConflict", "같은 요청 번호로 다른 제안을 만들 수 없습니다.", 409);
        var invalid = await ValidateTermsAsync(request.Terms, cancellationToken);
        if (invalid is not null) return Fail("InvalidTerms", invalid, 400);
        if (!ValidIntroduction(request.IntroductionConsented, request.PublicDisplayName))
            return Fail("InvalidIntroduction", "공개할 이름에서 상세 주소·연락처를 빼고 소개 동의를 확인해 주세요.", 400);
        var now = clock.GetUtcNow();
        var record = new 공간공동운영Record
        {
            LedgerId = id, Revision = 1, TermsRevision = 1, OwnerUserId = actor,
            Terms = Clone(request.Terms), PublishProposal = request.PublishProposal,
            ExecutionMode = execution.Mode.ToString(), CreateRequestId = request.ClientRequestId, CreateFingerprint = fingerprint,
            CreatedAtUtc = now, UpdatedAtUtc = now,
            Participants = [new() { UserId = actor, RoleCode = request.RoleCode,
                IntroductionConsented = request.IntroductionConsented,
                PublicDisplayName = request.IntroductionConsented ? request.PublicDisplayName.Trim() : "" }]
        };
        if (!await store.생성Async(record, cancellationToken))
        {
            var concurrent = await store.조회Async(id, cancellationToken);
            return concurrent is not null && concurrent.OwnerUserId == actor && concurrent.CreateFingerprint == fingerprint
                ? Result.Ok(await ToResponseAsync(concurrent, actor, true, cancellationToken))
                : Fail("RevisionConflict", "제안 결과를 다시 확인해 주세요.", 409);
        }
        return Result.Ok(await ToResponseAsync(record, actor, false, cancellationToken));
    }

    public async Task<Result<공간공동운영상세Response>> 변경Async(string ledgerId, 공간공동운영명령Request request, CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor) return Fail("AuthenticationRequired", "로그인 후 참여할 수 있습니다.", 401);
        if (request is null || request.ClientRequestId == Guid.Empty || request.ExpectedRevision < 1 || request.Action?.Length is not (> 0 and <= 60))
            return Fail("InvalidCommand", "현재 요청과 판본을 확인해 주세요.", 400);
        var record = await store.조회Async(ledgerId, cancellationToken);
        if (record is null) return Fail("NotFound", "제안을 찾을 수 없습니다.", 404);
        if (record.ExecutionMode != execution.Mode.ToString() && request.Action is not (공간공동운영명령Codes.IntroductionConsent or 공간공동운영명령Codes.Withdraw or 공간공동운영명령Codes.ProposalVisibility))
            return Fail("ExecutionModeChanged", "현재 실행 모드에서 이 제안의 업무를 이어갈 수 없습니다. 공개 동의 철회는 가능합니다.", 409);
        var ownRoles = record.Participants.Where(item => item.UserId == actor).ToArray();
        if (ownRoles.Length > 1 && request.RoleCode is null && request.Action is 공간공동운영명령Codes.AcceptRole or 공간공동운영명령Codes.Withdraw or 공간공동운영명령Codes.IntroductionConsent or 공간공동운영명령Codes.AgreeTerms)
            return Fail("RoleRequired", "이번에 선택할 본인 역할을 지정해 주세요.", 400);
        var member = ownRoles.FirstOrDefault(item => request.RoleCode is null || item.RoleCode == request.RoleCode);
        if (!CanRead(record, actor) && !(request.Action == 공간공동운영명령Codes.Join && record.PublishProposal && record.StatusCode == "gathering"))
            return Fail("NotFound", "현재 계정의 참여 제안을 찾을 수 없습니다.", 404);
        var fingerprint = Fingerprint(request);
        var receipt = record.Receipts.SingleOrDefault(item => item.ClientRequestId == request.ClientRequestId);
        if (receipt is not null)
            return receipt.ActorUserId == actor && receipt.Fingerprint == fingerprint
                ? Result.Ok(await ToResponseAsync(record, actor, true, cancellationToken))
                : Fail("IdempotencyConflict", "같은 요청 번호로 다른 결정을 보낼 수 없습니다.", 409);
        if (request.ClientRequestId == record.CreateRequestId) return Fail("IdempotencyConflict", "제안 접수 번호를 명령에 다시 쓸 수 없습니다.", 409);
        if (record.Revision != request.ExpectedRevision) return Fail("RevisionConflict", "다른 참여자가 내용을 바꿨습니다. 최신 내용을 확인해 주세요.", 409);
        if (record.Receipts.Count >= 1500) return Fail("CommandLimitReached", "이 제안의 기록 한도에 도달했습니다. 현재 업무를 정리해 주세요.", 409);
        if (!AllowedActions(record, actor).Contains(request.Action, StringComparer.Ordinal))
            return Fail("ActionNotAllowed", "현재 상태나 역할에서는 이 결정을 할 수 없습니다.", 409);
        var now = clock.GetUtcNow();
        switch (request.Action)
        {
            case 공간공동운영명령Codes.Join:
                if (!request.ConfirmNonBinding || !공간공동운영역할Codes.IsKnown(request.RoleCode))
                    return Fail("NonBindingConsentRequired", "본인 역할과 비구속 참여 안내를 확인해 주세요.", 400);
                if (record.Participants.Count >= 300 && member is null) return Fail("ParticipantLimitReached", "현재 제안의 참여 한도에 도달했습니다.", 409);
                if (member is { StateCode: not "withdrawn" }) return Fail("RoleAlreadyJoined", "이 역할에는 이미 참여하고 있습니다.", 409);
                if (member is null) record.Participants.Add(member = new() { UserId = actor });
                member.RoleCode = request.RoleCode!; member.StateCode = "interested"; member.AgreedTermsRevision = 0;
                break;
            case 공간공동운영명령Codes.AcceptRole:
                if (member is null || member.StateCode != "interested") return Fail("RoleRequired", "먼저 이 역할의 관심 참여를 선택해 주세요.", 409);
                if (!request.ConfirmNonBinding) return Fail("NonBindingConsentRequired", "역할 참여는 근무·계약·실행 권한이 아님을 확인해 주세요.", 400);
                member!.StateCode = "accepted"; member.AgreedTermsRevision = 0;
                break;
            case 공간공동운영명령Codes.Withdraw:
                if (member is null || member.StateCode == "withdrawn") return Fail("RoleRequired", "철회할 본인 역할을 선택해 주세요.", 409);
                member!.StateCode = "withdrawn"; member.AgreedTermsRevision = 0;
                member.IntroductionConsented = false; member.PublicDisplayName = "";
                var retainsOperatorRole = record.Participants.Any(item => item.UserId == actor && item.StateCode == "accepted"
                    && item.RoleCode is 공간공동운영역할Codes.SpaceProvider or 공간공동운영역할Codes.Coordinator);
                if (!retainsOperatorRole && record.OperatorUserId == actor)
                { record.OperatorUserId = null; record.PendingOperatorUserId = null; record.PendingOperatorTermsRevision = 0; }
                if (!retainsOperatorRole && record.PendingOperatorUserId == actor)
                { record.PendingOperatorUserId = null; record.PendingOperatorTermsRevision = 0; }
                break;
            case 공간공동운영명령Codes.IntroductionConsent:
                if (member is null || member.StateCode == "withdrawn") return Fail("RoleRequired", "공개 소개를 정할 본인 역할을 선택해 주세요.", 409);
                if (request.Consented is null || !ValidIntroduction(request.Consented.Value, request.PublicDisplayName))
                    return Fail("InvalidIntroduction", "공개 소개 동의와 이름을 확인해 주세요. 상세 주소·연락처는 적지 마세요.", 400);
                member!.IntroductionConsented = request.Consented.Value;
                member.PublicDisplayName = request.Consented.Value ? request.PublicDisplayName!.Trim() : "";
                break;
            case 공간공동운영명령Codes.AcceptOperator:
                if (!ownRoles.Any(item => item.RoleCode == 공간공동운영역할Codes.SpaceProvider && item.StateCode == "accepted") || request.Consented != true
                    || record.OperatorUserId is not null && record.OperatorUserId != actor)
                    return Fail("OperatorAcceptanceRequired", "공간 제공 역할을 수락한 본인이 운영 담당을 명시적으로 수락해야 합니다.", 409);
                record.OperatorUserId = actor;
                break;
            case 공간공동운영명령Codes.ProposeOperatorHandover:
                if (request.Consented != true || string.IsNullOrWhiteSpace(request.TargetUserId)
                    || request.TargetUserId == actor || !record.Participants.Any(item => item.UserId == request.TargetUserId
                        && item.StateCode == "accepted" && item.RoleCode is 공간공동운영역할Codes.Coordinator or 공간공동운영역할Codes.SpaceProvider))
                    return Fail("OperatorHandoverRequired", "참여를 수락한 공간 제공자 또는 조율 담당자를 지정해 인계를 제안해 주세요.", 409);
                record.PendingOperatorUserId = request.TargetUserId; record.PendingOperatorTermsRevision = record.TermsRevision;
                break;
            case 공간공동운영명령Codes.AcceptOperatorHandover:
                if (request.Consented != true || record.PendingOperatorUserId != actor || record.PendingOperatorTermsRevision != record.TermsRevision
                    || record.OperatorUserId is null || !record.Participants.Any(item => item.UserId == actor && item.StateCode == "accepted"
                        && item.RoleCode is 공간공동운영역할Codes.Coordinator or 공간공동운영역할Codes.SpaceProvider))
                    return Fail("OperatorHandoverChanged", "현재 운영 담당자의 인계 제안을 본인이 확인해 주세요.", 409);
                record.OperatorUserId = actor; record.PendingOperatorUserId = null; record.PendingOperatorTermsRevision = 0;
                break;
            case 공간공동운영명령Codes.ReleaseOperator:
                record.OperatorUserId = null; record.PendingOperatorUserId = null; record.PendingOperatorTermsRevision = 0;
                break;
            case 공간공동운영명령Codes.UpdateTerms:
                var invalid = await ValidateTermsAsync(request.Terms, cancellationToken);
                if (invalid is not null) return Fail("InvalidTerms", invalid, 400);
                record.Terms = Clone(request.Terms!); record.TermsRevision++;
                foreach (var item in record.Participants) item.AgreedTermsRevision = 0;
                // 동네나 소개 맥락이 바뀌면 이전 공개 동의를 새 조건에 승계하지 않습니다.
                foreach (var item in record.Participants) { item.IntroductionConsented = false; item.PublicDisplayName = ""; }
                record.OperatorUserId = null; record.PendingOperatorUserId = null; record.PendingOperatorTermsRevision = 0;
                break;
            case 공간공동운영명령Codes.AgreeTerms:
                if (member is null || member.StateCode != "accepted") return Fail("RoleRequired", "조건에 동의할 본인 수락 역할을 선택해 주세요.", 409);
                if (request.Consented != true || request.ExpectedTermsRevision != record.TermsRevision)
                    return Fail("TermsRevisionChanged", "현재 운영 조건 판본을 확인한 뒤 본인이 동의해 주세요.", 409);
                member!.AgreedTermsRevision = record.TermsRevision;
                break;
            case 공간공동운영명령Codes.CreateCycle:
                if (record.Cycles.Count >= 100) return Fail("CycleLimitReached", "회차 한도에 도달했습니다.", 409);
                record.Cycles.Add(new() { CycleId = "cycle-" + request.ClientRequestId.ToString("N"), Number = record.Cycles.Count + 1, TermsRevision = record.TermsRevision });
                break;
            case 공간공동운영명령Codes.LinkReference:
            {
                var cycle = record.Cycles.SingleOrDefault(item => item.CycleId == request.CycleId);
                if (cycle is null || cycle.StatusCode != "prepared" || cycle.TermsRevision != record.TermsRevision)
                    return Fail("CycleChanged", "현재 조건의 준비 회차를 먼저 선택해 주세요.", 409);
                if (!ValidReference(request.Reference)) return Fail("InvalidReference", "전문 원장의 종류·번호·판본을 확인해 주세요.", 400);
                if (request.Reference!.KindCode is "DistributedOrder" or "MartOrder" or "DistributedWave"
                    && record.Cycles.Any(item => item.CycleId != cycle.CycleId && item.References.Any(reference => reference.KindCode == request.Reference.KindCode && reference.ReferenceId == request.Reference.ReferenceId)))
                    return Fail("SourceAlreadyUsedInCycle", "이 주문·배송 처리 단위는 다른 회차에 이미 연결되어 있습니다. 남은 재고를 쓰더라도 다음 회차에는 새 주문을 연결해 주세요.", 409);
                var references = cycle.References.Where(item => !(item.KindCode == request.Reference!.KindCode && item.ReferenceId == request.Reference.ReferenceId))
                    .Append(Clone(request.Reference!)).ToArray();
                if (references.Length > 30) return Fail("ReferenceLimitReached", "한 회차에는 전문 원장 30개까지 연결할 수 있습니다.", 409);
                var actual = await bridge.ValidateAsync(actor, references, cancellationToken);
                if (!ReferencesVerified(references, actual)) return Fail("ReferenceNotVerified", "권한·현재 판본·같은 업무 관계가 확인된 전문 원장만 연결할 수 있습니다.", 409);
                var selected = actual.Single(source => source.KindCode == request.Reference.KindCode && source.ReferenceId == request.Reference.ReferenceId);
                if (request.Reference.ExpectedRevision.HasValue && selected.VerifiedRevision != request.Reference.ExpectedRevision)
                    return Fail("ReferenceRevisionChanged", "선택한 전문 원장의 판본이 바뀌었습니다. 최신 후보를 다시 확인해 주세요.", 409);
                cycle.References = references;
                break;
            }
            case 공간공동운영명령Codes.UnlinkReference:
            {
                var cycle = record.Cycles.SingleOrDefault(item => item.CycleId == request.CycleId);
                if (cycle is null || cycle.StatusCode != "prepared" || !ValidReference(request.Reference))
                    return Fail("CycleChanged", "준비 회차에서 해제할 전문 원장을 선택해 주세요.", 409);
                cycle.References = cycle.References.Where(item => item.KindCode != request.Reference!.KindCode || item.ReferenceId != request.Reference.ReferenceId).ToArray();
                break;
            }
            case 공간공동운영명령Codes.StartCycle:
            case 공간공동운영명령Codes.CompleteCycle:
            {
                var cycle = record.Cycles.SingleOrDefault(item => item.CycleId == request.CycleId);
                if (cycle is null || cycle.TermsRevision != record.TermsRevision) return Fail("CycleChanged", "현재 조건의 회차를 확인해 주세요.", 409);
                var readiness = await ReadinessAsync(record, actor, cycle, cancellationToken);
                if (request.Action == 공간공동운영명령Codes.StartCycle)
                {
                    if (cycle.StatusCode != "prepared" || !readiness.CanStart) return Fail("ReadinessRequired", string.Join(" ", readiness.Blockers), 409);
                    cycle.StatusCode = "running"; cycle.StartedAtUtc = now;
                }
                else
                {
                    if (cycle.StatusCode != "running" || !readiness.CanComplete) return Fail("SourceCompletionRequired", "실제 입고·전달·수령의 현재 결과를 먼저 확인해 주세요.", 409);
                    if ((request.ResultNote?.Length ?? 0) > 1000) return Fail("InvalidResultNote", "회차 결과는 1,000자 이내로 적어 주세요.", 400);
                    cycle.StatusCode = "completed"; cycle.CompletedAtUtc = now; cycle.SourceCompletionVerified = true; cycle.ResultNote = request.ResultNote?.Trim() ?? "";
                }
                break;
            }
            case 공간공동운영명령Codes.Pause: record.StatusCode = "paused"; break;
            case 공간공동운영명령Codes.Resume: record.StatusCode = "gathering"; break;
            case 공간공동운영명령Codes.Close: record.StatusCode = "closed"; record.PublishProposal = false; break;
            case 공간공동운영명령Codes.ProposalVisibility:
                if (request.Consented is null) return Fail("ConsentRequired", "제안 공개 여부를 선택해 주세요.", 400);
                record.PublishProposal = request.Consented.Value;
                break;
        }
        record.Revision++; record.UpdatedAtUtc = now;
        record.Receipts.Add(new() { ClientRequestId = request.ClientRequestId, ActorUserId = actor, Fingerprint = fingerprint });
        if (!await store.교체Async(record, request.ExpectedRevision, actor, cancellationToken))
        {
            var concurrent = await store.조회Async(record.LedgerId, cancellationToken);
            var applied = concurrent?.Receipts.SingleOrDefault(item => item.ClientRequestId == request.ClientRequestId);
            if (concurrent is not null && applied?.ActorUserId == actor && applied.Fingerprint == fingerprint)
                return Result.Ok(await ToResponseAsync(concurrent, actor, true, cancellationToken));
            return Fail("RevisionConflict", "다른 결정이 먼저 저장되었습니다. 최신 결과를 다시 확인해 주세요.", 409);
        }
        return Result.Ok(await ToResponseAsync(record, actor, false, cancellationToken));
    }

    public async Task<공간공동운영상세Response?> 상세Async(string ledgerId, CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor) return null;
        var record = await 조회Async(ledgerId, actor, cancellationToken);
        return record is null ? null : await ToResponseAsync(record, actor, false, cancellationToken);
    }

    public async Task<IReadOnlyList<공간공동운영연결후보Dto>?> 연결후보Async(string ledgerId, string cycleId, CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor) return null;
        var record = await 조회Async(ledgerId, actor, cancellationToken);
        var cycle = record?.Cycles.SingleOrDefault(item => item.CycleId == cycleId);
        if (record is null || cycle is null || record.OperatorUserId != actor || cycle.StatusCode != "prepared" || record.ExecutionMode != execution.Mode.ToString()) return null;
        return await bridge.CandidatesAsync(actor, cycle.References, cancellationToken);
    }

    public async Task<공간공동운영Record?> 조회Async(string ledgerId, string actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId)) return null;
        var record = await store.조회Async(ledgerId, cancellationToken);
        return record is not null && CanRead(record, actorUserId) ? record : null;
    }

    public async Task<공간공동운영상세Response?> 요청결과Async(Guid clientRequestId, string? ledgerId = null, CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor || clientRequestId == Guid.Empty) return null;
        var record = await store.조회Async(ledgerId ?? StableId(actor, clientRequestId), cancellationToken);
        if (record is null || !CanRead(record, actor)) return null;
        return record.CreateRequestId == clientRequestId && record.OwnerUserId == actor
            || record.Receipts.Any(item => item.ClientRequestId == clientRequestId && item.ActorUserId == actor)
                ? await ToResponseAsync(record, actor, true, cancellationToken) : null;
    }

    public async Task<공간공동운영내목록Response> 내목록Async(CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor) return new();
        var result = new List<공간공동운영상세Response>();
        await foreach (var record in AllAsync(cancellationToken))
            if (CanRead(record, actor)) result.Add(await ToResponseAsync(record, actor, false, cancellationToken));
        return new() { Items = result };
    }

    public async Task<공간공동운영공개Response> 공개Async(string? regionKey = null, int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 10000);
        var result = new 공간공동운영공개Response { ExecutionMode = execution.Mode.ToString(), Page = page, AsOfUtc = clock.GetUtcNow() };
        if (execution.IsSimulation) result.StatusCode = "simulation-excluded";
        var directory = (await regions.목록Async(cancellationToken)).ToDictionary(item => item.RegionKey, StringComparer.Ordinal);
        var visible = new List<공간공동운영Record>();
        await foreach (var record in AllAsync(cancellationToken))
            if (record.ExecutionMode == execution.Mode.ToString() && record.PublishProposal && record.StatusCode != "closed"
                && directory.ContainsKey(record.Terms.PublicNeighborhoodRegionKey)
                && (string.IsNullOrWhiteSpace(regionKey) || record.Terms.PublicNeighborhoodRegionKey == regionKey)) visible.Add(record);
        result.PublicParticipantCount = execution.IsSimulation ? 0 : visible.SelectMany(item => item.Participants)
            .Where(item => item.StateCode != "withdrawn" && item.IntroductionConsented).Select(item => item.UserId).Distinct(StringComparer.Ordinal).Count();
        result.SpaceCount = execution.IsSimulation ? 0 : visible.Count(item => item.Participants.Any(participant => participant.RoleCode == 공간공동운영역할Codes.SpaceProvider && participant.IntroductionConsented && participant.StateCode != "withdrawn"));
        result.RecruitmentCount = execution.IsSimulation ? 0 : visible.Count(item => item.StatusCode == "gathering");
        result.CompletedCycleCount = execution.IsSimulation ? 0 : visible.Sum(item => item.Cycles.Count(cycle => cycle.StatusCode == "completed" && cycle.SourceCompletionVerified));
        result.HasMore = visible.Count > page * 20;
        result.Items = visible.Skip((page - 1) * 20).Take(20).Select(item => new 공간공동운영공개제안Dto
        {
            LedgerId = item.LedgerId, Revision = item.Revision, TermsRevision = item.TermsRevision,
            Title = item.Terms.Title, Purpose = item.Terms.Purpose, PublicNeighborhoodRegionKey = item.Terms.PublicNeighborhoodRegionKey,
            NeighborhoodLabel = directory[item.Terms.PublicNeighborhoodRegionKey].DisplayName,
            StatusCode = item.StatusCode, ExecutionMode = item.ExecutionMode, MissingRoleCodes = MissingRoles(item),
            Introductions = item.Participants.Where(member => member.StateCode != "withdrawn" && member.IntroductionConsented)
                .Select(member => new 공간공동운영공개소개Dto { DisplayName = member.PublicDisplayName, RoleCode = member.RoleCode,
                    PublicNeighborhoodRegionKey = item.Terms.PublicNeighborhoodRegionKey }).ToArray()
        }).ToArray();
        return result;
    }

    public async Task<공간공동운영공개제안Dto?> 초대조회Async(string ledgerId, CancellationToken cancellationToken = default)
    {
        var item = await store.조회Async(ledgerId, cancellationToken);
        if (item is null || !item.PublishProposal || item.StatusCode == "closed" || item.ExecutionMode != execution.Mode.ToString()) return null;
        var region = (await regions.목록Async(cancellationToken)).SingleOrDefault(value => value.RegionKey == item.Terms.PublicNeighborhoodRegionKey);
        if (region is null) return null;
        return new()
        {
            LedgerId = item.LedgerId, Revision = item.Revision, TermsRevision = item.TermsRevision, Title = item.Terms.Title,
            Purpose = item.Terms.Purpose, PublicNeighborhoodRegionKey = item.Terms.PublicNeighborhoodRegionKey,
            NeighborhoodLabel = region.DisplayName, StatusCode = item.StatusCode, ExecutionMode = item.ExecutionMode,
            MissingRoleCodes = MissingRoles(item), Introductions = item.Participants.Where(member => member.StateCode != "withdrawn" && member.IntroductionConsented)
                .Select(member => new 공간공동운영공개소개Dto { DisplayName = member.PublicDisplayName, RoleCode = member.RoleCode,
                    PublicNeighborhoodRegionKey = item.Terms.PublicNeighborhoodRegionKey }).ToArray()
        };
    }

    private async IAsyncEnumerable<공간공동운영Record> AllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var skip = 0; ; skip += 200)
        {
            var rows = await store.목록Async(skip, 200, cancellationToken);
            foreach (var record in rows) yield return record;
            if (rows.Count < 200) yield break;
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private async Task<공간공동운영상세Response> ToResponseAsync(공간공동운영Record record, string actor, bool replay, CancellationToken cancellationToken)
    {
        var readiness = await ReadinessAsync(record, actor, record.Cycles.LastOrDefault(), cancellationToken);
        return new()
        {
            LedgerId = record.LedgerId, Revision = record.Revision, TermsRevision = record.TermsRevision,
            OwnerUserId = record.OwnerUserId, OperatorUserId = record.OperatorUserId, PendingOperatorUserId = record.PendingOperatorUserId,
            IsOwner = record.OwnerUserId == actor, IsOperator = record.OperatorUserId == actor,
            PublishProposal = record.PublishProposal, ExecutionMode = record.ExecutionMode, StatusCode = record.StatusCode,
            Terms = Clone(record.Terms), Cycles = Clone(record.Cycles), MissingRoleCodes = MissingRoles(record),
            IdempotentReplay = replay, UpdatedAtUtc = record.UpdatedAtUtc,
            Participants = record.Participants.Select(item => new 공간공동운영참여Dto
            {
                UserId = item.UserId, IsMe = item.UserId == actor, RoleCode = item.RoleCode, ParticipationStateCode = item.StateCode,
                IntroductionConsented = item.IntroductionConsented, PublicDisplayName = item.IntroductionConsented ? item.PublicDisplayName : "",
                AgreedTermsRevision = item.AgreedTermsRevision
            }).ToArray(), AllowedActions = AllowedActions(record, actor),
            Readiness = readiness,
            SharedOutcomes = await 공간공동운영변화Query.조회Async(record, actor, execution.Mode.ToString(),
                readiness.Sources, bridge, clock, cancellationToken)
        };
    }

    private async Task<공간공동운영실행점검Dto> ReadinessAsync(공간공동운영Record record, string actor, 공간공동운영회차Dto? cycle, CancellationToken cancellationToken)
    {
        var blockers = new List<string>();
        var issues = new List<공간공동운영준비보류Dto>();
        void Add(string message, string code, string scope, string[]? stages = null, string[]? roles = null)
        {
            blockers.Add(message);
            issues.Add(new() { Code = code, ScopeCode = scope, StageKeys = stages ?? [], RoleCodes = roles ?? [], Message = message });
        }
        if (record.ExecutionMode != execution.Mode.ToString()) Add("현재 실행 모드와 제안의 모드가 다릅니다.", "ExecutionModeMismatch", "cooperation");
        if (record.StatusCode is "paused" or "closed") Add("모임이 중지 또는 종료되었습니다.", "CooperationStopped", "cooperation");
        if (record.OperatorUserId is null || !record.Participants.Any(item => item.UserId == record.OperatorUserId
            && item.StateCode == "accepted" && item.RoleCode is 공간공동운영역할Codes.SpaceProvider or 공간공동운영역할Codes.Coordinator))
            Add("공간 제공자의 운영 담당 수락 또는 쌍방 인계 수락이 필요합니다.", "OperatorAcceptanceRequired", "cooperation");
        var missingRoles = MissingRoles(record);
        if (missingRoles.Count > 0) Add("필수 역할의 본인 수락이 필요합니다.", "RequiredRoleAcceptanceMissing", "cooperation",
            roles: missingRoles.Where(공간공동운영역할Codes.IsKnown).ToArray());
        if (record.Participants.Where(item => item.StateCode == "accepted").Any(item => item.AgreedTermsRevision != record.TermsRevision))
            Add("역할을 수락한 참여자의 현재 조건 동의가 필요합니다.", "CurrentTermsAgreementRequired", "cooperation");
        if (cycle is null || cycle.TermsRevision != record.TermsRevision) Add("현재 조건 판본의 회차를 준비해 주세요.", "CycleTermsMismatch", "cycle");
        var references = cycle?.References ?? [];
        IReadOnlyList<공간공동운영원천상태Dto> sources = references.Count == 0 ? [] : await bridge.ValidateAsync(actor, references, cancellationToken);
        foreach (var reason in 공간공동운영예약UseCase.회차준비보류사유(record, cycle?.CycleId, clock.GetUtcNow(), sources))
            Add(reason, "RequiredAppointmentReviewRequired", "required-appointment");
        if (!ReferencesVerified(references, sources)) Add("전문 원장의 현재 권한·판본·관계를 확인해야 합니다.", "ProfessionalSourcesUnverified", "cycle");
        foreach (var kind in new[] { "DistributedSite", "StorageAgreement", "InboundPlan" })
            if (!sources.Any(item => item.KindCode == kind && item.Verified)) Add(kind switch
            { "DistributedSite" => "유효한 공간 거점 연결이 필요합니다.", "StorageAgreement" => "현재 보관 합의 연결이 필요합니다.", _ => "실제 입고 계획 연결이 필요합니다." },
                kind + "ReferenceRequired", "source", [kind == "InboundPlan" ? "inbound" : "storage"]);
        if (!sources.Any(item => item.KindCode == "DistributedSite" && item.Verified && item.Completed))
            Add("공간 거점의 현재 사용 동의를 확인해 주세요.", "DistributedSiteConsentRequired", "source", ["storage"]);
        if (!sources.Any(item => item.KindCode == "StorageAgreement" && item.Verified && item.Completed))
            Add("보관 합의의 양측 동의와 유효 기간을 확인해 주세요.", "StorageAgreementConsentOrValidityRequired", "source", ["storage"]);
        if (!sources.Any(item => item.KindCode is "DistributedOrder" or "MartOrder" && item.Verified))
            Add("현재 주문 원장 연결이 필요합니다.", "OrderReferenceRequired", "source", ["order"]);
        var canStart = blockers.Count == 0;
        var physical = sources.Where(item => item.KindCode is "InboundPlan" or "DistributedOrder" or "MartOrder" or "DistributedWave").ToArray();
        // 합의 철회는 진행 중인 실물 작업을 삭제하지 않습니다. 완료는 전문 원천 결과만 다시 확인합니다.
        var readableSources = references.All(reference => sources.Count(source => source.KindCode == reference.KindCode
            && source.ReferenceId == reference.ReferenceId && source.Verified) == 1);
        var canComplete = record.ExecutionMode == execution.Mode.ToString() && readableSources && cycle?.StatusCode == "running" && physical.Length >= 2
            && physical.Any(item => item.KindCode == "InboundPlan") && physical.Any(item => item.KindCode is "DistributedOrder" or "MartOrder")
            && physical.All(item => item.Verified && item.Completed && !item.RequiresAttention);
        return new() { CycleId = cycle?.CycleId ?? "", CycleTermsRevision = cycle?.TermsRevision ?? 0, AsOfUtc = clock.GetUtcNow(),
            CanStart = canStart && cycle?.StatusCode == "prepared", CanComplete = canComplete, Blockers = blockers, Issues = issues, Sources = sources };
    }

    private static bool ReferencesVerified(IReadOnlyList<공간공동운영전문참조Dto> references, IReadOnlyList<공간공동운영원천상태Dto> sources)
        => references.All(reference => sources.Count(source => source.KindCode == reference.KindCode && source.ReferenceId == reference.ReferenceId
            && source.Verified && !source.RequiresAttention) == 1);

    private static IReadOnlyList<string> MissingRoles(공간공동운영Record record)
        => record.Terms.RequiredRoleCodes.Where(role => !record.Participants.Any(item => item.RoleCode == role && item.StateCode == "accepted")).ToArray();

    private static IReadOnlyList<string> AllowedActions(공간공동운영Record record, string actor)
    {
        var actions = new List<string>();
        var members = record.Participants.Where(item => item.UserId == actor).ToArray();
        var member = members.FirstOrDefault(item => item.StateCode != "withdrawn");
        var owner = record.OwnerUserId == actor;
        var running = record.Cycles.Any(item => item.StatusCode == "running");
        if (member?.StateCode != "withdrawn" && member is not null)
        {
            actions.Add(공간공동운영명령Codes.IntroductionConsent);
            actions.Add(공간공동운영명령Codes.Withdraw);
        }
        if (owner) actions.Add(공간공동운영명령Codes.ProposalVisibility);
        if (record.StatusCode == "closed") return actions;
        if (record.StatusCode == "gathering" && record.PublishProposal && !running) actions.Add(공간공동운영명령Codes.Join);
        if (member is null && !owner) return actions;
        if (!running && members.Any(item => item.StateCode == "interested")) actions.Add(공간공동운영명령Codes.AcceptRole);
        if (members.Any(item => item.StateCode == "accepted"))
        {
            actions.Add(공간공동운영명령Codes.AgreeTerms);
            if (!running && members.Any(item => item.RoleCode == 공간공동운영역할Codes.SpaceProvider && item.StateCode == "accepted") && record.OperatorUserId is null) actions.Add(공간공동운영명령Codes.AcceptOperator);
        }
        if (!running && record.OperatorUserId == actor) actions.Add(공간공동운영명령Codes.ReleaseOperator);
        if (!running && record.OperatorUserId == actor) actions.Add(공간공동운영명령Codes.ProposeOperatorHandover);
        if (!running && record.PendingOperatorUserId == actor) actions.Add(공간공동운영명령Codes.AcceptOperatorHandover);
        if (owner || record.OperatorUserId == actor)
        {
            if (!running) { actions.Add(공간공동운영명령Codes.UpdateTerms); actions.Add(공간공동운영명령Codes.Close); }
            actions.Add(record.StatusCode == "paused" ? 공간공동운영명령Codes.Resume : 공간공동운영명령Codes.Pause);
        }
        if (record.OperatorUserId == actor && record.StatusCode == "gathering")
        {
            if (!running && !record.Cycles.Any(item => item.StatusCode == "prepared" && item.TermsRevision == record.TermsRevision)) actions.Add(공간공동운영명령Codes.CreateCycle);
            if (record.Cycles.Any(item => item.StatusCode == "prepared" && item.TermsRevision == record.TermsRevision))
            { actions.Add(공간공동운영명령Codes.LinkReference); actions.Add(공간공동운영명령Codes.UnlinkReference); actions.Add(공간공동운영명령Codes.StartCycle); }
            if (running) actions.Add(공간공동운영명령Codes.CompleteCycle);
        }
        if (running && owner && !actions.Contains(공간공동운영명령Codes.CompleteCycle)) actions.Add(공간공동운영명령Codes.CompleteCycle);
        return actions;
    }

    private async Task<string?> ValidateTermsAsync(공간공동운영조건Dto? terms, CancellationToken cancellationToken)
    {
        if (terms is null || string.IsNullOrWhiteSpace(terms.Title) || terms.Title.Length > 100
            || string.IsNullOrWhiteSpace(terms.Purpose) || terms.Purpose.Length > 300)
            return "제목은 100자, 목적은 300자 이내로 적어 주세요.";
        if (생활보관공개정보Policy.민감정보포함(terms.Title + " " + terms.Purpose)) return "공개 제목과 목적에는 상세 주소·연락처·출입 번호를 적지 마세요.";
        if (!(await regions.목록Async(cancellationToken)).Any(item => item.RegionKey == terms.PublicNeighborhoodRegionKey)) return "공개 동네 목록에서 지역을 선택해 주세요.";
        if (new[] { terms.SpaceUse, terms.AllowedGoods, terms.Capacity, terms.OperatingHours, terms.CostAndCompensation, terms.Responsibilities, terms.StopAndReturnPlan }.Any(value => value is null || value.Length > 1500))
            return "운영 조건은 항목별 1,500자 이내로 적어 주세요.";
        if (terms.RequiredRoleCodes is null || terms.RequiredRoleCodes.Count is < 1 or > 7 || terms.RequiredRoleCodes.Distinct(StringComparer.Ordinal).Count() != terms.RequiredRoleCodes.Count
            || terms.RequiredRoleCodes.Any(role => !공간공동운영역할Codes.IsKnown(role)) || !terms.RequiredRoleCodes.Contains(공간공동운영역할Codes.SpaceProvider))
            return "공간 제공자를 포함한 필수 역할을 중복 없이 선택해 주세요.";
        return null;
    }

    private static bool CanRead(공간공동운영Record record, string actor) => record.OwnerUserId == actor || record.Participants.Any(item => item.UserId == actor);
    private static bool ValidIntroduction(bool consent, string? name) => !consent || !string.IsNullOrWhiteSpace(name) && name.Length <= 40 && !생활보관공개정보Policy.민감정보포함(name);
    private static bool ValidReference(공간공동운영전문참조Dto? reference) => reference is not null && ReferenceKinds.Contains(reference.KindCode, StringComparer.Ordinal)
        && !string.IsNullOrWhiteSpace(reference.ReferenceId) && reference.ReferenceId.Length <= 160 && (reference.ParentId?.Length ?? 0) <= 160 && reference.ExpectedRevision is null or > 0;
    private static string StableId(string actor, Guid requestId) => "space-cooperation-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actor + ":" + requestId.ToString("N")))).ToLowerInvariant();
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json))).ToLowerInvariant();
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
    private static Result<공간공동운영상세Response> Fail(string code, string message, int status)
        => Result.Fail<공간공동운영상세Response>(new Error(message).WithMetadata("Code", code).WithMetadata("StatusCode", status));
}
