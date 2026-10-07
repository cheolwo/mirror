# 생활 협업·작은 보관 공간 r9

사용자가 승인한 네 항목을 기존 생활 지도와 교류 화면에 연결한다. [승인 계획](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/life-collaboration.implementation.r9.md)과 [페이지 책임 카드](../ProjectOverview/page-docs/neighborhood-collaboration-r1.md)가 범위를 소유한다.

## 사용자 흐름

- 지도에서 제공·필요 글 선택 → 독립 신청 화면 → 양측 조건 동의 → 수행 → 한쪽 완료 제안과 상대 확인.
- 하나의 계정으로 신청·제공·참여. 실제 관계에 맞춰 ‘내가 신청한 일’과 ‘받은 요청·맡은 일’ 목록을 제공한다.
- 작은 보관 능력의 물품·수량·시간 등록. 시간별 수량 예약, 양측 인수·반환과 제공 중단을 별도로 기록한다.
- 개인 이력은 당사자만 조회하며, 완료 후 모든 관련자가 별도로 동의한 종류·동네·날짜만 공개한다. 입금·지급 인증이나 신뢰 점수는 만들지 않는다.

## 서버·개인정보 경계

협업은 `neighborhood_collaborations_private` 보호 Mongo 문서, 보관은 `neighborhood_storage_spaces`의 암호화된 인계 정보와 수량 예약이 원본이다. SQL 공개 글은 출처·작성자·조건 판본 확인에 사용한다. 공개 지도 위치는 검증한 동네 대표점이며 정확 주소·전화·출입 안내는 본인과 현재 제공 안내 판본에 양측 동의한 상대방에게만 제공한다. 조건 변경·종결·계정 변경 때 동의와 표시 범위를 다시 확인한다.

보관 예약 intent를 협업 문서 CAS에 먼저 기록하여 취소·조건 변경과 경쟁하지 않게 한다. 공간 저장 후 확정하며, 결과를 모르면 원래 요청 번호로 조회한다. 미저장 예약의 복구는 공간 판본에 중단 증적을 저장하여 늦은 쓰기를 차단한 뒤 예약권을 해제한다. 양측 반환 전에는 보관 수량을 다시 사용하지 않는다.

배송 접수와 협업 연결의 부분 성공은 같은 운송 ID를 보존하고 연결만 다시 확인한다. 신규 배송의 출처를 서버 메모에 보존하며 구형 의뢰의 출처는 역추정하지 않는다. 실제 운송 원장의 인수 완료와 보관 원장의 양측 반환이 관련 협업 완료의 조건이다. 음식 조리·전문 창고·기사 권한·배차 운영 관문은 기존 업무가 소유한다.

## 검증 상태

집중 시험 308/308과 Task `20261005-140852`의 전체 3.5 제품 빌드(오류 0, 경고 61), 전수 시험 7,256/7,256을 통과했다. 공개 글 출처와 실제 SQLite/Mongo 저장을 사용하는 루프백 HTTP에서 신청·양측 동의·진행·상대 완료 확인·전원 공개 동의, 보관 제공·예약 재전송·양측 인수/반환·제공 중단 후 반환·완료 후 비공개 조회 차단을 확인했다. 실제 저장 BSON에서 협업 본문과 인계 정보를 보호한 것도 확인했다. 기존 인증 Mongo 컨테이너는 변경하지 않았다.

같은 공통 Razor 화면에서 계정을 전환하며 신청→합의→완료, 개인정보를 암호화해 전송한 공간 등록→비로그인 공개 상세, 동의한 완료 기록과 실제 Web 지도 타일/동네별 보관 집계를 확인했다. 320/390/1100px, 가로 넘침 없음, 브라우저 오류 0, 화면 9장을 확인했다. 지도 메뉴는 좁은 화면에서 단어를 유지한 채 줄을 바꾼다. UI 검증 중 발견한 개인정보 전송 JS의 잘못된 자산 위치와 실제 Mongo 예약 GUID 직렬화를 수정했으며 보호 전송을 생략하지 않았다.

최신 코드·공통 화면·런타임을 내장한 Android Debug APK도 생성했다(오류 0, 경고 2). 서명 v2/v3, ZIP 정렬, ARM64/x86_64 assembly store와 개인정보 전송 자산, 사본 SHA-256을 확인했다. 132,396,002바이트, SHA-256 `12ca69860eb6b5a6bb43345b311ecef3edfb6cb6abdfe387668d30f31fc71828`이며 파일은 `artifacts/local/neighborhood-collaboration-r1/SsalddelApp-neighborhood-collaboration-r1-debug.apk`다. 이전 r8 APK와 구별한다.

로컬 검토 서버의 로그인 주체와 배송 값은 명시한 대역이다. 실제 운영 로그인·외부 경로·기사 수행·전화기 설치·Android 지도 키/타일·Azure 공개 운영·은행 입금은 이 실행으로 확인하지 않는다. 서버 키 ring과 운영 인증 설정도 실제 배포 때 별도로 검증해야 한다. 이 구현의 상태는 `Implemented / ValidatedLocal / PublicDeploymentPending`이다.

## 화면

| 화면 | 확인 자료 |
| --- | --- |
| 현재 협업과 상태별 행동 | [모바일 진행 화면](../assets/changes/2026-10-05-neighborhood-collaboration-r1/work-progress-mobile.png), [320px 완료 상세](../assets/changes/2026-10-05-neighborhood-collaboration-r1/work-detail-320.png) |
| 공개와 비공개 입력 분리 | [공간 등록](../assets/changes/2026-10-05-neighborhood-collaboration-r1/storage-create-mobile.png), [비로그인 공간 상세](../assets/changes/2026-10-05-neighborhood-collaboration-r1/storage-public-mobile.png) |
| 전원 동의한 최소 완료 기록 | [공개 완료 이력](../assets/changes/2026-10-05-neighborhood-collaboration-r1/public-history-mobile.png) |
| 동네 대표점의 공간 수와 목록 이동 | [생활 지도](../assets/changes/2026-10-05-neighborhood-collaboration-r1/storage-map-mobile.png), [데스크톱 목록](../assets/changes/2026-10-05-neighborhood-collaboration-r1/storage-list-desktop.png) |

로컬 원시 근거: `artifacts/local/neighborhood-collaboration-r1/`의 `http-verification.json`, `ui-verification.json`, `mongo-storage-verification.json`, `apk-verification.json`, `ownership-verification.json`, 경로 목록과 최종 소스 해시다. 다른 dirty 파일 605개의 SHA-256 변경 0을 확인했다. 커밋·푸시·외부 배포는 하지 않았다. 검토 서버와 이번에 만든 루프백 Mongo는 확인 후 중단하며 기존 서비스·업무 DB는 유지한다.
