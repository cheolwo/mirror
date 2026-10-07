# 일반 이용자 지도 작업공간 r10

지도에서 선택한 글·협업·공간·배송을 읽고 현재 단계를 처리하도록 일반용 앱을 정리했다. 작성·조건·주소·인증은 별도 입력 화면으로 유지하며 저장·취소 뒤 같은 동네와 선택 대상으로 복귀한다. [승인 계획](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/map-workspace.implementation.r10.md)과 [구현 전 책임 카드](../ProjectOverview/page-docs/neighborhood-map-workspace-r1.md)가 범위를 소유한다.

## 구현

`주변 보기 / 내 할 일 / 등록`을 기본 진입으로 두고 표시 설정과 출처는 펼쳐서 확인한다. 모바일은 지도 아래, 넓은 화면은 지도 옆에 선택한 대상 하나를 표시한다. Android native 지도와 HTML 패널은 겹치지 않는 영역을 사용한다. 기존 상세 직접 URL과 전문 역할 앱, API/DB·업무 상태·허용 행동·동의 관문은 유지한다.

계정별 메모리 세션, 레이어 조회 수명, 지도 장면 구성, 플랫폼 표시와 업무 패널을 분리했다. 지도 카메라·목록 페이지·구분·연결 협업을 복원하며 정확 주소·업무 응답을 영구 설정에 넣지 않는다. 독립 조회는 지역 검증 뒤 병렬 실행하고 오래된 응답과 계정 변경을 차단한다. 같은 지도에서 공개 패널만 바꾸면 타일과 핀을 다시 만들지 않는다.

앱 중단 때 개인 좌표·응답을 지우고 중단 중 개인 조회도 차단한다. Web 지도 중심이 정확 전달지에 남아 있지 않도록 공개 대표점으로 돌리며 오래된 개인 장면을 다시 받지 않는다. 재개 때 새 서버 권한 조회 후 표시하고, 조회 중 사용자가 선택을 바꾸면 이전 배송으로 돌아가지 않는다. 네이티브 지도는 키보드·대화상자·패널에 가려지면 숨긴다.

통합 Web과 독립 `01 Community`의 로그인 복원, 취소·성공의 안전한 앱 내부 복귀를 연결했다. 독립 앱의 `/roles/01/` 경로를 보존한다. 문의·비밀번호·삭제 입력은 기존 독립 상세 화면에서 처리한다. 배송 작성의 초안은 기존 설계상 화면 이탈·401 때 지워지며, 지도 문맥 복귀가 비공개 초안 보존을 뜻하지 않는다.

## 검증

집중 시험 321/321과 최종 Task `20261005-155118`의 전체 3.5 제품 빌드(오류 0, 경고 55), 전수 시험 7,297/7,297을 통과했다. 실제 화면 PNG 12장을 보관했다. 상태는 `Implemented / ValidatedLocal / PublicDeploymentPending`이다.

생산용 공통 Razor→실제 HTTP→SQLite/보호 Mongo에서 글 선택·별도 작성 후 지도 복귀, 신청→양측 동의→수행→상호 완료, 공간 목록→선택 상세와 입력 취소를 확인했다. 배송 동의 체크는 같은 대상의 지도 표시 설정 변경에도 유지된다. DOM 중단 이벤트를 전달하면 배송 주소가 화면에서 제거되고, 재개 후 새 조회와 미선택 동의 상태로 복원된다. 이 이벤트 검증은 실제 Android 앱 중단 실행과 구별한다. 320/390/1100px의 가로 넘침 없음, 지도/패널 겹침 없음, 브라우저 오류 0과 실제 Google Web 타일을 확인했다.

게시용으로 생성한 독립 01 WASM을 루프백 `/roles/01/`에서 실행하여 기본 지도 진입, 글 선택·새 글 저장 후 동네·대상 복귀, 로그인 취소→원래 입력→지도 복귀를 확인했다. 누락 자산과 브라우저 오류는 0이다. 검토 서버는 이 실행에서만 루프백의 same-origin 페이지 근거로 지도 개발 설정을 제공하며 운영 controller의 Origin 정책은 변경하지 않는다. 실제 운영 로그인 성공이나 Azure 지도 설정 검증을 뜻하지 않는다.

원본 Web/native 지도 JavaScript의 실제 Edge DOM 검증은 27항목(native 영역 11, Web 표시 16)을 통과했다. 이 검증의 Google SDK는 대역이며 실제 타일 증거는 위 Web UI 실행이 제공한다. 네이티브 타일·휴대폰은 별도다.

최신 Android Debug APK는 오류 0·경고 2로 생성했다. 서명 v2/v3, ZIP 정렬, ARM64/x86_64 assembly store·런타임과 지도 JS·공통 CSS·개인정보 전송 JS의 현재 소스 일치를 확인했다. 132,457,442바이트이며 SHA-256은 `08b2807b2b6c0a05fe5c7f41757a039e7a80400f1511b172d80b83195128056f`다. 파일은 `artifacts/local/map-workspace-r1/SsalddelApp-map-workspace-r1-debug.apk`이며 이전 r9 APK를 덮어쓰지 않았다.

원시 근거는 `artifacts/local/map-workspace-r1/`의 `ui-verification.json`, `community-web-verification.json`, `host-dom-result.json`, `apk-verification.json`, 최종 검증 로그와 소스 해시에 보관한다. 검토용 서버와 해당 루프백 Mongo는 확인 후 중단하고 데이터와 기존 서비스는 보존했다. 실제 운영 로그인·외부 배송/입금·개인 단말·Azure·Android 지도 키 설정은 이 실행으로 확인하지 않는다.

이번 변경은 코드·시험·문서·PNG를 포함한 71개 경로다. 시작 당시 다른 dirty 파일 655개의 SHA-256 변경 0을 확인했으며 `ownership-verification.json`과 `final-source-snapshot.json`에 범위를 보관한다. 기존 브랜치·HEAD는 유지하고 커밋·푸시는 수행하지 않았다.

## 화면

| 흐름 | 실제 화면 |
| --- | --- |
| 지도에서 글 선택 | [모바일 글 패널](../assets/changes/2026-10-05-map-workspace-r1/map-post-mobile.png) |
| 지도에서 조건 입력으로 이동 | [독립 협업 신청](../assets/changes/2026-10-05-map-workspace-r1/work-input-mobile.png) |
| 상태에 따른 현재 행동 | [양측 합의](../assets/changes/2026-10-05-map-workspace-r1/map-work-mobile.png), [상호 완료](../assets/changes/2026-10-05-map-workspace-r1/map-work-completed-mobile.png) |
| 공간 상세와 배송 동의 | [선택 공간](../assets/changes/2026-10-05-map-workspace-r1/map-space-mobile.png), [선택 배송](../assets/changes/2026-10-05-map-workspace-r1/map-delivery-mobile.png) |
| 화면 폭별 배치 | [320px 내 배송](../assets/changes/2026-10-05-map-workspace-r1/map-mine-320.png), [데스크톱 글 패널](../assets/changes/2026-10-05-map-workspace-r1/map-post-desktop.png) |
| 독립 01 앱 경로 | [기본 지도](../assets/changes/2026-10-05-map-workspace-r1/community-web-mobile.png), [저장 후 복귀](../assets/changes/2026-10-05-map-workspace-r1/community-web-post-saved-mobile.png) |

Figma `01 Community` 지도 화면과 `/community/map`을 대응 대상으로 기록했다. 연결된 Figma 편집 도구와 확인된 node ID가 없어 실제 코드·PNG를 근거로 보관하며 Figma 동기화 완료로 보고하지 않는다. 실제 운영 인증·개인 휴대폰·Android 지도 키/타일·Azure 공개 운영은 별도 검증이다. 커밋·푸시·외부 배포는 이번 범위에서 하지 않는다.
