# 운송 자격·플랫폼 등록 준비 화면 책임 r1

2026-10-19. [페이지 원칙](../../Architecture/WholeRoadmapPagePrinciple.md)·[시각 기준](../../Architecture/RoleAppVisualDesignStandard.md)을 적용한다. 자료 검토는 법정 적격 판정·기관 등록·영업 활성화와 별개다.

## 기사 본인: 운송 자격 확인

- 사용자·질문·결과: 기사가 본인 운송수단·실제 차량의 자료 검토 상태를 확인하고 보완 자료 검토를 요청한다.
- 진입·복귀: 통합앱·Web의 화물기사 지도 내 정보 → `/workspace/cargo-driver/qualification` → 같은 기사 지도. 전문 기사 앱 메뉴 → `/driver/qualification` → 기존 메뉴.
- 같은 Web 페이지를 소비하는 전문 기사 Web은 기존 기본 레이아웃·인증 세션을 재사용한다. 복귀는 해당 앱 BaseUri 안의 `driver/me`, 로그인은 기존 역할 셸과 같은 `/roles/01/login`이다. 통합 Web은 해당 앱의 기사 workspace와 workspace 로그인으로 연결한다. 전문 Web에 없는 통합 workspace 로그인·레이아웃 의존성을 주입하지 않는다. 공통 로그인 후 전문 기사 앱으로 자동 복귀한다고 표시하지 않는다.
- 기본 정보: 내부 검토 상태·현재 신규 업무 가능 사유·운송수단·프로필 차량·비공개 실제차량·운송사업자 참조, 수단별 자료 참조·유효기간. 원본 주민번호·면허번호·문서 사진 업로드는 수집하지 않는다. 별도 비공개 자료관리에서 발급된 참조가 선행 조건이며, 이 화면이 서류 보관·진위 확인을 대신하지 않는다.
- 주 행동: 검토 요청. 관리자 승인·정산·운행은 별도 업무다. 정지 상태는 본인 재제출 대신 관리자 안내를 표시한다.
- 소유권: 공통 `TransportQualificationPanel` → `운송자격ViewModel` → 기존 인증 API → `api/v1/drivers/me/transport-qualification`. 본인 ID를 본문·URL로 임의 지정하지 않는다.
- 실패·복귀: 중복 누름 차단, 전송 결과 미확인 시 재POST 없이 최신 GET 확인, CAS 판본 역행 차단, 로그인 변경 시 초안·근거·정본 폐기. 서버 거절을 실제 자격 유효나 빈 자료로 표시하지 않는다.

## 관리자: 기사 운송 자격 검토

- 사용자·질문·결과: 기존 서버관리자가 선택 기사의 비공개 자료와 차량·보장 범위를 검토하고 내부 유효·보완·정지를 기록한다.
- 진입·복귀: 기존 `/drivers` 기사 목록 → `/drivers/{driverId}/qualification` → 기사 관리. 대형 최상위 메뉴를 늘리지 않는다.
- 기본 정보: 제출 상태·실차 참조·항목별 근거·기간·검토 확인·사유·재확인 예정일. 자체 자료 검토와 공식기관 조회를 문구로 구분한다.
- 주 행동: 검토 중 자료의 내부 유효 기록. 보완 요청·정지는 보조 판단이며 서버 권한·자기 승인 차단·상태 전이가 최종 결정한다.
- 검증 입력: 전체 자료 확인·실제 차량 재대조·검토 사유·미래 재확인일, 보험의 유효기한. 일률적인 임의 만료일은 만들지 않는다.
- 원장·인계: `api/v1/admin/drivers/{driverId}/transport-qualification`와 `/review`, 같은 자격 원장 최신 재조회. 신규 운행·추천·수락 판단은 별도 공통 서버 관문이 소유한다.

## 관리자: 플랫폼 등록 준비 조회

- 사용자·질문·결과: 서버관리자가 기관 등록·사업계획·사무실·약관·요금·보안 등 준비 항목과 미확인을 파악한다.
- 진입·복귀: 기존 기사 관리의 보조 링크 → `/drivers/platform-registration-readiness` → 기사 관리.
- 읽기 전용: `api/v1/admin/operations/freight-platform-readiness`. 준비 근거·기관 접수 근거 여부와 다음 확인만 표시하며 원본 경로·비밀값은 표시하지 않는다.
- 주 행동: 준비 현황 다시 확인. 등록 신청·운송주선 허가·운영 활성화 기능은 제공하지 않는다. 미확인은 성공이나 미필요로 바꾸지 않는다.

### 2026-10-26 정액 정보추천 준비 r4

같은 관리자가 **현재 적용 설정과 향후 사업 모델을 혼동하지 않고 남은 확인을 판단하는 것**이 이번 보완의 단일 책임이다. 기존 읽기 API·권한·주 행동·복귀를 유지한다. 현재 무료·0원 설정과 별도 카드의 월 5천원·1만원 후보, 확정 요금 없음, 건별 중개수수료 없음, 기사 수락·거절과 계약 당사자 최종 결정 방향을 기본 표시한다. 요금 선택·입금·계약 확정·배차 실행 버튼은 추가하지 않는다.

`PlannedInformationSubscription`이 없는 이전 서버 응답에는 후보 요금을 임의로 표시하지 않는다. 새 `information-subscription-classification` 항목은 준비·접수만으로 충족하지 않고 검토 유효성과 기관 회신 참조를 구분한다. 분류 회신이 확인돼도 플랫폼 등록·주선 허가·요금/약관 신고나 영업 허용을 대신하지 않는다. 긴 사업 기준과 서면 질문은 [r4 기준](../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-information-subscription.direction.r4.md)을 참조한다.

이번 보완의 새 시험·실제 렌더·소비 빌드 범위는 [r4 변경 기록](../../Changes/2026-10-26-cargo-information-subscription-r4.md)에 따르며 아래 r71 결과를 새 판본의 검증으로 승계하지 않는다. Figma 대응·실제 회원/휴대폰·기관 회신·정액 청구는 별도다.

## 검증 범위

집중 VM·실제 Razor 렌더 시험과 `/qualification-readiness-preview/{driver,admin,platform,error}` 소비 호스트를 준비했다. 실제 공통 화면을 `http://127.0.0.1:5596`에서 메모리 예시로 렌더해 **320/390/1280px의 12개 장면**을 확인했다. 운송수단 변경 시 자료 5→8→5개, 오류 안내·재확인, 가로 넘침 없음, 브라우저 오류 0개·외부/업무 API 요청 0건을 확인했다. 대표 네 화면을 직접 검토해 필드·문구·버튼의 겹침이 없고 기관 확인 미완료와 자체 자료 검토 경계가 표시됨을 확인했다.

- 최종 소비 DLL로 재실행한 원시 결과: `artifacts/local/freight-registration-ui-r71-final-20261019-123648/result.json`. 이전 성공·실패 원시는 보존한다.
- [대표 화면·사본 해시·소비 DLL 지문](../../assets/changes/freight-registration-r71/validation-summary.json).
- [기사 본인 390px](../../assets/changes/freight-registration-r71/driver-390.png), [관리자 검토 1280px](../../assets/changes/freight-registration-r71/admin-1280.png), [플랫폼 준비 1280px](../../assets/changes/freight-registration-r71/platform-1280.png), [조회 오류 390px](../../assets/changes/freight-registration-r71/error-390.png).

동일 최종 DLL에서 기존 공통 화주 화물 입력 화면도 **390/1280px의 2개 장면·4개 검사**를 확인했다. 포장·깔판 포함 총중량과 포장 길이·너비·높이의 누락 안내, 입력 후 준비 상태, 중량 0일 때 보완 안내와 양수 복구, 입력 유지·가로 넘침 없음·브라우저 오류 0개·외부/업무 API 요청 0건을 확인했다. [화주 모바일 입력](../../assets/changes/freight-registration-r71/cargo-input-390.png)·[화주 데스크톱 입력](../../assets/changes/freight-registration-r71/cargo-input-1280.png)을 직접 검토했다. 모바일 대표는 고정 셸의 본문 스크롤을 총중량 시작 위치에 맞춘 화면이며 제품 레이아웃·스타일을 바꾸지 않았다. 원시는 `artifacts/local/cargo-registration-fields-r71-r2-20261019-123855/result.json`이다.

첫 실행의 입력 탐색 실패는 `select`의 접근 가능한 이름에 옵션 글자도 포함된 시험 선택자 문제였다. 실제 Razor의 선택 입력을 가리키도록 검증 스크립트를 수정했으며 제품 소스는 변경하지 않았다. 이전 실패 폴더와 자료는 보존했다. 미리보기는 운영 HTTP·DB·공식기관 확인·실제 서류·APK 기기·외부 푸시 검증이 아니다. 빌드·집중 시험 결과는 이번 작업 통합 변경 기록에서 별도로 남긴다.
