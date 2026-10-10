# 운영 알림 배포 준비

이 폴더는 배포 설정이다. 생성/발송 기본 OFF이며 이 작업에서 실제 Azure 리소스를 만들지 않는다.

1. `budget.json`은 월 500,000원의 **배분 한도**다. 정상 목표 300,000원이며 금액은 가격 견적이 아니다. D2as_v5 Korea Central Linux에 기존 `compose.yaml`과 `compose.override.yaml`을 합성한다. 기존 역할 flag는 활성화하지 않는다.
2. 서버의 `20261009140000_AddOperationsAlerts` migration을 기존 격리 DB에서 먼저 검사한다. 기존 root DB migration 절차를 사용하며 실행 중 임의 EnsureCreated로 대체하지 않는다.
3. `OperationsAlerts.Enabled`만 켜면 앱 내 기록을 검증할 수 있다. 운영자 ID·관리자 역할·수신 메일/SMTP·HTTPS 주소와 해당 관리자 앱 Firebase 설정이 완료된 뒤 외부 발송 flag를 켠다. 기존 일반 회원/기사의 Firebase 앱 설정을 관리자 앱에 복사하지 않는다. Android package는 `com.ssalddel.adminapp`, 설정은 외부 파일 `SSALDDEL_ADMIN_FIREBASE_GOOGLE_SERVICES_JSON`이다.
4. `monitoring.bicep`은 두 공개 health를 3개 아시아 위치에서 300초마다 관찰하고 2곳 이상 실패 시 Azure Action Group 메일을 보낸다. 웹 테스트는 앱 서버·MySQL·Mongo와 독립이다. VM CPU와 AMA workspace의 disk/heartbeat/backup을 감시한다. 같은 리소스 그룹의 VM에 AMA agent·DCR·연결 선언을 포함하며 VM Managed Identity가 필요하다. 활성화 전 수집 실패 경고/DB 중단/앱 중단/실제 메일 수신을 따로 시험한다.
5. `budget.bicep`은 구독 청구 통화의 금액을 필수로 받는다. 실제 통화를 확인하고 400,000 KRW 상당으로 환산한다. 시작일은 월 첫날이어야 한다. 80/90/100% 실제, 100% 예측 경고는 자동 서버 종료가 아니다. 지도/API 외부 비용도 별도 대조한다.
6. `backup.sh`는 Linux에서 매일 실행한다. age 공개 수신 키로 암호화한 DB dump만 별도 private Blob 계정에 올리며 Managed Identity의 Blob Data Contributor 권한을 사용한다. `backup-lifecycle.json`의 `mirror-backups`를 실제 컨테이너명으로 바꿔 일7/주28일 보존을 설정한다. lifecycle 삭제 시각은 Azure 처리 지연이 있을 수 있다. 개인 사진 저장소, Data Protection keyring/인증서 및 암호화 키는 별도 복구 자료다. 원본 비밀 키는 백업 archive와 다른 보관처에 둔다.
7. `restore-check.sh`는 내려받은 백업의 해독·checksum만 검사한다. 격리 DB import, 복호화 가능 여부, 원장 연결, 사진 조회까지 성공해야 복구 시험 완료다. 운영 DB를 자동 덮어쓰지 않는다.
8. `mirror-backup.service`/`.timer`는 매일 03:00 KST(최대 5분 분산)에 실행하는 Linux systemd 준비 파일이다. 배포 시 스크립트를 `/opt/mirror/operations-alerts`에 두고 실제 환경 파일을 `/etc/mirror/backup.env`에 비공개로 저장한다. 의존 도구·Docker/Managed Identity·개별 보호 키 백업을 검증한 뒤 units를 설치·활성화한다. 이번에는 타이머 설치·기동을 실행하지 않았다. 주간 사본은 UTC 일요일 실행 시 추가된다.

Azure Bicep compile/Compose config/로컬 암호화 검사를 실제 배포·수신·off-VM 복구 완료와 구별한다. 단일 VM은 장애 지점 하나이며 실서비스 고가용성 구성이 아니다.

`OperationsBudget`는 선택적인 신규 경로 견적의 서버 NaverDirections/GoogleRouteDistance 요청만 일일 제한한다. 이미 수행 중인 배차·배송은 중단하지 않는다. 사진 준비 요청은 집계·경고만 하며 수행 증빙 업로드를 차단하지 않는다. 네이티브 지도 SDK·다른 외부 호출·Azure 사용량 전체의 하드 상한이 아니다. quota 숫자는 가격에 연결된 월 사용량으로 보정한 뒤 활성화한다.

관리자 Android 앱과 서버 `PushNotifications`의 Firebase 프로젝트가 같아야 한다. 앱 설정 파일·서비스 계정 키·Gmail 앱 비밀번호는 각각 외부 비밀 저장소에서 제공한다. 이메일/푸시 응답 유실 시 중복 전달 가능성이 있으며 앱의 같은 notification ID는 같은 알림을 갱신한다. 서버 스캔이 오래 걸리거나 수신 기기가 오프라인이면 즉시 전달 시간은 보장하지 않는다.

자료: [Standard availability 설정](https://learn.microsoft.com/en-us/azure/azure-monitor/app/create-workspace-resource?tabs=bicep), [시험 위치와 주기](https://learn.microsoft.com/en-gb/azure/azure-monitor/app/availability), [예산 리소스 형식](https://learn.microsoft.com/en-us/azure/templates/microsoft.consumption/2024-08-01/budgets), [예산은 사용을 자동 중단하지 않음](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets).
