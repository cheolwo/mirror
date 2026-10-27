# 화물 AI 추천부터 휴대폰 사진 완료까지 검증 r1

- 날짜: 2026-10-21
- 결과: `CompletedSyntheticDeviceValidation / LocalOnly`
- 화면: **직접 확인** — USB로 연결한 Samsung SM-A346N의 실제 화면과 카메라를 사용했다.
- [계획·범위](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-ai-phone-verification.r1.md) · [작업 명세](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-ai-phone-verification.r1.work-order.json)
- [단계별 화면 갤러리](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/index.html) · [화면·해시 색인](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/evidence.json) · [서버·사진 최종 대조](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/sanitized-final-proof.json)

가상 화물 한 건을 실제 휴대폰에서 추천 확인·수락·상차 사진·하차 사진·완료까지 처리했다. 서버의 실제 화물 후보 AI 선택 감사와 휴대폰 화면을 같은 의뢰·운송 ID에 연결했다. 제품 화면을 변경하거나 새 APK를 설치하지 않았고, 기존 설치판과 전용 로컬 서버를 사용했다.

## 업무가 진행된 순서

```text
가상 화물 접수 — 보조 도구의 정상 API, 휴대폰 작성 시험은 아님
└─ 서버가 현재 조건을 통과한 기사 후보를 검사
   └─ 실제 Ollama 선택 → 최신 후보 재검사 → 추천 저장
      └─ 휴대폰의 주기 조회 → 지도 상단 새 추천 배너
         └─ 배너 누름 → 별도 지도·픽업/전달 핀·6,200원 카드
            └─ 수락 → 주의사항 확인 → 서버 확정
               └─ 상차지 도착 → 확인 3개 → 실제 카메라·사진 첨부
                  └─ 상차 완료 → 자동으로 하차 기록 화면
                     └─ 하차지 도착 → 확인 3개 → 실제 카메라·사진 첨부
                        └─ 하차 완료 안내
                           ├─ SQL 인수완료 / Mongo 완료·인수완료 단계
                           ├─ 화주 화면 인수완료·기사 위치 비공개
                           └─ 결제·정산·인수증 등록은 별도 상태로 유지
```

## 같은 주문의 확인 결과

| 항목 | 확인 내용 |
| --- | --- |
| 의뢰 / 운송 | `d828af66-2803-4c14-2415-a56ddbb25fcc` / `9`, 사례 `phone-02` |
| 실제 AI 선택 | 제품 `CargoDispatchAI.Applied`, 감사 사건 `19`, `추천시작됨`, 규칙 복귀 아님 |
| 추천 / 수락 | 차수 `1`, 동일 합성 기사가 휴대폰에서 수락·수행 |
| 금액 | 카드 운임 및 동결 기사 보수 `6,200원`; 실수금·송금 아님 |
| 상차·하차 완료 관문 | 도착·확인 후에도 사진 전에는 완료 버튼 비활성, 사진 첨부 후 활성 |
| 최종 원장 | SQL `인수완료`, 단계 `90`, 노출 `900`; Mongo `완료`, 단계 `인수완료`, revision `8` |
| 화주 재조회 | 같은 의뢰의 인수완료·상하차 완료 표시, 운송 종료 후 기사 위치 비공개 |
| 상차 사진 | `2,876,195`바이트, SHA-256 `dc1307a9cfa0015737efd1f8956ca666ce9fdaee0bbab1203343b3e2e159b6eb` |
| 하차 사진 | `2,893,133`바이트, SHA-256 `02c28e53a9333e3aab3b684e0c276ebef7e9bc63f6e7cc8ac6d686577ef96446` |
| 사진 저장 | 서로 다른 사진 2개. 업로드 영수증·의뢰·운송·수행 기사·추천 차수·완료 첨부와 비공개 저장 파일의 바이트/SHA 일치 |

화면 캡처와 서버 proof는 별도의 증거다. 서버 검증 도구의 `deviceVerified=false` 또는 `physicalDeviceVerified=false`는 서버 응답 자체가 휴대폰 조작을 인증하지 않는다는 뜻이므로 덮어쓰지 않았다. 실제 단말 결과는 별도 PNG·캡처 시각·해시·화면 경로에서 확인한다. 자동 배우의 수락·촬영·완료는 사용하지 않았다.

## 대표 화면

| 단계 | 실제 캡처 | 읽는 방법 |
| --- | --- | --- |
| 새 추천 | [상단 배너](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/03b-recommendation-observation.png) | 전경 주기 조회로 새 추천 1건 수신 |
| 추천 지도 | [운임·주소·거절/수락](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/04b-offer-loaded.png) | 주황 픽업·파랑 전달 핀, 실제 SDK 지도; 위치·경로 입력은 합성 |
| 상차 확인 | [사진 전](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/07-pickup-arrived.png) · [촬영](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/08-pickup-camera.png) · [첨부 후](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/09-pickup-review.png) | 실제 카메라 사용과 완료 관문 |
| 다음 상태 | [자동 하차 화면](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/10-pickup-completed.png) | 상차 완료 응답 후 필요한 다음 맥락으로 이동 |
| 하차 확인 | [사진 전](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/11-dropoff-arrived.png) · [첨부 후](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/13-dropoff-review.png) | 상차 사진을 재사용하지 않고 새로 촬영 |
| 완료 | [기사 완료 안내](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/14-delivery-completed.png) · [화주 결과](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/15-shipper-result.png) | 동일 주문 완료·위치 비공개 확인 |
| 후속 업무 | [화주 진행 이력](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/16-shipper-timeline.png) · [증빙 관리](../assets/changes/2026-10-21-cargo-ai-phone-verification-r1/17-shipper-proof-status.png) | 운송 완료와 결제·정산·인수증 등록은 별도 |

초기 로딩·제출 중 화면을 완료 화면으로 쓰지 않는다. raw `03-recommendation-banner`, `04-offer-map-card`, `06-accepted-work`는 각각 배너 도착 전·카드 로딩·수락 처리 중 캡처라 대표 갤러리에서 제외했다. 서버 재시작 중 `02b-native-map-observation`의 조회 실패·버튼 비활성도 보존했으며 카드 접기 성공으로 표시하지 않는다.

상차·하차 화면은 길어서 일부 하단 완료 버튼이 PNG의 보이는 범위 밖에 있다. 사진 전/후 활성 여부는 별도 실제 UI 조회로 확인했으며, 해당 정지 화면만으로 모든 버튼이 보인다는 뜻은 아니다.

## 이번에 준비·보완한 검증 환경

- 기존 통합앱 `0.1.200/200`, 화물 기사앱 `0.1.190/190`의 설치 바이트 SHA를 기존 APK와 대조했다. 새 설치·재빌드는 하지 않았다.
- `eng/CargoAiDeviceVerificationR114`에 명시적 `--serve --cargo-primary-ai` 프로필을 추가해 제품의 현재 화물 AI 선택 경로를 사용했다. 구형 `--serve`와 운영 기본 OFF는 유지한다. 기본 시작 스크립트는 구형 프로필이다.
- 로컬 `5364` 서버와 USB reverse를 사용했다. 기존 `5595` 검토 서버·Ollama·다른 캠페인 원문은 유지했다. 모델은 기존 `qwen3:1.7b`; 다운로드·가중치 학습 없음.
- 기존 전용 DB의 누락 nullable 열 `10개`만 허용 목록에 따라 추가하고 기존 행수 보존을 확인했다. 전체 migration이나 기존 원장 초기화는 하지 않았다.
- 첫 `phone-01`은 차량 제원 0행 때문에 후보 없음·공개 단계 전환이었다. 실패 원장·추천 차수 0·원문을 보존했다. 기존 프로젝트의 1톤 카고 수치를 합성 fixture 1행으로 보완하고 새 `phone-02`로 시험했다. 실차 측정·승인 아님.
- 새 시도의 추천 저장 성공은 결과 코드·기사·정본 차수 일치를 확인한다. 공개 단계 전환을 추천 성공으로 표시하지 않으며, 실제 모델 선택은 별도 제품 감사로 판정한다.
- 최종 도구 빌드 경고/오류 `0`, 최신 primary 자체 검사 `23/23`, 기존 selector 검사 `31/31` 통과. 제품 전체 시험의 이전 결과를 이번 재실행으로 합산하지 않는다.

세부 실행 기록은 `artifacts/local/cargo-ai-phone-verification/20261021-r1/`에 있다. 최신 23개 검사는 `primary-profile-selfchecks-vehicle-trace.json`이며, 이름이 final인 이전 18개 결과와 구분한다. `api_setup.py`는 준비·추천·읽기 전용 proof만 허용하고 휴대폰 수행 명령을 대행하지 않는다.

최종 범위 지정 Fast/Task는 `Guidance/docs only`로 분류되어 diff 검사만 통과했고 build/test는 생략됐다. 따라서 명시적 도구 빌드·자체 검사·실제 기기 완주와 별도로 기록한다. 대표 PNG16개의 복사 전후·HTTP 조회 SHA, 새 본문 Markdown 링크20개와 Python 도우미6개 구문을 검사했다. 갤러리는 1280px·390px에서 이미지16개 로드와 가로 넘침 없음을 확인했다. 최초 headless 검증은 번들 Chromium 부재로 시작되지 않았고, 설치된 Chrome으로 다시 확인했다.

완료 안내의 ‘진행 중 운송으로’를 실제로 눌러 현재 진행 중 운송 없음도 확인했다. 이 추가 `18-driver-no-active-work` raw 캡처는 별도 보존하며 대표16장과 그 시점 raw manifest 해시를 바꾸지 않는다. 시험 호스트와 화면 모음 서버는 각각 loopback5364/5392에 남겨뒀고 자동 수락·완료 작업은 없다.

## 남은 점과 다음 순서

1. **기사의 지도 → 현재 운송 진입 재현 확인.** 첫 ‘운송 보기’ 누름 뒤 전환을 확인하지 못했고, 기존 ‘목록 → 운송 → 상차 처리하기’로 이어갔다. 화면·이벤트 코드를 대조했으나 원인을 확정하지 않았다. 설치 앱의 첫 화면도 요약 홈이어서 운행 지도까지 메뉴 진입이 필요하다.
2. **화주 화면의 설명과 원장 용어 정리.** 진행 이력에는 GUID·adapter·endpoint·FakePG 같은 개발 문구가 보인다. 증빙 화면의 ‘의뢰 상태 생성됨’·‘증빙 방식 없음’은 운송 완료/기사 사진 저장과 다른 원장 필드를 읽으므로 사용자가 미완료로 오해할 여지가 있다. 실제 SQL/Mongo 운송과 사진 바인딩은 일치한다. 사진/POD 원본 미리보기는 이 화면에서 검증하지 않았다.
3. **외부 통신·백그라운드 푸시 별도 시험.** 이번 배너는 전경 주기 조회다. FCM·잠금/백그라운드·종료 상태 수신과 알림음 청취는 확인하지 않았다.
4. **운영 DB 준비와 실제 이동·재원 확인.** 기존 EnsureCreated DB의 migration 이력 공백으로 persistence health는 Unhealthy가 남는다. 실제 API·SQL/Mongo 결과와 별개이며 운영 준비 완료가 아니다. 실제 도로 ETA/GPS·실물 인도·실결제·기사 입금도 미검증이다.

사진은 사용자가 준비한 빈 벽·책상 방향의 시험 장면이다. 업무 확인 항목의 클릭은 가상 업무 수행이며 실제 화물·수령자의 인계를 증명하지 않는다. 음식·피킹 혼합, 다건 조합, 모든 예외·역할의 전수 단말 검증 완료로 확대하지 않는다. 제품 소스 변경·운영 활성화·클라우드 배포·stage/commit/push는 하지 않았다.
