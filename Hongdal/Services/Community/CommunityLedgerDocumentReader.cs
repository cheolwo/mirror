using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

internal static class 커뮤니티원장문서읽기Mapper
{
    public static 커뮤니티원장Dto ToDto(커뮤니티원장문서 문서)
        => new()
        {
            원장Id = 문서.원장Id,
            커뮤니티Id = 문서.커뮤니티Id,
            원장템플릿Key = 문서.원장템플릿Key,
            제목 = 문서.제목,
            원함 = 문서.원함,
            상태 = 문서.상태,
            현재단계Key = 문서.현재단계Key,
            대상OsCode = 문서.대상OsCode,
            대상OsName = 문서.대상OsName,
            생성자UserId = 문서.생성자UserId,
            생성자표시명 = 문서.생성자표시명,
            블록목록 = 문서.블록목록.Select(ToDto).ToArray(),
            참여자목록 = 문서.참여자목록.Select(ToDto).ToArray(),
            포함원장목록 = 문서.포함원장목록?.Select(ToDto).OrderBy(x => x.표시순서).ToArray() ?? [],
            다이어그램스냅샷 = 문서.다이어그램스냅샷 is null ? null : ToDto(문서.다이어그램스냅샷, 문서.원장Id),
            외부참조 = 문서.외부참조,
            확장속성 = 문서.확장속성,
            상태이력 = 문서.상태이력.Select(ToDto).ToArray(),
            Revision = 문서.Revision,
            투영완료Revision = 문서.투영완료Revision,
            투영상태 = 문서.Revision <= 문서.투영완료Revision
                ? 커뮤니티원장투영상태.완료
                : 문서.투영상태,
            투영EventId = 문서.투영EventId,
            투영마지막오류 = 문서.투영마지막오류,
            생성시각Utc = 문서.생성시각Utc,
            수정시각Utc = 문서.수정시각Utc
        };

    private static 커뮤니티원장블록Dto ToDto(커뮤니티원장블록문서 문서)
        => new()
        {
            BlockId = 문서.BlockId,
            BlockType = 문서.BlockType,
            Title = 문서.Title,
            State = 문서.State,
            담당자목록 = 문서.담당자목록.Select(ToDto).ToArray(),
            Data = 문서.Data
        };

    private static 커뮤니티원장블록담당자Dto ToDto(커뮤니티원장블록담당자문서 문서)
        => new()
        {
            UserId = 문서.UserId,
            DisplayName = 문서.DisplayName,
            RoleLabel = 문서.RoleLabel,
            ResponsibilityType = 문서.ResponsibilityType
        };

    private static 커뮤니티원장참여자Dto ToDto(커뮤니티원장참여자문서 문서)
        => new()
        {
            UserId = 문서.UserId,
            DisplayName = 문서.DisplayName,
            RoleLabel = 문서.RoleLabel,
            ParticipationState = 문서.ParticipationState
        };

    private static 커뮤니티포함원장참조Dto ToDto(커뮤니티포함원장참조문서 문서)
        => new()
        {
            원장Id = 문서.원장Id,
            원장템플릿Key = 문서.원장템플릿Key,
            역할 = 문서.역할,
            관계유형 = string.IsNullOrWhiteSpace(문서.관계유형)
                ? CommunityLedgerRelationTypes.Contains
                : 문서.관계유형,
            필수여부 = 문서.필수여부,
            표시순서 = 문서.표시순서
        };

    private static 커뮤니티원장상태이력Dto ToDto(커뮤니티원장상태이력문서 문서)
        => new()
        {
            EventId = 문서.EventId,
            상태 = 문서.상태,
            이전상태 = 문서.이전상태,
            현재단계Key = 문서.현재단계Key,
            메모 = 문서.메모,
            변경자 = 문서.변경자,
            변경시각Utc = 문서.변경시각Utc
        };

    private static DiagramSnapshotDto ToDto(커뮤니티원장다이어그램문서 문서, string 원장Id)
        => new()
        {
            DiagramId = 문서.DiagramId,
            DiagramName = 문서.DiagramName,
            LedgerId = 원장Id,
            LedgerTemplateKey = 문서.LedgerTemplateKey,
            WorkflowModeKey = 문서.WorkflowModeKey,
            Nodes = 문서.Nodes.Select(node => new DiagramNodeDto
            {
                NodeId = node.NodeId,
                Kind = node.Kind,
                Title = node.Title,
                GroupLabel = node.GroupLabel,
                Description = node.Description,
                X = node.X,
                Y = node.Y,
                RelatedRoute = node.RelatedRoute,
                OrganizationReferences = (node.OrganizationReferences ?? [])
                    .Select(ToDto)
                    .ToArray(),
                Data = node.Data
            }).ToArray(),
            Edges = 문서.Edges.Select(edge => new DiagramEdgeDto
            {
                EdgeId = edge.EdgeId,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                Label = edge.Label,
                MeaningCode = edge.MeaningCode,
                Data = edge.Data
            }).ToArray(),
            Metadata = 문서.Metadata
        };

    private static DiagramOrganizationReferenceDto ToDto(
        커뮤니티원장다이어그램업체참조문서 문서)
        => new()
        {
            ReferenceId = 문서.ReferenceId,
            OrganizationKey = 문서.OrganizationKey,
            DisplayName = 문서.DisplayName,
            RoleLabel = 문서.RoleLabel,
            CountryCode = 문서.CountryCode,
            OfficialWebsiteUrl = 문서.OfficialWebsiteUrl,
            SourceKindCode = 문서.SourceKindCode,
            SourceReferenceUrl = 문서.SourceReferenceUrl,
            DirectoryStatusCode = 문서.DirectoryStatusCode,
            PlatformRelationshipStatusCode = 문서.PlatformRelationshipStatusCode,
            CompanySourceVerificationStatusCode = 문서.CompanySourceVerificationStatusCode,
            RegulatoryVerificationStatusCode = 문서.RegulatoryVerificationStatusCode,
            IsPlatformPartner = 문서.IsPlatformPartner,
            CanBeSelectedForOperations = 문서.CanBeSelectedForOperations,
            CapabilityCodes = (문서.CapabilityCodes ?? []).ToArray()
        };
}
