using System.Security.Cryptography;
using System.Text;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace 살뜰.Services.Dispatch.Common;

public static class 운영배차활동사건Factory
{
    public static 운영배차활동사건 제안표시확인(string 기사Id, string 제안Id, int 추천라운드, DateTime 발생시각Utc)
        => 생성(기사Id, 운영배차주체역할Code.기사, 제안Id, null, 추천라운드, 발생시각Utc,
            운영배차사건유형Code.제안표시확인, 운영배차제안유효성Code.유효, 운영배차책임Code.해당없음,
            운영배차미응답Code.앱표시확인, 운영배차미응답Code.라운드상태(추천라운드));

    public static 운영배차활동사건 무응답(string 기사Id, string 제안Id, int 추천라운드, DateTime 만료시각Utc)
        => 생성(기사Id, 운영배차주체역할Code.기사, 제안Id, null, 추천라운드, 만료시각Utc,
            운영배차사건유형Code.무응답, 운영배차제안유효성Code.유효, 운영배차책임Code.미확정,
            운영배차미응답Code.표시제안만료, 운영배차미응답Code.라운드상태(추천라운드));

    public static 운영배차활동사건 유효제안(
        string 기사Id,
        string 제안Id,
        int 추천라운드,
        DateTime 발생시각Utc)
        => 생성(
            기사Id,
            운영배차주체역할Code.기사,
            제안Id,
            주문Id: null,
            추천라운드,
            발생시각Utc,
            운영배차사건유형Code.제안전달,
            운영배차제안유효성Code.유효,
            운영배차책임Code.해당없음,
            "EligibleRecommendationStarted",
            "Presented");

    public static 운영배차활동사건 거절(
        string 기사Id,
        string 제안Id,
        int 추천라운드,
        DateTime 발생시각Utc,
        string? 사유Code)
        => 생성(
            기사Id,
            운영배차주체역할Code.기사,
            제안Id,
            주문Id: null,
            추천라운드,
            발생시각Utc,
            운영배차사건유형Code.거절,
            운영배차제안유효성Code.유효,
            운영배차책임Code.기사,
            운영배차거절사유Code.정규화(사유Code),
            "Rejected");

    public static 운영배차활동사건 수락(
        string 기사Id,
        string 제안Id,
        string 주문Id,
        int 추천라운드,
        DateTime 발생시각Utc,
        string? 업무시도Id = null)
        => 생성(
            기사Id,
            운영배차주체역할Code.기사,
            제안Id,
            주문Id,
            추천라운드,
            발생시각Utc,
            운영배차사건유형Code.수락,
            운영배차제안유효성Code.유효,
            운영배차책임Code.기사,
            "DriverExplicitAcceptance",
            "Accepted",
            업무시도Id);

    public static 운영배차활동사건 완료(
        string 기사Id,
        string 제안Id,
        string 주문Id,
        int 추천라운드,
        DateTime 발생시각Utc,
        string? 업무시도Id = null)
        => 생성(
            기사Id,
            운영배차주체역할Code.기사,
            제안Id,
            주문Id,
            추천라운드,
            발생시각Utc,
            운영배차사건유형Code.완료,
            운영배차제안유효성Code.해당없음,
            운영배차책임Code.기사,
            "DriverDeliveryCompleted",
            "Completed",
            업무시도Id);

    public static 운영배차활동사건 중단(
        string 기사Id,
        string 제안Id,
        string 주문Id,
        int 추천라운드,
        DateTime 발생시각Utc,
        string 업무시도Id,
        string 사유Code,
        string 책임Code)
        => 생성(
            기사Id,
            운영배차주체역할Code.기사,
            제안Id,
            주문Id,
            추천라운드,
            발생시각Utc,
            운영배차사건유형Code.중단,
            책임Code is 운영배차책임Code.기사
                ? 운영배차제안유효성Code.유효
                : 운영배차제안유효성Code.보호사유,
            책임Code,
            사유Code,
            "Interrupted",
            업무시도Id);

    public static 운영배차활동사건 균형반환(
        string 기사Id,
        string 제안Id,
        string 주문Id,
        int 추천라운드,
        DateTime 발생시각Utc,
        string 업무시도Id,
        string 책임Code)
        => 생성(
            기사Id,
            운영배차주체역할Code.기사,
            제안Id,
            주문Id,
            추천라운드,
            발생시각Utc,
            운영배차사건유형Code.균형건수반환,
            운영배차제안유효성Code.해당없음,
            책임Code,
            "InterruptionBalanceReleased",
            "Released",
            업무시도Id);

    public static 운영배차활동사건 책임판정(
        string 기사Id,
        string 제안Id,
        string 주문Id,
        int 추천라운드,
        DateTime 발생시각Utc,
        string 업무시도Id,
        string 책임Code,
        string 사유Code,
        Guid 검토요청Id)
    {
        var activity = 생성(
            기사Id,
            운영배차주체역할Code.기사,
            제안Id,
            주문Id,
            추천라운드,
            발생시각Utc,
            운영배차사건유형Code.책임판정,
            운영배차제안유효성Code.해당없음,
            책임Code,
            사유Code,
            "Reviewed",
            업무시도Id);
        activity.사건StableId = $"dispatch-responsibility-review:{검토요청Id:N}";
        return activity;
    }

    public static 운영배차활동사건 음식점유효주문제안(
        long 음식점Id,
        string 주문Id,
        DateTime 발생시각Utc)
        => 생성(
            음식점주체Id(음식점Id),
            운영배차주체역할Code.음식점,
            주문Id,
            주문Id,
            추천라운드: 0,
            발생시각Utc,
            운영배차사건유형Code.제안전달,
            운영배차제안유효성Code.유효,
            운영배차책임Code.해당없음,
            "ValidatedFoodOrderRegistered",
            "Presented");

    public static 운영배차활동사건 음식점수락(
        long 음식점Id,
        string 주문Id,
        DateTime 발생시각Utc)
        => 생성(
            음식점주체Id(음식점Id),
            운영배차주체역할Code.음식점,
            주문Id,
            주문Id,
            추천라운드: 0,
            발생시각Utc,
            운영배차사건유형Code.수락,
            운영배차제안유효성Code.유효,
            운영배차책임Code.음식점,
            "RestaurantExplicitAcceptance",
            "Accepted");

    public static 운영배차활동사건 음식점거절(
        long 음식점Id,
        string 주문Id,
        DateTime 발생시각Utc,
        string? 사유Code)
    {
        var reason = 운영배차음식점거절사유Code.정규화(사유Code);
        var isServiceIncident = reason == 운영배차음식점거절사유Code.서비스장애;
        var isIneligible = 운영배차음식점거절사유Code.유효제안분모제외대상(reason);

        return 생성(
            음식점주체Id(음식점Id),
            운영배차주체역할Code.음식점,
            주문Id,
            주문Id,
            추천라운드: 0,
            발생시각Utc,
            운영배차사건유형Code.거절,
            isServiceIncident
                ? 운영배차제안유효성Code.보호사유
                : isIneligible
                    ? 운영배차제안유효성Code.조건부적합
                    : 운영배차제안유효성Code.유효,
            isServiceIncident ? 운영배차책임Code.보호대상 : 운영배차책임Code.음식점,
            reason,
            "Rejected");
    }

    public static 운영배차활동사건 주문자취소(
        string 주문자Id,
        string 주문Id,
        DateTime 발생시각Utc,
        string? 사유Code)
        => 생성(
            필수값(주문자Id, nameof(주문자Id)),
            운영배차주체역할Code.주문자,
            주문Id,
            주문Id,
            추천라운드: 0,
            발생시각Utc,
            운영배차사건유형Code.취소,
            운영배차제안유효성Code.유효,
            운영배차책임Code.주문자,
            운영배차주문자취소사유Code.정규화(사유Code),
            "CancelledBeforeRestaurantAcceptance");

    public static 운영배차활동사건 음식점제안철회(
        long 음식점Id,
        string 주문Id,
        DateTime 발생시각Utc,
        string? 사유Code)
        => 생성(
            음식점주체Id(음식점Id),
            운영배차주체역할Code.음식점,
            주문Id,
            주문Id,
            추천라운드: 0,
            발생시각Utc,
            운영배차사건유형Code.취소,
            운영배차제안유효성Code.보호사유,
            운영배차책임Code.주문자,
            운영배차주문자취소사유Code.정규화(사유Code),
            "WithdrawnBeforeRestaurantResponse");

    private static 운영배차활동사건 생성(
        string 주체Id,
        string 주체역할Code,
        string 제안Id,
        string? 주문Id,
        int 추천라운드,
        DateTime 발생시각Utc,
        string 사건유형Code,
        string 제안유효성Code,
        string 책임Code,
        string 사유Code,
        string 상태값Code,
        string? 업무시도Id = null)
    {
        var subjectId = 필수값(주체Id, nameof(주체Id));
        var offerId = 필수값(제안Id, nameof(제안Id));
        var occurredAtUtc = DateTime.SpecifyKind(발생시각Utc, DateTimeKind.Utc);
        var stableSource = $"{사건유형Code}|{주체역할Code}|{subjectId}|{offerId}|{추천라운드}|{업무시도Id}";
        var stableHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stableSource)))
            .ToLowerInvariant();

        return new 운영배차활동사건
        {
            사건StableId = $"dispatch-activity:{stableHash}",
            발생시각Utc = occurredAtUtc,
            운영시장시간대Id = 운영배차시장시간대Code.대한민국,
            제안Id = offerId,
            주문Id = string.IsNullOrWhiteSpace(주문Id) ? null : 주문Id.Trim(),
            업무시도Id = string.IsNullOrWhiteSpace(업무시도Id) ? null : 업무시도Id.Trim(),
            주체Id = subjectId,
            주체역할Code = 주체역할Code,
            사건유형Code = 사건유형Code,
            제안유효성Code = 제안유효성Code,
            책임Code = 책임Code,
            사유Code = 사유Code,
            상태값Code = 상태값Code,
            지표단위수 = 1,
            기록시각Utc = DateTime.UtcNow
        };
    }

    private static string 음식점주체Id(long 음식점Id)
        => 음식점Id > 0
            ? $"restaurant:{음식점Id}"
            : throw new ArgumentOutOfRangeException(nameof(음식점Id), "음식점 ID는 1 이상이어야 합니다.");

    private static string 필수값(string value, string parameterName)
    {
        var clean = value?.Trim();
        return string.IsNullOrWhiteSpace(clean)
            ? throw new ArgumentException("운영 배차 사건 식별자를 비워 둘 수 없습니다.", parameterName)
            : clean;
    }
}
