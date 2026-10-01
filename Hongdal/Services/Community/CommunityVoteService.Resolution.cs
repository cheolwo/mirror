using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ContractManagement;

namespace Hongdal.Services.Community;

public partial class CommunityVoteService
{
    public async Task<CommunityVoteResolutionDocumentResponse?> CreateResolutionDraftAsync(
        Guid voteId,
        CommunityVoteResolutionDraftRequest request,
        CancellationToken cancellationToken)
    {
        var vote = await _store.GetAsync(voteId, cancellationToken);
        if (vote is null)
        {
            return null;
        }

        if (!vote.ResolutionDocumentEnabled)
        {
            throw new InvalidOperationException("이 투표는 결의문 생성을 사용하지 않습니다.");
        }

        if (vote.Status == CommunityVoteStatusCodes.Open)
        {
            throw new InvalidOperationException("투표를 마감한 뒤 결의문을 만들 수 있습니다.");
        }

        var requiredSigners = request.RequiredSigners;
        if (requiredSigners.Count == 0
            && vote.SignatureRequired
            && vote.GroupPurchase is not null)
        {
            requiredSigners = vote.Votes
                .OrderBy(x => x.VotedAtUtc)
                .Select(x => new CommunityVoteResolutionSignerRequest
                {
                    PartyId = x.VoterHash,
                    RoleCode = "GroupPurchaseParticipant",
                    SignerDisplayName = x.VoterDisplayName
                })
                .ToArray();
        }

        if (requiredSigners.Count == 0 && vote.SignatureRequired)
        {
            throw new InvalidOperationException("서명 필수 결의문은 서명 요청 대상이 필요합니다.");
        }

        if (!request.LegalReviewRequested && vote.SignatureRequired)
        {
            EnsureGroupImportContractReady(vote);
        }

        var legalEffectNotice = BuildLegalEffectNotice(vote);
        var documentText = BuildDocumentText(vote, request, legalEffectNotice);
        var documentHash = Hash(documentText);
        var documentNumber = $"COMM-VOTE-{DateTime.UtcNow:yyyyMMdd}-{vote.Id:N}"[..42];
        var signatureBundle = requiredSigners.Count == 0
            ? null
            : ContractElectronicSignaturePlanner.CreateBundle(
                documentNumber,
                documentHash,
                requiredSigners.Select(x => new ContractSignatureRequest(
                    x.PartyId,
                    x.RoleCode,
                    x.SignerDisplayName,
                    IsRequiredSigner: true,
                    DateTimeOffset.UtcNow)),
                DateTimeOffset.UtcNow);

        vote.Status = CommunityVoteStatusCodes.ResolutionDrafted;
        vote.ResolutionDocument = new CommunityVoteResolutionDocumentRecord
        {
            Id = Guid.NewGuid(),
            VoteId = vote.Id,
            DocumentNumber = documentNumber,
            DocumentTitle = Normalize(request.DocumentTitle, $"{vote.Title} 결의문"),
            ResolutionText = Normalize(request.ResolutionText, "투표 결과에 따른 커뮤니티 결의 초안입니다."),
            DocumentHash = documentHash,
            Status = request.LegalReviewRequested
                ? CommunityVoteResolutionStatusCodes.LegalReviewRequired
                : vote.SignatureRequired
                    ? CommunityVoteResolutionStatusCodes.ReadyToSign
                    : CommunityVoteResolutionStatusCodes.Draft,
            LegalEffectNotice = legalEffectNotice,
            CreatedAtUtc = DateTime.UtcNow,
            SignatureBundle = signatureBundle
        };

        await SaveMutationAsync(vote, cancellationToken);
        await _ledgerWorkflow.진행Async(
            vote.Id,
            new CommunityGroupPurchaseLedgerProgressRequest
            {
                StageCode = CommunityGroupPurchaseLedgerStageCodes.Resolution,
                Memo = "공동구매 확정안 결의문을 작성했습니다."
            },
            vote.CreatedByDisplayName,
            cancellationToken);
        return ToResolutionResponse(vote.ResolutionDocument);
    }

    public async Task<CommunityVoteResolutionDocumentResponse?> SignResolutionAsync(
        Guid voteId,
        CommunityVoteResolutionSignRequest request,
        CancellationToken cancellationToken)
    {
        var vote = await _store.GetAsync(voteId, cancellationToken);
        var document = vote?.ResolutionDocument;
        if (document?.SignatureBundle is null)
        {
            return null;
        }

        if (document.Status == CommunityVoteResolutionStatusCodes.LegalReviewRequired)
        {
            throw new InvalidOperationException("법무/운영 검토가 필요한 결의문은 서명 가능 상태로 전환한 뒤 서명해야 합니다.");
        }

        EnsureGroupImportContractReady(vote!);

        var evidence = new ContractSignatureEvidence(
            request.PartyId,
            Normalize(request.SignerDisplayName, "익명 참여자"),
            Normalize(request.SignatureMethodCode, ContractSignatureMethodCode.PlatformClickSign),
            document.DocumentHash,
            Hash(request.ConsentText),
            Hash(request.SignatureEvidencePayload),
            DateTimeOffset.UtcNow,
            request.ClientIpHash);
        document.SignatureBundle = ContractElectronicSignaturePlanner.AddEvidence(document.SignatureBundle, evidence);

        var plan = ContractElectronicSignaturePlanner.Plan(document.SignatureBundle, DateTimeOffset.UtcNow);
        document.Status = plan.IsFullySigned
            ? CommunityVoteResolutionStatusCodes.Signed
            : CommunityVoteResolutionStatusCodes.PartiallySigned;

        await SaveMutationAsync(vote!, cancellationToken);
        await _ledgerWorkflow.진행Async(
            vote!.Id,
            new CommunityGroupPurchaseLedgerProgressRequest
            {
                StageCode = document.Status == CommunityVoteResolutionStatusCodes.Signed
                    ? CommunityGroupPurchaseLedgerStageCodes.FulfillmentPlan
                    : CommunityGroupPurchaseLedgerStageCodes.Signature,
                Memo = document.Status == CommunityVoteResolutionStatusCodes.Signed
                    ? "필수 전자서명이 완료되어 이행 계획 단계로 진행했습니다."
                    : "공동구매 결의문에 전자서명 증적을 추가했습니다."
            },
            request.SignerDisplayName,
            cancellationToken);
        return ToResolutionResponse(document);
    }

    public async Task<CommunityVoteResolutionDocumentResponse?> MarkResolutionReadyToSignAsync(
        Guid voteId,
        CommunityVoteResolutionReadyToSignRequest request,
        CancellationToken cancellationToken)
    {
        var vote = await _store.GetAsync(voteId, cancellationToken);
        var document = vote?.ResolutionDocument;
        if (document is null)
        {
            return null;
        }

        if (document.SignatureBundle is null)
        {
            document.Status = CommunityVoteResolutionStatusCodes.Draft;
        }
        else
        {
            EnsureGroupImportContractReady(vote!);
            document.Status = CommunityVoteResolutionStatusCodes.ReadyToSign;
        }

        document.LegalEffectNotice = $"{BuildLegalEffectNotice(vote!)} 검토자: {Normalize(request.ReviewedByDisplayName, "운영자")}.";
        await SaveMutationAsync(vote!, cancellationToken);
        await _ledgerWorkflow.진행Async(
            vote!.Id,
            new CommunityGroupPurchaseLedgerProgressRequest
            {
                StageCode = document.SignatureBundle is null
                    ? CommunityGroupPurchaseLedgerStageCodes.Resolution
                    : CommunityGroupPurchaseLedgerStageCodes.Signature,
                Memo = document.SignatureBundle is null
                    ? "결의문 검토 결과를 원장에 기록했습니다."
                    : "결의문 검토를 마치고 전자서명 단계로 진행했습니다."
            },
            request.ReviewedByDisplayName,
            cancellationToken);
        return ToResolutionResponse(document);
    }

    private static CommunityVoteResolutionDocumentResponse ToResolutionResponse(CommunityVoteResolutionDocumentRecord document)
    {
        return new CommunityVoteResolutionDocumentResponse
        {
            Id = document.Id,
            VoteId = document.VoteId,
            DocumentNumber = document.DocumentNumber,
            DocumentTitle = document.DocumentTitle,
            ResolutionText = document.ResolutionText,
            DocumentHash = document.DocumentHash,
            Status = document.Status,
            LegalEffectNotice = document.LegalEffectNotice,
            CreatedAtUtc = document.CreatedAtUtc,
            SignaturePlan = document.SignatureBundle is null
                ? null
                : ContractElectronicSignaturePlanner.Plan(document.SignatureBundle, DateTimeOffset.UtcNow)
        };
    }

    private static string BuildDocumentText(
        CommunityVoteRecord vote,
        CommunityVoteResolutionDraftRequest request,
        string legalEffectNotice)
    {
        var resultLines = ToResponse(vote).Options
            .OrderByDescending(x => x.VoteCount)
            .Select(x => $"- {x.Text}: {x.VoteCount}표");
        return string.Join('\n',
            Normalize(request.DocumentTitle, $"{vote.Title} 결의문"),
            vote.Title,
            vote.Description,
            Normalize(request.ResolutionText, "투표 결과에 따른 커뮤니티 결의 초안입니다."),
            "투표 결과:",
            string.Join('\n', resultLines),
            "법적 효력 고지:",
            legalEffectNotice);
    }

    private static string BuildLegalEffectNotice(CommunityVoteRecord vote)
    {
        var settings = vote.GroupPurchase;
        if (settings is null)
        {
            return GenericLegalEffectNotice;
        }

        var proposalOriginNotice = settings.ProposalOriginLegalEffectNotice;
        var agreementNotice = string.IsNullOrWhiteSpace(proposalOriginNotice)
            ? $"{GenericLegalEffectNotice} {CommunityGroupPurchaseAgreementPolicy.FullLegalEffectNotice}"
            : $"{GenericLegalEffectNotice} {proposalOriginNotice}";

        return CommunityGroupPurchaseTradeRouteCodes.IsGroupImport(settings.TradeRouteCode)
            ? $"{agreementNotice} {CommunityGroupPurchaseTradeRoutePolicy.GroupImportCandidateNotice}"
            : agreementNotice;
    }

    private static void EnsureGroupImportContractReady(CommunityVoteRecord vote)
    {
        var settings = vote.GroupPurchase;
        if (settings is null
            || !CommunityGroupPurchaseTradeRouteCodes.IsGroupImport(settings.TradeRouteCode))
        {
            return;
        }

        var tradeRouteDecision = CommunityGroupPurchaseTradeRoutePolicy.Evaluate(
            new CommunityGroupPurchaseTradeRouteInput(
                settings.SellerCountryCode,
                settings.ShipFromCountryCode,
                settings.DeliveryCountryCode,
                settings.CustomsClearanceStatusCode,
                settings.OperatingMarketCountryCode));
        if (!tradeRouteDecision.IsGroupImportCandidate
            || tradeRouteDecision.RequiresManualReview)
        {
            throw new InvalidOperationException(
                "공동수입 계약 확정 전에 상품 출발국가, 국내 배송국가와 통관 상태를 다시 확인해 주세요.");
        }

        var hasValidHsCode = new[] { settings.HsCode }
            .Concat(vote.Options.Select(option => option.HsCode))
            .Any(code => CommunityVoteHsCode.NormalizeOptional(code) is not null);
        if (!hasValidHsCode)
        {
            throw new InvalidOperationException(
                "공동수입 계약을 확정하려면 검토 가능한 HS 코드가 하나 이상 필요합니다.");
        }
    }
}
