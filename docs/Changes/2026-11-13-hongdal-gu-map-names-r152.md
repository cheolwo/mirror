# 홍달 Windows 구 지도 이름 판독 보정 r152

- 날짜: 2026-11-13
- 기획/범위: [구 지도 정밀화 r152](../AI/Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/district-map-readability-continuation.design.r152.md) · [작업 명세](../AI/Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/district-map-readability.work-order.r152.md).
- 상태: `Implemented / IsolatedWindowsRenderVerified / NativePanZoomReturnVerified / NameSelectionDetailPending`.
- 기준: Hongdal `7c83c18cf`, 별도 Unity checkout `605e87ef4`의 미커밋 WindowsGame 모듈. 기존 실행판을 덮어쓰지 않은 로컬 검토판이다. commit/push·배포·다른 PC 검증은 하지 않았다.

## 변경과 실제 모습

글자의 실제 폭·회전 사각형을 측정해 구/동 이름·운영 표식과 패널을 보호한 뒤 큰길 이름을 놓는다. 같은 이름의 가까운 중복은 줄이고 충분히 떨어진 기존 앵커는 반복할 수 있다. 숨긴 이름에는 빈 선택 영역을 만들지 않는다. 원본 이름·좌표·경계·도로 등급·커널·경제·저장 계약은 그대로다.

처음 고정한 소스 이후 작업본에 추가된 Claude의 제자리 회전·Overflow 교정을 발견해 보존·병합했다. 첫 수정판도 원점의 음수 사각형이 IMGUI 그룹에서 잘리는 문제가 실제 렌더에서 드러나 재검증했다. 최종판은 기존 교정으로 글자를 그리며 새 측정/점유를 적용한다.

전체 범위, 면목·장안, 사가정 근접의 세 축척에서 변경 전후를 촬영했다. 여섯 원본은 작업본의 `docs/assets/changes/2026-11-13-hongdal-gu-names/`에 보관한다. [README에서 볼 수 있는 동네 디오라마 두 장](../../README.md#홍달-경영-시뮬레이션)은 같은 게임의 동네 지도 장면이다.

전/후 모두 같은 자료·축척·중심과 정지 상태를 사용했다. 최종 승인 도로명은 22/12/7곳이며 전체 장면에 동일로·망우로·사가정로·천호대로가 있다. 화면 밖이거나 패널에 가린 앵커를 강제로 화면 안으로 옮기지 않는다. 렌더 수량은 실제 자료의 도로 수가 아니다.

## 변경 파일과 검증

Unity `Assets/Ssalddel/WindowsGame/` 안의 변경이다.

- `Runtime/SagajeongDispatchGu.cs`: 점유 준비와 승인 이름·선택 영역 소비. 기존 회전 교정 유지.
- `Runtime/SagajeongDispatchGuLabels.cs`, `Runtime/구지도이름배치Policy.cs`: 실측·우선순위·회전 충돌·패널/표식 보호.
- `Checks~/GuLabelPlacementChecks.csproj`, `Checks~/Program.cs`: 회전 경계·실제 충돌과 거짓 충돌·잘못된 값·반복 배치의 독립 23개 검증. 23/23 통과.
- 모듈 `README.md`: 범위·검증·남은 항목 안내.

최종 격리 빌드는 Unity `6000.5.6f1`, 오류0·경고0·커널 검사56통과/실패0/생략0이다. 최종 제품 소스 3개와 빌드 복사본의 SHA가 같다. 검토 전용 캡처 코드는 복사 프로젝트에만 넣었고 `--sagajeong-*`의 기존 저장 차단으로 사용자 저장을 읽거나 쓰지 않았다.

전체·중간·근접 캡처 각각 Runtime 오류0이며 캡처 구간의 배달 상태·커널 상태가 전후 동일하다. 표시 프레임 간격 약16.67ms는 이 PC·정지 장면·30프레임 평균이며 최대 부하 성능 보장이 아니다. Hidden 창 캡처 실패와 글자 잘림이 남은 중간 실행은 최종 성공 증거에서 제외했다.

네이티브 입력으로 드래그 이동→휠 축소→`동네로 돌아가기`를 조작하고 실제 도로명 유지와 동네 화면 귀환을 관찰했다. 동 이름 클릭은 상세 패널 변화가 확인되지 않아 선택 완료로 세지 않았다. 구 이름/표식 선택의 세부 회귀, 확대 재진입·여러 창 크기·다른 PC 검증은 남아 있다. 공용 Goal/WI·Logic/Presentation E는 자동 승격하지 않았다.

원시 근거는 `artifacts/local/hongdal-gu-precision-20261113/`의 `baseline.json`, `source-audit.json`, `preservation-audit-spatial.json`, `build-final.log`, `player-final/build-result.json`, `player-final/kernel-checks.txt`, `b/receipt.json`, `f/receipt.json`에 있다. 최종 보존 감사는 별도 영수증을 참조한다.

## 출처·제한과 후속

기존 공간 팩의 출처·기준일·이용 조건을 유지한다. 새 수집·재굽기는 하지 않았다. ITS 원본/영수증 날짜 차이, 높이 단위·수직 기준, 브이월드 경계·차선의 배포 조건은 미해결이다. 지도 선명도 보정이 자료 정확성·배포 허가를 새로 입증하지 않는다. 역·공식 지명·서울25구 확장·영역/경제 변경은 후속이다. 새 디오라마 규칙 후보 없음.
