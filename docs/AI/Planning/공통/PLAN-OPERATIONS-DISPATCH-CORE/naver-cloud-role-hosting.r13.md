# 역할 앱 서버의 네이버 클라우드 배포 방향 r13

상태: DirectionSelected / MigrationPlanPrepared / DeploymentNotStarted. 조사일: 2026-10-22.

사용자는 네이버 Maps·SDK와 함께 역할 앱 서버도 네이버 클라우드에서 운영하는 방향을 선호했다. **역할 앱 API의 다음 배포 후보를 Naver Cloud 한국 VPC의 Linux 서버로 잡는다.** 현재 네이버 서버 생성·유료 신청·도메인/키 변경·데이터 이전·운영 활성화를 수행한 것은 아니다. 기존 Azure 리소스의 실제 상태도 이번에 조회하지 않았다.

## 구성과 적용 범위

```text
기사·화주·주문자·음식점·관리자 앱
├─ 네이버 Maps API / Mobile SDK
└─ HTTPS 역할 앱 API · Naver Cloud
   ├─ 기존 ASP.NET Core 서버 컨테이너
   ├─ 기존 MySQL · MongoDB 전문 원장
   ├─ 기존 실시간 연결 · 알림 Outbox / FCM
   ├─ 사진: 기존 비공개 GCS 우선 유지
   └─ 배차 판단: 기존 규칙 · 검증 후 Ollama 연계
```

이동 대상은 역할 앱의 서버 실행 환경이다. 기존 원장·업무 상태·권한을 클라우드 사업자에 맞춰 새로 만들지 않는다. Unity 검토 전용 배포, MYBOX 영상, GCS 원본/메뉴 사진과 기존 Azure 자원을 자동 이전·삭제하지 않는다. 지도 SDK와 서버 호스팅은 서로 다른 구성 요소이므로 호스팅 이전을 지도 인증/표시 성공으로 기록하지 않는다.

## 현재 코드로 재사용할 부분과 남은 일

| 항목 | 현재 확인 | NCP 준비 |
| --- | --- | --- |
| API | [.NET10 Linux Dockerfile](../../../../../Ssalddel/Dockerfile), 비루트 실행·ready 검사 있음 | NCP 대상 이미지 빌드·기동·헬스 확인. 파일 존재는 이미지 실행 증거가 아님 |
| DB·HTTPS | [기존 Azure 명칭 Compose](../../../../../deploy/azure-vm/compose.yaml)는 MySQL8.4·Mongo8·앱·Caddy 구성 | NCP 전용 배포 경로와 비밀파일/볼륨/도메인을 분리. 기존 설정을 통째로 복사해 Operational로 시작하지 않음 |
| 원장 | [DB 등록](../../../../../Ssalddel/Extensions/ServiceCollectionExtensions.Persistence.cs)의 MySQL·Mongo 필수 | 시험 DB로 먼저 접수→배차→완료를 확인. 기존 실제 DB 이전은 백업·복원·결속 대조와 별도 전환 절차 |
| 인증·보호 | 기존 JWT·인증서·Data Protection 키링·개인정보 암호화 사용 | 서버 주소만 바꾸다 기존 암호문/로그인을 읽지 못하지 않도록 application name·issuer/audience·키·인증서를 명시적으로 대조. 값을 문서/Git에 저장하지 않음 |
| 알림 | SignalR·FCM 코드 있음 | HTTPS/WebSocket·기기 토큰 등록·앱 종료/전경 수신·중복 억제를 실제 기기로 확인. NCP 이전이 Firebase 설정을 대신하지 않음 |
| 파일 | [provider](../../../../../Ssalddel/Services/Options/ObjectStorageOptions.cs)는 GoogleCloud·AzureBlob·Local | GCS 유지 가능. NCP Object Storage 전용 adapter는 현재 목록에 없어 별도 구현·호환시험 후 선택 |
| AI | [Ollama 로컬 Docker](../../../../../eng/local-dispatch-ai/docker/compose.yaml), [화물 옵션](../../../../../Ssalddel/Services/Dispatch/AI/CargoDispatchOllamaOptions.cs)은 숫자 loopback 주소만 허용 | 다른 VM IP/일반 컨테이너 DNS로 단순 치환하지 않음. 네트워크 배치·시간 제한·규칙 대체·판단 이력 검증 후 연결하고, 미연결을 AI 운영 완료로 표시하지 않음 |

초기는 단일 서버에서 기존 컨테이너들을 분리 실행하는 작은 시험 구성을 우선 검토한다. 서버가 멈추면 API·DB가 함께 영향을 받는 구성이므로 외부 백업과 복원 검증이 필요하다. 여러 API 인스턴스로 늘릴 때는 Memory 임시상태·실시간 연결·작업 단일 실행 경계를 별도로 검증한다. 관리형 DB와 GPU를 처음부터 필수 자원으로 만들지 않는다.

## 진행 순서와 완료 기준

1. **배포 설정 준비:** 기존 구성에서 클라우드 공통 부분과 Azure 전용 이미지 태그·인증/번역/모니터링 등을 대조한다. NCP용 Compose·환경변수 예제·백업/복구 안내를 새 경로에 만들되 비밀값은 포함하지 않는다. 현재 이 단계는 미구현이다.
2. **로컬에서 배포 방식 검증:** Linux 컨테이너 빌드, 기존 시험 DB를 사용하는 기동, health/HTTPS 경계·재시작·백업 복원과 미확정 요청 복구를 확인한다. 현재 제품의 r12 로컬 Razor/HTTP 검증을 이 검증으로 대신하지 않는다.
3. **NCP 시험 서버 구성:** 계정·리전·사양·도메인·비용을 실제 콘솔에서 확인해 대상 자원을 확정한다. 외부에는 필요한 HTTPS만 제공하고 DB·Ollama는 공개하지 않는다. 서버/볼륨/백업·공인 IP·트래픽 등 실제 합계 요금을 확인한 뒤 생성한다.
4. **앱 연결:** 앱 API 주소·인증 복귀·Maps의 Web 도메인/Android 패키지·FCM 설정을 각각 연결하고 시험 APK에서 통신·배차 배너·수락·픽업·전달·오류 복구를 확인한다.
5. **운영 전환:** 당사자·지원 담당·원장/재원·신고 보류 등 기존 조건을 확인한 업무만 단계적으로 켠다. 기존 feature/worker OFF와 Simulation/Operational 경계를 일괄 변경하지 않는다. 복구 계획 확인 전 이전 환경을 삭제하지 않는다.

## 비용 비교의 기준

후속 [월50만원 단일 서버 운영안 r14](naver-cloud-budget.r14.md)에 공식 요금·730h/31일·사용량/예산 배정 가정별 계산을 추가했다. 실제 계정 견적이나 자원 생성으로 승격하지 않는다.

서버와 지도는 각각의 이용 요금이 있다. 기존 월50만원 운영 예산은 상한 검토 기준으로 두되, 현재 실제 사용량·NCP 선택 사양·가용 할인/크레딧을 확인하지 않아 월 합계는 확정하지 않는다.

- 서버 구성: CPU/RAM·운영시간·디스크·백업·공인 IP·전송량을 합산한다. 무료 Micro 또는 서버 한 대 요금만으로 전체 운영 비용을 표현하지 않는다.
- 관리형 DB: MySQL HA/복제·DB 스토리지·백업 등을 별도 비교한다. 현재 자체 MySQL8.4/Mongo8와 대상 서비스 버전·연결·복원 호환을 확인한다.
- 사진: GCS 유지 시 인증 방식과 전송량을 확인한다. NCP Object Storage 이전은 복사 비용·해시/메뉴 관계·비공개 접근·만료 URL 검증을 포함한다.
- AI: 사용 모델·지연·메모리와 처리량을 먼저 측정한다. GPU 비용을 기본 서버 예산에 숨기지 않는다.
- Maps: 현재 계정의 상품·호출량·무료/할인 조건을 확인한다. 과거 AI NAVER API 상품의 요금표를 새 Maps 계약으로 자동 적용하지 않는다.

## 공식 자료

2026-10-22 확인. 실제 요금은 생성 시 콘솔에서 재확인한다.

- [NCP VPC Server 사양](https://guide.ncloud-docs.com/docs/server-spec-vpc): 한국 리전·Linux 등 서버 선택 및 세대/자원 제약 확인.
- [NCP 상품별 요금](https://www.ncloud.com/charge/region/ko): 환경·세대·사양별 요금표. 현재 구성의 최종 견적을 산출한 것은 아님.
- [Cloud DB for MySQL](https://guide.ncloud-docs.com/docs/clouddbformysql-spec): VPC/Classic·HA·종량 요금 경계.
- [Maps 개요](https://guide.ncloud-docs.com/docs/maps-overview), [지원·요금](https://guide.ncloud-docs.com/docs/maps-spec): 웹 API·모바일 SDK·REST API와 별도 호출 과금.
- [Object Storage 개요](https://guide.ncloud-docs.com/docs/storage-storage-6-1), [S3 호환 API](https://api.ncloud-docs.com/docs/storage-objectstorage): 기존 GCS endpoint를 바꾸는 것으로 연결되지 않으며 지원 오퍼레이션·인증을 대조해야 함.

이번 결과는 코드·배포 파일과 공식 자료의 읽기 전용 조사 및 방향 기록이다. Docker 재빌드·클라우드 접속/생성·실제 배포·이전·APK 변경·과금·commit/push는 실행하지 않았다.
