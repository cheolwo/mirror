# 일반 회원 생활 배송 완료 로컬 검증 r46

이 실행기는 실제 제품 `NeighborhoodMemberDeliveryController`·`생활협업Controller`와 UseCase, 현재 EF 모델의 MySQL 저장, 보호된 Mongo 협업 원장·운송 원장, 실제 비공개 로컬 사진 저장을 검증한다. 토큰은 검증 호스트 안에서 임시 서명 키로 만들고 실제 JWT bearer 검증을 거친다. 실제 계정 로그인·성년 인증·동의기관·도로 경로·GPS·FCM은 이 결과의 증거가 아니다.

2026-10-09 준비 시점에는 Docker Desktop/backend 프로세스는 있었으나 서비스는 중지 상태였고, Docker inventory 조회가 제한 시간을 넘겼다. SQL 13306·Mongo 27017/27028·기존 검증 서버 포트는 열려 있지 않았다. 서비스 시작 시도도 권한 문제로 완료되지 않았다. 실행기 빌드는 오류 0개·경고 0개로 완료했다. 따라서 현재 상태는 **실행기 준비·컴파일 완료 / 실제 MySQL·Mongo 종단 실행 보류**이며 아래 실행 절차의 성공을 이미 확보한 것으로 표시하지 않는다. Docker reset·강제 환경 정리는 수행하지 않는다.

공개 글·SQL 배송 의뢰는 실행기 내부 합성 표본이다. 협업 신청·정보 제공 동의·합의·시작, 회원 참여 설정·수락·픽업 사진/완료·전달 사진/완료, 협업 수령 확인은 제품 Controller HTTP를 통과한다. 생활 배송 **접수 Controller 전체**를 통과한 증거로 표시하지 않는다.

제공 글은 신청자가, 필요 글은 글 작성자가 수령하는 두 흐름을 검사한다. 처음 사진 촬영 권한과 사진 전송 후 완료 권한, 응답 손실 때 같은 요청 재전송, 다른 계정의 제안/업무 격리, 잘못된 수령자의 SQL/Mongo 무변경, 사진 바이트 hash, 수령 확인의 SQL 한 건·Mongo 완료 및 회원의 `Delivered` 상태를 대조한다. 수령 확인이 요금·결제·정산을 확정하지 않았는지도 비교한다.

Mongo 운송 원장을 독립 조회해 원 의뢰 ID·상태·배차 상태와 RDB snapshot 연결을 SQL과 대조하고 수령 후 원장 `완료`를 확인한다. 현재 제품 운송 투영에는 협업 ID·조건 판본·일반회원 수행자 필드가 없으므로 이를 새로 주입하지 않는다. 이 연결은 보호된 협업 Mongo의 배송 ID·조건 판본·신청 당사자와 SQL queue/member를 별도로 대조한다. 제품 기본 `이벤트발행커뮤니티원장저장소` 대신 직접 `Mongo커뮤니티원장저장소`를 사용하므로 Event/Outbox 후속 투영 종단 검증은 제외된다. 실제 DB 실행이 보류된 현재는 이 독립 조회도 미실행이다.

## 준비와 실행

환경 읽기 점검은 DB·컨테이너를 바꾸지 않는다. Docker 조회에는 제한 시간이 있으며 Docker 서비스·전체 환경을 재설정하지 않는다.

```powershell
pwsh -NoProfile -File eng/NeighborhoodMemberCompletionVerification/run.ps1 -Action Probe
```

구현이 안정된 뒤 별도로 빌드한다. 실행 스크립트는 빌드하지 않는다.

```powershell
dotnet build eng/NeighborhoodMemberCompletionVerification/NeighborhoodMemberCompletionVerification.csproj --nologo
pwsh -NoProfile -File eng/NeighborhoodMemberCompletionVerification/run.ps1 -Action Run
```

현재 로컬 Mongo를 사용할 수 없으면 기존 환경을 변경하지 않고 별도 표본 Mongo를 명시적으로 만들 수 있다. `mongo:8.0` 이미지가 이미 있어야 하며 이미지 다운로드는 수행하지 않는다.

```powershell
pwsh -NoProfile -File eng/NeighborhoodMemberCompletionVerification/run.ps1 -Action Run -CreateDedicatedMongo
```

MySQL은 `hongdal-mysql-1`의 기존 로컬 13306 포트를 재사용하고 새 `neighborhood_member_r46_<32자리 GUID>` DB만 만든다. Mongo도 같은 새 DB 이름만 사용한다. 별도 Mongo를 만들 때는 `hongdal-member-r46-mongo-<GUID>`·루프백 27046 포트를 사용한다. 기존 Mongo는 읽기 상태 확인 뒤 새 DB만 쓰며, 연결 환경이 따로 필요하면 `SSALDDEL_MEMBER_R46_MONGO`에 프로세스 환경으로 전달한다. 연결값·JWT는 보고서에 남기지 않는다.

`OutputRoot`는 아직 없는 `artifacts/local/` 하위 경로만 허용한다. 실행 결과는 `verification.json`, 표준 출력/오류 로그, 실제 사진과 비공개 Data Protection key ring이다. 검증 DB·전용 컨테이너는 자동 삭제하지 않는다. 실패하면 해당 실행의 표본을 보존하고 `pipeline-failure.json`/`failure.json`에 민감 정보 없는 실패 코드만 남긴다.

## 증거 경계

- `EnsureCreated`는 새 검증 DB에서 현재 EF 모델을 조립한다. 운영 migration 적용 검증과 구별한다.
- 실제 MySQL/Mongo·HTTP·JWT·사진 바이트 검증은 실제 단말 사진·지도·GPS·APK·Azure·운영 실행·금전 지급을 증명하지 않는다.
- 사진과 보호 키는 Git 제외 로컬 경로에만 저장하며 외부 업로드·알림을 보내지 않는다.
- 화면 검증은 기존 `eng/RoleWorkspacePreview`의 제품 Razor 미리보기와 별도 단계다.
- 일반 앱 APK 재생성은 기존 `eng/release/MobileFieldTest.Common.ps1`의 `shipper` 항목(`SsalddelApp`)을 이용한다. 이 실행기는 APK를 만들거나 설치하지 않는다.
