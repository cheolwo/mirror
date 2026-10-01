# 농수산물 가격 수집 배치

홍달 서버는 Quartz 작업으로 KAMIS 국내 농수산물 가격과 USDA NASS 미국 농산물 가격을 주기적으로 수집한다. 수집 서비스는 기존 관측값을 키 기준으로 다시 사용하거나 갱신하므로 같은 기간을 재실행해도 중복 행이 늘어나지 않는다. 각 실행 결과와 실패 내용은 농수산물 수집 실행 이력 테이블에 남는다.

## 기본 일정

| 작업 | 기본 실행 시각 | 수집 범위 |
| --- | --- | --- |
| KAMIS 일별 가격 | 매일 06:30 (한국 시간) | 전날 가격 |
| KAMIS 월평균 가격 | 매월 2일 07:00 (한국 시간) | 직전 완료 월까지 12개월 |
| USDA NASS 월별 가격 | 매월 10일 07:30 (한국 시간) | 현재 연도와 직전 1년 |

같은 종류의 작업은 동시에 실행되지 않는다. 실패하면 기본 1회 즉시 재시도하며, 서버가 꺼져 있던 동안 놓친 Cron 실행을 서버 시작 직후 몰아서 수행하지는 않는다. KAMIS 월평균 작업과 USDA 작업이 최근 기간을 다시 조회하므로 늦게 확정된 원천 자료는 다음 정기 실행에서 보완된다.

## 설정

배치는 기본적으로 꺼져 있다. API 인증 정보를 비밀 설정에 넣은 환경에서 다음 설정으로 켠다.

```json
{
  "AgriculturalFisheriesBatch": {
    "Enabled": true,
    "TimeZoneId": "Asia/Seoul",
    "ImmediateRetryCount": 1,
    "KamisDailyEnabled": true,
    "KamisDailyCronExpression": "0 30 6 * * ?",
    "KamisDailyDaysBehind": 1,
    "KamisMonthlyEnabled": true,
    "KamisMonthlyCronExpression": "0 0 7 2 * ?",
    "KamisMonthlyLookbackMonths": 12,
    "UsdaMonthlyEnabled": true,
    "UsdaMonthlyCronExpression": "0 30 7 10 * ?",
    "UsdaLookbackYears": 1
  }
}
```

환경 변수로 켤 때는 `AgriculturalFisheriesBatch__Enabled=true`를 사용한다. KAMIS 인증키와 요청자 ID, USDA API 키는 저장소에 넣지 않고 사용자 비밀 또는 배포 환경의 비밀 저장소에서 주입한다.

Quartz Cron 식과 표준 시간대 ID가 올바르지 않으면 서버 시작 단계에서 설정 오류가 드러난다. 여러 서버 인스턴스가 동시에 실행되는 운영 환경에서는 Quartz 영속 저장소와 클러스터 잠금 구성을 추가해야 인스턴스 간 중복 호출까지 방지할 수 있다.
