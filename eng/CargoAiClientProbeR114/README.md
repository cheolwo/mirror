# r114 화물 기사 읽기 전용 앱 표시 검증

전용 `http://127.0.0.1:5364`의 가상 기사 계정으로 기존 공통 화물 API client·추천/사진 업무 ViewModel·순수 카드 표시 함수·실제 공통 Razor를 검사한다. 이 도구는 추천 수락·위치 전송·사진 업로드·상하차 완료·알림 확인 명령을 보내지 않는다.

입력은 Git 제외 `artifacts/local/cargo-ai-device-r114/.private/`의 JSON 파일로만 전달한다. `baseAddress`, `accessToken`, `requestId`, `driverId`, `outputRoot`와 선택적 `transportId`, `stage`를 사용한다. 토큰은 출력·HTML·결과에 기록하지 않는다. 가상 기사 ID는 `cargo-journey-driver` 접두사로 제한하고 출력도 r114 산출물 하위로 제한한다. 결과가 있는 폴더는 덮어쓰지 않으므로 단계마다 새 폴더를 지정한다.

```powershell
dotnet run --project eng/CargoAiClientProbeR114 -- --input <비공개 입력 파일 절대경로>
```

결과 `result.json`에는 실제 GET 결과의 카드·허용 행동·보수 표시·ViewModel 상태·정적 렌더 오류·현재 DLL/소스 해시가 들어간다. `offer.html`, `pickup.html`, `dropoff.html`, `workspace.html` 등은 현재 제품 공통 컴포넌트의 실제 정적 HTML 렌더다. 조건에 해당하지 않는 화면은 생성하지 않는다. SSR 실패는 성공으로 바꾸지 않고 결과에 기록하며 종료 코드가 실패다. HTML은 Android 화면·실제 클릭·CSS 레이아웃·스크린샷을 증명하지 않는다.

`CargoDriverWorkspaceAdapter.LoadAsync`는 정상 앱에서 주기 위치 전송까지 담당하므로 읽기 전용 검증에서 호출하지 않는다. 대신 실제 GET 결과를 그 adapter의 현재 `Map/MapOffer`로 변환하고 제품 `RoleWorkspaceViewModel/RoleMapWorkspace`에 읽기 전용 adapter로 공급한다. 이는 서버 연결·업무 상태별 표시 검증이며 제품 adapter 전체 흐름 검증은 아니다.

실제 네이티브 추천 배너는 `DriverApp/NativeDriverHomePage.xaml(.cs)`에 있다. 여기서는 같은 `DriverOfferPresentation`의 색·명칭·업무 범위와 서버 추천 차수/만료를 확인할 수 있지만 실제 네이티브 배너·알림음·FCM·GPS·지도 SDK는 별도 기기 검증으로 남긴다. 제안 보수·운임·실제 입금도 구분한다.
