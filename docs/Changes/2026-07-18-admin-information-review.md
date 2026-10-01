# 관리자 자료 검토와 커뮤니티 글쓰기

## 변경 기록

| 상태 | 변경 축 | 화면 변경 여부 | 시각 증거 |
| --- | --- | --- | --- |
| 기능 커밋 | YouTube·KAMIS 수집 후보 조회, 출처 검토, 기존 커뮤니티 글쓰기 초안 조립 | 화면 변경 | Windows Admin 앱의 데스크톱 이동 경로와 모바일 폭 로그인 경계 확인 |

## 적용 범위

- `HongdalAdminApp` 내비게이션에 `자료 검토·글쓰기`를 추가했다.
- 원천, 국가, 검토 상태와 검색어로 최근 후보를 조회한다.
- 선택한 후보의 제공기관, 기준일, 원문, 출처 설명과 해석 한계를 함께 확인한다.
- 기존 `CommunityPostComposerViewModel`을 하위 ViewModel로 조립해 공통 임시 저장·검증·게시 흐름을 재사용한다.
- 기존 초안은 자동으로 덮어쓰지 않고 유지 또는 교체 선택을 요구한다.
- 자료 조회만으로 글, 원장, 공동행동이나 관계자 알림을 만들지 않는다.

## 화면

### 모바일 폭 로그인 경계

![관리자 자료 검토와 글쓰기 모바일 화면](../assets/changes/2026-07-18-admin-information-review/admin-information-review-mobile.png)

인증된 후보 목록은 실제 서버관리자 계정이 필요한 영역이므로 캡처를 위해 인증을 우회하지 않았다. 로그인 뒤 후보 조회와 초안 변환은 ViewModel 및 service 테스트로 확인한다.

## 검증

- `HongdalAdminApp` Windows 빌드 경고·오류 없음
- 정보 후보 수집·관리자 자료 검토 Page ViewModel 집중 테스트 7개 통과
- 자동 편집·YouTube 감시·KAMIS 인접 회귀를 포함한 관련 테스트 46개 통과
- Admin 앱에서 `/information-review` 이동, 로그인 버튼의 `/login` 이동과 좁은 창 레이아웃 확인
