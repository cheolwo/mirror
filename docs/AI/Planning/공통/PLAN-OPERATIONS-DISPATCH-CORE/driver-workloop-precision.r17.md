# 기사 운행·추천·수행 흐름 정밀 보완 r17

상태: Approved / ReadyToDispatch. 2026-10-27 사용자는 현재 구현 확인 뒤 기능을 늘리기보다 제안한 기존 운행 흐름의 보완을 진행하도록 지시했다. 이번 작업은 같은 로컬 개발 checkout에서 기획·구현·격리 검증을 수행한다. 서버가 업무 정본을 소유하며 운영 활성화·실사용 배차·실금융·FCM 설정은 포함하지 않는다.

## 작업지시와 수용 기준

- 기준 commit: `d3a19163ba96a7dbed31028f639f3bf0c534ae79`, branch `dev/mirror-integration`. 기존 dirty 1,444개는 다른 작업을 포함한다. 이번 경로는 수정 직전 사본과 SHA를 보존하고 최소 patch만 합친다.
- 목표: 운행 확정과 위치 송신이 확인됐을 때 기존 지도로 이어지고, 하차 완료를 같은 운송에서 확인한 뒤 사용자가 지도에서 다음 업무를 조회한다. 미확정·권한 실패·다른 계정 응답은 성공/이동으로 취급하지 않는다.
- 서버 정밀화: 현재 기사에게 속한 활성 운송의 시간약속만 위험 조회에 사용한다. 기존 위치 신선도 정책을 재사용해 오래된/미래/없는 위치로 현재 ETA를 만들지 않는다.
- 기존 배차 수신 ON/OFF, 제안 유효기간·대상 기사·수락 멱등성, 완료 알림과 결과 확인, 현재 운송/다음 제안 분리를 보존한다. 새 독차/혼적 규칙·고정거점 권역·새 추천 엔진·공개콜 전환 정책·운영 플래그를 추가하거나 바꾸지 않는다.

## 페이지 책임

운행 시작 페이지는 운행과 단말 위치 송신의 시작/복구를 확인한다. 확인 성공 후 기존 native 지도로 인계하며, 지도 열기 실패는 이미 확정된 운행 실패로 표현하지 않고 수동 재시도 수단을 남긴다. 위치 송신 안내는 실제 시작 요청과 서버 권장 간격 조정을 일치시킨다.

하차 페이지는 해당 운송의 도착·하차·인계를 확정한다. 완료 알림을 먼저 유지하고, 정본 인수완료일 때만 `지도에서 다음 업무 확인`을 제공한다. 기존 운송 화면 복귀와 증빙/실패/미확정 결과 확인을 보존한다. 지도로 돌아간다는 사실은 다음 콜 배정·자동 수락을 뜻하지 않는다. 기존 지도는 전경 재진입 때 현재 운송·수신 상태·유효 제안을 다시 조회한다.

## 허용 writePaths

- `DriverApp/ViewModels/Driver/Work/기사운행시작PageViewModel.cs`
- `DriverApp/Components/Pages/Driver/01_Work/운행시작Page.razor`
- `DriverApp/Components/Pages/Driver/03_Progress/하차Page.razor`
- 필요한 최소 `DriverApp/Services/DriverNativeMapNavigator.cs` 보완 (동일 지도 중복 적재 방지에 한함)
- `DriverApp/Services/IDriverNativeMapNavigator.cs`: 기존 호출을 보존하는 선택적 취소 토큰. 지도 적재 대기 중 페이지 이탈·대상/계정 변경의 늦은 이동을 막는 범위만 포함한다.
- `Ssalddel/Services/Dispatch/Continuity/화물연속배차UseCase.cs`
- `Ssalddel.Tests/Services/Dispatch/Continuity/CargoContinuityRiskPrecisionR17Tests.cs` 및 직접 영향 기존 회귀시험
- `eng/Ssalddel.IntegratedWorkflowR108/`의 이번 검증에 꼭 필요한 제한된 보완, 기존 합성/외부효과 차단 경계 유지
- 이 기획, 관련 기존 페이지 설명, `docs/AI/CURRENT_WORK.md`, `docs/AI/PLANNING.md`, `docs/Changes/README.md`, 이번 변경 보고와 실제 화면 자산
- `artifacts/local/driver-workloop-precision-r17/`의 사본·해시·시험/서버/기기 근거 (비밀은 기존 비공개 위치)

## 검증과 단계별 기록

1. 수정 전/후 해시, 이 기획 revision/hash와 담당 경로를 고정한다.
2. 직접 위험 조회 시험과 기존 시작 복구·단건 추천·진행 중 다음 건·하차 완료/응답유실·계정교체 시험, 범위 Fast/Task를 수행한다.
3. 기존 r108의 새 격리 Simulation 실행에서 동일 합성 기사로 2~3건을 순차 처리하고 역할 API·SQL/Mongo·중복 없음·재시작을 대조한다. 제어된 추천과 자동 스케줄러를 구별한다.
4. 최신 기사 APK를 실제 USB 휴대폰에서 확인한다. 앱 시작/지도 복귀·완료 후 다음 업무·중단/복귀를 가능한 실제 조작으로 확인하고, 합성 GPS/사진·지도 인증/실 GPS/FCM 한계와 수행하지 못한 구간을 분리한다. 사용자 원래 앱/설정/데이터는 보존한다.
5. 완료는 구현/자동시험/격리 서버/실기기/실제 운행/공개를 나눠 기록한다. 확인되지 않은 운영 경계는 비활성 상태를 유지한다. commit/push·서비스 상시 실행·자동화는 수행하지 않는다.
