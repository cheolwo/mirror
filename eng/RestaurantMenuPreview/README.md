# 음식점 메뉴 화면 로컬 미리보기

실제 `RestaurantDeskApp/Components/Pages/Menus.razor`를 링크하여 실행한다. 복제 HTML이나 이미지 시안이 아니다. 기본값은 합성 메뉴·메모리 전용 Client이며 실제 인증·API·DB를 검증하지 않는다. 명시적인 `--api` 선택에서만 아래의 전용 격리 서버에 연결한다. 두 방식 모두 실제 영업·알림·MAUI WebView·장치 검증과 다르다.

## 기본 합성 화면

```powershell
dotnet run --project eng/RestaurantMenuPreview --no-launch-profile
```

현재 컴퓨터의 `http://127.0.0.1:5387/`에서 연다. 430px 이하 콘텐츠 폭으로 메뉴 목록·입력·등록·수정을 검토한다. 다른 장치로 공개하지 않는다. 새 Circuit이나 서버 종료 후 합성 편집 내용은 보존하지 않는다. 주문함·조리시간 링크는 제품 앱 경로이며 이 메뉴 전용 미리보기에는 그 화면을 호스팅하지 않는다.

## 격리 API 연결 화면

기존 `food-observer`의 **Development + Simulation** 전용 컨테이너/DB와 health 준비 상태를 운영자가 먼저 확인한다. 이 도구는 서버를 준비하거나 배달 시나리오 Start, migration, seed, 컨테이너 변경을 수행하지 않는다. `verification/health`의204는 시험 준비 신호이지 임의 서버의 운영 안전성을 인증하는 기능이 아니다. 기존 Compose label과 실행 경계 확인을 대신하지 않는다.

현재 프로세스에 `FOOD_OBSERVER_BASE_URL`과 `FOOD_OBSERVER_ACCOUNT_PASSWORD`가 준비되어야 한다. 비밀번호는 명령행·URL·화면·소스·로그에 쓰지 않고 기존 저장소 밖 검증 환경에서 주입한다. 계정은 `food-observer-restaurant`로 고정한다. 주소는 `http://127.0.0.1:<전용 포트>/`, `localhost`, IPv6 loopback 원점만 허용한다. 외부 host, path/query/fragment, 자격 증명,80/443 및 미리보기 자체5387 포트는 거부한다.

```powershell
dotnet run --project eng/RestaurantMenuPreview --no-launch-profile -- --api
```

- 화면에 `격리 API 연결 / Development + Simulation / 시험 DB 저장`을 표시한다. 기본 합성 화면과 실제 서버 연결 결과를 구분한다.
- 실제 `RestaurantAuthService`와 `Ssalddel음식주문Client`로 인증·메뉴 GET/POST/PUT만 사용한다. 메뉴 저장은 전용 시험 DB에 남으며 이 도구 종료가 DB 삭제는 아니다. 운영 자료나 실제 이름·주소·사진을 입력하지 않는다.
- health 준비 실패·인증 실패·HTTP 실패는 그대로 실패이며 합성 Client로 대체하지 않는다. HTTP redirect·proxy·cookie를 비활성으로 둔다.
- Circuit별 세션/토큰은 서버 메모리에만 있다. 브라우저 연결 단절 시 진행 요청을 취소하고 토큰을 비우며, 재연결 후 다음 조회 때 health와 로그인을 다시 수행한다. Circuit 종료/DI scope 폐기도 토큰을 지운다. 해당 scope의 종료는 별도 제품 로그아웃 구현이나 메모리의 암호학적 영점화 보증이 아니다.
- 요청 중 단절은 서버 저장 취소를 보장하지 않는다. 같은 메뉴 목록을 다시 조회하고 제품 화면의 결과 미확인 복구를 따른다. 파일·PlayerPrefs·브라우저 저장소·쿠키에 인증 정보를 기록하지 않는다.

## 자동 검사

```powershell
dotnet run --project eng/RestaurantMenuPreview --no-launch-profile -- --verify
```

11개 검사는 실제 Razor 처리 메서드를 인메모리 Client로 실행한다. 실제 초기화의 계정·초안 결속과 기존10개인 최초 조회, 빈 이름/HTTP 사진 주소 차단, 등록 응답 유실·같은 요청 재시도, 명시적 저장 거절 후 잠금 해제, 저장 성공 뒤 조회 실패 구분이다. 추가 API 연결 검사는 모의 HttpMessageHandler만 사용해 주소 거부, 명시 선택/환경 결손, 제품 인증/메뉴 Client 연결, Circuit별 메모리 독립, 단절·폐기 시 소거/요청 취소, 재인증, 실패 시 fallback 없음과409 전달을 확인한다. 실제 서버·DB·브라우저를 실행하지 않는다.

브라우저 클릭 시험이나 운영 DB 멱등성 증거로 해석하지 않는다. 서버는 요청 ID 존재를 검사하지만 현재 등록 중복 판정은 음식점·메뉴명·동일 내용에 기반한다. 실제 API/DB·390px 브라우저·APK/Android는 같은 기준선의 별도 실행 증거로 남긴다.
