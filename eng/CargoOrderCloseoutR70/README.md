# 공통 기사 앱으로 퀵 한 건 수락 확인 r70

승인된 [r70 작업](../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/order-closeout.design.r70.md)의 로컬 도구다. 기존 공통 `CargoOfferActionViewModel`이 추천과 보수를 재조회하고 실제 수락 요청을 보내도록 사용한다. 별도 수락 규칙이나 제품 화면을 복제하지 않는다.

```powershell
dotnet run --project eng/CargoOrderCloseoutR70 -- --input <r70 비공개 입력 JSON 절대경로>
```

입력 파일은 `artifacts/local/cargo-order-closeout-r70/.private/` 아래에 둔다. 필드는 `baseAddress`(`http://127.0.0.1:5364`), `accessToken`, `driverId`(기존 두 가상 기사 중 한 명), `requestId`(GUID), `outputRoot`, `mode`(`inspect` 또는 `accept`), 선택적인 `transportId`·`stage`다. 토큰은 출력·결과·명령 기록에 보존하지 않는다. 출력은 `artifacts/local/cargo-order-closeout-r70/client/`의 새 단계 폴더로 제한한다.

- `inspect`: 선택 추천/의뢰/작업 공간/운송의 GET만 허용한다. 확인 체크의 메모리 상태는 검사할 수 있지만 수락·위치 전송·사진 업로드·운송 완료를 실행하지 않는다.
- `accept`: 실제 ViewModel의 조회 → 확인 전 차단 → 조건 확인 → 수락 확인 → `SubmitAsync` → 저장된 운송 재조회를 검사한다. 선택한 의뢰의 `/accept` POST만 한 번 허용한다.
- POST 전 `accept-command.json`을 `Pending`과 실제 전송 payload 해시로 디스크에 기록한다. 같은 의뢰의 전역 비공개 guard도 배타적으로 생성하여 출력 폴더를 바꾼 재전송을 막는다. 이전 기록은 성공·거절·미확정 상태와 관계없이 자동 재전송하지 않는다. 응답 유실은 `Unknown`, HTTP 200은 `Confirmed`로 남기며 ViewModel 저장과 동일 운송 확인은 별도 검사다.
- 수락 이후 단계는 root의 기존 API 도구가 진행한다. 이 도구는 상태별 실제 공통 Razor 정적 HTML·허용 행동·기사 보수·고객 금액·소스/DLL 해시를 남긴다. 결과의 최상위 `passed`, `saved`, `requestId`, `transportId`로 후속 인계를 확인한다.

정적 HTML은 브라우저 클릭·물리 휴대폰·네이티브 추천 배너·알림음·GPS·실카메라·운영 배차·입금 완료를 증명하지 않는다. 지도 위치 전송을 수행하는 운영 adapter 대신 현재 GET 결과를 제품의 순수 표시 함수로 변환한 카드만 읽기 adapter에 공급한다. 생성 HTML의 테마는 실제 기본 `MudThemeProvider`를 렌더하지만 공통 CSS 연결과 화면 캡처는 별도 검증이다. 기존 r114 자료와 제품 코드는 보존한다.
