using FluentResults;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using 홍달.Services.Versioning;

namespace Hongdal.Tests.Services.Community;

public sealed class 게시글원장ContextServiceTests
{
    [Fact]
    public async Task 본인이_만들었거나_참여한_원장을_글쓰기_선택지로_조회한다()
    {
        var service = CreateService(CreateLedger());

        var ownerLedgers = await service.연결가능원장목록조회Async("owner-1", null, default);
        var participantLedgers = await service.연결가능원장목록조회Async("worker-1", null, default);

        var ownerLedger = Assert.Single(ownerLedgers);
        var participantLedger = Assert.Single(participantLedgers);
        Assert.True(ownerLedger.내가만든원장);
        Assert.Equal("생성자", ownerLedger.참여역할);
        Assert.False(participantLedger.내가만든원장);
        Assert.Equal("운반자", participantLedger.참여역할);
        Assert.Equal("국내 화물 운송", participantLedger.WorkflowTag);
    }

    [Fact]
    public async Task 글쓰기_업무분류와_다른_원장은_선택지에서_제외한다()
    {
        var service = CreateService(CreateLedger());

        var ledgers = await service.연결가능원장목록조회Async("owner-1", "음식 주문", default);

        Assert.Empty(ledgers);
    }

    [Fact]
    public async Task 생성자와_참여자는_원장을_게시글에_연결할_수_있다()
    {
        var service = CreateService(CreateLedger());

        var ownerResult = await service.연결가능원장조회Async("ledger-1", "owner-1", "국내 화물 운송", default);
        var participantResult = await service.연결가능원장조회Async("ledger-1", "worker-1", "국내 화물 운송", default);

        Assert.True(ownerResult.IsSuccess);
        Assert.True(participantResult.IsSuccess);
    }

    [Fact]
    public async Task 참여하지_않은_사용자는_원장을_게시글에_연결할_수_없다()
    {
        var service = CreateService(CreateLedger());

        var result = await service.연결가능원장조회Async("ledger-1", "outsider", "국내 화물 운송", default);

        Assert.True(result.IsFailed);
        Assert.Equal(403, result.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 비참여자에게는_공개_요약과_참여_요청만_제공한다()
    {
        var service = CreateService(CreateLedger());

        var context = await service.조회Async("ledger-1", "outsider", default);

        Assert.NotNull(context);
        Assert.False(context.상세조회가능여부);
        Assert.True(context.참여요청필요여부);
        Assert.NotEqual("서울 긴급 운송", context.제목);
        Assert.Empty(context.현재단계);
        Assert.Equal(["참여 요청"], context.가능한행동목록);
    }

    [Fact]
    public async Task 참여자에게는_현재_단계와_업무_행동을_제공한다()
    {
        var service = CreateService(CreateLedger());

        var context = await service.조회Async("ledger-1", "worker-1", default);

        Assert.NotNull(context);
        Assert.True(context.상세조회가능여부);
        Assert.False(context.참여요청필요여부);
        Assert.Equal("서울 긴급 운송", context.제목);
        Assert.Equal("상차 대기", context.현재단계);
        Assert.Contains("상차 확인", context.가능한행동목록);
        Assert.Equal("cargo-transport", context.업무분류Code);
        Assert.True(context.기능활성화여부);
    }

    [Fact]
    public async Task 운반_참여자에게는_현재_상태에_맞는_상차_노드_행동을_제공한다()
    {
        var service = CreateService(CreateLedger());

        var context = await service.조회Async("ledger-1", "worker-1", default);

        Assert.NotNull(context);
        Assert.Equal(7, context.Revision);
        Assert.Collection(
            context.노드행동목록,
            arrive =>
            {
                Assert.Equal(CommunityLedgerNodeActionCodes.TransportArrivePickup, arrive.행동Code);
                Assert.Equal("기사운송진행Controller.상차지도착", arrive.ApiEndpointKey);
                Assert.Equal("101", arrive.실행대상Id);
                Assert.True(arrive.실행가능여부);
            },
            complete =>
            {
                Assert.Equal(CommunityLedgerNodeActionCodes.TransportCompletePickup, complete.행동Code);
                Assert.False(complete.실행가능여부);
                Assert.True(complete.사진필수여부);
            });
    }

    [Fact]
    public async Task 상차지도착_상태에서는_상차완료_행동만_실행할_수_있다()
    {
        var ledger = CreateLedger();
        ledger.현재단계Key = "상차지도착";
        ledger.확장속성 = new Dictionary<string, string> { ["운송상태"] = "상차지도착" };
        var service = CreateService(ledger);

        var context = await service.조회Async("ledger-1", "worker-1", default);

        Assert.NotNull(context);
        Assert.False(context.노드행동목록.Single(x => x.행동Code == CommunityLedgerNodeActionCodes.TransportArrivePickup).실행가능여부);
        Assert.True(context.노드행동목록.Single(x => x.행동Code == CommunityLedgerNodeActionCodes.TransportCompletePickup).실행가능여부);
    }

    [Fact]
    public async Task 원장_소유자라도_기사_참여자가_아니면_운송_명령을_노출하지_않는다()
    {
        var service = CreateService(CreateLedger());

        var context = await service.조회Async("ledger-1", "owner-1", default);

        Assert.NotNull(context);
        Assert.Empty(context.노드행동목록);
    }

    [Fact]
    public async Task 업무_분류와_원장_종류가_다르면_연결하지_않는다()
    {
        var service = CreateService(CreateLedger());

        var result = await service.연결가능원장조회Async("ledger-1", "owner-1", "음식 주문", default);

        Assert.True(result.IsFailed);
        Assert.Equal(400, result.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 업무_기능이_꺼져_있으면_원장을_연결하지_않는다()
    {
        var service = CreateService(CreateLedger(), featureEnabled: false);

        var result = await service.연결가능원장조회Async("ledger-1", "owner-1", "국내 화물 운송", default);

        Assert.True(result.IsFailed);
        Assert.Equal(409, result.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 비공개_원장은_비참여자에게_노출하지_않는다()
    {
        var service = CreateService(CreateLedger(), sharingScope: 커뮤니티원장공개범위.비공개);

        var context = await service.조회Async("ledger-1", "outsider", default);

        Assert.Null(context);
    }

    [Fact]
    public async Task 커뮤니티_공개_원장은_비로그인_사용자에게_노출하지_않는다()
    {
        var service = CreateService(CreateLedger());

        var context = await service.조회Async("ledger-1", null, default);

        Assert.Null(context);
    }

    [Fact]
    public async Task 재공유가_허용된_공개_원장은_다른_사용자도_게시글에_연결할_수_있다()
    {
        var service = CreateService(CreateLedger(), allowReshare: true);

        var result = await service.연결가능원장조회Async("ledger-1", "outsider", "국내 화물 운송", default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task 다이어그램_항목만_공개하면_민감한_노드_Data를_제외한_구조만_보인다()
    {
        var service = CreateService(
            CreateLedger(),
            publicItemKeys: [커뮤니티원장공개항목Key.다이어그램구조]);

        var context = await service.조회Async("ledger-1", "outsider", default);

        var diagram = Assert.IsType<DiagramSnapshotDto>(context?.다이어그램);
        var node = Assert.Single(diagram.Nodes);
        Assert.Null(node.Description);
        Assert.Empty(node.Data);
    }

    [Fact]
    public async Task 참여자는_원장_블록의_내부_항목을_모두_조회한다()
    {
        var service = CreateService(CreateLedger());

        var context = await service.조회Async("ledger-1", "worker-1", default);

        var block = Assert.Single(context?.블록목록 ?? []);
        Assert.Equal("상차지", block.제목);
        Assert.Equal("확인 완료", block.상태);
        var assignee = Assert.Single(block.담당자목록);
        Assert.Equal("worker-1", assignee.UserId);
        Assert.Equal("운반자", assignee.DisplayName);
        Assert.Equal("주담당", assignee.ResponsibilityName);
        Assert.Equal("서울 중구", block.항목["주소"]);
        Assert.Equal("010-0000-0000", block.항목["연락처"]);
    }

    [Fact]
    public async Task 공개_원장은_허용된_블록_항목만_조회한다()
    {
        var service = CreateService(
            CreateLedger(),
            publicItemKeys:
            [
                커뮤니티원장공개항목Key.다이어그램구조,
                커뮤니티원장공개항목Key.블록제목("node-1"),
                커뮤니티원장공개항목Key.블록상태("node-1"),
                커뮤니티원장공개항목Key.블록Data("node-1", "주소")
            ]);

        var context = await service.조회Async("ledger-1", "outsider", default);

        var block = Assert.Single(context?.블록목록 ?? []);
        Assert.Equal("상차지", block.제목);
        Assert.Equal("확인 완료", block.상태);
        Assert.Equal("서울 중구", Assert.Single(block.항목).Value);
        Assert.DoesNotContain("연락처", block.항목.Keys);
        Assert.Empty(block.담당자목록);
    }

    [Fact]
    public async Task 공동주문_묶음에서_개별주문과_후속원장_다이어그램을_계층으로_조회한다()
    {
        var transport = CreateHierarchyLedger(
            "transport-1",
            CommunityLedgerTemplateKeys.CargoTransport,
            "공동주문 국내 운송",
            "owner-1");
        var order = CreateHierarchyLedger(
            "order-1",
            CommunityLedgerTemplateKeys.Order,
            "101동 생활용품 주문",
            "owner-1",
            new 커뮤니티포함원장참조Dto
            {
                원장Id = transport.원장Id,
                원장템플릿Key = transport.원장템플릿Key,
                역할 = 주문원장포함역할.운송,
                필수여부 = true
            });
        var group = CreateHierarchyLedger(
            "group-1",
            CommunityLedgerTemplateKeys.GroupPurchase,
            "아파트 생활용품 공동주문",
            "owner-1",
            new 커뮤니티포함원장참조Dto
            {
                원장Id = order.원장Id,
                원장템플릿Key = order.원장템플릿Key,
                역할 = 주문원장포함역할.개별주문,
                필수여부 = true
            });
        var service = CreateHierarchyService(group, order, transport);

        var context = await service.조회Async(group.원장Id, "owner-1", default);

        var orderNode = Assert.Single(context?.포함원장목록 ?? []);
        Assert.True(orderNode.접근가능여부);
        Assert.NotNull(orderNode.원장?.다이어그램);
        var transportNode = Assert.Single(orderNode.포함원장목록);
        Assert.Equal(주문원장포함역할.운송, transportNode.역할);
        Assert.NotNull(transportNode.원장?.다이어그램);
    }

    [Fact]
    public async Task 공동주문_하위원장에_권한이_없으면_관계만_표시하고_상세를_숨긴다()
    {
        var privateOrder = CreateHierarchyLedger(
            "order-private",
            CommunityLedgerTemplateKeys.Order,
            "다른 주문자의 비공개 주문",
            "another-owner");
        var group = CreateHierarchyLedger(
            "group-1",
            CommunityLedgerTemplateKeys.GroupPurchase,
            "공개 범위 확인 공동주문",
            "owner-1",
            new 커뮤니티포함원장참조Dto
            {
                원장Id = privateOrder.원장Id,
                원장템플릿Key = privateOrder.원장템플릿Key,
                역할 = 주문원장포함역할.개별주문,
                필수여부 = true
            });
        var service = CreateHierarchyService(group, privateOrder);

        var context = await service.조회Async(group.원장Id, "owner-1", default);

        var restricted = Assert.Single(context?.포함원장목록 ?? []);
        Assert.False(restricted.접근가능여부);
        Assert.Equal("접근권한필요", restricted.조회상태);
        Assert.Null(restricted.원장);
    }

    [Fact]
    public async Task 공개_원장_재사용은_허용된_항목만_새_비공개_원장으로_복사한다()
    {
        var source = CreateLedger();
        var ledgerStore = new 원장저장소Stub(source);
        var featureFlag = new 기능설정Stub(true);
        var service = new 커뮤니티원장공유Service(
            ledgerStore,
            new 원장공유정책저장소Stub(
                source,
                커뮤니티원장공개범위.커뮤니티,
                allowReshare: false,
                publicItemKeys: [커뮤니티원장공개항목Key.다이어그램구조],
                allowReuse: true),
            featureFlag);

        var result = await service.재사용Async(
            source.원장Id,
            new 커뮤니티원장재사용Request(),
            "outsider",
            default);

        Assert.True(result.IsSuccess);
        var saved = Assert.IsType<커뮤니티원장저장요청>(ledgerStore.LastSaveRequest);
        Assert.Equal("outsider", saved.생성자UserId);
        Assert.Equal(커뮤니티원장상태.초안, saved.상태);
        Assert.Empty(saved.블록목록);
        var node = Assert.Single(Assert.IsType<DiagramSnapshotDto>(saved.다이어그램스냅샷).Nodes);
        Assert.Null(node.Description);
        Assert.Empty(node.Data);
        Assert.Empty(node.OrganizationReferences);
        Assert.Equal(source.원장Id, saved.외부참조["재사용출처원장Id"]);
    }

    [Fact]
    public async Task 관세사_역할조회에서는_허용된_노드와_그_사이의_연결선만_서버가_반환한다()
    {
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = "group-import-role-scope",
            원장템플릿Key = CommunityLedgerTemplateKeys.GroupImport,
            제목 = "공동수입 감귤",
            상태 = 커뮤니티원장상태.진행중,
            현재단계Key = "통관 상태",
            생성자UserId = "buyer-owner",
            블록목록 =
            [
                new 커뮤니티원장블록Dto { BlockId = "import-decision", Title = "수입 결정", Data = new Dictionary<string, string> { ["가격"] = "비공개" } },
                new 커뮤니티원장블록Dto { BlockId = "customs-state", Title = "통관 상태", Data = new Dictionary<string, string> { ["문서관리번호"] = "M-100" } },
                new 커뮤니티원장블록Dto { BlockId = "domestic-release", Title = "국내 반출", Data = new Dictionary<string, string> { ["상태"] = "대기" } }
            ],
            다이어그램스냅샷 = new DiagramSnapshotDto
            {
                DiagramId = "diagram-role-scope",
                DiagramName = "감귤 공동수입",
                LedgerId = "group-import-role-scope",
                LedgerTemplateKey = CommunityLedgerTemplateKeys.GroupImport,
                Nodes =
                [
                    new DiagramNodeDto { NodeId = "import-decision", Title = "수입 결정" },
                    new DiagramNodeDto { NodeId = "customs-state", Title = "통관 상태" },
                    new DiagramNodeDto { NodeId = "domestic-release", Title = "국내 반출" }
                ],
                Edges =
                [
                    new DiagramEdgeDto { EdgeId = "private-to-customs", FromNodeId = "import-decision", ToNodeId = "customs-state" },
                    new DiagramEdgeDto { EdgeId = "customs-to-release", FromNodeId = "customs-state", ToNodeId = "domestic-release" }
                ]
            }
        };
        var ledgerStore = new 원장저장소Stub(ledger);
        var featureFlag = new 기능설정Stub(true);
        var sharingService = new 커뮤니티원장공유Service(
            ledgerStore,
            new 원장공유정책저장소Stub(
                ledger,
                커뮤니티원장공개범위.비공개,
                allowReshare: false,
                publicItemKeys: []),
            featureFlag);
        var roleAccess = new 역할접근Stub(new CommunityLedgerRoleAccessDecision(
            HasRoleAccess: true,
            UseRoleScope: true,
            CanManage: false,
            RoleCode: CommunityLedgerAccessRoleCodes.CustomsBroker,
            RoleName: "관세사",
            VisibleNodeIds: ["customs-state", "domestic-release"],
            EditableNodeIds: ["customs-state"],
            CanCoordinateTransport: true));
        var service = new 게시글원장ContextService(ledgerStore, featureFlag, sharingService, roleAccess);

        var context = await service.조회Async(ledger.원장Id, "broker", default);

        Assert.NotNull(context);
        Assert.True(context.역할범위조회여부);
        Assert.False(context.상세조회가능여부);
        Assert.False(context.참여요청필요여부);
        Assert.Equal(["customs-state", "domestic-release"], context.다이어그램!.Nodes.Select(node => node.NodeId));
        Assert.Equal("customs-to-release", Assert.Single(context.다이어그램.Edges).EdgeId);
        Assert.Equal(["customs-state", "domestic-release"], context.블록목록.Select(block => block.블록Id));
        Assert.DoesNotContain(context.블록목록, block => block.항목.ContainsKey("가격"));
        Assert.Equal(["관세사 역할 노드 조회", "허용된 노드 편집", "운송 주선 제안"], context.가능한행동목록);
        Assert.Empty(context.노드행동목록);
    }

    private static 게시글원장ContextService CreateService(
        커뮤니티원장Dto ledger,
        bool featureEnabled = true,
        string sharingScope = 커뮤니티원장공개범위.커뮤니티,
        bool allowReshare = false,
        IReadOnlyList<string>? publicItemKeys = null)
    {
        var ledgerStore = new 원장저장소Stub(ledger);
        var featureFlag = new 기능설정Stub(featureEnabled);
        var sharingService = new 커뮤니티원장공유Service(
            ledgerStore,
            new 원장공유정책저장소Stub(ledger, sharingScope, allowReshare, publicItemKeys),
            featureFlag);
        return new 게시글원장ContextService(ledgerStore, featureFlag, sharingService);
    }

    private static 게시글원장ContextService CreateHierarchyService(params 커뮤니티원장Dto[] ledgers)
    {
        var ledgerStore = new 원장저장소Stub(ledgers);
        var featureFlag = new 기능설정Stub(true);
        var root = ledgers[0];
        var sharingService = new 커뮤니티원장공유Service(
            ledgerStore,
            new 원장공유정책저장소Stub(
                root,
                커뮤니티원장공개범위.비공개,
                allowReshare: false,
                publicItemKeys: []),
            featureFlag);
        return new 게시글원장ContextService(ledgerStore, featureFlag, sharingService);
    }

    private static 커뮤니티원장Dto CreateHierarchyLedger(
        string ledgerId,
        string templateKey,
        string title,
        string ownerUserId,
        params 커뮤니티포함원장참조Dto[] children)
        => new()
        {
            원장Id = ledgerId,
            Revision = 1,
            원장템플릿Key = templateKey,
            제목 = title,
            상태 = 커뮤니티원장상태.진행중,
            현재단계Key = "진행 중",
            생성자UserId = ownerUserId,
            포함원장목록 = children,
            다이어그램스냅샷 = new DiagramSnapshotDto
            {
                DiagramId = $"diagram-{ledgerId}",
                DiagramName = $"{title} 흐름",
                LedgerId = ledgerId,
                LedgerTemplateKey = templateKey,
                Nodes =
                [
                    new DiagramNodeDto
                    {
                        NodeId = $"node-{ledgerId}",
                        Kind = CommunityLedgerBlockTypes.State,
                        Title = title
                    }
                ]
            }
        };

    private static 커뮤니티원장Dto CreateLedger()
        => new()
        {
            원장Id = "ledger-1",
            Revision = 7,
            원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
            제목 = "서울 긴급 운송",
            상태 = 커뮤니티원장상태.진행중,
            현재단계Key = "상차 대기",
            생성자UserId = "owner-1",
            외부참조 = new Dictionary<string, string>
            {
                ["운송실행투영Id"] = "101"
            },
            참여자목록 =
            [
                new 커뮤니티원장참여자Dto
                {
                    UserId = "worker-1",
                    DisplayName = "운반자",
                    RoleLabel = "운반자"
                }
            ],
            블록목록 =
            [
                new 커뮤니티원장블록Dto
                {
                    BlockId = "node-1",
                    BlockType = CommunityLedgerBlockTypes.Place,
                    Title = "상차지",
                    State = "확인 완료",
                    담당자목록 =
                    [
                        new 커뮤니티원장블록담당자Dto
                        {
                            UserId = "worker-1",
                            DisplayName = "운반자",
                            RoleLabel = "운반자",
                            ResponsibilityType = CommunityLedgerBlockResponsibilityTypes.Primary
                        }
                    ],
                    Data = new Dictionary<string, string>
                    {
                        ["주소"] = "서울 중구",
                        ["연락처"] = "010-0000-0000"
                    }
                }
            ],
            다이어그램스냅샷 = new DiagramSnapshotDto
            {
                DiagramId = "diagram-1",
                DiagramName = "서울 긴급 운송",
                LedgerId = "ledger-1",
                LedgerTemplateKey = CommunityLedgerTemplateKeys.CargoTransport,
                Nodes =
                [
                    new DiagramNodeDto
                    {
                        NodeId = "node-1",
                        Kind = "transport",
                        Title = "상차",
                        Description = "비공개 연락처가 들어 있는 설명",
                        OrganizationReferences =
                        [
                            new DiagramOrganizationReferenceDto
                            {
                                ReferenceId = "private-provider-reference",
                                OrganizationKey = "private-provider",
                                DisplayName = "비공개 물류 후보",
                                RoleLabel = "운송 후보",
                                CountryCode = "KR"
                            }
                        ],
                        Data = new Dictionary<string, string> { ["phone"] = "010-0000-0000" }
                    }
                ]
            }
        };

    private sealed class 원장저장소Stub : I커뮤니티원장저장소
    {
        private readonly IReadOnlyDictionary<string, 커뮤니티원장Dto> _ledgers;
        public 커뮤니티원장저장요청? LastSaveRequest { get; private set; }

        public 원장저장소Stub(params 커뮤니티원장Dto[] ledgers)
        {
            _ledgers = ledgers.ToDictionary(ledger => ledger.원장Id, StringComparer.OrdinalIgnoreCase);
        }

        public Task<커뮤니티원장Dto?> 원장조회Async(string 원장Id, CancellationToken cancellationToken = default)
            => Task.FromResult(_ledgers.GetValueOrDefault(원장Id));

        public Task<커뮤니티원장Dto> 원장저장Async(
            커뮤니티원장저장요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            LastSaveRequest = request;
            return Task.FromResult(new 커뮤니티원장Dto
            {
                원장Id = request.원장Id ?? string.Empty,
                원장템플릿Key = request.원장템플릿Key,
                제목 = request.제목,
                상태 = request.상태 ?? 커뮤니티원장상태.초안,
                생성자UserId = request.생성자UserId,
                블록목록 = request.블록목록,
                다이어그램스냅샷 = request.다이어그램스냅샷
            });
        }

        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(
            커뮤니티원장조회조건 query,
            CancellationToken cancellationToken = default)
        {
            var accessible = _ledgers.Values
                .Where(ledger => string.Equals(query.접근UserId, ledger.생성자UserId, StringComparison.OrdinalIgnoreCase)
                                 || ledger.참여자목록.Any(x =>
                                     string.Equals(x.UserId, query.접근UserId, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            return Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(accessible);
        }

        public Task<커뮤니티원장Dto?> 원장상태변경Async(
            커뮤니티원장상태변경요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class 원장공유정책저장소Stub : I커뮤니티원장공유정책저장소
    {
        private 커뮤니티원장공유정책 _policy;

        public 원장공유정책저장소Stub(
            커뮤니티원장Dto ledger,
            string sharingScope,
            bool allowReshare,
            IReadOnlyList<string>? publicItemKeys,
            bool allowReuse = false)
        {
            _policy = new 커뮤니티원장공유정책
            {
                원장Id = ledger.원장Id,
                소유자UserId = ledger.생성자UserId ?? string.Empty,
                공개범위 = sharingScope,
                재사용허용여부 = allowReuse,
                재공유허용여부 = allowReshare,
                공개항목Key목록 = publicItemKeys ?? []
            };
        }

        public Task<커뮤니티원장공유정책?> 조회Async(string 원장Id, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장공유정책?>(
                string.Equals(원장Id, _policy.원장Id, StringComparison.OrdinalIgnoreCase) ? _policy : null);

        public Task<IReadOnlyList<커뮤니티원장공유정책>> 공개목록조회Async(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<커뮤니티원장공유정책>>([_policy]);

        public Task<커뮤니티원장공유정책> 저장Async(
            커뮤니티원장공유정책 policy,
            long? 기대Revision,
            CancellationToken cancellationToken = default)
        {
            _policy = policy;
            return Task.FromResult(_policy);
        }
    }

    private sealed class 기능설정Stub : IVersionFeatureFlagService
    {
        private readonly bool _enabled;

        public 기능설정Stub(bool enabled)
        {
            _enabled = enabled;
        }

        public bool IsEnabled(string featureKey) => _enabled;

        public IReadOnlyDictionary<string, bool> GetAll()
            => new Dictionary<string, bool>();
    }

    private sealed class 역할접근Stub : ICommunityLedgerRoleAccessService
    {
        private readonly CommunityLedgerRoleAccessDecision _decision;

        public 역할접근Stub(CommunityLedgerRoleAccessDecision decision)
        {
            _decision = decision;
        }

        public Task<Result<CommunityLedgerRoleAccessSettingsResponse>> GetSettingsAsync(
            string ledgerId,
            string? userId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Result<CommunityLedgerRoleAccessSettingsResponse>> UpdateSettingsAsync(
            string ledgerId,
            CommunityLedgerRoleAccessUpdateRequest request,
            string? userId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CommunityLedgerRoleAccessDecision> EvaluateAsync(
            커뮤니티원장Dto ledger,
            string? userId,
            CancellationToken cancellationToken)
            => Task.FromResult(_decision);
    }
}
