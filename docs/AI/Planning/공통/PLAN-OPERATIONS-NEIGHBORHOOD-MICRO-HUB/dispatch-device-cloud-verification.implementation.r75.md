# USB 없이 주문·배차·기사 앱 검증 r75

2026-10-15. 상태 `Implemented / LocalValidated / DeviceCloudDeferred`.

사용자의 “USB는 해제해 놨으니까 할 수 있는 것들에 대해서만 진행”에 따라 [r75 검증 계획](dispatch-device-cloud-verification.plan.r75.md) 중 로컬 업무 시험·복구·알림 계약·요금 보존과 APK 준비를 실행한다. [작업 명세](dispatch-device-cloud-verification.r75.work-order.json)의 허용 경로와 기존 미커밋 기준선을 사용한다. 실제 휴대폰 소리·카메라·FCM 수신·Azure 전체 업무·실금전은 별도다.

root는 음식 기사 현재 업무 요금 근거·실행 범위 보존, 필요한 상태 안내 UI, 통합 빌드/시험·문서를 맡는다. driver_accept_adapters_r71은 기존 실제 엔진과 모바일 loopback 경로를 호출하는 격리 runner·시나리오, driver_preferences_ui_r71은 화물 푸시 식별자·정본 조회/클릭 복귀, driver_work_proposal은 화물 운행 시작 유실 복구·부가 조회 장애 분리를 맡는다. 기존 r74 호스트의 보호 조건을 완화하지 않는다.

보완 기준은 서버 정본·요금 동결·기존 전문 상태/사진/수령 관문·계정/판본 경계다. 실패를 0원·빈 목록·시험 성공으로 바꾸지 않는다. 로컬 음성 파일을 준비하더라도 APK 포함·OS 게시 요청·실제 청취 성공은 각각 기록한다.

## 로컬 완료 근거

전체 소비 앱 빌드 오류 0개·Task 11,865개 통과/실패 0개/환경 의존 16개 건너뜀, 집중 202개와 별도 정책/복구 실행 9개 통과. 음식·화물 APK 0.1.77/177의 같은 시험 서명·패키지 해시·현재 소비 소스 해시를 확인했다. 음식 음성 파일은 APK 내부 해시를 대조했으며 설치·실제 소리·FCM·카메라·Azure 업무·실금전·commit/push는 미실시다. [변경·실행·한계](../../../../Changes/2026-10-15-local-dispatch-verification-r75.md)를 따른다.
