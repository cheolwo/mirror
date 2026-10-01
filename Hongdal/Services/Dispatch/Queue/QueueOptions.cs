namespace 홍달.Services.Dispatch.Queue
{
    public sealed class 배차큐정책Options
    {
        // 추천 유지 기본 시간(초)
        public int 추천유지시간초 { get; set; } = 60;

        // 최대 추천 라운드 수
        public int 최대추천라운드 { get; set; } = 5;

        // 추천 후 공개 전환까지의 기본 대기시간(초)
        public int 공개전환대기초 { get; set; } = 0;

        // 상차가 임박했거나 당일 성격인 의뢰는 배차대기 생성 후 이 시간 안에 확정하지 못하면 공개배차로 전환한다. 0 이하이면 비활성.
        public int 당일미배정공개전환분 { get; set; } = 30;

        // 상차 시작까지 이 시간보다 많이 남은 예약 의뢰는 상차 시작 전 이 시간까지 추천 큐에 머물 수 있다.
        public int 예약상차전공개전환시간 { get; set; } = 24;

        // 예약 의뢰는 상차 전 공개전환 기준에 걸리더라도 최소 이 시간만큼은 서버 추천 큐에 머물게 한다.
        public int 예약최소추천유지분 { get; set; } = 60;

        // 상차지 반경 기준 기사 후보 검색 범위(km)
        public decimal 기사후보검색반경Km { get; set; } = 50m;

        // geo-index에서 먼저 가져올 최대 기사 수
        public int 기사후보최대조회수 { get; set; } = 100;

        // 배차 후보로 인정할 기사 위치의 최대 수신 경과시간(분)
        public int 기사위치유효시간분 { get; set; } = 10;

        // 기사님이 원거리 상차 접근 의사를 밝힌 경우 active-index에서 추가로 검토할 최대 기사 수
        public int 원거리지원후보최대조회수 { get; set; } = 300;

        // 기사님이 허용 반경을 크게 입력하더라도 OS가 검토할 최대 상차 접근 반경(km)
        public decimal 원거리상차접근최대반경Km { get; set; } = 50m;

        // 원거리 후보의 상차 시간창 도착 가능성을 대략 판단할 평균 속도
        public decimal 원거리상차평균속도KmH { get; set; } = 45m;

        // 상차 시간창 종료 전 최소 도착 여유분
        public decimal 원거리상차도착여유분 { get; set; } = 10m;
    }
}
