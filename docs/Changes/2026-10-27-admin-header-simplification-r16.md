# 관리자 상단 보조 문구 제거 r16

[사용자 요청 범위](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/admin-header-simplification.r16.md)에 따라 관리자 공통 상단의 `플랫폼 모바일 관제` 한 줄을 제거했다. `살뜰 관리자` 제목·메뉴·계정·앱 상태·기존 알림 연결은 보존했다.

변경 전 사본과 단일 삭제 차이, scoped diff를 확인했다. 소비 APK 재빌드·새 렌더/휴대폰 화면은 미검증이며, r15 APK128에는 이전 보조 문구가 남아 있다. 제품 동작이나 FCM 설정은 변경하지 않았고 새 반복 시험은 추가하지 않았다. 근거는 `artifacts/local/admin-header-r16/`에 저장했다.
