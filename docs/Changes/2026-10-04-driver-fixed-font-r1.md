# 기사 화면의 고정 글자 크기

2026-10-04. 사용자는 자동 확대보다 정해진 글자 크기로 표시하기를 요청했다. [시각 기준 r7](../Architecture/RoleAppVisualDesignStandard.md)과 [페이지 책임 r7](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#고정-글자-크기-r7--사용자-후속-기준)에 따라 FDriverApp의 Driver 제목·본문·버튼·입력 기반 스타일에서 자동 글자 확대를 끈다. 기존 페이지20·섹션/본문/입력16·보조/버튼14·금액18 크기는 유지한다. 중단 사유/메모의 본문16과 앱의 네이티브 지도 안내 DIP14도 명시한다.

제품 변경은 `FDriverApp/Resources/Styles/AppStyles.xaml`, `FDriverApp/Pages/MainPage.xaml`, `FDriverApp/Handlers/FDriverNativeMapViewHandler.Android.cs`다. 업무 상태/행동·카드 표면과 정렬·접기·전체 주소 줄바꿈·필요한 본문 스크롤·금액·API/DB는 유지한다. Android/iOS 화면 전체나 제삼자 지도 SDK의 모든 글자를 강제로 같은 크기로 바꾼다는 뜻은 아니다.

진행 상태를 이어 쓰는 FormattedString의 Span3개에도 False를 명시했다. 현재 MAUI10.0.20의 [FontExtensions](https://github.com/dotnet/maui/blob/10.0.20/src/Controls/src/Core/FontExtensions.cs#L26-L32)는 크기의 기본값과 Span의 확대 설정을 별도로 사용하므로 Label에만 False를 설정하는 것으로 끝내지 않는다.

[MAUI 공식 글자 설정](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/fonts?view=net-maui-10.0)의 `FontAutoScalingEnabled=False`를 사용한다. 화면 밀도에 따른 DIP 환산은 유지하며 OS 글자 확대만 해당 컨트롤에 반영하지 않는다. 이전 r6의 확대 보존 및 실제 캡처 기록은 당시 판본의 이력으로 보존한다. 이번 결과와 구분한다.

## 기본 화면과 검증

최종 설치본의 [픽업 카드](../assets/changes/2026-10-04-driver-fixed-font-r1/pickup.png), [접힌 카드](../assets/changes/2026-10-04-driver-fixed-font-r1/pickup-collapsed.png), [전달 카드](../assets/changes/2026-10-04-driver-fixed-font-r1/delivery.png)를 기본1080×2400·density420·글자1.0에서 확인했다. PNG는 편집하지 않았고 원시 캡처와 문서 사본의 SHA-256이 일치한다. 카드 내부 정렬·전체 주소 줄바꿈·고정 버튼·진행 요약 문구가 유지된다. 이번 확인에서는 별도 글자200% 화면이나 확대 스크롤 검증을 다시 하지 않았다.

- 최종 Fast `20261004-111046`: FDriverApp 빌드·관련58/58 통과. 새 시험·시험 수정 없음.
- 최종 Task `20261004-111220`: 전체3.5 빌드 통과·6,326/6,333 통과·기존7실패. 기준 `20261004-104520`과 실패 이름·오류·전체 스택이 동일하고 모든 결과 차이는0이다. 전체 게이트는 미통과다. 기존 API 한국어 prefix/역할 metadata, HIOPS 문구, 재료 카드 높이, beta route capability 실패는 원시 `task-baseline-comparison.json`에 시험 이름과 첫 원인·스택을 보존했다.
- 최종 보관/설치 base APK SHA-256: `0D8FD18617593E76DD3BA15EC9A546DBD8A89DE9668FEEDC0515CCEE11B75756`. 3개 소스 지문은 최종 빌드/설치 확인 뒤에도 같다. 로컬 HTTP에서 실제 UI의 수락→도착→픽업→전달 완료·대기 복귀와 접기를 확인했다. 실제 DB·실배달·입금 근거가 아니다.
- 기존 네이버 지도 SDK 미설정 안내가 유지됐다. 실제 지도 타일·도로선·핀·물리 단말은 미검증이다. Google 지도 SDK를 설치하거나 실제 API를 호출하지 않았다.
- 기기 화면/글자 설정을 바꾸지 않았고 전용 HTTP/에뮬레이터를 종료했다. 기존 서버/DB·다른 변경·영상/MYBOX는 보존했으며 커밋·푸시는 수행하지 않았다.

원시 자료는 Git 제외 `artifacts/local/driver-fixed-font-r1/`다. Google 지도는 [조사서](../ProjectOverview/driver-google-maps-review-r1.md)에서 확인하며 이 변경으로 지도 공급자를 바꾸지 않았다.
