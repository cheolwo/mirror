using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Services.Community;

/// <summary>생활 배송의 선택과 쓰기 fence. null은 기존 운송 정책을 뜻하며 선택을 추정하지 않습니다.</summary>
public static class 생활배송배차Policy
{
    public static string 접수키(string actor, string clientId) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(actor + ":" + clientId)));
    public const string Marker = ":dispatch-policy:";
    public static string 신청메모(NeighborhoodDeliveryRequest request) => request.DispatchMode is null ? string.Empty
        : Marker + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new 선택근거(request.DispatchMode, request.CollaborationId, request.ExpectedTermsRevision))));
    public static void 큐초기화(운송원장 queue, string? memo)
    {
        var index = memo?.IndexOf(Marker, StringComparison.Ordinal) ?? -1;
        if (index < 0) return;
        var evidence = JsonSerializer.Deserialize<선택근거>(Encoding.UTF8.GetString(Convert.FromBase64String(memo![(index + Marker.Length)..])))
            ?? throw new InvalidDataException("Neighborhood dispatch evidence missing.");
        if (!NeighborhoodDispatchModes.IsKnown(evidence.Mode)) throw new InvalidDataException("Neighborhood dispatch mode invalid.");
        queue.생활배송배차방식 = evidence.Mode; queue.생활배송선택판본 = 1;
        queue.생활배송협업Id = evidence.CollaborationId; queue.생활배송조건판본 = evidence.TermsRevision;
        queue.생활배송수락준비완료 = false;
        대기적용(queue);
    }
    public static bool 자동추천가능(운송원장 queue) => queue.생활배송배차방식 is null
        || queue.생활배송수락준비완료 && queue.생활배송배차방식 is NeighborhoodDispatchModes.Automatic or NeighborhoodDispatchModes.Hybrid;
    public static bool 공개허용(운송원장 queue) => queue.생활배송배차방식 is null
        || queue.생활배송수락준비완료 && queue.생활배송배차방식 is NeighborhoodDispatchModes.PublicCall or NeighborhoodDispatchModes.Hybrid;
    public static bool 병행공개가능(운송원장 queue) => queue.생활배송수락준비완료 && queue.생활배송배차방식 == NeighborhoodDispatchModes.Hybrid
        && queue.상태 == 상태값.배차대기상태.대기 && queue.확정기사Id is null && queue.배차큐단계 != 상태값.배차큐단계.종료;
    public static void 대기적용(운송원장 queue)
    {
        queue.현재추천대상기사Id = null; queue.추천시작시각 = null; queue.추천만료시각 = null;
        var open = queue.생활배송수락준비완료 && queue.생활배송배차방식 == NeighborhoodDispatchModes.PublicCall;
        queue.배차큐단계 = open ? 상태값.배차큐단계.공개배차 : 상태값.배차큐단계.계획배차;
        queue.배차노출상태 = open ? 공개상태 : 상태값.배차노출상태.계획대기;
        queue.공개전환시각 = open ? DateTime.UtcNow : null; queue.UpdatedAt = DateTime.UtcNow;
    }
    private const int 공개상태 = 상태값.배차노출상태.공개중;
    private sealed record 선택근거(string Mode, string? CollaborationId, long? TermsRevision);
}
