# 음식점 메뉴 목록의 좁은 폭 가독성

음식점 운영자가 메뉴 관리에서 판매 가격과 공개·품절 상태를 확인한다. 기존 320px 화면에서는 상단 바로가기 두 개가 두 줄에 걸쳐 첫 카드가 화면 아래로 밀렸다. 공백 없이 이어진 53자 메뉴명은 카드 오른쪽을 벗어났다.

메뉴 관리 화면에 한해 좁은 폭에서 상단 카드의 안쪽 여백을 24px에서 16px로 줄였다. 320px에서 **주문함**과 **조리시간 설정**이 한 줄에 들어왔고 첫 메뉴는 60px 위로 올라왔다. 390px에서는 16px 앞당겨졌다. 메뉴 카드의 긴 텍스트는 카드 안에서 줄바꿈되며 320px·390px의 가로 넘침이 사라졌다. [메뉴 목록 390px](../assets/changes/2026-11-06-restaurant-menu-readability/menus-390.png) · [메뉴 수정 화면](../assets/changes/2026-11-06-restaurant-menu-readability/editor-390.png)

320px·390px·1280px에서 실제 메뉴 화면과 가상 조회 응답을 연결했다. 수정 전 35개와 수정 후 37개 검사를 통과했다. 가상 메뉴 4건, 같은 항목의 수정 화면과 목록 복귀, 정상 빈 결과, 첫 조회 실패 뒤 재조회, 기존 목록이 있는 상태의 조회 실패를 확인했다. 긴 메뉴명은 별도 비교 입력으로 확인했다. [수정 전 320px](../assets/changes/2026-11-06-restaurant-menu-readability/before-320.png) · [수정 후 320px](../assets/changes/2026-11-06-restaurant-menu-readability/after-320.png)

음식점 앱 Fast 관련 시험 78건과 통합 3.5 Task 전체 시험 17,045건이 통과했다. 환경 조건이 필요한 17건은 생략했다. 빌드 오류는 0개였다. Fast 경고 20개, Task 경고 62개를 확인했다. 실기기, 운영 서버·DB, 메뉴 저장은 미실행이다. 판본과 실행 근거는 artifacts/local/mirror-ui-assist/20261106-restaurant-menu-readability/completion.json에 연결했다.
