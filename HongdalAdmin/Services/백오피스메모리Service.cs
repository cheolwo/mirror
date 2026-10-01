using System.Collections.Concurrent;
using Hongdal.Contracts.CommonContents;

namespace HongdalAdmin.Services;

public sealed partial class 백오피스메모리Service : I백오피스Service
{
    private readonly List<관리자대시보드요약응답> _dashboard = [new() { 오늘의뢰수 = 12, 결제대기수 = 3, 결제완료수 = 9, 배차대기수 = 4, 배차확정수 = 8, 운송중수 = 5, 완료수 = 18, 취소환불수 = 1, 운송예외수 = 1, 관리자확인필요수 = 1 }];
    private readonly List<화주운송의뢰응답> _requests = [];
    private readonly List<결제목록응답> _payments = [];
    private readonly List<배차대기응답> _dispatchWait = [];
    private readonly List<기사목록응답> _drivers = [];
    private readonly Dictionary<string, 기사상세응답> _driverDetails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<기사배차내역응답>> _driverDispatches = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<기사월정산관리응답> _settlements = [];
    private readonly List<운송진행응답> _transports = [];
    private readonly List<운송이벤트로그응답> _transportEvents = [];
    private readonly List<업체관리응답> _companies = [];
    private readonly List<화주관리응답> _shippers = [];
    private readonly List<파일POD응답> _filePods = [];
    private readonly List<공개화물요약응답> _publicCargo = [];
    private readonly List<관리자공통콘텐츠요약응답> _commonContents = [];
    private readonly Dictionary<long, 관리자공통콘텐츠상세응답> _commonContentDetails = new();
    private readonly List<공통콘텐츠보상정책Dto> _commonContentRewardPolicies = [];
    private readonly 문서관리메모리Service _documentMemory;

    public 백오피스메모리Service(문서관리메모리Service documentMemory)
    {
        _documentMemory = documentMemory;
        Seed();
    }

    public Task<관리자대시보드요약응답> 대시보드조회Async(CancellationToken cancellationToken = default)
        => Task.FromResult(_dashboard[0]);

    public Task<IReadOnlyList<화주운송의뢰응답>> 의뢰목록조회Async(string? 결제상태 = null, string? 배차상태 = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<화주운송의뢰응답> query = _requests;
        if (!string.IsNullOrWhiteSpace(결제상태)) query = query.Where(x => x.결제상태 == 결제상태.Trim());
        if (!string.IsNullOrWhiteSpace(배차상태)) query = query.Where(x => x.배차상태 == 배차상태.Trim());
        return Task.FromResult<IReadOnlyList<화주운송의뢰응답>>(query.ToArray());
    }

    public Task<IReadOnlyList<공개화물요약응답>> 공개화물요약조회Async(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<공개화물요약응답>>(_publicCargo.ToArray());

    public Task<화주운송의뢰응답?> 의뢰상세조회Async(string requestId, CancellationToken cancellationToken = default)
        => Task.FromResult(_requests.FirstOrDefault(x => string.Equals(x.의뢰Id, requestId, StringComparison.OrdinalIgnoreCase)));

    public Task<화주운송의뢰응답?> 의뢰취소환불처리Async(string requestId, CancellationToken cancellationToken = default)
    {
        var item = _requests.FirstOrDefault(x => string.Equals(x.의뢰Id, requestId, StringComparison.OrdinalIgnoreCase));
        if (item is null) return Task.FromResult<화주운송의뢰응답?>(null);
        item.의뢰상태 = "취소";
        item.결제상태 = "환불됨";
        item.배차상태 = "취소";
        var payment = _payments.FirstOrDefault(x => string.Equals(x.의뢰Id, requestId, StringComparison.OrdinalIgnoreCase));
        if (payment is not null)
        {
            payment.결제상태 = "환불됨";
        }

        _dispatchWait.RemoveAll(x => string.Equals(x.의뢰Id, requestId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<화주운송의뢰응답?>(item);
    }

    public Task<IReadOnlyList<결제목록응답>> 결제목록조회Async(string? 결제상태 = null, string? 의뢰Id = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<결제목록응답> query = _payments;
        if (!string.IsNullOrWhiteSpace(결제상태)) query = query.Where(x => x.결제상태 == 결제상태.Trim());
        if (!string.IsNullOrWhiteSpace(의뢰Id)) query = query.Where(x => x.의뢰Id == 의뢰Id.Trim());
        return Task.FromResult<IReadOnlyList<결제목록응답>>(query.ToArray());
    }

    public Task<토스결제환경응답> 토스결제환경조회Async(CancellationToken cancellationToken = default)
        => Task.FromResult(new 토스결제환경응답 { ClientKey = "sample-client-key", BaseUrl = "https://example.invalid", IsConfigured = false });

    public Task<IReadOnlyList<배차대기응답>> 배차대기목록조회Async(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<배차대기응답>>(_dispatchWait.ToArray());

    public Task<배차대기응답?> 배차대기상태변경Async(long id, string status, CancellationToken cancellationToken = default)
    {
        var item = _dispatchWait.FirstOrDefault(x => x.Id == id);
        if (item is null) return Task.FromResult<배차대기응답?>(null);
        item.상태 = status;
        item.UpdatedAt = DateTime.UtcNow;
        var request = _requests.FirstOrDefault(x => string.Equals(x.의뢰Id, item.의뢰Id, StringComparison.OrdinalIgnoreCase));
        if (request is not null)
        {
            request.배차상태 = status == "확정" ? "배차확정" : status;
        }

        if (status == "확정" && _transports.All(x => x.운송번호 != item.의뢰Id))
        {
            _transports.Add(new 운송진행응답
            {
                Id = 100 + id,
                운송번호 = item.의뢰Id,
                상태 = "배차확정",
                출발_픽업 = null,
                도착 = null,
                기사_운송자 = "DRV-001",
                출발지 = item.픽업_도로명주소,
                도착지 = item.하차_도로명주소,
                운임 = request?.최종운임,
                UpdatedAt = DateTime.UtcNow
            });
        }
        return Task.FromResult<배차대기응답?>(item);
    }

    public Task 배차대기삭제Async(long id, CancellationToken cancellationToken = default)
    {
        var item = _dispatchWait.FirstOrDefault(x => x.Id == id);
        if (item is not null)
        {
            var request = _requests.FirstOrDefault(x => string.Equals(x.의뢰Id, item.의뢰Id, StringComparison.OrdinalIgnoreCase));
            if (request is not null)
            {
                request.배차상태 = "미시작";
            }
        }

        _dispatchWait.RemoveAll(x => x.Id == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<기사목록응답>> 기사목록조회Async(string? 운행상태 = null, string? 활동지역검색어 = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<기사목록응답> query = _drivers;
        if (!string.IsNullOrWhiteSpace(운행상태)) query = query.Where(x => x.운행상태 == 운행상태.Trim());
        if (!string.IsNullOrWhiteSpace(활동지역검색어)) query = query.Where(x => x.주_활동지역.Contains(활동지역검색어.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<기사목록응답>>(query.ToArray());
    }

    public Task<기사상세응답?> 기사상세조회Async(string driverId, CancellationToken cancellationToken = default)
        => Task.FromResult(_driverDetails.TryGetValue(driverId, out var item) ? item : null);

    public Task<IReadOnlyList<기사배차내역응답>> 기사배차내역조회Async(string driverId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<기사배차내역응답>>(_driverDispatches.TryGetValue(driverId, out var list) ? list.ToArray() : []);

    public Task<IReadOnlyList<기사월정산관리응답>> 기사월정산목록조회Async(int? year = null, int? month = null, string? driverId = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<기사월정산관리응답> query = _settlements;
        if (year.HasValue) query = query.Where(x => x.년도 == year.Value);
        if (month.HasValue) query = query.Where(x => x.월 == month.Value);
        if (!string.IsNullOrWhiteSpace(driverId)) query = query.Where(x => x.기사Id == driverId.Trim());
        return Task.FromResult<IReadOnlyList<기사월정산관리응답>>(query.ToArray());
    }

    public Task<IReadOnlyList<운송진행응답>> 운송진행목록조회Async(string? 상태 = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<운송진행응답> query = _transports;
        if (!string.IsNullOrWhiteSpace(상태)) query = query.Where(x => x.상태 == 상태.Trim());
        return Task.FromResult<IReadOnlyList<운송진행응답>>(query.ToArray());
    }

    public Task<IReadOnlyList<운송이벤트로그응답>> 운송이벤트조회Async(string? requestId = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<운송이벤트로그응답> query = _transportEvents;
        if (!string.IsNullOrWhiteSpace(requestId)) query = query.Where(x => x.의뢰Id == requestId.Trim());
        return Task.FromResult<IReadOnlyList<운송이벤트로그응답>>(query.ToArray());
    }

    public Task<IReadOnlyList<업체관리응답>> 업체목록조회Async(string? 상태 = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<업체관리응답> query = _companies;
        if (!string.IsNullOrWhiteSpace(상태)) query = query.Where(x => x.상태 == 상태.Trim());
        return Task.FromResult<IReadOnlyList<업체관리응답>>(query.ToArray());
    }

    public Task<IReadOnlyList<화주관리응답>> 화주목록조회Async(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<화주관리응답>>(_shippers.ToArray());

    public Task<관리자연락처검색응답> 연락처뒤8자리검색Async(string phoneLast8, CancellationToken cancellationToken = default)
    {
        var last8 = OnlyDigits(phoneLast8);
        if (last8.Length > 8)
        {
            last8 = last8[^8..];
        }

        var people = new List<관리자연락처인물응답>();

        foreach (var driver in _drivers.Where(x => EndsWithLast8(x.연락처, last8)))
        {
            people.Add(new 관리자연락처인물응답
            {
                UserId = driver.기사Id,
                사용자명 = driver.기사명,
                연락처 = driver.연락처,
                전화번호뒤8자리 = last8,
                역할목록 = ["용달기사"],
                연락처출처목록 = ["기사 프로필 연락처"],
                기사정보 = new 관리자연락처기사정보응답
                {
                    기사명 = driver.기사명,
                    연락처 = driver.연락처,
                    차량 = driver.차량,
                    운행상태 = driver.운행상태,
                    활동지역 = driver.주_활동지역
                }
            });
        }

        foreach (var shipper in _shippers.Where(x => EndsWithLast8(x.연락처, last8)))
        {
            people.Add(new 관리자연락처인물응답
            {
                UserId = shipper.화주Id,
                사용자명 = shipper.사용자명,
                이메일 = shipper.이메일,
                연락처 = shipper.연락처,
                전화번호뒤8자리 = last8,
                역할목록 = ["화주"],
                연락처출처목록 = ["화주 계정 연락처"],
                화주정보 = new 관리자연락처화주요약응답
                {
                    의뢰건수 = shipper.의뢰건수,
                    진행중의뢰건수 = Math.Max(0, shipper.의뢰건수 - 1),
                    최근의뢰일시 = shipper.최근의뢰일시
                }
            });
        }

        return Task.FromResult(new 관리자연락처검색응답
        {
            전화번호뒤8자리 = last8,
            검색결과수 = people.Count,
            조회일시Utc = DateTime.UtcNow,
            인물목록 = people.OrderBy(x => x.사용자명).ToArray()
        });
    }

    private static bool EndsWithLast8(string? phoneNumber, string last8)
    {
        if (last8.Length != 8)
        {
            return false;
        }

        var digits = OnlyDigits(phoneNumber);
        return digits.Length >= 8 && digits.EndsWith(last8, StringComparison.Ordinal);
    }

    private static string OnlyDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private void Seed()
    {
        var now = DateTime.UtcNow;

        _requests.Add(new 화주운송의뢰응답
        {
            의뢰Id = "REQ-2026-001",
            의뢰상태 = "생성됨",
            결제상태 = "결제완료",
            정산상태 = "인수증대기",
            배차상태 = "운송중",
            운송방식 = "혼적",
            결제수단 = "카드",
            정산시점 = "선결제",
            증빙방식 = "인수증",
            수납주체 = "플랫폼",
            세금계산서필요 = true,
            현금영수증필요 = false,
            정산메모 = "샘플 데이터",
            생성일시 = now,
            픽업지 = "서울시 강남구 테헤란로",
            픽업상세지 = "100",
            하차지 = "경기도 성남시 분당구",
            하차상세지 = "200",
            대기료 = 0,
            수작업비 = 0,
            할증 = 0,
            최종운임 = 45000,
            요약 = new 의뢰요약 { 화물종류 = "전자부품", 픽업지 = "서울시 강남구", 하차지 = "경기도 성남시" }
        });

        _requests.Add(new 화주운송의뢰응답
        {
            의뢰Id = "REQ-2026-002",
            의뢰상태 = "생성됨",
            결제상태 = "결제대기",
            정산상태 = "결제대기",
            배차상태 = "미시작",
            운송방식 = "전세",
            결제수단 = "가상계좌",
            정산시점 = "선결제",
            증빙방식 = "운송확인서",
            수납주체 = "플랫폼",
            세금계산서필요 = false,
            현금영수증필요 = true,
            정산메모 = "결제 대기 샘플",
            생성일시 = now.AddHours(-2),
            픽업지 = "인천시 서구",
            픽업상세지 = "물류창고 A동",
            하차지 = "서울시 마포구",
            하차상세지 = "상가 1층",
            대기료 = 0,
            수작업비 = 10000,
            할증 = 5000,
            최종운임 = 80000,
            요약 = new 의뢰요약 { 화물종류 = "생활용품", 픽업지 = "인천시 서구", 하차지 = "서울시 마포구" }
        });

        _requests.Add(new 화주운송의뢰응답
        {
            의뢰Id = "REQ-2026-003",
            의뢰상태 = "완료",
            결제상태 = "결제완료",
            정산상태 = "정산완료",
            배차상태 = "인수완료",
            운송방식 = "혼적",
            결제수단 = "카드",
            정산시점 = "후불",
            증빙방식 = "인수증",
            수납주체 = "플랫폼",
            세금계산서필요 = true,
            현금영수증필요 = false,
            정산메모 = "완료 샘플",
            생성일시 = now.AddDays(-1),
            픽업지 = "경기도 수원시",
            픽업상세지 = "공장 출고장",
            하차지 = "대전시 유성구",
            하차상세지 = "연구소",
            대기료 = 5000,
            수작업비 = 0,
            할증 = 0,
            최종운임 = 120000,
            요약 = new 의뢰요약 { 화물종류 = "시험장비", 픽업지 = "경기도 수원시", 하차지 = "대전시 유성구" }
        });

        _payments.Add(new 결제목록응답 { 결제Id = "PAY-001", 의뢰Id = "REQ-2026-001", 화주Id = "SHIP-001", 결제금액 = 45000, 결제수단 = "카드", 결제상태 = "결제완료", OrderId = "ORDER-001", 생성일시Utc = now, 승인일시Utc = now });
        _payments.Add(new 결제목록응답 { 결제Id = "PAY-002", 의뢰Id = "REQ-2026-002", 화주Id = "SHIP-002", 결제금액 = 80000, 결제수단 = "가상계좌", 결제상태 = "결제대기", OrderId = "ORDER-002", 생성일시Utc = now.AddHours(-2) });
        _payments.Add(new 결제목록응답 { 결제Id = "PAY-003", 의뢰Id = "REQ-2026-003", 화주Id = "SHIP-001", 결제금액 = 120000, 결제수단 = "카드", 결제상태 = "결제완료", OrderId = "ORDER-003", 생성일시Utc = now.AddDays(-1), 승인일시Utc = now.AddDays(-1).AddMinutes(5) });
        _dispatchWait.Add(new 배차대기응답 { Id = 1, 의뢰Id = "REQ-2026-001", 화주Id = "SHIP-001", 픽업_도로명주소 = "서울시 강남구 테헤란로", 픽업_상세주소 = "100", 하차_도로명주소 = "경기도 성남시 분당구", 하차_상세주소 = "200", 상태 = "대기", CreatedAt = now, UpdatedAt = now });
        _dispatchWait.Add(new 배차대기응답 { Id = 2, 의뢰Id = "REQ-2026-002", 화주Id = "SHIP-002", 픽업_도로명주소 = "인천시 서구", 픽업_상세주소 = "물류창고 A동", 하차_도로명주소 = "서울시 마포구", 하차_상세주소 = "상가 1층", 상태 = "결제대기", CreatedAt = now.AddHours(-2), UpdatedAt = now.AddHours(-2) });
        _dispatchWait.Add(new 배차대기응답
        {
            Id = 3,
            의뢰Id = "GP-IMPORT-DOMESTIC-APT-001",
            화주Id = "platform",
            배차업무유형 = 20,
            원본의뢰유형 = "ImportCargoTransport",
            원본의뢰Id = "GP-IMPORT-DOMESTIC-APT-001",
            공동구매도착지유형코드 = "ApartmentComplexDirectDistribution",
            공동구매기사세대배송여부 = true,
            공동구매세대배송방식코드 = "DriverToUnitDoor",
            공동구매세대배송건수 = 33,
            공동구매분배책임코드 = "Driver",
            픽업_도로명주소 = "평택항 보세구역",
            픽업_상세주소 = "반출 대기장",
            하차_도로명주소 = "살뜰아파트",
            하차_상세주소 = "101동 세대배송",
            상태 = "대기",
            CreatedAt = now.AddMinutes(-40),
            UpdatedAt = now.AddMinutes(-40)
        });
        _drivers.Add(new 기사목록응답 { 기사Id = "DRV-001", 기사명 = "홍기사", 연락처 = "010-1111-2222", 차량 = "1톤 트럭", 주_활동지역 = "서울", 운행상태 = "운행중", 최근위도 = 37.5m, 최근경도 = 127.0m, 최근위치기록시각 = now, 배차건수 = 3 });
        _drivers.Add(new 기사목록응답 { 기사Id = "DRV-002", 기사명 = "달기사", 연락처 = "010-3333-4444", 차량 = "라보", 주_활동지역 = "경기", 운행상태 = "대기", 최근위도 = 37.3m, 최근경도 = 127.1m, 최근위치기록시각 = now.AddMinutes(-20), 배차건수 = 1 });
        _driverDetails["DRV-001"] = new 기사상세응답 { 기사Id = "DRV-001", 기사명 = "홍기사", 연락처 = "010-1111-2222", 차량 = "1톤 트럭", 주_활동지역 = "서울", 운행상태 = "운행중", 메모 = "샘플 기사", 등록일 = now, 최근위도 = 37.5m, 최근경도 = 127.0m, 최근위치기록시각 = now };
        _driverDetails["DRV-002"] = new 기사상세응답 { 기사Id = "DRV-002", 기사명 = "달기사", 연락처 = "010-3333-4444", 차량 = "라보", 주_활동지역 = "경기", 운행상태 = "대기", 메모 = "경기권 샘플 기사", 등록일 = now.AddMonths(-1), 최근위도 = 37.3m, 최근경도 = 127.1m, 최근위치기록시각 = now.AddMinutes(-20) };
        _driverDispatches["DRV-001"] = [new 기사배차내역응답 { Id = 1, 배차명 = "샘플 배차", 상태 = "완료", 배차일 = now.Date, 픽업지 = "서울", 배송지 = "성남", 배차점수 = 98, 실패사유 = string.Empty, 배차생성시각 = now.AddHours(-3), 배차완료시각 = now.AddHours(-2) }];
        _driverDispatches["DRV-002"] = [new 기사배차내역응답 { Id = 2, 배차명 = "경기권 예약", 상태 = "예약", 배차일 = now.Date.AddDays(1), 픽업지 = "수원", 배송지 = "대전", 배차점수 = 91, 실패사유 = string.Empty, 배차생성시각 = now.AddHours(-5) }];
        _settlements.Add(new 기사월정산관리응답 { 기사Id = "DRV-001", 년도 = now.Year, 월 = now.Month, 배차건수 = 3, 이용료 = 15000, 월상한적용여부 = false, 결제완료 = true, UpdatedAt = now });
        _settlements.Add(new 기사월정산관리응답 { 기사Id = "DRV-002", 년도 = now.Year, 월 = now.Month, 배차건수 = 1, 이용료 = 5000, 월상한적용여부 = false, 결제완료 = false, UpdatedAt = now.AddMinutes(-30) });
        _transports.Add(new 운송진행응답
        {
            Id = 101,
            운송번호 = "TR-101",
            상태 = "운송중",
            출발_픽업 = now.AddHours(-2),
            도착 = null,
            기사_운송자 = "DRV-001",
            출발지 = "서울",
            도착지 = "성남",
            운임 = 45000,
            예외신고됨 = true,
            최근예외단계 = "상차",
            최근예외코드 = "CargoMismatch",
            최근예외메시지 = "상차지 도착 후 실제 수량이 의뢰 수량과 다릅니다.",
            관리자확인필요 = true,
            UpdatedAt = now.AddMinutes(-12)
        });
        _transports.Add(new 운송진행응답 { Id = 102, 운송번호 = "REQ-2026-003", 상태 = "인수완료", 출발_픽업 = now.AddDays(-1).AddHours(1), 도착 = now.AddDays(-1).AddHours(4), 기사_운송자 = "DRV-002", 출발지 = "수원", 도착지 = "대전", 운임 = 120000, UpdatedAt = now.AddDays(-1).AddHours(4) });
        _transportEvents.Add(new 운송이벤트로그응답 { Id = 1, 의뢰Id = "REQ-2026-001", 이벤트타입 = "상차완료", 이벤트시각 = now.AddHours(-1), 메타데이터 = "{}" });
        _transportEvents.Add(new 운송이벤트로그응답 { Id = 2, 의뢰Id = "REQ-2026-003", 이벤트타입 = "인수완료", 이벤트시각 = now.AddDays(-1).AddHours(4), 메타데이터 = "{\"receipt\":\"created\"}" });
        _transportEvents.Add(new 운송이벤트로그응답 { Id = 3, 의뢰Id = "TR-101", 이벤트타입 = "운송예외신고", 이벤트시각 = now.AddMinutes(-12), 메타데이터 = "{\"stage\":\"상차\",\"exceptionCode\":\"CargoMismatch\",\"reason\":\"상차지 도착 후 실제 수량이 의뢰 수량과 다릅니다.\",\"adminReviewRequired\":true}" });
        _companies.Add(new 업체관리응답 { Id = 1, 업체명 = "살뜰물류", 상태 = "거래중", 대표연락처 = "02-1234-5678", 담당자 = "김담당", 이메일 = "biz@example.com", 주소 = "서울", 정산결제조건 = "월말", 등록일 = now });
        _companies.Add(new 업체관리응답 { Id = 2, 업체명 = "달빛상사", 상태 = "심사중", 대표연락처 = "032-555-0000", 담당자 = "이담당", 이메일 = "moon@example.com", 주소 = "인천", 정산결제조건 = "선결제", 등록일 = now.AddDays(-3) });
        _shippers.Add(new 화주관리응답 { 화주Id = "SHIP-001", 사용자명 = "화주A", 이메일 = "shipper@example.com", 연락처 = "010-2222-3333", 의뢰건수 = 5, 최근의뢰일시 = now, 거래상태 = "거래중" });
        _shippers.Add(new 화주관리응답 { 화주Id = "SHIP-002", 사용자명 = "화주B", 이메일 = "shipper-b@example.com", 연락처 = "010-5555-6666", 의뢰건수 = 1, 최근의뢰일시 = now.AddHours(-2), 거래상태 = "신규" });
        _filePods.Add(new 파일POD응답 { Id = Guid.NewGuid(), FileType = "인수증", RequestId = "REQ-2026-001", BucketName = "local", ObjectName = "receipt.pdf", Url = "#", OriginalFileName = "receipt.pdf", UploadStatus = "업로드완료", UploadedAtUtc = now, UpdatedAtUtc = now });
        _filePods.Add(new 파일POD응답 { Id = Guid.NewGuid(), FileType = "배송완료사진", RequestId = "REQ-2026-003", BucketName = "local", ObjectName = "pod-photo.jpg", Url = "#", OriginalFileName = "pod-photo.jpg", UploadStatus = "검수완료", UploadedAtUtc = now.AddDays(-1), UpdatedAtUtc = now.AddDays(-1).AddHours(4) });
        _publicCargo.Add(new 공개화물요약응답 { 의뢰Id = "REQ-2026-001", 화물종류 = "전자부품", 화물수량 = 10, 화물중량Kg = 120, 운송방식 = "혼적", 차량종류 = "1톤", 의뢰상태 = "생성됨", 배차상태 = "운송중", 생성일시 = now });
        _publicCargo.Add(new 공개화물요약응답 { 의뢰Id = "REQ-2026-002", 화물종류 = "생활용품", 화물수량 = 25, 화물중량Kg = 300, 운송방식 = "전세", 차량종류 = "1톤", 의뢰상태 = "생성됨", 배차상태 = "미시작", 생성일시 = now.AddHours(-2) });

        var rewardPolicy = new 공통콘텐츠보상정책Dto
        {
            Id = 1,
            보상유형 = 계약홍달보상유형.할인율,
            할인율 = 0.03m,
            최소시청초 = 30,
            필요시청비율 = 0.8m,
            사용자당1회만지급 = true,
            최대할인금액 = 15000
        };
        _commonContentRewardPolicies.Add(rewardPolicy);

        var contentRequest = new 관리자공통콘텐츠저장요청
        {
            제목 = "살뜰 소개 영상",
            설명 = "결제 전 혜택 안내 콘텐츠",
            콘텐츠유형 = 계약홍달콘텐츠유형.영상링크,
            영상Url = "https://example.invalid/hongdal-intro",
            노출위치 = 계약홍달노출위치.결제전혜택 | 계약홍달노출위치.홈화면위젯,
            기사노출 = true,
            화주노출 = true,
            운영자노출 = false,
            활성화여부 = true,
            보상정책Id = rewardPolicy.Id
        };

        var detail = BuildDetail(1, contentRequest, now);
        _commonContentDetails[detail.Id] = detail;
        UpsertSummary(detail);
    }
}
