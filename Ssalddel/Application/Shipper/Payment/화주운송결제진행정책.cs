using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Shipper.Payment;

public static class 화주운송결제진행정책
{
    private static readonly string[] 상차완료이후상태목록 =
    [
        상태값.배차상태.상차완료,
        상태값.배차상태.운송중,
        "하차지도착",
        상태값.배차상태.하차완료,
        상태값.배차상태.인수완료
    ];

    public static bool 상차완료이후인가(string? 배차상태)
        => !string.IsNullOrWhiteSpace(배차상태)
           && 상차완료이후상태목록.Contains(배차상태.Trim(), StringComparer.Ordinal);

    public static 화주운송결제검증결과 결제준비요청검증(
        화주운송의뢰 request,
        string? userId,
        string? role,
        bool 선결제허용 = false)
    {
        if (!화주결제권한정책.의뢰결제권한있음(request, userId, role))
        {
            return 화주운송결제검증결과.실패("의뢰를 찾을 수 없습니다.");
        }

        if (request.모의처리처수행)
        {
            return 화주운송결제검증결과.실패("모의 처리처 수행은 결제·지급·정산 대상이 아닙니다.");
        }

        if (request.상태 == 상태값.의뢰상태.취소 || request.배차상태 == 상태값.배차상태.취소)
        {
            return 화주운송결제검증결과.실패("취소된 의뢰는 결제를 진행할 수 없습니다.");
        }

        if (!상차완료이후인가(request.배차상태) && !(선결제허용 && 선결제초기상태인가(request)))
        {
            return 화주운송결제검증결과.실패("상차완료 이후에만 결제를 진행할 수 있습니다.");
        }

        if (request.결제상태 == 상태값.결제상태.결제완료)
        {
            return 화주운송결제검증결과.실패("이미 결제완료된 의뢰입니다.");
        }

        return 화주운송결제검증결과.통과함();
    }

    public static bool 선결제초기상태인가(화주운송의뢰 request)
        => request.정산시점 == "선결제" && request.상태 == 상태값.의뢰상태.생성됨
           && request.배차상태 == 상태값.배차상태.미시작;

    public static 화주운송결제검증결과 결제승인요청검증(
        결제 payment,
        화주운송의뢰? request,
        string? userId,
        string? role,
        int amount)
    {
        if (!화주결제권한정책.결제승인권한있음(payment, request, userId, role))
        {
            return 화주운송결제검증결과.실패("결제 요청을 찾을 수 없습니다.");
        }

        if (request?.모의처리처수행 == true)
        {
            return 화주운송결제검증결과.실패("모의 처리처 수행은 결제·지급·정산 대상이 아닙니다.");
        }

        if (payment.결제금액 != amount)
        {
            return 화주운송결제검증결과.실패("결제 금액이 일치하지 않습니다.");
        }

        if (payment.결제상태 == 상태값.결제상태.결제완료)
        {
            return 화주운송결제검증결과.완료됨();
        }

        if (payment.결제상태 != 상태값.결제상태.결제대기)
        {
            return 화주운송결제검증결과.실패("결제 승인 대상 상태가 아닙니다.");
        }

        if (request?.결제상태 == 상태값.결제상태.결제완료)
        {
            return 화주운송결제검증결과.실패("이미 결제완료된 의뢰입니다.");
        }

        if (payment.결제대상유형 == 결제공통정의.결제대상유형.용달운송의뢰)
        {
            var evidence = 배송료결제준비근거Policy.현재근거검증(payment, request);
            if (evidence.IsFailed) return 화주운송결제검증결과.실패(evidence.Errors[0].Message);
        }

        return 화주운송결제검증결과.통과함();
    }
}

public sealed record 화주운송결제검증결과(bool 통과, string 실패사유, bool 이미완료됨 = false)
{
    public static 화주운송결제검증결과 통과함()
        => new(true, string.Empty);

    public static 화주운송결제검증결과 실패(string 실패사유)
        => new(false, 실패사유);

    public static 화주운송결제검증결과 완료됨()
        => new(true, string.Empty, 이미완료됨: true);
}
