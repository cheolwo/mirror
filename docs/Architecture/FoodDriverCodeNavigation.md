# 음식 기사 코드 탐색

음식 기사 업무를 수정할 때 [작업 영역](../../eng/work-areas/food-driver.json)의 `readFirst`와 [생성 지도](../AI/generated/food-driver-code-map.md)에서 시작한다. 기능 키는 `food-driver-work`다. 기존 [메타데이터 기준](SsalddelCodeMetadata.md)을 그대로 사용한다.

```powershell
dotnet run --project eng/Ssalddel.CodeMap -- --work-area food-driver --feature food-driver-work
dotnet run --project eng/Ssalddel.CodeMap -- --work-area food-driver --write
dotnet run --project eng/Ssalddel.CodeMap -- --work-area food-driver --check
```

옵션을 생략하면 기존 Simulation·Unity 프로필을 사용한다. 생성 JSON/Markdown은 직접 수정하지 않는다. 작업 코드의 attribute 또는 manifest를 고친 뒤 생성하고 검사한다.

`StepKey`는 안정된 탐색 식별자, `DependsOnStepKeys`는 코드 책임 사이의 참조, `FlowOrder`는 읽기 순서다. 런타임 호출 추적이나 업무 완료 증거가 아니다. `ContractType`은 응답·입력 계약을 가리킨다. `ReadsFrom/WritesTo`, `Effects`, `Boundary`를 함께 읽어 순수 계산·저장 정의·실제 쓰기·위임 효과를 구별한다.

현재 업무와 정산은 별도 단계다. SQL은 운행·음식 수행 상태와 후속 예약을, Mongo는 원주문 관계를, Redis는 위치·추천 투영을 담당한다. 각 단계에 적힌 실제 책임을 우선하며 저장소 전체가 같은 권위를 갖는다고 추정하지 않는다. 거절의 두 단계 저장과 종료 인덱스 정리 실패의 기존 한계는 해당 경계에 남긴다.

공통 셸은 `IRoleWorkspaceAdapter`·`IRoleWorkspaceRecovery` 포트에 의존한다. 음식 기사 Adapter의 일시 요청 보존은 앱 재시작 뒤의 암호화 복구 저장이 아니다. native 코드는 Android assembly를 불러오지 않고 별도 `sourceBindings`로 연결한다. XAML과 코드부, 각 partial 파일·심볼을 명시하며 줄번호는 생성 시 확인한다. 이 결속은 reflection 단계나 실제 호출 관계를 대신하지 않는다.

필수 단계·중복 키·없는 의존·소스 경로/심볼·XAML 결속 오류와 오래된 생성 지도가 검증 실패다. 범위 안의 미분류 보조 형식은 경고로 두며 전체 형식을 억지로 태깅하지 않는다. 범위를 벗어난 리팩토링은 manifest를 넓히기 전에 이유를 남긴다.

`eng/validate-changes.ps1 -Paths <이번 파일들>`은 음식 기사 소스·manifest·도구·생성 지도 변경 시 이 프로필도 검사한다. 공용 메타데이터 변경은 기존 Simulation·Unity 코드 지도와 E 책임 검사도 유지한다. 지도 검사는 API·DB·기기 실행을 대신하지 않는다.
