# 일반 사용자 공통 글쓰기와 커뮤니티 컴파일 모듈

## 변경 요약

- 일반 사용자와 운영자가 함께 쓰는 글쓰기 Component·ViewModel·browser 초안 저장을 `CommunityWritingUiModule`로 분리했다.
- 다른 호스트가 전체 업무 UI를 등록하지 않고 글쓰기만 사용할 수 있도록 `AddHongdalCommunityWritingServices<TAccessTokenProvider>()`를 공개했다.
- 글쓰기 전용 등록은 `ICommunityPostClient`만 노출하고, 참여·원장·공동구매·투표 client는 전체 커뮤니티 UI 등록에만 포함한다.
- 서버 본체에서 순수 커뮤니티 규칙, 원장 port·DTO, 공개 참여 상태를 `Hongdal.Community` 프로젝트로 옮겼다.
- 새 모듈은 `Hongdal.Contracts`만 참조하고, Mongo 원장 구현과 실제 상태 변경은 기존 서버에 유지했다.

## 화면 확인

화면 변경 없음, 간접 확인. WebApp의 기존 일반 사용자 route `/community/write`가 공통 `PlatformCommunityPostComposer`를 계속 렌더링하며, 현재 전체 공통 UI 등록도 새 글쓰기 모듈을 포함한다.

가장 가까운 실제 화면 증거는 [커뮤니티 글쓰기 저장·복구 신뢰성](2026-07-19-community-authoring-reliability.md)의 데스크톱·모바일 캡처다.

## 검증

- `Hongdal.Community` 단독 빌드
- 공통 글쓰기 DI 및 모듈 의존 경계 테스트 24개
- 이동된 커뮤니티 정책·참여 서비스 테스트 298개
- 전체 테스트 1,546개
- WebApp Debug, HongdalApp Windows Release 및 Admin Windows Release 빌드
- WebApp `/community/write` desktop 및 390×844 mobile 실제 route와 browser console 확인
