# 화주 의뢰 상세의 긴 화물명 줄바꿈

화주가 화물 종류에 입력한 긴 제품명이나 로트 번호를 의뢰 상세 제목에서 읽는다. 가상 의뢰에 공백 없는 50자 화물명을 넣으니 320px·390px에서 제목이 한 줄로 남아 오른쪽이 잘렸다. 390px 화면의 제목 오른쪽 끝은 약 661px였고 카드 오른쪽 경계는 362px였다.

의뢰 상세 요약의 제목 영역이 긴 한 단어를 카드 안에서 줄바꿈하도록 지정했다. 수정은 공용 요약의 스타일 한 곳에 적용했다. [공용 요약 스타일](../../Ssalddel.Ui.Common/Areas/App/Components/Transport/ShipperRequestSummaryScreen.razor.css)

수정 후 긴 제목은 320px·390px에서 세 줄로, 1440px에서 한 줄로 보였다. 가상 의뢰의 두 주소, 결제 예정액 142,000원과 최종 운임 138,000원, 동일 의뢰 재조회와 **돌아가기**의 목록 2페이지 복귀를 확인했다. 일반·긴 화물명에 대해 수정 전후 브라우저 검사 69개씩을 실행했다. [390px 의뢰 상세](../assets/changes/2026-11-06-shipper-detail-cargo-name-wrap/detail-390.png) · [같은 의뢰의 경로와 운임](../assets/changes/2026-11-06-shipper-detail-cargo-name-wrap/route-cost-390.png)

Fast 관련 시험 129개와 화주 앱·통합 웹을 포함한 3.5 Task 전체 시험 17,045개가 통과했다. 별도 환경이 필요한 17개는 생략했다. 빌드 오류는 0개였고 Fast 경고는 0개, 통합 빌드 경고는 62개였다.

검증 범위는 가상 의뢰를 표시하는 공용 화면과 화주 레이아웃, 가상 목록 복귀까지다. 전후 캡처와 시험 결과는 `artifacts/local/mirror-ui-assist/20261106-shipper-populated-detail/completion.json`에 연결했다.
