# 화물기사 웹 현재 운송의 운임과 결제 배치

390px에서 가상 운송을 열면 운임·결제/증빙·최근 갱신이 각각 한 줄을 차지했다. 좁은 화면에서는 운임과 결제/증빙을 나란히 표시하고 긴 값은 각 칸 안에서 줄바꿈한다. [현재 운송 요약 코드](../../Ssalddel.WebApp/Pages/DriverCurrentTransport/DriverCurrentTransportOverview.razor) · [이전 390px 운송 화면](../assets/changes/2026-11-06-driver-current-transport-action/on-road-390.png)

같은 가상 운송을 다시 조회한 390px 화면에서 **하차 증빙으로** 버튼이 약 71px 위로 올라왔다. 상차·하차 주소, 최근 갱신, 인수증 서명과 현장 안내를 함께 확인했다. [2026-11-07 390px 가상 운송 화면](../assets/changes/2026-11-07-driver-current-transport-metrics/on-road-390.png)

가상 운송으로 320px·390px·1440px를 비교해 수정 전 54개, 수정 후 55개 브라우저 검사를 통과했다. 기본 결제 문구에서 버튼은 320px에서 약 47px 위로 올라왔으며 1440px에서는 같은 위치였다. 공백 없이 이어지는 긴 결제 참조값은 320px에서 문서 너비를 387px까지 늘렸다. 수정 후에는 값 전체가 칸 안에서 줄바꿈돼 문서 너비가 320px에 맞았고, 이 입력에서는 버튼 위치가 약 25px 내려갔다. 상차·하차 증빙의 대상 운송과 하차 버튼의 키보드 초점을 확인했다.

Fast 관련 시험 30개와 통합 0.0 Task 전체 시험 17,051개가 통과했다. 별도 환경이 필요한 17개는 생략했다. 화물기사 전용 웹과 통합 웹 빌드에 오류가 없었고, Fast와 전용 웹 빌드는 경고 0개, 통합 빌드는 경고 7개였다.

격리 브라우저에서 실제 웹 페이지에 가상 조회 응답을 연결했다. 운영 서버·DB, 실기기, 증빙 제출은 미검증이다. 전후 캡처·측정·시험 근거는 `artifacts/local/mirror-ui-assist/20261106T161626598Z-driver-current-transport-metrics/completion.json`에 연결했다.
