# [기획 · 월드·데이터·관찰 · PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD · 구현 r21]

## 범위

HS 2022 법정 HS6 조사 대장을 오름차순으로 이어, 기존 30개 뒤의 `010121`부터 `020621`까지 58개 코드를 다섯 반복 배치로 처리했다.

- `chapter01-live-animals-01`~`03`: 제1류 34개 HS6, 관세청 HSK10 자식 69개.
- `chapter02-meat-01`~`02`: 제2류 첫 24개 HS6, 관세청 HSK10 자식 28개.

관세청 2026 HSK10 성질 분류표 `11,327행`은 국내 10단위 분류이고, 순차 조사 대장은 WCO HS 2022 법정 HS6 `5,612개`를 단위로 한다. 따라서 기존의 “남은 5,582개”는 HSK10 잔여가 아니라 당시 검토되지 않은 HS6 수였다.

## 조사 판정

- 제1류 34개는 쿠팡의 [판매 불가 품목 안내](https://marketplace.coupang.com/information-center/blog-news7?rf=MARKETPLACE)에 따라 모두 `RestrictedOrSensitive / LiveAnimalOnlineSaleProhibited`로 닫았다. 대표 상품은 만들지 않았다.
- 제2류 첫 12개 중 소·돼지 도체와 이분도체 4개는 소비자 소매 단위가 아니므로 `ChannelInapplicable`로 닫았다.
- 나머지 20개 중 16개는 냉장·냉동, 축종, 뼈 유무가 검색 색인 문구와 맞는 쿠팡 후보 36건을 기록했다. 모두 `PendingHumanReview`, `affiliate=false`, `distributionApproved=false`다.
- `020312`, `020322`의 뼈사태 후보는 제0203호 절단육과 제0206호 식용 설육의 경계를 사람이 검토하기 전에는 확정하지 않는다.
- `020621` 냉동 소 혀는 제목에서 냉동을 확인한 후보 한 건만 남기고 `Fresh/생` 표기 후보 두 건을 제외했다.
- 검색 색인 위치와 가격·재고는 쿠팡 내부 순위나 현재 판매 사실이 아니다. 직접 접근 403은 우회하지 않는다.

## 수집·저장 결과

| 배치 | HS6 | HSK10 | 원본 무역행 | 정규화행 | 상품 후보 |
| --- | ---: | ---: | ---: | ---: | ---: |
| `chapter01-live-animals-01` | 12 | 23 | 28 | 94 | 0 |
| `chapter01-live-animals-02` | 12 | 21 | 16 | 80 | 0 |
| `chapter01-live-animals-03` | 10 | 25 | 158 | 124 | 0 |
| `chapter02-meat-01` | 12 | 16 | 70 | 155 | 19 |
| `chapter02-meat-02` | 12 | 12 | 22 | 109 | 17 |

- 다섯 순차 배치 합계: HS6 58개, HSK10 자식 97개, 정규화 562행.
- 기존 다섯 반복 배치 450행을 포함한 전체 열 배치: 1,012행.
- 각 신규 배치는 최초 저장, 독립 재조회, 동일 입력 재적용 신규 0행을 확인했다.
- 전수 대장 11,226행은 후보 관찰 46개, 제한 완료 34개, 채널 부적합 8개, 대기 5,524개, 통계 특수 1개로 재생성·저장·독립 재조회했다.
- UN Comtrade 응답에 행이 없는 코드는 `NoReportedRows`로 남겼으며 0으로 바꾸지 않았다. 순중량은 원본 소수 정밀도를 그대로 동결했다.

## 출처와 검증 상한

- HS6: [UN Comtrade H6 참고 목록](https://comtradeapi.un.org/files/v1/app/reference/H6.json).
- 대한민국 2025년 수입: [UN Comtrade 공개 API](https://comtradeapi.un.org/public/v1/preview/C/A/HS), 연간·수입·보고국 410.
- HSK10·성질 분류: [관세청 HS부호](https://www.data.go.kr/data/15049722/fileData.do), [관세청 HSK별 성질별 분류](https://www.data.go.kr/data/15049720/fileData.do), 2026 판본.
- 소매 제한·후보: 쿠팡 공식 정책과 공개 검색 색인. 개별 상품 HS 결속은 확정하지 않았다.

수집기 build는 경고 0·오류 0이며 배치 자체시험 12개와 전수 대장 자체시험 10개를 통과했다. 읽기 API, Unity 투영·Game View, 외부 게시, commit·push는 수행하지 않았다.

다음 오름차순 조사 시작점은 `020622`다.
