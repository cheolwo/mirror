# 생활 배송 간편 작성 페이지 책임 r29

생활 이용자가 배송할 물품과 장소·시간을 정리하고 기존 서버 견적을 확인해 한 건의 배송 의뢰를 접수한다. [승인 r29](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/simple-input.implementation.r29.md)와 [페이지 단일 책임](../../Architecture/WholeRoadmapPagePrinciple.md)을 따른다.

기존 `/community/exchange/deliveries/new`를 재사용한다. 같은 배송의 직접 작성과 엑셀 선택행은 한 페이지의 입력 방식이며 별도 원장이 아니다. 기본은 물품·장소·시간·확인 순서이고 중량·담당자 연락처 같은 필수 보완은 해당 단계에서 묻는다. 포장 크기·상세 요청은 보조 정보다. 기존 상세 작성으로 돌아가도 같은 초안을 유지한다.

각 단계의 주 행동은 다음 단계 또는 입력 확인이다. 마지막에는 기존 서버 견적 확인과 현재 금액·개인정보 제공·접수 동의를 거친다. 엑셀 미리보기·선택은 배송 접수 동작이 아니다. 잘못된 행·누락·로그인/동의 실패·업로드 오류·접수 불명확은 별도로 표시하고 성공으로 숨기지 않는다.

픽업 시간창은 필수이며 시간 단계에서 사용자가 검토한다. 전달 시간창은 선택이고 `전달 시간 지정 안 함 · 선택`을 선택하면 null로 보낸다. 불완전한 시간창은 자동으로 현재 시각으로 바꾸지 않는다. 최종 확인에서도 앞 단계로 돌아가 수정할 수 있으며 현재 견적·동의는 입력 변경 즉시 무효화한다.

엑셀은 여러 행의 요약을 보여주고 한 행을 작성 초안에 적용한다. 동일 session에서는 행의 요청 번호를 유지한다. 이미 제출했거나 결과 확인 중인 요청은 교체하지 않는다. 로그아웃/소유자 변경/화면 종료에서 초안·원 파일·미리보기와 늦은 응답을 폐기한다. 로컬 파일 저장·외부 AI·음성·일괄 자동 접수는 이번 페이지 책임이 아니다.

통합 Web/MAUI의 공통 컴포넌트를 소비하며 전문 앱과 기존 기능을 보존한다. 실제 모바일 폭 렌더와 소비 앱 build는 물리 휴대폰·인증 HTTP·실DB·실업체·지급 증거와 구분한다. 결과는 [변경 기록](../../Changes/2026-10-07-neighborhood-simple-input-r29.md)에 연결한다.

## 실제 공통 UI 검토 샘플

합성 계정·주소·시간을 사용한 검토 호스트 화면이며 확정 시안·실제 배송 접수 증거가 아니다. 제품 엑셀 파서와 Razor/VM은 재사용하지만 동의·견적·등록은 메모리 대역이다.

- [물품 입력 390px](../../assets/changes/2026-10-07-neighborhood-simple-input-r29/input-goods-390.png)
- [장소 입력 390px](../../assets/changes/2026-10-07-neighborhood-simple-input-r29/input-places-390.png)
- [시간 입력 390px](../../assets/changes/2026-10-07-neighborhood-simple-input-r29/input-time-final-390.png)
- [엑셀 행 선택 320px](../../assets/changes/2026-10-07-neighborhood-simple-input-r29/import-rows-320.png)
- [선택행 최종 확인 390px](../../assets/changes/2026-10-07-neighborhood-simple-input-r29/import-confirm-390.png)
