# 화주 최종 검토의 늦은 초안 조회 무효화 r7

화주 작성/최종 검토의 등록 준비 중 서버가 바뀌거나 화면 수명이 끝났을 때, 늦게 끝난 초안 읽기가 준비 완료를 표시하지 않도록 보완한다. 새 화면·기능·등록 정책은 추가하지 않는다. [r5의 저장소 오류 안내](2026-11-04-shipper-authoring-storage-errors-r5.md)와 [r6의 검증 서버 연결 보완](2026-11-04-verification-mongo-idle-r6.md)은 보존한다.

## 발견과 최소 수정

- 실제 `AuthSession`·`WorkflowRecoveryContext`와 MAUI 보호 저장소의 읽기 경계를 사용하는 합성 회귀2건에서 재현했다. 서버별 미확정 요청/이전 요청 조회 뒤 초안 읽기만 지연하고, endpoint 변경 또는 ViewModel Dispose 뒤 정상적인 빈 응답을 반환했다. 변경 전 두 경우 모두 `RegistrationEnabled`가 null에서 true로 바뀌어 두 검사가 실패했다. 실제 휴대폰의 서버/자료를 바꾸거나 저장소를 손상시킨 재현은 아니다.
- `RestoreDraftAsync`는 오래된 응답을 적용하지 않고 반환했으나, 호출자인 `InitializeAsync`가 그 뒤 등록 준비를 완료했다. 등록 준비 완료 직전에 기존 `IsCurrent(context)`를 한 번 더 확인한다. 기존 계정·서버·세대·화면 수명 판정을 재사용하며 별도 상태나 재시도 규칙을 만들지 않는다.
- 회귀는 새 서버에서 명시적으로 다시 준비하는 복구, 기존 입력/동의와 전송·저장·삭제·화면 이동이 발생하지 않는 경계도 확인한다. 새 접수나 인증·서버 요청을 자동 재전송하지 않는다.

## 검증과 재개

- 수정 전 두 회귀가 각각 `Assert.Null`의 실제값 true로 실패했다. 수정 후 Fast는 관련24/24 통과·시험 의존 빌드 오류0/경고229·소비 화주 앱 빌드 오류0/경고22다. Task는 `Ssalddel.v0.0.slnx` 빌드 오류0/경고5와 전체16,904통과·실패0·환경17생략이다. TRX 개별 결과로 실행·생략을 구분했으며 이전 통과 수를 승계하지 않았다.
- APK144를 새 출력으로 한 번 만들고 선택 입력3,273개 변경0·기존 서명/패키지·두 ABI의 실제 앱 DLL·`http://127.0.0.1:5378/`를 대조했다. APK SHA는 `85100e12e9b42d4c13fa98e2a66f4ea13e9eb94878ccdded226d0e7aaf88f931`, 앱 DLL SHA는 `2475b55e14885a07dc774e23146f15d56ee3a9d6598628408058fe1247d483aa`다. 포장 오류0·경고행13개이며 경고행 수와 고유 경고 수를 구분한다. **설치·실제 화면 수행·144의 서버 통신은 아직 확인하지 않았다.** 기존143/이전 출력은 보존한다.
- 빌드/시험 후에도 같은 r6 서버의 경로·PID/시작 시각·DLL이 유지되고 실제 live/ready·홈 집계·업무 proof를 재조회했다. 두 업무 proof SHA와 같은 run/격리 DB·금융 행0을 유지했다. 서버 재시작·DB 변경 없이 클라이언트 수정만 검증했으며 API의 정상 응답을 수정 VM의 기기 수행 근거로 쓰지 않는다.

실제 등록 단일 USB의 OS 잠금과 설치143 SHA는 읽기로 대조했다. 배민 앱이 전면이고 사용자 사용 여부를 확정할 수 없어 전환·설치·입력은 하지 않았다. 화면 켜짐·stay-on은 관측했으나 마지막 사용자 입력 시각은 해석하지 못했으며 이를 기기가 비었다는 근거로 사용하지 않았다. 수집 checkpoint는 완료 상태임을 읽었을 뿐 변경하지 않았다. 이번 회귀와 실제 앱 조작은 별도다.

재개 위치는 `artifacts/local/app-stability-r7/8e075c4fb2914411a4faa6e34d83eb25/completion.json`이다. 같은 폴더의 `source-before.json`, `reproduction/inputs.json`/`reproduction.trx`/`result.json`, `validation.json`, `revised/proof.json`/`endpoint.json`, `runtime-identity.json`/`runtime-before.json`/`runtime-after.json`/`runtime-final-identity.json`, `public-cooperation-before.json`, `phone-read-only.json`에 재현·소스·APK·현재 서버/기기를 구분한다. Fast/Task 로그는 각각 `artifacts/local/validation/20261104-160413`, `20261104-160557`이다. 이전 실패·정상 결과와 기기 자료는 보존한다.

다음 한 가지는 기기를 사용할 수 있을 때 단일 USB·현재 설치/서명·소스와 endpoint를 다시 대조하고, 준비된144를 데이터 삭제 없이 한 번 업데이트해 화주 최종 검토 진입/입력 복귀를 확인하는 것이다. 기존143에는 이번 한 줄 수정이 반영되지 않았다. 다른 역할 전체·실기기 오류 재현·앱 전체 안정화·원격 운영은 별도다. 실제 금융·배차/운행·운영 활성화·원격 배포·배민 내역 수집·영상·유료 API·commit/push는 수행하지 않았다.
