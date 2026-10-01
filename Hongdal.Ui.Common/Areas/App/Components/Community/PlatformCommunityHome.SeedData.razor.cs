using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private static readonly IReadOnlyList<CommunitySeedPost> SeedPosts =
    [
        new(
            "커뮤니티에서 시작해 업무로 이어지는 흐름",
            "질문과 경험은 게시판에서 함께 다듬고, 실행이 필요한 내용만 업무 화면으로 넘깁니다.\n\n커뮤니티 글 -> 참여자 확인 -> 업무 원장 -> 처리 상태 공유",
            "시스템 다이어그램",
            "오늘",
            Icons.Material.Filled.AccountTree,
            Color.Info,
            "홍달 아키텍처 모임",
            18,
            7,
            true),
        new(
            "창고 업무 첫 화면은 공정별 인증 게이트로 통일",
            "입고, 출고, 포장 업무는 이메일 번호 확인 뒤 근무 시간 조건을 함께 검증합니다.",
            "업무 기록",
            "이번 주",
            Icons.Material.Filled.Warehouse,
            Color.Warning,
            "공유창고 실무자들",
            12,
            4,
            false),
        new(
            "구독료 인하 권고와 운영비 우선 기준 공유",
            "수익 환원은 정산 항목과 운영 정책 안에서 조용히 확인하는 방향으로 정리합니다.",
            "개선 제안",
            "검토 중",
            Icons.Material.Filled.Payments,
            Color.Success,
            "화주 운영 모임",
            9,
            3,
            false)
    ];

    private static readonly IReadOnlyList<string> OperatingNotes =
    [
        "여러 게시판에서 질문, 경험, 개선 제안과 다이어그램을 공유",
        "실행이 필요한 내용은 역할별 업무 화면으로 연결",
        "게시글은 닉네임과 비밀번호로 수정 권한을 보호"
    ];
}
