using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private PlatformCommunityDiagramNodeDetailPresentation BuildNodeDetailPresentation(원장블록노드 node)
    {
        var state = ResolveNodeDetailProcessingState(node);
        var readiness = 노드입력준비도해결(node, state);
        var currentLedger = 선택현재원장;
        return new(
            node,
            BuildNodeProcessingStateLabel(state),
            BuildNodeProcessingStateColor(state),
            BuildNodeKindLabel(node.Kind),
            IsDiagramMode ? BuildDiagramNodeStackLabel(node) : null,
            node.Kind.Equals("form", StringComparison.OrdinalIgnoreCase)
                ? ResolveDiagramFormKindLabel(node.FormKind)
                : null,
            readiness,
            readiness.Percent >= 100
                ? "platform-diagram-node-readiness-card platform-diagram-node-readiness-card--complete"
                : "platform-diagram-node-readiness-card",
            BuildNodeReadinessStyle(readiness),
            ResolveNodeDetailContextValues(node),
            도형입력항목해결(node),
            currentLedger is null ? null : $"{currentLedger.Title} · {currentLedger.StateLabel}",
            BuildNodeDetailAction(node),
            IsDiagramMode,
            CanBringSelectedDiagramNodeToFront(),
            CanSendSelectedDiagramNodeToBack(),
            창고대행신청노드인가(node));
    }

    private string ResolveNodeDetailFormValue(도형입력항목 field)
        => nodeDetailPanelNode is null ? string.Empty : GetDiagramFormValue(nodeDetailPanelNode, field);

    private void HandleNodeDetailLedgerBlockValueChanged(PlatformCommunityLedgerBlockValueChange change)
        => Set원장블록입력값(change.BlockCode, change.Value);

    private void HandleNodeDetailFormValueChanged(PlatformCommunityDiagramFormValueChange change)
    {
        if (nodeDetailPanelNode is not null)
        {
            SetDiagramFormValue(nodeDetailPanelNode, change.Field, change.Value);
        }
    }

    private 도형상세동작 BuildNodeDetailAction(원장블록노드 node)
    {
        var basePath = ResolveNodeDetailBasePath(node);
        var values = new Dictionary<string, string?>
        {
            ["source"] = "diagram-node",
            ["ledgerTemplateKey"] = selectedLedgerTemplateKey,
            ["ledgerId"] = 선택현재원장?.Id,
            ["nodeTitle"] = node.Title,
            ["nodeKind"] = node.Kind,
            ["formKind"] = node.FormKind
        };

        return new(
            PlatformCommunityNavigationQuery.Build(basePath, values),
            BuildNodeDetailActionDescription(node, basePath),
            ResolveNodeDetailActionIcon(basePath),
            ResolveNodeDetailActionColor(basePath));
    }

    private string ResolveNodeDetailBasePath(원장블록노드 node)
    {
        if (node.Kind.Equals("form", StringComparison.OrdinalIgnoreCase) &&
            ResolveDiagramFormDetailPath(node.FormKind) is { } formPath)
        {
            return formPath;
        }

        var title = node.Title;
        if (selectedLedgerTemplateKey is CommunityLedgerTemplateKeys.CargoTransport)
        {
            if (ContainsAny(title, "운송 의뢰"))
            {
                return "/shipper/request";
            }

            if (ContainsAny(title, "배차", "기사 수락", "기사 거절"))
            {
                return "/driver/recommendations";
            }

            if (ContainsAny(title, "운송 구간"))
            {
                return "/driver/transports/current";
            }

            if (ContainsAny(title, "상차", "하차", "수령", "인수", "증빙"))
            {
                return "/driver/transport/proof";
            }

            if (ContainsAny(title, "정산"))
            {
                return "/shipper/request/payment-status";
            }

            return "/shipper/request/HD-WEB-001";
        }

        if (selectedLedgerTemplateKey is CommunityLedgerTemplateKeys.HongdalMart)
        {
            if (ContainsAny(title, "피킹", "포장", "픽업"))
            {
                return "/warehouse/mart/picking";
            }

            if (ContainsAny(title, "재고", "창고"))
            {
                return "/warehouse/mart/work-board";
            }

            return "/warehouse/mart";
        }

        if (selectedLedgerTemplateKey is CommunityLedgerTemplateKeys.WarehouseInbound)
        {
            if (ContainsAny(title, "운송 하차", "하차 증빙"))
            {
                return "/driver/transport/proof";
            }

            if (ContainsAny(title, "검수"))
            {
                return "/warehouse/work/inbound/inspection";
            }

            if (ContainsAny(title, "상품", "바코드", "입고"))
            {
                return "/warehouse/work/inbound/products";
            }

            return "/shipper/inbound/requests";
        }

        if (selectedLedgerTemplateKey is CommunityLedgerTemplateKeys.WarehouseOutbound)
        {
            if (ContainsAny(title, "운송 상차", "운송 하차", "상차 증빙", "하차 증빙"))
            {
                return "/driver/transport/proof";
            }

            if (ContainsAny(title, "창고 입고"))
            {
                return "/warehouse/work/inbound/products";
            }

            if (ContainsAny(title, "피킹"))
            {
                return "/warehouse/work/picking-batch";
            }

            if (ContainsAny(title, "출고", "포장", "작업"))
            {
                return "/warehouse/work-board";
            }

            if (ContainsAny(title, "배송", "운송"))
            {
                return "/driver/recommendations";
            }

            return "/warehouse";
        }

        if (selectedLedgerTemplateKey is CommunityLedgerTemplateKeys.FoodOrder)
        {
            return "/community";
        }

        if (selectedLedgerTemplateKey is CommunityLedgerTemplateKeys.FoodDelivery)
        {
            return "/driver/recommendations";
        }

        return node.Kind switch
        {
            "warehouse" => "/warehouse",
            "delivery" => "/driver/recommendations",
            "confirm" => "/community",
            _ => "/community"
        };
    }

    private static string BuildNodeDetailActionDescription(원장블록노드 node, string basePath)
        => basePath switch
        {
            "/shipper/request" => $"{node.Title} 노드를 운송 의뢰 작성 화면에서 확인합니다.",
            "/shipper/request/HD-WEB-001" => $"{node.Title} 노드를 의뢰 상세 타임라인에서 확인합니다.",
            "/driver/recommendations" => $"{node.Title} 노드를 기사 배차/추천 화면에서 확인합니다.",
            "/driver/transports/current" => $"{node.Title} 노드를 진행 중 운송 화면에서 확인합니다.",
            "/driver/transport/proof" => $"{node.Title} 노드를 상차/하차 증빙 확인 화면에서 확인합니다.",
            "/shipper/request/payment-status" => $"{node.Title} 노드를 결제/정산 상태 화면에서 확인합니다.",
            "/warehouse/mart" => $"{node.Title} 노드를 알뜰살뜰 마트 화면에서 확인합니다.",
            "/warehouse/mart/picking" => $"{node.Title} 노드를 알뜰살뜰 마트 피킹/포장 화면에서 확인합니다.",
            "/warehouse/mart/work-board" => $"{node.Title} 노드를 알뜰살뜰 마트 작업 보드에서 확인합니다.",
            "/warehouse/work/inbound/inspection" => $"{node.Title} 노드를 입고 검수 화면에서 확인합니다.",
            "/warehouse/work/inbound/products" => $"{node.Title} 노드를 입고 상품 확인 화면에서 확인합니다.",
            "/shipper/inbound/requests" => $"{node.Title} 노드를 입고/물류 대행 신청 화면에서 확인합니다.",
            "/warehouse/work/picking-batch" => $"{node.Title} 노드를 피킹 배치 화면에서 확인합니다.",
            "/warehouse/work-board" => $"{node.Title} 노드를 창고 작업 보드에서 확인합니다.",
            "/warehouse" => $"{node.Title} 노드를 창고·현장 화면에서 확인합니다.",
            _ => $"{node.Title} 노드를 현재 커뮤니티 원장 맥락에서 확인합니다."
        };

    private static string ResolveNodeDetailActionIcon(string basePath)
        => basePath switch
        {
            var path when path.Contains("warehouse", StringComparison.OrdinalIgnoreCase) => Icons.Material.Filled.Warehouse,
            var path when path.Contains("driver", StringComparison.OrdinalIgnoreCase) => Icons.Material.Filled.LocalShipping,
            var path when path.Contains("payment", StringComparison.OrdinalIgnoreCase) => Icons.Material.Filled.Payments,
            var path when path.Contains("shipper", StringComparison.OrdinalIgnoreCase) => Icons.Material.Filled.Assignment,
            _ => Icons.Material.Filled.OpenInNew
        };

    private static Color ResolveNodeDetailActionColor(string basePath)
        => basePath switch
        {
            var path when path.Contains("warehouse", StringComparison.OrdinalIgnoreCase) => Color.Success,
            var path when path.Contains("driver", StringComparison.OrdinalIgnoreCase) => Color.Secondary,
            var path when path.Contains("payment", StringComparison.OrdinalIgnoreCase) => Color.Info,
            var path when path.Contains("shipper", StringComparison.OrdinalIgnoreCase) => Color.Primary,
            _ => Color.Default
        };

    private 원장블록처리상태 ResolveNodeDetailProcessingState(원장블록노드 node)
    {
        var nodes = 정렬된원장블록노드목록가져오기(선택원장블록흐름도);
        var nodeIndex = 원장블록노드순서찾기(nodes, node.Title);
        return nodeIndex >= 0
            ? 원장블록처리상태해결(nodeIndex, nodes)
            : 원장블록처리상태.대기;
    }

    private IReadOnlyList<KeyValuePair<string, string>> ResolveNodeDetailContextValues(원장블록노드 node)
    {
        var ledger = 선택현재원장;
        if (ledger is null)
        {
            return [];
        }

        return ledger.ContextValues
            .Where(pair => IsNodeDetailContextMatch(node, pair.Key))
            .Take(4)
            .ToList();
    }

    private static bool IsNodeDetailContextMatch(원장블록노드 node, string contextKey)
    {
        var nodeText = $"{node.Title} {node.GroupLabel} {node.Description}";
        if (nodeText.Contains(contextKey, StringComparison.OrdinalIgnoreCase) ||
            contextKey.Contains(node.Title, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ContainsAny(node.Title, "상차") && contextKey.Equals("상차", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ContainsAny(node.Title, "하차") && contextKey.Equals("하차", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ContainsAny(node.Title, "운송", "배차", "기사", "의뢰") &&
            ContainsAny(contextKey, "참여자", "화물"))
        {
            return true;
        }

        if (ContainsAny(node.Title, "증빙", "확인") && ContainsAny(contextKey, "증빙", "참여자"))
        {
            return true;
        }

        if (ContainsAny(node.Title, "정산") && contextKey.Equals("정산", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ContainsAny(node.Title, "재고", "창고") && ContainsAny(contextKey, "재고", "보관", "입고"))
        {
            return true;
        }

        if (ContainsAny(node.Title, "피킹", "포장") && ContainsAny(contextKey, "피킹", "포장"))
        {
            return true;
        }

        return false;
    }

    private static string BuildNodeProcessingStateLabel(원장블록처리상태 state)
        => state switch
        {
            원장블록처리상태.완료 => "처리 완료",
            원장블록처리상태.진행중 => "처리 중",
            _ => "처리 대기"
        };

    private static Color BuildNodeProcessingStateColor(원장블록처리상태 state)
        => state switch
        {
            원장블록처리상태.완료 => Color.Success,
            원장블록처리상태.진행중 => Color.Primary,
            _ => Color.Default
        };

    private static string BuildNodeKindLabel(string kind)
        => 원장블록종류정규화(kind) switch
        {
            "product" => "요청/상품",
            "sales-channel" => "판매채널",
            "place" => "장소",
            "warehouse" => "창고/재고",
            "work" => "작업",
            "delivery" => "운송/전달",
            "confirm" => "확인/증빙",
            "form" => "입력 폼",
            _ => "업무 노드"
        };

    private static bool 창고대행신청노드인가(원장블록노드 node)
        => node.Kind.Equals("warehouse", StringComparison.OrdinalIgnoreCase);

}
