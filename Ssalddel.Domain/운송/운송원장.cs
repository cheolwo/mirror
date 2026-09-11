using System;
using 살뜰.도메인.공통;

namespace 살뜰.도메인.운송
{
    // 호환용 타입명입니다. 원장 원본은 Mongo 커뮤니티 원장 문서가 맡고,
    // 이 RDB 엔티티는 배차/기사 진행/관리자 조회를 위한 운송 실행 투영입니다.
    public class 운송원장
    {
        public long Id { get; set; }

        public string 운송번호 { get; set; } = string.Empty;

        public string 의뢰Id { get; set; } = string.Empty;

        public string 화주Id { get; set; } = string.Empty;

        public int 배차업무유형 { get; set; } = 상태값.배차업무유형.용달운송;

        public string 원본의뢰유형 { get; set; } = "CargoTransport";

        public string 원본의뢰Id { get; set; } = string.Empty;

        public string? 커뮤니티원장Id { get; set; }

        public string? 커뮤니티원장템플릿Key { get; set; }

        public string? 커뮤니티원장상태 { get; set; }

        public DateTime? 커뮤니티원장동기화시각Utc { get; set; }

        public string? 공동구매도착지유형코드 { get; set; }

        public bool? 공동구매기사세대배송여부 { get; set; }

        public string? 공동구매세대배송방식코드 { get; set; }

        public int? 공동구매세대배송건수 { get; set; }

        public string? 공동구매분배책임코드 { get; set; }

        public string 상태 { get; set; } = "배차대기";

        public int 배차큐단계 { get; set; } = 상태값.배차큐단계.계획배차;

        public int 배차노출상태 { get; set; } = 상태값.배차노출상태.계획대기;

        public string? 현재추천대상기사Id { get; set; }

        public DateTime? 추천시작시각 { get; set; }

        public DateTime? 추천만료시각 { get; set; }

        public int 추천라운드 { get; set; }

        public int 계획배차시도횟수 { get; set; }

        public string? 마지막거절기사Id { get; set; }

        public DateTime? 공개전환시각 { get; set; }

        public string? 확정기사Id { get; set; }

        public string 픽업_도로명주소 { get; set; } = string.Empty;

        public string 픽업_상세주소 { get; set; } = string.Empty;

        public decimal? 픽업_위도 { get; set; }

        public decimal? 픽업_경도 { get; set; }

        public string 하차_도로명주소 { get; set; } = string.Empty;

        public string 하차_상세주소 { get; set; } = string.Empty;

        public decimal? 하차_위도 { get; set; }

        public decimal? 하차_경도 { get; set; }

        public DateTime? 출발_픽업 { get; set; }

        public DateTime? 도착 { get; set; }

        public string 기사_운송자 { get; set; } = string.Empty;

        public string 출발지 { get; set; } = string.Empty;

        public string 도착지 { get; set; } = string.Empty;

        public decimal? 운임 { get; set; }

        public decimal? 기사기본거리지급액 { get; set; }

        public decimal? 기사기상할증액 { get; set; }

        public decimal? 기사지급예정액 { get; set; }

        public bool 기사기상할증적용여부 { get; set; }

        public string? 픽업지기상자료상태 { get; set; }

        public string? 픽업지기상코드 { get; set; }

        public DateTime? 픽업지기상기준시각Utc { get; set; }

        public string? 픽업지기상자료출처 { get; set; }

        public string? 픽업지기상자료Hash { get; set; }

        public string? 기사제안요금정책판본 { get; set; }

        public DateTime? 기사제안요금판정시각Utc { get; set; }

        public string 첨부_json { get; set; } = "[]";

        public string 메모 { get; set; } = string.Empty;

        public DateTime? RowVersion { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
