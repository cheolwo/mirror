# 홍달 모바일 음식 배달 공통 코어

WPF 관찰판·Windows 콘솔 엔진·Unity 모바일이 같은 배차·도로 경로·가상 정산 소스를 사용한다. 정본은 `UnityPackage/Runtime`이며 이전 WPF/콘솔 경로의 파일은 호환 위치 안내만 남긴다. Unity GameObject·외부 프로세스·HTTP·실제 운영 API를 참조하지 않는다.

## 호출

`new Ssalddel.Sagajeong.Core.SagajeongSimulationCore(deliverySampleJson, roadGraphJson)`을 한 회차 소유자가 보관하고 `Execute(requestJson)`을 호출한다. 입력 표본은 기존 `Samples/SeoulEast`의 JSON 두 파일이다. Unity 응답의 기존 `state`·`roads` 형식을 유지하며 원래 자동 관찰은 `manualPickup=false`가 기본이다.

| action | 의미 |
| --- | --- |
| `start`, `campaign-start` | 초기 회차 시작. `manualPickup=true`이면 앱의 명시적 확인까지 매장에서 대기 |
| `tick` | 단일 소유자가 `elapsedMs`로 가상 시각 진행 |
| `candidates` | `orderId`의 적격 후보와 기존 규칙 추천 조회 |
| `assign` | `orderId`, `driverId`, `revision`, `commandId`, `elapsedMs`로 기사 지정 |
| `pickup-confirm` | 같은 필드로 담당 기사·도착·준비·판본을 검증하고 픽업 확인 |
| `campaign-policy` | 기존 캠페인 운영 방침 선택 |
| `snapshot`, `state` | 현재 상태 읽기 |
| `save` | 입력 지문·규칙 판본·행동 재생 기록을 가진 `saveJson` 반환 |
| `restore` | `saveJson`을 별도 코어에서 재생·검증한 뒤 성공 시에만 현재 회차 교체 |
| `export` | 실제 원장·지도 원본·가상 배송지 좌표를 제외한 내부 검토용 `exportJson` 반환 |

`candidates`는 `candidates[]`에 `driverId`, `label`, `eligible`, `pickupKilometers`, `arrivalAt`, `readyDifferenceSeconds`, `reason`을 반환한다. 최상위 `recommendedDriverId`, `revision`은 후보 조회 당시의 값이다. 각 주문의 `canConfirmPickup`이 true일 때 픽업 버튼을 활성화한다. `sessionId`를 요청에 넣으면 다른 회차의 명령은 거절한다.

저장 형식은 `hongdal-mobile-food-save.v1`, 자료 내보내기는 `game-business-preparation.v1`이다. 내보내기 자료는 Simulation이며 운영 실행·학습 승인·가중치 학습 여부는 모두 false다. 저장 지문은 입력 일치 검사이며 사용자 인증이나 실제 업무 권한을 뜻하지 않는다. 실제 자료가 바뀐 입력에는 과거 저장을 자동 적용하지 않는다.

## 소비와 확인

.NET은 `Ssalddel.Sagajeong.Core.csproj`를 참조하고 Unity는 `UnityPackage` 폴더를 `com.ssalddel.sagajeong.core` 패키지로 참조한다. .NET도 같은 `UnityPackage/Runtime` 소스만 명시적으로 컴파일한다. 기존 WorkflowRules 패키지와 Unity Newtonsoft 패키지를 사용한다. C# 9·.NET Standard 2.1에 맞춰 컬렉션·라우팅 큐·직렬화 경계를 정리했으며 `link.xml`은 반사 기반 JSON DTO를 보존한다. 이것만으로 Android IL2CPP·실기기 확인이 끝난 것은 아니다.

의미 있는 회귀 콘솔은 `Checks~/Ssalddel.Sagajeong.Core.Checks.csproj`다. 기존 샘플 두 경로를 인자로 주어 자동 정산 값 보존, 수동 픽업 대기·중복·판본 검사, 저장 재생·실패 보존과 내보내기 경계를 확인한다. 검사와 과거 `bin`·`obj`는 UPM 루트 밖에 보존하며, 새 .NET 빌드 출력은 로컬 artifacts로 보낸다. 제품 Unity·WPF 빌드와 등록 휴대폰 확인은 현재 실행 소유자가 직렬로 수행한다.
