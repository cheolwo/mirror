# 생활 앱 외부 배송 가정 연결 r28 구현·검증

2026-10-07. `Implemented / LocalSimulationValidated / SimulationOnly / RealProviderPending`.

승인 [r28](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/assumed-providers.implementation.r28.md)의 SHA256 `90AF9E79DD06F085785207838094BF511552F7643D89E9E3F81CFC08E8D629B8`을 유지했다. [작업지시서](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/development-work-order.r28.md)의 경로만 수용했으며 기존 r24/r25/r26/r27와 다른 역할 표시 보완을 되돌리거나 함께 stage하지 않았다.

## 구현 결과

- 기존 `I자동수행Provider`·견적·동의·현재 선택·영속 시도·모의 사건 경로에 `assumed-kakao-t-quick`, `assumed-baemin`, `assumed-coupang-eats`를 추가했다. 카카오는 공식 문서 참고 모의, 나머지는 API 존재를 확인하지 않은 개발 가정이다. 실제 endpoint·인증·업체별 요청/응답·요금을 만들지 않았다.
- `FulfillmentRouting:Enabled`와 `FulfillmentRouting:AssumedProvidersEnabled`의 기본값은 모두 false다. 가정 후보는 Simulation 생활 배송에서만 노출·접수된다. 기존 음식 원천과 provider ID는 보존한다. 가정 provider 직접 호출도 Operational의 접수·조회·취소를 차단하며, Simulation에서 opt-in이 철회된 기존 시도는 조회·취소할 수 있다.
- 서버 `수행처후보(sourceKind)`와 견적의 `AutomaticProviders`를 화면에 연결했다. 빈 서버 목록을 client의 기본 목록으로 보충하지 않는다. 후보 세 개·네 번 반복의 고정 한도를 등록/동의 후보의 유한 소진으로 바꿨다. 불명확한 결과·취소·발생 비용은 여전히 다른 업체 접수를 막는다.
- `PackageSizeCode`는 Small/Medium/Large의 선택 입력이다. null JSON과 기존 지문을 보존한다. 필요한 크기가 없으면 가정 후보는 미지원/검토 보류이며 크기·무게·GPS·실제 요금을 추정하지 않는다. 새 nullable SQL 열과 수동 migration을 준비했고 공용 DB에는 적용하지 않았다.
- 포장 크기를 원 의뢰·Mongo 원장 블록·SQL 재구성에 보존한다. 최초 수행 동의 전에 원장 동기화를 확인하며 실패는 같은 요청 번호로 복구한다. 구형/null/알 수 없는/늦은 다른 값은 기존 근거를 지우거나 덮지 않는다. 현재 동의 이후 재요청은 원천 판본을 다시 변경하지 않는다.
- 외부 모의 사건은 현재 시도·원천·동의·모의 job의 관계를 검사한다. 명시 배정→픽업→전달만 진행시키며 내부 기사·큐·배차 완료를 대신 만들지 않는다. 같은 원천과 완료 증거가 일치할 때만 생활 협업의 완료 확인이 가능해진다. 양측 확인·공개 이력 동의·결제·지급은 따로 유지한다. 모의 내부 완료도 Operational의 실운송 완료 근거로 읽지 않는다.

## 검증을 구분한 결과

| 증거 | 실제 확인 | 한계 |
| --- | --- | --- |
| 신규 자동 시험 | 수행처 29·입력/원장 22·완료 source 13·양측 완료 1·UI 수명 8, 총 73개 통과 | SQLite와 명시 대역 중심이며 실제 업체/공용 MySQL/Mongo/휴대폰 증거 아님 |
| 첫 집중 시험 | 기존 Coordinator·협업·입력·VM 포함 155개 통과 | 이후 추가 경계는 최종 Fast/Task에서 다시 확인 |
| 최종 Fast | 관련 701개 통과, contracts/shared UI/server 영향 build 통과 | 로컬 코드/시험 |
| 최종 Task | 전체 3.5 solution build 오류 0·경고 102, 8,470개 통과·2개 건너뜀 | 환경 조건 실제 Mongo 시험 2개는 미실행 |
| 실제 공통 UI 렌더 | 11상태×320/390px = 22장면, 입력 변경/원 결과 확인 4흐름, 가로 넘침·page error 0 | 합성 메모리 client이며 실제 운영 API 연결이나 Android 화면 아님 |
| 외부 효과 | 렌더에서 외부/업무 API 요청 0, 실제 endpoint·결제·배송 호출 없음 | 제휴·실제 업체 지원 여부는 미확인 |

원시 시험은 `artifacts/local/assumed-fulfillment-r28/tests/r28-focused.trx`, 최종 Fast/Task는 `artifacts/local/validation/20261007-193416/`와 `20261007-193533/`, 렌더는 `artifacts/local/assumed-fulfillment-r28/ui/report.json`에 있다. 렌더 전후 대상 UI 소스 hash가 같고 22장면의 가로 넘침과 JavaScript 오류가 없음을 자동 확인했다. 작성390·빈 후보320·완료320 화면을 별도로 시각 판독했다. 모든 페이지·Figma·접근성의 전수 완료 주장은 아니다.

검증 과정에서 없던 Moq 참조를 명시적으로 호출 시 실패하는 기존 포트 대역으로 교체했고 최종 빌드·시험을 다시 통과했다. 모의 완료의 실행 모드와 두 SQL 조회 사이 원천 변경 가능성은 읽기 검토에서 발견해 보완·회귀 확인했다.

## 화면 샘플

아래 PNG는 실제 공통 컴포넌트의 합성 자료 렌더이며 **샘플용 이미지, 확정 시안 아님**이다. 주소·연락처·계정은 합성 값이고 실제 이용자의 정보가 아니다.

[작성·서버 후보 390px](../assets/changes/2026-10-07-assumed-fulfillment-r28/create-390.png) · [모의 전달 완료 320px](../assets/changes/2026-10-07-assumed-fulfillment-r28/completed-320.png)

## 개발 재확인

화면 preview는 Development·loopback만 허용한다. `ASPNETCORE_ENVIRONMENT=Development`에서 `dotnet run --project eng/AssumedFulfillmentPreview/AssumedFulfillmentPreview.csproj --no-launch-profile`로 실행하고 `http://127.0.0.1:5400/?state=create`를 연다. 실제 앱과 DB를 대체하지 않는다. `verify-ui.cjs`는 설치된 Chrome과 Playwright로 위 검토를 반복하며 산출물은 로컬 artifacts에만 쓴다. 검증 후 전용 preview를 종료했다.

실제 서버 후보 실험에는 기존 전용 Simulation 호스트에서 두 플래그를 명시적으로 켜고 호스트 schema에 신규 migration을 적용하는 별도 준비가 필요하다. 이번에 공용 DB·운영 설정은 변경하지 않았다. API/사업자별 스펙과 계약을 확인한 뒤 실제 adapter를 구현할 때 공통 의뢰·동의·시도 경계를 재사용한다.

## 후속과 제외

현재 완료선은 코드·SQLite/double 기반 통합 시험·공통 UI 샘플이다. 현재 소스의 인증 HTTP→실제 MySQL/Mongo 종단, 실제 외부 sandbox/계약, 실제 가격, Android APK/휴대폰, 실 GPS, 실결제·지급, Unity, 공개 운영·배포는 별도다. 원 의뢰 자체 취소 뒤 외부 취소가 잠기는 기존 `CanCancel=!SourceClosed` 정책은 후속 검토로 남기며 새 모의 validator가 동의 철회 후 취소를 막는 문제는 회귀 시험으로 배제했다.

문서 작성 지침을 사용해 승인 가정·실행 상한·실제 증거를 기존 기획/책임 카드에 연결했다. 승인 본문·hash와 과거 결정은 덮지 않았다. commit·push·배포는 수행하지 않았다.
